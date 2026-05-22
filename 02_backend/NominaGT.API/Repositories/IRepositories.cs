using NominaGT.API.DTOs;
using NominaGT.API.Models;

namespace NominaGT.API.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario);
    Task<Usuario?> ObtenerPorRefreshTokenAsync(string refreshToken);
    Task<List<string>> ObtenerRolesAsync(int usuarioId);
    Task ActualizarRefreshTokenAsync(int usuarioId, string token, DateTime expira);
    Task ActualizarUltimoAccesoAsync(int usuarioId);
    Task IncrementarIntentosFallidosAsync(int usuarioId);
    Task ResetearIntentosAsync(int usuarioId);

    // Gestion de cuentas (vinculadas a empleados)
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario);
    Task<bool> ExisteEmailAsync(string email);
    Task<int?> ObtenerUsuarioIdPorEmpleadoAsync(int empleadoId);
    Task<int> CrearUsuarioAsync(int empresaId, int empleadoId, string nombreUsuario,
                                 string email, string passwordHash);
    Task AsignarRolAsync(int usuarioId, string nombreRol);
    Task<(int UsuarioId, string NombreUsuario, string Email)?> ObtenerUsuarioBasicoPorEmpleadoAsync(int empleadoId);
    Task ActualizarPasswordHashAsync(int usuarioId, string passwordHash);
    /// <summary>Sincroniza el email de la cuenta con el email corporativo del empleado.</summary>
    Task<bool> ActualizarEmailAsync(int usuarioId, string nuevoEmail);
}

public interface IEmpleadoRepository
{
    Task<(List<EmpleadoListDto> Items, int Total)> ListarAsync(int empresaId, int page, int pageSize, string? estado, string? busqueda);
    Task<EmpleadoDetalleDto?> ObtenerAsync(int empleadoId);
    Task<int> CrearAsync(CrearEmpleadoRequest req);
    Task<bool> ActualizarAsync(int empleadoId, ActualizarEmpleadoRequest req);
    Task<bool> CambiarEstadoAsync(int empleadoId, string estado, string? motivo);
    Task<bool> ExisteDpiAsync(string dpi, int? excluirEmpleadoId = null);
    Task<bool> ExisteCodigoAsync(int empresaId, string codigo);
}

public interface INominaRepository
{
    Task<int> CrearPeriodoAsync(CrearPeriodoRequest req);
    Task<List<PeriodoDto>> ListarPeriodosAsync(int empresaId, int? anio = null);
    Task<PeriodoDto?> ObtenerPeriodoAsync(int periodoId);
    Task<int> CalcularNominaAsync(int periodoId);
    Task<List<NominaResumenDto>> ObtenerResumenAsync(int periodoId);
    Task<ReciboPagoDto?> ObtenerReciboAsync(int nominaEncId);
    Task<bool> AprobarPeriodoAsync(int periodoId, string usuarioAprobador);
    Task<List<dynamic>> ListarBoletasEmpleadoAsync(int empleadoId);
}

public interface IReporteRepository
{
    Task<List<dynamic>> ObtenerNominaMensualAsync(int anio, int mes, string tipoPeriodo);
    Task<List<dynamic>> ObtenerResumenMensualAsync(int empresaId, int anio);
    Task<List<dynamic>> ObtenerHistoricoEmpleadoAsync(int empleadoId);
    Task<DashboardKpisDto> ObtenerKpisDashboardAsync(int empresaId);
    /// <summary>Serie de 12 meses con totales (para sparklines y tendencias).</summary>
    Task<List<dynamic>> ObtenerSerieMensualAsync(int empresaId, int anio, string tipoPeriodo);
}

public interface ICatalogoRepository
{
    Task<List<Departamento>> ListarDepartamentosAsync(int empresaId);
    Task<List<Puesto>> ListarPuestosAsync(int empresaId);
}

public interface ILiquidacionRepository
{
    Task<int> CrearAsync(LiquidacionDto dto, string usuarioActual);
    Task<LiquidacionDto?> ObtenerAsync(int liquidacionId);
    Task<List<LiquidacionDto>> ListarPorEmpleadoAsync(int empleadoId);
}

public interface IVacacionRepository
{
    Task<List<VacacionDto>> ListarAsync(int? empleadoId, string? estado);
    Task<List<VacacionSaldoDto>> ListarSaldosAsync();
    Task<VacacionSaldoDto?> ObtenerSaldoAsync(int empleadoId);
    Task<int> CrearAsync(CrearVacacionRequest req, string usuario);
    Task<bool> CambiarEstadoAsync(int vacacionId, string estado, string? observaciones, string usuario);
}

public interface IAuditoriaRepository
{
    Task RegistrarAsync(string usuario, string accion, string? tabla, int? registroId, string? valorAnterior, string? valorNuevo, string? ip);
    Task<(List<Auditoria> Items, int Total)> ListarAsync(int page, int pageSize, string? usuario, string? accion, DateTime? desde, DateTime? hasta);
}
