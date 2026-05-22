using Dapper;
using NominaGT.API.Data;
using NominaGT.API.Models;

namespace NominaGT.API.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly DapperContext _db;
    public UsuarioRepository(DapperContext db) => _db = db;

    public async Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario)
    {
        using var conn = _db.CreateConnection();
        // Agregamos el mapeo manual para asegurar que los strings vengan limpios de Oracle
        var user = await conn.QueryFirstOrDefaultAsync<Usuario>(@"
            SELECT usuario_id AS UsuarioId, 
                   empresa_id AS EmpresaId, 
                   empleado_id AS EmpleadoId, 
                   nombre_usuario AS NombreUsuario, 
                   email,
                   password_hash AS PasswordHash, 
                   refresh_token AS RefreshToken, 
                   refresh_expira AS RefreshExpira, 
                   activo,
                   intentos_fallidos AS IntentosFallidos, 
                   bloqueado_hasta AS BloqueadoHasta, 
                   ultimo_acceso AS UltimoAcceso
            FROM usuarios
            WHERE LOWER(nombre_usuario) = LOWER(:NombreUsuario) AND activo = 1",
            new { NombreUsuario = nombreUsuario });

        // CRITICAL: Limpiar espacios que Oracle a�ade al final
        if (user != null)
        {
            user.PasswordHash = user.PasswordHash?.Trim();
            user.NombreUsuario = user.NombreUsuario?.Trim();
        }

        return user;
    }

    public async Task<Usuario?> ObtenerPorRefreshTokenAsync(string refreshToken)
    {
        using var conn = _db.CreateConnection();
        var user = await conn.QueryFirstOrDefaultAsync<Usuario>(@"
            SELECT usuario_id AS UsuarioId, 
                   empresa_id AS EmpresaId, 
                   empleado_id AS EmpleadoId, 
                   nombre_usuario AS NombreUsuario, 
                   email,
                   password_hash AS PasswordHash, 
                   refresh_token AS RefreshToken, 
                   refresh_expira AS RefreshExpira, 
                   activo
            FROM usuarios
            WHERE refresh_token = :Token
              AND refresh_expira > SYSTIMESTAMP
              AND activo = 1",
            new { Token = refreshToken });

        if (user != null)
        {
            user.PasswordHash = user.PasswordHash?.Trim();
            user.RefreshToken = user.RefreshToken?.Trim();
        }

        return user;
    }

    public async Task<List<string>> ObtenerRolesAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        var roles = await conn.QueryAsync<string>(@"
            SELECT r.nombre
            FROM usuario_roles ur
            JOIN roles r ON r.rol_id = ur.rol_id
            WHERE ur.usuario_id = :UsuarioId",
            new { UsuarioId = usuarioId });

        // Limpiamos los nombres de los roles por si vienen con padding
        return roles.Select(r => r.Trim()).ToList();
    }

    public async Task ActualizarRefreshTokenAsync(int usuarioId, string token, DateTime expira)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE usuarios SET refresh_token = :Token, refresh_expira = :Expira
            WHERE usuario_id = :Id",
            new { Token = token, Expira = expira, Id = usuarioId });
    }

    public async Task ActualizarUltimoAccesoAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE usuarios SET ultimo_acceso = SYSTIMESTAMP, intentos_fallidos = 0 WHERE usuario_id = :Id",
            new { Id = usuarioId });
    }

    public async Task IncrementarIntentosFallidosAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        // Usamos una consulta un poco m�s robusta para Oracle
        await conn.ExecuteAsync(@"
            UPDATE usuarios
            SET intentos_fallidos = intentos_fallidos + 1,
                bloqueado_hasta = CASE WHEN (intentos_fallidos + 1) >= 5
                                       THEN SYSTIMESTAMP + INTERVAL '15' MINUTE
                                       ELSE bloqueado_hasta END
            WHERE usuario_id = :Id",
            new { Id = usuarioId });
    }

    public async Task ResetearIntentosAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE usuarios SET intentos_fallidos = 0, bloqueado_hasta = NULL WHERE usuario_id = :Id",
            new { Id = usuarioId });
    }

    // ─────────────────────────────────────────────────────────────────
    //  Gestion de cuentas
    // ─────────────────────────────────────────────────────────────────
    public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario)
    {
        using var conn = _db.CreateConnection();
        var n = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM usuarios WHERE LOWER(nombre_usuario) = LOWER(:Nu)",
            new { Nu = nombreUsuario });
        return n > 0;
    }

    public async Task<bool> ExisteEmailAsync(string email)
    {
        using var conn = _db.CreateConnection();
        var n = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM usuarios WHERE LOWER(email) = LOWER(:Em)",
            new { Em = email });
        return n > 0;
    }

    public async Task<int?> ObtenerUsuarioIdPorEmpleadoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT usuario_id FROM usuarios WHERE empleado_id = :Id",
            new { Id = empleadoId });
        return id;
    }

    public async Task<int> CrearUsuarioAsync(int empresaId, int empleadoId, string nombreUsuario,
                                             string email, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO usuarios (empresa_id, empleado_id, nombre_usuario, email, password_hash, activo)
            VALUES (:EmpresaId, :EmpleadoId, :Nu, :Em, :Hash, 1)",
            new { EmpresaId = empresaId, EmpleadoId = empleadoId, Nu = nombreUsuario, Em = email, Hash = passwordHash });

        // Recuperar id (la tabla tiene UNIQUE en nombre_usuario)
        var id = await conn.ExecuteScalarAsync<int>(
            "SELECT usuario_id FROM usuarios WHERE LOWER(nombre_usuario) = LOWER(:Nu)",
            new { Nu = nombreUsuario });
        return id;
    }

    public async Task<(int UsuarioId, string NombreUsuario, string Email)?>
        ObtenerUsuarioBasicoPorEmpleadoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<(int U, string N, string E)?>(@"
            SELECT usuario_id AS U, nombre_usuario AS N, email AS E
              FROM usuarios
             WHERE empleado_id = :EmpleadoId AND activo = 1",
            new { EmpleadoId = empleadoId });
        return row;
    }

    public async Task<bool> ActualizarEmailAsync(int usuarioId, string nuevoEmail)
    {
        // NOTA: email YA NO es UNIQUE en usuarios (script 20_email_no_unique.sql).
        // Multiples cuentas pueden compartir email (caso real cuando un empleado
        // gestiona varias cuentas o en pruebas).
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            "UPDATE usuarios SET email = :Em WHERE usuario_id = :UsuarioId",
            new { UsuarioId = usuarioId, Em = nuevoEmail.ToLowerInvariant() });
        return rows > 0;
    }

    public async Task ActualizarPasswordHashAsync(int usuarioId, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE usuarios
               SET password_hash      = :Hash,
                   intentos_fallidos  = 0,
                   bloqueado_hasta    = NULL,
                   refresh_token      = NULL,
                   refresh_expira     = NULL
             WHERE usuario_id = :UsuarioId",
            new { UsuarioId = usuarioId, Hash = passwordHash });
    }

    public async Task AsignarRolAsync(int usuarioId, string nombreRol)
    {
        using var conn = _db.CreateConnection();
        // Encuentra el rol; si no existe ignora (no creamos roles desde aqui)
        var rolId = await conn.ExecuteScalarAsync<int?>(
            "SELECT rol_id FROM roles WHERE UPPER(nombre) = UPPER(:N)",
            new { N = nombreRol });
        if (rolId == null)
            throw new InvalidOperationException($"Rol '{nombreRol}' no existe en la BD.");

        // Insertar evitando duplicado (la PK es compuesta).
        // NOTA: NO usar :Uid ni :Rid - colisionan con las pseudo-funciones UID/RID
        // de Oracle (ORA-01745).
        await conn.ExecuteAsync(@"
            INSERT INTO usuario_roles (usuario_id, rol_id)
            SELECT :UsuarioId, :RolId FROM dual
             WHERE NOT EXISTS (
                SELECT 1 FROM usuario_roles WHERE usuario_id = :UsuarioId AND rol_id = :RolId)",
            new { UsuarioId = usuarioId, RolId = rolId.Value });
    }
}