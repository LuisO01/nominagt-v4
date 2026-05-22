namespace NominaGT.API.DTOs;

// ────────── Generic envelope ──────────
public record ApiResponse<T>(bool Ok, string Mensaje, T? Data);

public record PagedResponse<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);

// ────────── AUTH ──────────
public record LoginRequest(string NombreUsuario, string Password);

public record LoginResponse(
    string Token,
    string RefreshToken,
    string NombreUsuario,
    string Email,
    List<string> Roles,
    DateTime Expira);

public record RefreshTokenRequest(string RefreshToken);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

// ────────── EMPLEADOS ──────────
// NOTE: los DTOs que se materializan desde Oracle se declaran como `class`
// con `{ get; set; }`. Asi Dapper los rellena por property setters y convierte
// automaticamente decimal->int, double->decimal, etc. Records con primary
// constructor exigen firma exacta y fallan con Oracle (NUMBER -> decimal).
public class EmpleadoListDto
{
    public int      EmpleadoId          { get; set; }
    public string   CodigoEmpleado      { get; set; } = "";
    public string   NombreCompleto      { get; set; } = "";
    public string?  NombreDepartamento  { get; set; }
    public string?  NombrePuesto        { get; set; }
    public decimal? SalarioBase         { get; set; }
    public string   Estado              { get; set; } = "";
    /// <summary>1 si el empleado tiene cuenta de acceso al sistema; 0 si no.</summary>
    public int      TieneAcceso         { get; set; }
}

public class EmpleadoDetalleDto
{
    public int       EmpleadoId         { get; set; }
    public int       EmpresaId          { get; set; }
    public string    CodigoEmpleado     { get; set; } = "";
    public string    PrimerNombre       { get; set; } = "";
    public string?   SegundoNombre      { get; set; }
    public string    PrimerApellido     { get; set; } = "";
    public string?   SegundoApellido    { get; set; }
    public string    Dpi                { get; set; } = "";
    public string?   Nit                { get; set; }
    public string?   NumAfiliacionIgss  { get; set; }
    public DateTime  FechaNacimiento    { get; set; }
    public string?   Genero             { get; set; }
    public string?   EstadoCivil        { get; set; }
    public string?   Telefono           { get; set; }
    public string?   EmailCorporativo   { get; set; }
    public string    Estado             { get; set; } = "";
    public int?      DepartamentoId     { get; set; }
    public string?   NombreDepartamento { get; set; }
    public int?      PuestoId           { get; set; }
    public string?   NombrePuesto       { get; set; }
    public decimal?  SalarioBase        { get; set; }
    public decimal?  Bonificacion       { get; set; }
    public string?   TipoContrato       { get; set; }
    public string?   FormaPago          { get; set; }
}

public record CrearEmpleadoRequest(
    int EmpresaId,
    int? SucursalId,
    int? DepartamentoId,
    int? PuestoId,
    string CodigoEmpleado,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string Dpi,
    string? Nit,
    string? NumAfiliacionIgss,
    DateTime FechaNacimiento,
    string Genero,
    string EstadoCivil,
    string? Telefono,
    string? EmailCorporativo,
    decimal SalarioBase,
    decimal Bonificacion = 250.00m,
    string TipoContrato = "INDEFINIDO",
    string JornadaLaboral = "DIURNA",
    string FormaPago = "MENSUAL",
    DateTime? FechaInicioContrato = null,
    string? BancoNombre = null,
    string? NumeroCuenta = null,
    string TipoCuenta = "MONETARIA",
    CrearAccesoRequest? Acceso = null);

/// <summary>
/// Configuracion opcional de cuenta de acceso al crear empleado, o al crear
/// acceso retroactivo desde el endpoint POST /api/empleados/{id}/crear-acceso.
/// </summary>
public record CrearAccesoRequest(
    string? NombreUsuario,                  // si null, se genera del codigo del empleado
    string? PasswordTemporal,               // si null, se genera aleatoriamente
    string RolInicial = "EMPLEADO",         // ADMIN|RRHH|NOMINA|EMPLEADO|AUDITOR
    bool EnviarCredencialesPorEmail = true);

/// <summary>Resultado de crear acceso (lo que se le devuelve al RRHH).</summary>
public record CrearAccesoResultadoDto(
    int UsuarioId,
    string NombreUsuario,
    string Rol,
    string? Email,
    string? PasswordTemporal,   // solo se devuelve si no se envio por email
    bool CorreoEnviado);

public record ActualizarEmpleadoRequest(
    int? DepartamentoId,
    int? PuestoId,
    string? Telefono,
    string? EmailCorporativo,
    string? EstadoCivil);

public record CambiarEstadoRequest(string Estado, string? Motivo);

// ────────── NOMINA ──────────
public record CrearPeriodoRequest(int EmpresaId, int Anio, int Mes, string TipoPeriodo);

public class PeriodoDto
{
    public int      PeriodoId    { get; set; }
    public int      Anio         { get; set; }
    public int      Mes          { get; set; }
    public string   TipoPeriodo  { get; set; } = "";
    public DateTime FechaInicio  { get; set; }
    public DateTime FechaFin     { get; set; }
    public DateTime FechaPago    { get; set; }
    public string   Estado       { get; set; } = "";
}

public record CalcularNominaRequest(int PeriodoId);

public class NominaResumenDto
{
    public int     PeriodoId        { get; set; }
    public string  Periodo          { get; set; } = "";
    public string  Estado           { get; set; } = "";
    public int     TotalEmpleados   { get; set; }
    public decimal TotalIngresos    { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalNeto        { get; set; }
}

public record ReciboPagoDto(
    int NominaEncId,
    string CodigoEmpleado,
    string NombreEmpleado,
    string? Departamento,
    string Periodo,
    decimal SalarioBase,
    List<LineaReciboDto> Ingresos,
    List<LineaReciboDto> Deducciones,
    decimal TotalIngresos,
    decimal TotalDeducciones,
    decimal SalarioNeto);

public record LineaReciboDto(string Concepto, decimal Monto, string? Referencia);

// ────────── REPORTES / DASHBOARD ──────────
public class DashboardKpisDto
{
    public int     TotalEmpleadosActivos  { get; set; }
    public int     TotalEmpleadosBaja     { get; set; }
    public decimal NominaUltimoMes        { get; set; }
    public int     PeriodosAprobadosAnio  { get; set; }
    public decimal SaldoPrestamosVigentes { get; set; }
}

public record FiltroReporteDto(int Anio, int Mes, string? TipoPeriodo);

// ────────── VACACIONES ──────────
public class VacacionDto
{
    public int      VacacionId      { get; set; }
    public int      EmpleadoId      { get; set; }
    public string?  CodigoEmpleado  { get; set; }
    public string?  NombreEmpleado  { get; set; }
    public DateTime FechaInicio     { get; set; }
    public DateTime FechaFin        { get; set; }
    public int      Dias            { get; set; }
    public string?  Motivo          { get; set; }
    public string   Estado          { get; set; } = "SOLICITADA";
    public string?  SolicitadoPor   { get; set; }
    public DateTime? SolicitadoEn   { get; set; }
    public string?  AprobadoPor     { get; set; }
    public DateTime? AprobadoEn     { get; set; }
    public string?  Observaciones   { get; set; }
}

public class VacacionSaldoDto
{
    public int     EmpleadoId       { get; set; }
    public string  CodigoEmpleado   { get; set; } = "";
    public string  Nombre           { get; set; } = "";
    public DateTime? FechaInicioContrato { get; set; }
    public int     AniosTrabajados  { get; set; }
    public int     DiasAcumulados   { get; set; }
    public int     DiasGozados      { get; set; }
    public int     DiasPendientes   { get; set; }
    public int     DiasEnSolicitud  { get; set; }
}

public record CrearVacacionRequest(
    int EmpleadoId,
    DateTime FechaInicio,
    DateTime FechaFin,
    string? Motivo);

public record CambiarEstadoVacacionRequest(string Estado, string? Observaciones);

// ────────── LIQUIDACION / FINIQUITO ──────────
/// <summary>Request para calcular o ejecutar liquidacion al dar de baja.</summary>
public record LiquidarEmpleadoRequest(
    DateTime FechaBaja,
    string Motivo,                  // RENUNCIA | DESPIDO_JUSTIFICADO | DESPIDO_INJUSTIFICADO | MUTUO_ACUERDO | JUBILACION | FALLECIMIENTO | OTRO
    string? MotivoDetalle,
    decimal OtrosPagos = 0,
    decimal Descuentos = 0,
    string? Observaciones = null,
    bool EnviarFiniquitoPorEmail = false);

/// <summary>Resultado del calculo de prestaciones (preview o registro real).</summary>
public class LiquidacionDto
{
    public int      LiquidacionId         { get; set; }
    public int      EmpleadoId            { get; set; }
    public string?  CodigoEmpleado        { get; set; }
    public string?  NombreEmpleado        { get; set; }
    public DateTime FechaInicioContrato   { get; set; }
    public DateTime FechaBaja             { get; set; }
    public string   Motivo                { get; set; } = "RENUNCIA";
    public string?  MotivoDetalle         { get; set; }

    public decimal  SalarioBase           { get; set; }
    public decimal  SalarioDiario         { get; set; }
    public decimal  AniosServicio         { get; set; }
    public int      DiasServicioTotal     { get; set; }

    public decimal  Indemnizacion         { get; set; }
    public decimal  Bono14Proporcional    { get; set; }
    public decimal  AguinaldoProporcional { get; set; }
    public decimal  VacacionesNoGozadas   { get; set; }
    public int      DiasVacacionesPend    { get; set; }
    public decimal  OtrosPagos            { get; set; }
    public decimal  Descuentos            { get; set; }
    public decimal  Total                 { get; set; }

    public string   Estado                { get; set; } = "CALCULADA";
    public string?  CalculadoPor          { get; set; }
    public DateTime? CalculadoEn          { get; set; }
    public string?  Observaciones         { get; set; }
}

// ────────── EMAIL ──────────
public record EnviarReporteEmailRequest(
    string Para,
    string Formato,        // "excel" | "pdf"
    int Anio,
    int Mes,
    string TipoPeriodo,
    string? Asunto,
    string? Mensaje);

public record EnviarReciboEmailRequest(
    string? Para,          // si es null, usa el email del empleado
    string? Asunto,
    string? Mensaje);
