using System.Net;
using System.Security.Claims;
using System.Text.Json;
using NominaGT.API.DTOs;
using NominaGT.API.Repositories;
using Oracle.ManagedDataAccess.Client;

namespace NominaGT.API.Middleware;

/// <summary>
/// Captura excepciones no manejadas y devuelve JSON consistente.
/// Traduce errores Oracle (UQ, FK, NOT NULL, etc.) a mensajes claros.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    // Importante: camelCase para que el frontend lea data.mensaje (no Mensaje)
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
    {
        _next = next; _log = log;
    }

    private static Task WriteError(HttpContext ctx, int status, string mensaje)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(
            new ApiResponse<object>(false, mensaje, null), JsonOpts));
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (OracleException ex)
        {
            _log.LogWarning(ex, "Oracle error {Code}", ex.Number);
            var (status, msg) = TraducirOracle(ex);
            await WriteError(ctx, status, msg);
        }
        catch (InvalidOperationException ex)
        {
            _log.LogWarning(ex, "Validation error");
            await WriteError(ctx, (int)HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");
            await WriteError(ctx, (int)HttpStatusCode.InternalServerError,
                "Error interno del servidor.");
        }
    }

    private static (int status, string mensaje) TraducirOracle(OracleException ex)
    {
        // Codigos comunes: https://docs.oracle.com/error-help/db/
        var raw = ex.Message ?? "";
        return ex.Number switch
        {
            00001 => (409, MensajeUnique(raw)),                                          // UNIQUE constraint
            01400 => (400, "Falta un campo obligatorio en la solicitud."),                // NOT NULL
            02291 => (409, "Referencia invalida: el registro relacionado no existe."),    // FK violated
            02292 => (409, "No se puede eliminar: hay registros que dependen de este."),  // FK child
            02290 => (400, "Validacion de datos fallida (CHECK)."),
            01861 => (400, "Formato de fecha invalido en la solicitud."),
            01821 => (500, "Error en una vista de la base de datos (formato de fecha). Aplica el script 13_fix_views_ora01821.sql."),
            12541 => (503, "No hay listener de Oracle activo en el servidor."),
            12514 => (503, "El servicio de Oracle no esta disponible."),
            01017 => (500, "Credenciales de la base de datos invalidas."),
            _     => (500, $"Error de base de datos (ORA-{ex.Number:D5}). Revisa los logs.")
        };
    }

    private static string MensajeUnique(string raw)
    {
        // Mensajes amigables por nombre de constraint
        if (raw.Contains("UQ_PERIODO", StringComparison.OrdinalIgnoreCase))
            return "Ya existe un periodo con ese año, mes y tipo.";
        if (raw.Contains("UQ_EMPLEADO_CODIGO", StringComparison.OrdinalIgnoreCase))
            return "Ya existe un empleado con ese codigo.";
        if (raw.Contains("UQ_EMPLEADO_DPI", StringComparison.OrdinalIgnoreCase))
            return "Ya existe un empleado con ese DPI.";
        if (raw.Contains("UQ_USUARIO", StringComparison.OrdinalIgnoreCase))
            return "Ya existe un usuario con ese nombre.";
        return "Ya existe un registro con esos datos (restriccion unica).";
    }
}

/// <summary>
/// Registra LOGIN, LOGOUT, EXPORT y operaciones criticas en la tabla auditoria.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IAuditoriaRepository audit)
    {
        await _next(ctx);

        // Solo registra POST/PUT/PATCH/DELETE exitosos
        if (ctx.Response.StatusCode >= 200 && ctx.Response.StatusCode < 300
            && ctx.Request.Method != "GET" && ctx.User.Identity?.IsAuthenticated == true)
        {
            var path = ctx.Request.Path.Value ?? "";
            if (path.Contains("/auth/login"))
            {
                var user = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "?";
                var ip = ctx.Connection.RemoteIpAddress?.ToString();
                await audit.RegistrarAsync(user, "LOGIN", null, null, null, path, ip);
            }
        }
    }
}
