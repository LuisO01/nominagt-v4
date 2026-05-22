using System.Security.Cryptography;
using NominaGT.API.DTOs;
using NominaGT.API.Helpers;
using NominaGT.API.Repositories;

namespace NominaGT.API.Services;

public class EmpleadoService
{
    private readonly IEmpleadoRepository _repo;
    private readonly IUsuarioRepository  _usuarioRepo;
    private readonly IAuditoriaRepository _audit;
    private readonly EmailService _email;
    private readonly ILogger<EmpleadoService> _log;

    public EmpleadoService(
        IEmpleadoRepository repo,
        IUsuarioRepository usuarioRepo,
        IAuditoriaRepository audit,
        EmailService email,
        ILogger<EmpleadoService> log)
    {
        _repo = repo;
        _usuarioRepo = usuarioRepo;
        _audit = audit;
        _email = email;
        _log = log;
    }

    public Task<(List<EmpleadoListDto>, int)> ListarAsync(
        int empresaId, int page, int pageSize, string? estado, string? busqueda)
        => _repo.ListarAsync(empresaId, page, pageSize, estado, busqueda);

    public Task<EmpleadoDetalleDto?> ObtenerAsync(int id) => _repo.ObtenerAsync(id);

    public async Task<int> CrearAsync(CrearEmpleadoRequest req, string usuarioActual)
    {
        if (req.SalarioBase < GuatemalaValidators.SalarioMinimoNoAgricola2026)
            throw new InvalidOperationException(
                $"El salario no puede ser menor al minimo vigente (Q{GuatemalaValidators.SalarioMinimoNoAgricola2026:N2}).");

        if (await _repo.ExisteDpiAsync(req.Dpi))
            throw new InvalidOperationException($"Ya existe un empleado con DPI {req.Dpi}.");

        if (await _repo.ExisteCodigoAsync(req.EmpresaId, req.CodigoEmpleado))
            throw new InvalidOperationException($"Ya existe un empleado con codigo {req.CodigoEmpleado}.");

        var id = await _repo.CrearAsync(req);
        await _audit.RegistrarAsync(usuarioActual, "INSERT", "empleados", id, null,
            System.Text.Json.JsonSerializer.Serialize(req), null);

        // Crear acceso al sistema si se solicito
        if (req.Acceso != null)
        {
            try
            {
                await CrearAccesoAsync(id, req.Acceso, usuarioActual,
                    nombreCompleto: $"{req.PrimerNombre} {req.PrimerApellido}",
                    emailEmpleado: req.EmailCorporativo);
            }
            catch (Exception ex)
            {
                // No deshacer el empleado si falla solo la cuenta; queda como pendiente.
                _log.LogWarning(ex, "Empleado {Id} creado, pero fallo crear acceso: {Msg}", id, ex.Message);
            }
        }
        return id;
    }

    /// <summary>
    /// Crea la cuenta de acceso (usuarios + usuario_roles) para un empleado existente.
    /// </summary>
    public async Task<CrearAccesoResultadoDto> CrearAccesoAsync(
        int empleadoId, CrearAccesoRequest req, string usuarioActual,
        string? nombreCompleto = null, string? emailEmpleado = null)
    {
        // Validar empleado existe + obtener datos si no se pasaron
        var emp = await _repo.ObtenerAsync(empleadoId)
            ?? throw new InvalidOperationException("Empleado no encontrado.");

        // No duplicar acceso
        var existe = await _usuarioRepo.ObtenerUsuarioIdPorEmpleadoAsync(empleadoId);
        if (existe != null)
            throw new InvalidOperationException("Este empleado ya tiene una cuenta de acceso.");

        nombreCompleto ??= $"{emp.PrimerNombre} {emp.PrimerApellido}";
        emailEmpleado  ??= emp.EmailCorporativo;

        // Generar nombre de usuario si no viene (codigo del empleado en minusculas)
        var nombreUsuario = string.IsNullOrWhiteSpace(req.NombreUsuario)
            ? emp.CodigoEmpleado.ToLowerInvariant()
            : req.NombreUsuario!.Trim().ToLowerInvariant();

        if (await _usuarioRepo.ExisteNombreUsuarioAsync(nombreUsuario))
            throw new InvalidOperationException($"El usuario '{nombreUsuario}' ya existe. Elige otro.");

        // Email requerido por la columna NOT NULL UNIQUE de la tabla usuarios.
        // Si no hay email del empleado, generamos uno "interno" basado en el codigo.
        var email = !string.IsNullOrWhiteSpace(emailEmpleado)
            ? emailEmpleado!.Trim().ToLowerInvariant()
            : $"{nombreUsuario}@nominagt.local";

        // NOTA: email ya no es UNIQUE en usuarios — multiples cuentas pueden compartirlo.
        // Solo validamos UNIQUE en nombre_usuario (arriba).

        // Generar password si no viene
        var passwordPlano = string.IsNullOrWhiteSpace(req.PasswordTemporal)
            ? GenerarPasswordTemporal()
            : req.PasswordTemporal!;
        if (passwordPlano.Length < 6)
            throw new InvalidOperationException("La contrasena temporal debe tener al menos 6 caracteres.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(passwordPlano, workFactor: 11);

        // Validar rol
        var rolesValidos = new[] { "ADMIN", "RRHH", "NOMINA", "EMPLEADO", "AUDITOR" };
        var rol = (req.RolInicial ?? "EMPLEADO").ToUpperInvariant();
        if (!rolesValidos.Contains(rol))
            throw new InvalidOperationException($"Rol invalido. Validos: {string.Join(", ", rolesValidos)}.");

        // Insertar
        var usuarioId = await _usuarioRepo.CrearUsuarioAsync(
            emp.EmpresaId == 0 ? 1 : emp.EmpresaId, empleadoId, nombreUsuario, email, passwordHash);
        await _usuarioRepo.AsignarRolAsync(usuarioId, rol);

        await _audit.RegistrarAsync(usuarioActual, "INSERT", "usuarios", usuarioId, null,
            $"empleado_id={empleadoId} nombre_usuario={nombreUsuario} rol={rol}", null);

        // Enviar correo si se pidio
        var correoEnviado = false;
        if (req.EnviarCredencialesPorEmail && _email.EstaConfigurado() &&
            !string.IsNullOrWhiteSpace(emailEmpleado))
        {
            try
            {
                var html = EmailService.PlantillaHtml(
                    $"Bienvenido(a) a NominaGT, {nombreCompleto}",
                    $@"Se ha creado tu cuenta para acceder al portal de NominaGT v4.<br><br>
                       <div style='background:#f1f5f9;border-radius:8px;padding:16px;font-family:monospace;'>
                         <strong>Usuario:</strong> {nombreUsuario}<br>
                         <strong>Contraseña temporal:</strong> {passwordPlano}<br>
                         <strong>Rol:</strong> {rol}
                       </div>
                       <p style='margin-top:16px;color:#475569;font-size:13px;'>
                         Por seguridad cambia tu contraseña al iniciar sesión por primera vez.
                       </p>");
                await _email.EnviarAsync(emailEmpleado!,
                    "Tus credenciales de acceso a NominaGT", html);
                correoEnviado = true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cuenta creada para empleado {Id} pero fallo el envio del email", empleadoId);
            }
        }

        return new CrearAccesoResultadoDto(
            UsuarioId:        usuarioId,
            NombreUsuario:    nombreUsuario,
            Rol:              rol,
            Email:            email,
            // Solo devolvemos la password si NO se mando por correo;
            // asi RRHH puede compartirla manualmente.
            PasswordTemporal: correoEnviado ? null : passwordPlano,
            CorreoEnviado:    correoEnviado);
    }

    /// <summary>
    /// Resetea la password de la cuenta vinculada al empleado y opcionalmente
    /// envía la nueva password por correo. Útil cuando el empleado pierde el correo
    /// inicial o necesita reenvío manual.
    /// </summary>
    public async Task<CrearAccesoResultadoDto> ResetearPasswordAsync(
        int empleadoId, bool enviarPorEmail, string usuarioActual)
    {
        var emp = await _repo.ObtenerAsync(empleadoId)
            ?? throw new InvalidOperationException("Empleado no encontrado.");

        var cuenta = await _usuarioRepo.ObtenerUsuarioBasicoPorEmpleadoAsync(empleadoId);
        if (cuenta == null)
            throw new InvalidOperationException(
                "Este empleado no tiene cuenta de acceso. Usa \"Crear acceso\" primero.");

        var (usuarioId, nombreUsuario, emailUsuario) = cuenta.Value;

        // El email corporativo del empleado es la fuente de verdad; si difiere
        // del que tiene la cuenta de usuario, sincronizamos antes de enviar.
        var emailDestino = emailUsuario;
        if (!string.IsNullOrWhiteSpace(emp.EmailCorporativo) &&
            !string.Equals(emp.EmailCorporativo, emailUsuario, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _usuarioRepo.ActualizarEmailAsync(usuarioId, emp.EmailCorporativo);
                emailDestino = emp.EmailCorporativo;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo sincronizar email del usuario {U}; se usará el actual.", usuarioId);
            }
        }

        var passwordPlano = GenerarPasswordTemporal();
        var hash = BCrypt.Net.BCrypt.HashPassword(passwordPlano, workFactor: 11);
        await _usuarioRepo.ActualizarPasswordHashAsync(usuarioId, hash);

        await _audit.RegistrarAsync(usuarioActual, "UPDATE", "usuarios", usuarioId,
            null, "RESET_PASSWORD", null);

        // Roles para devolver
        var roles = await _usuarioRepo.ObtenerRolesAsync(usuarioId);
        var rolPrincipal = roles.FirstOrDefault() ?? "EMPLEADO";

        var correoEnviado = false;
        if (enviarPorEmail && _email.EstaConfigurado() && !string.IsNullOrWhiteSpace(emailDestino)
            && !emailDestino.EndsWith("@nominagt.local", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var nombreCompleto = $"{emp.PrimerNombre} {emp.PrimerApellido}";
                var html = EmailService.PlantillaHtml(
                    "Tus credenciales de acceso a NominaGT",
                    $@"Hola <strong>{nombreCompleto}</strong>,<br><br>
                       Tus credenciales de acceso han sido restablecidas. Usa la siguiente
                       contraseña temporal para iniciar sesión y cámbiala desde tu perfil.<br><br>
                       <div style='background:#f1f5f9;border-radius:8px;padding:16px;font-family:monospace;'>
                         <strong>Usuario:</strong> {nombreUsuario}<br>
                         <strong>Contraseña temporal:</strong> {passwordPlano}
                       </div>
                       <p style='margin-top:16px;color:#475569;font-size:13px;'>
                         Si no solicitaste este cambio, contacta a Recursos Humanos.
                       </p>");
                await _email.EnviarAsync(emailDestino, "Tus credenciales de NominaGT — reenvío", html);
                correoEnviado = true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Password reseteada para empleado {Id} pero fallo el envio por email", empleadoId);
            }
        }

        return new CrearAccesoResultadoDto(
            UsuarioId:        usuarioId,
            NombreUsuario:    nombreUsuario,
            Rol:              rolPrincipal,
            Email:            emailDestino,
            PasswordTemporal: correoEnviado ? null : passwordPlano,
            CorreoEnviado:    correoEnviado);
    }

    /// <summary>Genera un password legible de 10 caracteres: 8 random + dígito.</summary>
    private static string GenerarPasswordTemporal()
    {
        const string letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[10];
        for (int i = 0; i < 10; i++) chars[i] = letras[bytes[i] % letras.Length];
        return new string(chars);
    }

    public async Task<bool> ActualizarAsync(int id, ActualizarEmpleadoRequest req, string usuarioActual)
    {
        var actual = await _repo.ObtenerAsync(id);
        if (actual == null) throw new InvalidOperationException("Empleado no encontrado.");

        var ok = await _repo.ActualizarAsync(id, req);
        if (ok) await _audit.RegistrarAsync(usuarioActual, "UPDATE", "empleados", id,
            System.Text.Json.JsonSerializer.Serialize(actual),
            System.Text.Json.JsonSerializer.Serialize(req), null);

        // Si cambió el email corporativo y el empleado tiene cuenta de acceso,
        // sincronizar el email en la tabla usuarios (es el que se usa para
        // recibir credenciales, recibos, etc.).
        if (ok && !string.IsNullOrWhiteSpace(req.EmailCorporativo) &&
            !string.Equals(req.EmailCorporativo, actual.EmailCorporativo, StringComparison.OrdinalIgnoreCase))
        {
            var cuenta = await _usuarioRepo.ObtenerUsuarioBasicoPorEmpleadoAsync(id);
            if (cuenta != null)
            {
                try
                {
                    await _usuarioRepo.ActualizarEmailAsync(cuenta.Value.UsuarioId, req.EmailCorporativo);
                    _log.LogInformation("Email del usuario {U} sincronizado a {E}", cuenta.Value.UsuarioId, req.EmailCorporativo);
                }
                catch (Exception ex)
                {
                    // No fallar el update del empleado si solo falla la sincronizacion del email
                    _log.LogWarning(ex, "Empleado {Id} actualizado, pero no se pudo sincronizar el email del usuario", id);
                }
            }
        }
        return ok;
    }

    public async Task<bool> CambiarEstadoAsync(int id, CambiarEstadoRequest req, string usuarioActual)
    {
        var ok = await _repo.CambiarEstadoAsync(id, req.Estado, req.Motivo);
        if (ok) await _audit.RegistrarAsync(usuarioActual, "UPDATE", "empleados", id, null,
            $"Estado={req.Estado}, motivo={req.Motivo}", null);
        return ok;
    }
}

public class NominaService
{
    private readonly INominaRepository _repo;
    private readonly IAuditoriaRepository _audit;

    public NominaService(INominaRepository repo, IAuditoriaRepository audit)
    {
        _repo = repo; _audit = audit;
    }

    public async Task<int> CrearPeriodoAsync(CrearPeriodoRequest req, string usuario)
    {
        var id = await _repo.CrearPeriodoAsync(req);
        await _audit.RegistrarAsync(usuario, "INSERT", "periodos_nomina", id, null,
            System.Text.Json.JsonSerializer.Serialize(req), null);
        return id;
    }

    public Task<List<PeriodoDto>> ListarPeriodosAsync(int empresaId, int? anio)
        => _repo.ListarPeriodosAsync(empresaId, anio);

    public Task<PeriodoDto?> ObtenerPeriodoAsync(int id) => _repo.ObtenerPeriodoAsync(id);

    public async Task<int> CalcularNominaAsync(int periodoId, string usuario)
    {
        var total = await _repo.CalcularNominaAsync(periodoId);
        await _audit.RegistrarAsync(usuario, "UPDATE", "periodos_nomina", periodoId, null,
            $"Calculado: {total} empleados", null);
        return total;
    }

    public Task<List<NominaResumenDto>> ObtenerResumenAsync(int periodoId)
        => _repo.ObtenerResumenAsync(periodoId);

    public Task<ReciboPagoDto?> ObtenerReciboAsync(int nominaEncId)
        => _repo.ObtenerReciboAsync(nominaEncId);

    public Task<List<dynamic>> ListarBoletasEmpleadoAsync(int empleadoId)
        => _repo.ListarBoletasEmpleadoAsync(empleadoId);

    /// <summary>
    /// Genera el recibo de pago en PDF para una nomina_encabezado.
    /// </summary>
    public async Task<byte[]?> GenerarReciboPdfAsync(int nominaEncId, Reports.PdfReportGenerator pdf)
    {
        var recibo = await _repo.ObtenerReciboAsync(nominaEncId);
        if (recibo == null) return null;
        return pdf.GenerarReciboPago(recibo);
    }

    public async Task<bool> AprobarPeriodoAsync(int periodoId, string usuario)
    {
        var ok = await _repo.AprobarPeriodoAsync(periodoId, usuario);
        if (ok) await _audit.RegistrarAsync(usuario, "UPDATE", "periodos_nomina", periodoId, null,
            "APROBADO", null);
        return ok;
    }
}

public class ReporteService
{
    private readonly IReporteRepository _repo;
    private readonly Reports.ExcelReportGenerator _excel;
    private readonly Reports.PdfReportGenerator _pdf;
    private readonly IAuditoriaRepository _audit;

    public ReporteService(
        IReporteRepository repo,
        Reports.ExcelReportGenerator excel,
        Reports.PdfReportGenerator pdf,
        IAuditoriaRepository audit)
    {
        _repo = repo; _excel = excel; _pdf = pdf; _audit = audit;
    }

    public Task<List<dynamic>> ObtenerNominaMensualAsync(int anio, int mes, string tipoPeriodo)
        => _repo.ObtenerNominaMensualAsync(anio, mes, tipoPeriodo);

    public async Task<byte[]> GenerarExcelMensualAsync(int anio, int mes, string tipoPeriodo, string usuario)
    {
        var datos = await _repo.ObtenerNominaMensualAsync(anio, mes, tipoPeriodo);

        // Comparativo contra mes anterior (puede ser el mes 12 del anio anterior)
        var (anioPrev, mesPrev) = mes == 1 ? (anio - 1, 12) : (anio, mes - 1);
        var datosPrev = await _repo.ObtenerNominaMensualAsync(anioPrev, mesPrev, tipoPeriodo);

        // Serie de 12 meses para sparkline anual
        var serie = await _repo.ObtenerSerieMensualAsync(1, anio, tipoPeriodo);

        var bytes = _excel.GenerarPlanillaMensual(new Reports.PlanillaContext
        {
            Anio = anio, Mes = mes, TipoPeriodo = tipoPeriodo,
            Rows         = datos,
            RowsPrevMes  = datosPrev,
            SerieAnual   = serie,
            EmpresaNombre = "NominaGT v4"
        });

        await _audit.RegistrarAsync(usuario, "EXPORT", "reportes", null, null,
            $"Excel mensual {anio}-{mes:D2} ({tipoPeriodo})", null);
        return bytes;
    }

    public async Task<byte[]> GenerarPdfMensualAsync(int anio, int mes, string tipoPeriodo, string usuario)
    {
        var datos = await _repo.ObtenerNominaMensualAsync(anio, mes, tipoPeriodo);
        var bytes = _pdf.GenerarPlanillaMensual(datos, anio, mes, tipoPeriodo);
        await _audit.RegistrarAsync(usuario, "EXPORT", "reportes", null, null,
            $"PDF mensual {anio}-{mes:D2} ({tipoPeriodo})", null);
        return bytes;
    }
}

public class VacacionService
{
    private readonly IVacacionRepository _repo;
    private readonly IAuditoriaRepository _audit;

    public VacacionService(IVacacionRepository repo, IAuditoriaRepository audit)
    { _repo = repo; _audit = audit; }

    public Task<List<VacacionDto>> ListarAsync(int? empleadoId, string? estado)
        => _repo.ListarAsync(empleadoId, estado);
    public Task<List<VacacionSaldoDto>> ListarSaldosAsync()
        => _repo.ListarSaldosAsync();
    public Task<VacacionSaldoDto?> ObtenerSaldoAsync(int empleadoId)
        => _repo.ObtenerSaldoAsync(empleadoId);

    public async Task<int> CrearAsync(CrearVacacionRequest req, string usuario)
    {
        // Validacion de saldo: no permitir solicitar mas de lo disponible
        var saldo = await _repo.ObtenerSaldoAsync(req.EmpleadoId)
            ?? throw new InvalidOperationException("Empleado no encontrado o sin contrato activo.");
        var dias = (req.FechaFin.Date - req.FechaInicio.Date).Days + 1;
        if (saldo.DiasPendientes - saldo.DiasEnSolicitud < dias)
            throw new InvalidOperationException(
                $"Saldo insuficiente. Disponible: {saldo.DiasPendientes - saldo.DiasEnSolicitud} días (en solicitud: {saldo.DiasEnSolicitud}).");

        var id = await _repo.CrearAsync(req, usuario);
        await _audit.RegistrarAsync(usuario, "INSERT", "vacaciones", id, null,
            $"{dias} dias del {req.FechaInicio:yyyy-MM-dd} al {req.FechaFin:yyyy-MM-dd}", null);
        return id;
    }

    public async Task<bool> CambiarEstadoAsync(int id, CambiarEstadoVacacionRequest req, string usuario)
    {
        var validos = new[] { "APROBADA", "RECHAZADA", "GOZADA", "CANCELADA" };
        if (!validos.Contains(req.Estado))
            throw new InvalidOperationException("Estado no valido.");
        var ok = await _repo.CambiarEstadoAsync(id, req.Estado, req.Observaciones, usuario);
        if (ok) await _audit.RegistrarAsync(usuario, "UPDATE", "vacaciones", id, null, req.Estado, null);
        return ok;
    }
}

public class DashboardService
{
    private readonly IReporteRepository _repo;
    public DashboardService(IReporteRepository repo) => _repo = repo;

    public Task<DashboardKpisDto> ObtenerKpisAsync(int empresaId) => _repo.ObtenerKpisDashboardAsync(empresaId);
    public Task<List<dynamic>> ObtenerResumenAnualAsync(int empresaId, int anio) => _repo.ObtenerResumenMensualAsync(empresaId, anio);
}
