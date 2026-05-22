namespace NominaGT.API.Models;

// ────────── RRHH ──────────

public class Empresa
{
    public int EmpresaId { get; set; }
    public string RazonSocial { get; set; } = "";
    public string Nit { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? RepresentanteLegal { get; set; }
    public bool Activo { get; set; }
}

public class Empleado
{
    public int EmpleadoId { get; set; }
    public int EmpresaId { get; set; }
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public string CodigoEmpleado { get; set; } = "";
    public string PrimerNombre { get; set; } = "";
    public string? SegundoNombre { get; set; }
    public string PrimerApellido { get; set; } = "";
    public string? SegundoApellido { get; set; }
    public string Dpi { get; set; } = "";
    public string? Nit { get; set; }
    public string? NumAfiliacionIgss { get; set; }
    public DateTime FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? EstadoCivil { get; set; }
    public string? Telefono { get; set; }
    public string? EmailCorporativo { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string Estado { get; set; } = "ACTIVO";
    // Joined fields
    public string? NombreDepartamento { get; set; }
    public string? NombrePuesto { get; set; }
    public string? NombreSucursal { get; set; }
    public decimal? SalarioBase { get; set; }
}

public class ContratoLaboral
{
    public int ContratoId { get; set; }
    public int EmpleadoId { get; set; }
    public string TipoContrato { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public decimal SalarioBase { get; set; }
    public decimal Bonificacion { get; set; }
    public string JornadaLaboral { get; set; } = "DIURNA";
    public string FormaPago { get; set; } = "MENSUAL";
    public bool Activo { get; set; }
}

public class CuentaBancaria
{
    public int CuentaId { get; set; }
    public int EmpleadoId { get; set; }
    public string BancoNombre { get; set; } = "";
    public string? BancoCodigo { get; set; }
    public string NumeroCuenta { get; set; } = "";
    public string TipoCuenta { get; set; } = "MONETARIA";
    public bool EsPrincipal { get; set; }
}

public class Departamento
{
    public int DepartamentoId { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Codigo { get; set; } = "";
    public bool Activo { get; set; }
}

public class Puesto
{
    public int PuestoId { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Codigo { get; set; } = "";
    public decimal? SalarioMinimo { get; set; }
    public decimal? SalarioMaximo { get; set; }
    public bool Activo { get; set; }
}

// ────────── NOMINA ──────────

public class PeriodoNomina
{
    public int PeriodoId { get; set; }
    public int EmpresaId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string TipoPeriodo { get; set; } = "MENSUAL";
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public DateTime FechaPago { get; set; }
    public string Estado { get; set; } = "ABIERTO";
}

public class NominaEncabezado
{
    public int NominaEncId { get; set; }
    public int PeriodoId { get; set; }
    public int EmpleadoId { get; set; }
    public decimal SalarioBase { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal SalarioNeto { get; set; }
    public string Estado { get; set; } = "CALCULADO";
}

public class NominaDetalle
{
    public int NominaDetId { get; set; }
    public int NominaEncId { get; set; }
    public string TipoMovimiento { get; set; } = "";
    public string Concepto { get; set; } = "";
    public decimal Monto { get; set; }
    public bool EsCalculado { get; set; }
    public string? Referencia { get; set; }
}

public class Prestamo
{
    public int PrestamoId { get; set; }
    public int EmpleadoId { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal MontoOriginal { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal CuotaMensual { get; set; }
    public int NumeroCuotas { get; set; }
    public int CuotasPagadas { get; set; }
    public string Estado { get; set; } = "VIGENTE";
}

// ────────── SEGURIDAD ──────────

public class Usuario
{
    public int UsuarioId { get; set; }
    public int EmpresaId { get; set; }
    public int? EmpleadoId { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime? RefreshExpira { get; set; }
    public bool Activo { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoAcceso { get; set; }
}

public class Auditoria
{
    public int AuditoriaId { get; set; }
    public string? Usuario { get; set; }
    public string Accion { get; set; } = "";
    public string? Tabla { get; set; }
    public int? RegistroId { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string? Ip { get; set; }
    public DateTime Fecha { get; set; }
}
