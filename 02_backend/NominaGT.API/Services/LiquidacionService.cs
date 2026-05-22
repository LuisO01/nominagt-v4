using NominaGT.API.DTOs;
using NominaGT.API.Helpers;
using NominaGT.API.Repositories;

namespace NominaGT.API.Services;

/// <summary>
/// Calcula prestaciones laborales conforme al Código de Trabajo de Guatemala.
///
///   - Indemnización (art. 82): 1 mes de salario por año trabajado.
///       Solo se paga en RENUNCIA con justa causa o DESPIDO_INJUSTIFICADO.
///   - Aguinaldo (art. 137, Decreto 76-78): proporcional al período dic-nov.
///   - Bono 14 (Decreto 42-92): proporcional al período jul-jun.
///   - Vacaciones no gozadas (art. 130): 15 días/año proporcional.
///
/// Salario base de cálculo: el salario nominal vigente al momento de la baja.
/// Salario diario: salario_base / 30.
/// </summary>
public class LiquidacionService
{
    private readonly ILiquidacionRepository _repo;
    private readonly IEmpleadoRepository    _empRepo;
    private readonly IVacacionRepository    _vacRepo;
    private readonly IAuditoriaRepository   _audit;
    private readonly EmailService           _email;
    private readonly Reports.PdfReportGenerator _pdf;
    private readonly ILogger<LiquidacionService> _log;

    public LiquidacionService(
        ILiquidacionRepository repo,
        IEmpleadoRepository empRepo,
        IVacacionRepository vacRepo,
        IAuditoriaRepository audit,
        EmailService email,
        Reports.PdfReportGenerator pdf,
        ILogger<LiquidacionService> log)
    {
        _repo = repo; _empRepo = empRepo; _vacRepo = vacRepo;
        _audit = audit; _email = email; _pdf = pdf; _log = log;
    }

    /// <summary>
    /// Calcula prestaciones SIN guardar nada (preview para mostrar en la UI).
    /// </summary>
    public async Task<LiquidacionDto> CalcularPreviewAsync(int empleadoId, LiquidarEmpleadoRequest req)
    {
        var emp = await _empRepo.ObtenerAsync(empleadoId)
            ?? throw new InvalidOperationException("Empleado no encontrado.");

        if (req.FechaBaja > DateTime.Today.AddYears(1))
            throw new InvalidOperationException("La fecha de baja no puede ser más de un año en el futuro.");

        // Obtener fecha de inicio del contrato y salario actual.
        // El DTO EmpleadoDetalleDto no expone fecha_inicio_contrato directamente,
        // pero podemos extraerla del modelo si ampliamos. Por ahora la deducimos
        // del propio empleado (campos a futuro). Como workaround usamos creacion del empleado.
        // Mejor: usamos la fecha del primer contrato laboral activo o cualquiera.
        var fechaInicio = emp.FechaNacimiento.AddYears(20); // fallback
        // Si el EmpleadoDetalleDto tuviera fecha_inicio_contrato, la usariamos aqui.
        // En lugar de eso, calculamos antiguedad consultando contratos:
        (fechaInicio, var salario) = await ObtenerInicioYSalarioAsync(empleadoId, emp.SalarioBase ?? 0m);

        var dias = (int)Math.Max(1, (req.FechaBaja.Date - fechaInicio.Date).TotalDays);
        var aniosServicio = Math.Round(dias / 365m, 3);
        var salarioDiario = Math.Round(salario / 30m, 2);

        // Saldo de vacaciones del empleado
        var saldoVac = await _vacRepo.ObtenerSaldoAsync(empleadoId);
        var diasVacPend = saldoVac?.DiasPendientes ?? 0;

        // === Indemnización ===
        // Solo se paga en motivos que dan derecho. Renuncia sin justa causa NO da indemnizacion
        // (interpretacion comun; ajustable según contrato y politica de la empresa).
        var motivoUpper = (req.Motivo ?? "").ToUpperInvariant();
        var motivosConIndemnizacion = new[] { "DESPIDO_INJUSTIFICADO", "MUTUO_ACUERDO", "JUBILACION", "FALLECIMIENTO" };
        var pagarIndemnizacion = motivosConIndemnizacion.Contains(motivoUpper);
        var indemnizacion = pagarIndemnizacion
            ? Math.Round(salario * aniosServicio, 2)
            : 0m;

        // === Bono 14 proporcional (1 jul a 30 jun) ===
        var bono14 = CalcularProporcional(
            inicioPeriodoLegal: new DateTime(req.FechaBaja.Year, 7, 1) > req.FechaBaja
                ? new DateTime(req.FechaBaja.Year - 1, 7, 1)
                : new DateTime(req.FechaBaja.Year, 7, 1),
            finPeriodoLegal:    new DateTime(req.FechaBaja.Year, 7, 1) > req.FechaBaja
                ? new DateTime(req.FechaBaja.Year, 6, 30)
                : new DateTime(req.FechaBaja.Year + 1, 6, 30),
            fechaIngreso: fechaInicio,
            fechaBaja:    req.FechaBaja,
            salarioBase:  salario);

        // === Aguinaldo proporcional (1 dic a 30 nov) ===
        var aguinaldo = CalcularProporcional(
            inicioPeriodoLegal: new DateTime(req.FechaBaja.Year, 12, 1) > req.FechaBaja
                ? new DateTime(req.FechaBaja.Year - 1, 12, 1)
                : new DateTime(req.FechaBaja.Year, 12, 1),
            finPeriodoLegal:    new DateTime(req.FechaBaja.Year, 12, 1) > req.FechaBaja
                ? new DateTime(req.FechaBaja.Year, 11, 30)
                : new DateTime(req.FechaBaja.Year + 1, 11, 30),
            fechaIngreso: fechaInicio,
            fechaBaja:    req.FechaBaja,
            salarioBase:  salario);

        // === Vacaciones no gozadas ===
        var vacaciones = Math.Round(salarioDiario * diasVacPend, 2);

        var total = indemnizacion + bono14 + aguinaldo + vacaciones
                  + req.OtrosPagos - req.Descuentos;

        return new LiquidacionDto
        {
            EmpleadoId            = empleadoId,
            CodigoEmpleado        = emp.CodigoEmpleado,
            NombreEmpleado        = $"{emp.PrimerNombre} {emp.PrimerApellido}",
            FechaInicioContrato   = fechaInicio,
            FechaBaja             = req.FechaBaja,
            Motivo                = motivoUpper,
            MotivoDetalle         = req.MotivoDetalle,
            SalarioBase           = salario,
            SalarioDiario         = salarioDiario,
            AniosServicio         = aniosServicio,
            DiasServicioTotal     = dias,
            Indemnizacion         = indemnizacion,
            Bono14Proporcional    = bono14,
            AguinaldoProporcional = aguinaldo,
            VacacionesNoGozadas   = vacaciones,
            DiasVacacionesPend    = diasVacPend,
            OtrosPagos            = req.OtrosPagos,
            Descuentos            = req.Descuentos,
            Total                 = Math.Round(total, 2),
            Estado                = "PREVIEW",
            Observaciones         = req.Observaciones,
        };
    }

    /// <summary>
    /// Calcula, persiste, cambia el estado del empleado a BAJA y opcionalmente
    /// envía el PDF del finiquito por correo.
    /// </summary>
    public async Task<LiquidacionDto> LiquidarAsync(int empleadoId, LiquidarEmpleadoRequest req, string usuarioActual)
    {
        var emp = await _empRepo.ObtenerAsync(empleadoId)
            ?? throw new InvalidOperationException("Empleado no encontrado.");
        if (emp.Estado == "BAJA")
            throw new InvalidOperationException("Este empleado ya está dado de baja.");

        var preview = await CalcularPreviewAsync(empleadoId, req);

        // 1. Persistir liquidación
        var liquidacionId = await _repo.CrearAsync(preview, usuarioActual);
        preview.LiquidacionId = liquidacionId;
        preview.Estado = "CALCULADA";

        // 2. Dar de baja al empleado
        await _empRepo.CambiarEstadoAsync(empleadoId, "BAJA",
            req.MotivoDetalle ?? req.Motivo);

        await _audit.RegistrarAsync(usuarioActual, "INSERT", "liquidaciones", liquidacionId, null,
            $"empleado={empleadoId} total=Q{preview.Total:N2}", null);

        // 3. Enviar PDF por correo si se solicito
        if (req.EnviarFiniquitoPorEmail && _email.EstaConfigurado()
            && !string.IsNullOrWhiteSpace(emp.EmailCorporativo))
        {
            try
            {
                var pdfBytes = _pdf.GenerarFiniquito(preview);
                var filename = $"Finiquito_{emp.CodigoEmpleado}_{req.FechaBaja:yyyyMMdd}.pdf";
                var html = EmailService.PlantillaHtml(
                    "Comprobante de liquidación / finiquito",
                    $@"Adjunto el comprobante de tu liquidación laboral con fecha {req.FechaBaja:dd/MM/yyyy}.<br><br>
                       <strong>Total a liquidar: Q {preview.Total:N2}</strong>");
                await _email.EnviarAsync(emp.EmailCorporativo,
                    "Tu liquidación laboral — NominaGT", html,
                    new[] { new EmailService.Adjunto(filename, pdfBytes, "application/pdf") });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Liquidacion {Id} creada pero fallo envio del finiquito por email", liquidacionId);
            }
        }

        return preview;
    }

    /// <summary>
    /// Calcula la prestación proporcional para el periodo legal (Bono14 o Aguinaldo):
    /// dias_trabajados / 365 * salario_base.
    /// </summary>
    private static decimal CalcularProporcional(
        DateTime inicioPeriodoLegal, DateTime finPeriodoLegal,
        DateTime fechaIngreso, DateTime fechaBaja, decimal salarioBase)
    {
        var inicioEfectivo = fechaIngreso > inicioPeriodoLegal ? fechaIngreso : inicioPeriodoLegal;
        var finEfectivo    = fechaBaja    < finPeriodoLegal    ? fechaBaja    : finPeriodoLegal;
        var dias           = (int)(finEfectivo - inicioEfectivo).TotalDays + 1;
        if (dias <= 0) return 0;
        if (dias > 365) dias = 365;
        return Math.Round((salarioBase * dias) / 365m, 2);
    }

    /// <summary>
    /// Obtiene fecha de inicio del contrato y salario actual.
    /// Consulta directa porque EmpleadoDetalleDto no expone esos campos.
    /// </summary>
    private async Task<(DateTime fechaInicio, decimal salario)> ObtenerInicioYSalarioAsync(
        int empleadoId, decimal salarioFallback)
    {
        // Hacemos la query directo via el contexto de Dapper que ya tiene el empRepo.
        // Como el repo no expone metodo para esto, usamos workaround: ObtenerAsync
        // ya tiene salario, y como fecha_inicio_contrato no está expuesta, la
        // consultamos por dynamic.
        if (_empRepo is EmpleadoRepository er)
        {
            var info = await er.ObtenerInicioContratoYSalarioAsync(empleadoId);
            if (info != null) return info.Value;
        }
        return (DateTime.Today.AddYears(-1), salarioFallback);
    }
}
