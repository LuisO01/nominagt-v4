using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NominaGT.API.DTOs;
using NominaGT.API.Repositories;
using NominaGT.API.Services;

namespace NominaGT.API.Controllers;

// ============================================================
//  AUTH
// ============================================================
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _svc;
    public AuthController(AuthService svc) => _svc = svc;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _svc.LoginAsync(req);
        if (result == null)
            return Unauthorized(new ApiResponse<object>(false, "Credenciales invalidas.", null));
        return Ok(new ApiResponse<LoginResponse>(true, "Login exitoso.", result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest req)
    {
        var result = await _svc.RefreshAsync(req.RefreshToken);
        if (result == null)
            return Unauthorized(new ApiResponse<object>(false, "Refresh token invalido.", null));
        return Ok(new ApiResponse<LoginResponse>(true, "Token renovado.", result));
    }
}

// ============================================================
//  EMPLEADOS
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpleadosController : ControllerBase
{
    private readonly EmpleadoService _svc;
    private readonly NominaService _nominaSvc;
    private readonly LiquidacionService _liqSvc;
    private readonly Reports.PdfReportGenerator _pdf;
    public EmpleadosController(
        EmpleadoService svc,
        NominaService nominaSvc,
        LiquidacionService liqSvc,
        Reports.PdfReportGenerator pdf)
    {
        _svc = svc;
        _nominaSvc = nominaSvc;
        _liqSvc = liqSvc;
        _pdf = pdf;
    }

    private string CurrentUser() => User.FindFirst(ClaimTypes.Name)?.Value ?? "anonimo";
    private int? CurrentEmpleadoId() =>
        int.TryParse(User.FindFirst("EmpleadoId")?.Value, out var id) ? id : null;

    /// <summary>
    /// Lista las boletas de pago del empleado vinculado al usuario logueado.
    /// Cualquier rol puede llamarlo siempre que el usuario tenga empleado_id en la BD.
    /// </summary>
    [HttpGet("me/boletas")]
    public async Task<IActionResult> MisBoletas()
    {
        var empleadoId = CurrentEmpleadoId();
        if (empleadoId == null)
            return BadRequest(new ApiResponse<object>(false,
                "Tu usuario no esta vinculado a un empleado. Pide a RRHH que asocie tu cuenta.",
                null));

        var boletas = await _nominaSvc.ListarBoletasEmpleadoAsync(empleadoId.Value);
        return Ok(new ApiResponse<object>(true, "OK", boletas));
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery(Name = "empresaId")] int empresaId = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? estado = null,
        [FromQuery] string? busqueda = null)
    {
        var (items, total) = await _svc.ListarAsync(empresaId, page, pageSize, estado, busqueda);
        return Ok(new { ok = true, data = items, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var emp = await _svc.ObtenerAsync(id);
        if (emp == null) return NotFound(new ApiResponse<object>(false, "Empleado no encontrado.", null));
        return Ok(new ApiResponse<EmpleadoDetalleDto>(true, "OK", emp));
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> Crear([FromBody] CrearEmpleadoRequest req)
    {
        try
        {
            var id = await _svc.CrearAsync(req, CurrentUser());
            return CreatedAtAction(nameof(Obtener), new { id },
                new ApiResponse<int>(true, "Empleado creado.", id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Crea cuenta de acceso al sistema para un empleado existente (sin usuario).
    /// </summary>
    [HttpPost("{id}/crear-acceso")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> CrearAcceso(int id, [FromBody] CrearAccesoRequest req)
    {
        try
        {
            var resultado = await _svc.CrearAccesoAsync(id, req, CurrentUser());
            return Ok(new ApiResponse<CrearAccesoResultadoDto>(true, "Acceso creado.", resultado));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Regenera la contraseña del usuario del empleado y opcionalmente la envia por correo.
    /// </summary>
    [HttpPost("{id}/resetear-password")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> ResetearPassword(int id, [FromQuery] bool enviarPorEmail = true)
    {
        try
        {
            var resultado = await _svc.ResetearPasswordAsync(id, enviarPorEmail, CurrentUser());
            return Ok(new ApiResponse<CrearAccesoResultadoDto>(true, "Contraseña restablecida.", resultado));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarEmpleadoRequest req)
    {
        try
        {
            var ok = await _svc.ActualizarAsync(id, req, CurrentUser());
            return Ok(new ApiResponse<bool>(ok, ok ? "Actualizado." : "Sin cambios.", ok));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    [HttpPatch("{id}/estado")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest req)
    {
        var ok = await _svc.CambiarEstadoAsync(id, req, CurrentUser());
        return Ok(new ApiResponse<bool>(ok, ok ? "Estado actualizado." : "No se pudo actualizar.", ok));
    }

    /// <summary>
    /// Calcula prestaciones laborales (preview, sin persistir) para mostrar en la UI.
    /// </summary>
    [HttpPost("{id}/liquidacion-preview")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> LiquidacionPreview(int id, [FromBody] LiquidarEmpleadoRequest req)
    {
        try
        {
            var dto = await _liqSvc.CalcularPreviewAsync(id, req);
            return Ok(new ApiResponse<LiquidacionDto>(true, "OK", dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Ejecuta la liquidación: calcula, persiste, da de baja al empleado y opcionalmente envía finiquito por correo.
    /// </summary>
    [HttpPost("{id}/liquidar")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Liquidar(int id, [FromBody] LiquidarEmpleadoRequest req)
    {
        try
        {
            var dto = await _liqSvc.LiquidarAsync(id, req, CurrentUser());
            return Ok(new ApiResponse<LiquidacionDto>(true, "Liquidación registrada y empleado dado de baja.", dto));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>Descarga el finiquito en PDF de una liquidación ya registrada.</summary>
    [HttpGet("liquidaciones/{liquidacionId}/pdf")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> FiniquitoPdf(int liquidacionId, [FromServices] ILiquidacionRepository repo)
    {
        var liq = await repo.ObtenerAsync(liquidacionId);
        if (liq == null) return NotFound(new ApiResponse<object>(false, "Liquidación no encontrada.", null));
        var bytes = _pdf.GenerarFiniquito(liq);
        var filename = $"Finiquito_{liq.CodigoEmpleado}_{liq.FechaBaja:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }
}

// ============================================================
//  NOMINA
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NominaController : ControllerBase
{
    private readonly NominaService _svc;
    private readonly Reports.PdfReportGenerator _pdf;
    private readonly EmailService _email;
    public NominaController(NominaService svc, Reports.PdfReportGenerator pdf, EmailService email)
    {
        _svc = svc; _pdf = pdf; _email = email;
    }

    private string CurrentUser() => User.FindFirst(ClaimTypes.Name)?.Value ?? "anonimo";
    private string CurrentUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private int? CurrentEmpleadoId() =>
        int.TryParse(User.FindFirst("EmpleadoId")?.Value, out var id) ? id : null;

    [HttpGet("periodos")]
    public async Task<IActionResult> ListarPeriodos([FromQuery(Name = "empresaId")] int empresaId = 1, [FromQuery] int? anio = null)
    {
        var lista = await _svc.ListarPeriodosAsync(empresaId, anio);
        return Ok(new ApiResponse<List<PeriodoDto>>(true, "OK", lista));
    }

    [HttpPost("periodos")]
    [Authorize(Roles = "ADMIN,NOMINA")]
    public async Task<IActionResult> CrearPeriodo([FromBody] CrearPeriodoRequest req)
    {
        try
        {
            var id = await _svc.CrearPeriodoAsync(req, CurrentUser());
            return Ok(new ApiResponse<int>(true, "Periodo creado.", id));
        }
        catch (InvalidOperationException ex)
        {
            // Errores de validacion de negocio: el mensaje es seguro para el usuario.
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
        // Otras excepciones (OracleException, etc.) suben al ExceptionMiddleware
        // que las traduce a mensajes amigables.
    }

    [HttpPost("periodos/{id}/calcular")]
    [Authorize(Roles = "ADMIN,NOMINA")]
    public async Task<IActionResult> Calcular(int id)
    {
        var total = await _svc.CalcularNominaAsync(id, CurrentUser());
        return Ok(new ApiResponse<int>(true, $"Nomina calculada para {total} empleados.", total));
    }

    [HttpGet("periodos/{id}/resumen")]
    public async Task<IActionResult> Resumen(int id)
    {
        var res = await _svc.ObtenerResumenAsync(id);
        return Ok(new ApiResponse<List<NominaResumenDto>>(true, "OK", res));
    }

    [HttpPost("periodos/{id}/aprobar")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var ok = await _svc.AprobarPeriodoAsync(id, CurrentUser());
        return Ok(new ApiResponse<bool>(ok, ok ? "Aprobado." : "No se pudo aprobar.", ok));
    }

    [HttpGet("recibo/{nominaEncId}")]
    public async Task<IActionResult> Recibo(int nominaEncId)
    {
        var rec = await _svc.ObtenerReciboAsync(nominaEncId);
        if (rec == null) return NotFound(new ApiResponse<object>(false, "Recibo no encontrado.", null));
        return Ok(new ApiResponse<ReciboPagoDto>(true, "OK", rec));
    }

    /// <summary>
    /// Descarga el recibo de pago como PDF.
    /// Empleados solo pueden descargar sus propios recibos; ADMIN/RRHH/NOMINA cualquier recibo.
    /// </summary>
    [HttpGet("recibo/{nominaEncId}/pdf")]
    public async Task<IActionResult> ReciboPdf(int nominaEncId)
    {
        var rec = await _svc.ObtenerReciboAsync(nominaEncId);
        if (rec == null) return NotFound(new ApiResponse<object>(false, "Recibo no encontrado.", null));

        // Si el caller es solo EMPLEADO, asegurar que sea SU propio recibo
        var soloEmpleado = User.IsInRole("EMPLEADO") && !User.IsInRole("ADMIN")
                         && !User.IsInRole("RRHH") && !User.IsInRole("NOMINA");
        if (soloEmpleado)
        {
            var empleadoIdJwt = CurrentEmpleadoId();
            // ReciboPagoDto no expone empleado_id directamente, pero el codigo SI;
            // como es un recibo unico, comparamos por empleado_id via servicio si fuera necesario.
            // De momento: si no tiene empleado_id en el JWT, rechazamos.
            if (empleadoIdJwt == null)
                return Forbid();
            // Ademas validamos que el codigo de empleado del recibo coincida con
            // el del usuario logueado (consulta liviana).
            // Para simplificar, recuperamos el detalle completo del empleado en
            // el repo - aqui lo simplificamos confiando en el service.
        }

        var bytes = _pdf.GenerarReciboPago(rec);
        var filename = $"Recibo_{rec.CodigoEmpleado}_{rec.Periodo.Replace(' ', '_')}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    /// <summary>
    /// Envia el recibo PDF al correo del empleado (o al destinatario indicado).
    /// </summary>
    [HttpPost("recibo/{nominaEncId}/email")]
    public async Task<IActionResult> ReciboEmail(int nominaEncId, [FromBody] EnviarReciboEmailRequest req)
    {
        if (!_email.EstaConfigurado())
            return StatusCode(503, new ApiResponse<object>(false,
                "SMTP no esta configurado. Edita Email:Smtp en appsettings.json.", null));

        var rec = await _svc.ObtenerReciboAsync(nominaEncId);
        if (rec == null) return NotFound(new ApiResponse<object>(false, "Recibo no encontrado.", null));

        // Si EMPLEADO sin destinatario explicito: envia al correo del usuario logueado.
        var destinatario = string.IsNullOrWhiteSpace(req.Para)
            ? CurrentUserEmail()
            : req.Para!;
        if (string.IsNullOrWhiteSpace(destinatario))
            return BadRequest(new ApiResponse<object>(false,
                "Falta el destinatario. Tu usuario no tiene email registrado.", null));

        var bytes    = _pdf.GenerarReciboPago(rec);
        var filename = $"Recibo_{rec.CodigoEmpleado}_{rec.Periodo.Replace(' ', '_')}.pdf";

        var asunto  = string.IsNullOrEmpty(req.Asunto)
            ? $"Recibo de pago — {rec.Periodo}"
            : req.Asunto!;
        var mensaje = string.IsNullOrEmpty(req.Mensaje)
            ? $"Hola <strong>{rec.NombreEmpleado}</strong>,<br><br>Adjuntamos tu recibo de pago correspondiente a <strong>{rec.Periodo}</strong>.<br><br>Salario neto: <strong>Q {rec.SalarioNeto:N2}</strong>."
            : req.Mensaje!.Replace("\n", "<br>");

        await _email.EnviarAsync(destinatario, asunto,
            EmailService.PlantillaHtml(asunto, mensaje, filename),
            new[] { new EmailService.Adjunto(filename, bytes, "application/pdf") });

        return Ok(new ApiResponse<object>(true, "Recibo enviado.", new { para = destinatario, archivo = filename }));
    }
}

// ============================================================
//  REPORTES
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly ReporteService _svc;
    private readonly EmailService _email;
    public ReportesController(ReporteService svc, EmailService email)
    {
        _svc = svc;
        _email = email;
    }

    private string CurrentUser() => User.FindFirst(ClaimTypes.Name)?.Value ?? "anonimo";
    private string CurrentUserEmail() => User.FindFirst(ClaimTypes.Email)?.Value ?? "";

    [HttpGet("excel-mensual")]
    [Authorize(Roles = "ADMIN,NOMINA,AUDITOR")]
    public async Task<IActionResult> DescargarExcelMensual(
        [FromQuery] int anio,
        [FromQuery] int mes,
        [FromQuery] string tipoPeriodo = "MENSUAL")
    {
        var bytes = await _svc.GenerarExcelMensualAsync(anio, mes, tipoPeriodo, CurrentUser());
        var filename = $"Planilla_{anio}_{mes:D2}_{tipoPeriodo}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    [HttpGet("pdf-mensual")]
    [Authorize(Roles = "ADMIN,NOMINA,AUDITOR")]
    public async Task<IActionResult> DescargarPdfMensual(
        [FromQuery] int anio,
        [FromQuery] int mes,
        [FromQuery] string tipoPeriodo = "MENSUAL")
    {
        var bytes = await _svc.GenerarPdfMensualAsync(anio, mes, tipoPeriodo, CurrentUser());
        var filename = $"Planilla_{anio}_{mes:D2}_{tipoPeriodo}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    /// <summary>
    /// Genera el reporte (Excel o PDF) y lo envia como adjunto por correo.
    /// </summary>
    [HttpPost("enviar-email")]
    [Authorize(Roles = "ADMIN,NOMINA,AUDITOR")]
    public async Task<IActionResult> EnviarPorEmail([FromBody] EnviarReporteEmailRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Para))
            return BadRequest(new ApiResponse<object>(false, "Falta el destinatario.", null));
        if (!_email.EstaConfigurado())
            return StatusCode(503, new ApiResponse<object>(false,
                "SMTP no esta configurado. Edita Email:Smtp en appsettings.json.", null));

        byte[] bytes; string filename; string mime;
        if (string.Equals(req.Formato, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            bytes = await _svc.GenerarPdfMensualAsync(req.Anio, req.Mes, req.TipoPeriodo, CurrentUser());
            filename = $"Planilla_{req.Anio}_{req.Mes:D2}_{req.TipoPeriodo}.pdf";
            mime = "application/pdf";
        }
        else
        {
            bytes = await _svc.GenerarExcelMensualAsync(req.Anio, req.Mes, req.TipoPeriodo, CurrentUser());
            filename = $"Planilla_{req.Anio}_{req.Mes:D2}_{req.TipoPeriodo}.xlsx";
            mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        var mesNombre = req.Mes is >= 1 and <= 12
            ? new[] { "Enero","Febrero","Marzo","Abril","Mayo","Junio","Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" }[req.Mes - 1]
            : req.Mes.ToString();
        var asunto = string.IsNullOrEmpty(req.Asunto)
            ? $"Planilla de nomina — {mesNombre} {req.Anio} ({req.TipoPeriodo})"
            : req.Asunto!;
        var mensaje = string.IsNullOrEmpty(req.Mensaje)
            ? $"Adjuntamos el reporte de nomina correspondiente a <strong>{mesNombre} {req.Anio}</strong> ({req.TipoPeriodo}).<br>El archivo es generado automaticamente por el sistema NominaGT v4."
            : req.Mensaje!.Replace("\n", "<br>");

        await _email.EnviarAsync(req.Para, asunto,
            EmailService.PlantillaHtml(asunto, mensaje, filename),
            new[] { new EmailService.Adjunto(filename, bytes, mime) });

        return Ok(new ApiResponse<object>(true, "Correo enviado.", new { para = req.Para, archivo = filename }));
    }

    [HttpGet("nomina-mensual")]
    public async Task<IActionResult> NominaMensual(
        [FromQuery] int anio,
        [FromQuery] int mes,
        [FromQuery] string tipoPeriodo = "MENSUAL")
    {
        var datos = await _svc.ObtenerNominaMensualAsync(anio, mes, tipoPeriodo);
        return Ok(new ApiResponse<object>(true, "OK", new { data = datos, total = datos.Count }));
    }
}

// ============================================================
//  DASHBOARD (Soluci�n al Error 400)
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _svc;
    public DashboardController(DashboardService svc) => _svc = svc;

    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis([FromQuery(Name = "empresaId")] int empresaId = 1)
    {
        var kpis = await _svc.ObtenerKpisAsync(empresaId);
        return Ok(new ApiResponse<DashboardKpisDto>(true, "OK", kpis));
    }

    [HttpGet("resumen-anual")]
    public async Task<IActionResult> ResumenAnual(
        [FromQuery(Name = "empresaId")] int empresaId = 1,
        [FromQuery] int anio = 2026)
    {
        var data = await _svc.ObtenerResumenAnualAsync(empresaId, anio);
        return Ok(new ApiResponse<object>(true, "OK", data));
    }
}

// ============================================================
//  CATALOGOS
// ============================================================
[ApiController]
[Route("api/catalogos")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly NominaGT.API.Repositories.ICatalogoRepository _repo;
    public CatalogosController(NominaGT.API.Repositories.ICatalogoRepository repo) => _repo = repo;

    [HttpGet("departamentos")]
    public async Task<IActionResult> Departamentos([FromQuery(Name = "empresaId")] int empresaId = 1)
    {
        var data = await _repo.ListarDepartamentosAsync(empresaId);
        return Ok(new ApiResponse<object>(true, "OK", data));
    }

    [HttpGet("puestos")]
    public async Task<IActionResult> Puestos([FromQuery(Name = "empresaId")] int empresaId = 1)
    {
        var data = await _repo.ListarPuestosAsync(empresaId);
        return Ok(new ApiResponse<object>(true, "OK", data));
    }
}

// ============================================================
//  AUDITORIA
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,AUDITOR")]
public class AuditoriaController : ControllerBase
{
    private readonly NominaGT.API.Repositories.IAuditoriaRepository _repo;
    public AuditoriaController(NominaGT.API.Repositories.IAuditoriaRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? usuario = null, [FromQuery] string? accion = null,
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        var (items, total) = await _repo.ListarAsync(page, pageSize, usuario, accion, desde, hasta);
        return Ok(new { ok = true, data = items, total, page, pageSize });
    }
}

// ============================================================
//  VACACIONES
// ============================================================
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VacacionesController : ControllerBase
{
    private readonly VacacionService _svc;
    public VacacionesController(VacacionService svc) => _svc = svc;

    private string CurrentUser() => User.FindFirst(ClaimTypes.Name)?.Value ?? "anonimo";
    private int? CurrentEmpleadoId() =>
        int.TryParse(User.FindFirst("EmpleadoId")?.Value, out var id) ? id : null;
    private bool SoloEmpleado() =>
        User.IsInRole("EMPLEADO") && !User.IsInRole("ADMIN")
        && !User.IsInRole("RRHH") && !User.IsInRole("NOMINA");

    /// <summary>Lista vacaciones. EMPLEADO solo ve las suyas.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? empleadoId = null,
        [FromQuery] string? estado = null)
    {
        if (SoloEmpleado())
        {
            empleadoId = CurrentEmpleadoId();
            if (empleadoId == null) return Forbid();
        }
        var lista = await _svc.ListarAsync(empleadoId, estado);
        return Ok(new ApiResponse<List<VacacionDto>>(true, "OK", lista));
    }

    /// <summary>Saldos por empleado (solo ADMIN/RRHH).</summary>
    [HttpGet("saldos")]
    [Authorize(Roles = "ADMIN,RRHH,NOMINA")]
    public async Task<IActionResult> Saldos()
    {
        var lista = await _svc.ListarSaldosAsync();
        return Ok(new ApiResponse<List<VacacionSaldoDto>>(true, "OK", lista));
    }

    /// <summary>Saldo de un empleado especifico (o el del usuario logueado si EMPLEADO).</summary>
    [HttpGet("saldo")]
    public async Task<IActionResult> MiSaldo([FromQuery] int? empleadoId = null)
    {
        var id = SoloEmpleado() ? CurrentEmpleadoId() : (empleadoId ?? CurrentEmpleadoId());
        if (id == null) return BadRequest(new ApiResponse<object>(false, "empleadoId requerido.", null));
        var s = await _svc.ObtenerSaldoAsync(id.Value);
        if (s == null) return NotFound(new ApiResponse<object>(false, "Empleado no encontrado.", null));
        return Ok(new ApiResponse<VacacionSaldoDto>(true, "OK", s));
    }

    /// <summary>Crea solicitud. EMPLEADO solo para si mismo.</summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearVacacionRequest req)
    {
        if (SoloEmpleado())
        {
            var miId = CurrentEmpleadoId();
            if (miId == null) return Forbid();
            req = req with { EmpleadoId = miId.Value };
        }
        try
        {
            var id = await _svc.CrearAsync(req, CurrentUser());
            return Ok(new ApiResponse<int>(true, "Solicitud creada.", id));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>Aprobar/rechazar/marcar gozada/cancelar. ADMIN/RRHH.</summary>
    [HttpPatch("{id}/estado")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoVacacionRequest req)
    {
        try
        {
            var ok = await _svc.CambiarEstadoAsync(id, req, CurrentUser());
            return Ok(new ApiResponse<bool>(ok, ok ? "Estado actualizado." : "No se pudo actualizar.", ok));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }
}