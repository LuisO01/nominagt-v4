using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NominaGT.API.DTOs;
using NominaGT.API.Repositories;

namespace NominaGT.API.Services;

public class AuthService
{
    private readonly IUsuarioRepository _repo;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _log;

    public AuthService(IUsuarioRepository repo, IConfiguration config, ILogger<AuthService> log)
    {
        _repo = repo; _config = config; _log = log;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest req)
    {
        var user = await _repo.ObtenerPorNombreAsync(req.NombreUsuario);

        if (user == null)
        {
            _log.LogWarning("Login fallido: usuario {U} no existe en Oracle", req.NombreUsuario);
            return null;
        }

        // --- DIAGN�STICO DE DATOS RECUPERADOS ---
        _log.LogInformation("--- DEBUG NOMINAGT AUTH ---");
        _log.LogInformation("Usuario: '{U}'", user.NombreUsuario);
        _log.LogInformation("Password enviado: '{P}'", req.Password);
        _log.LogInformation("Hash en DB: '{H}'", user.PasswordHash ?? "NULL");
        _log.LogInformation("Largo del Hash: {L}", user.PasswordHash?.Length ?? 0);
        _log.LogInformation("---------------------------");

        // Validar bloqueo por intentos fallidos
        if (user.BloqueadoHasta.HasValue && user.BloqueadoHasta > DateTime.Now)
        {
            _log.LogWarning("Login bloqueado para {U} hasta {T}", req.NombreUsuario, user.BloqueadoHasta);
            return null;
        }

        // L�GICA DE VALIDACI�N CON BYPASS TEMPORAL PARA ADMIN
        bool isPasswordValid = false;

        // Bypass para pruebas r�pidas
        if (req.NombreUsuario.ToLower() == "admin" && req.Password == "admin")
        {
            _log.LogCritical("�ACCESO POR BYPASS PARA ADMIN! Revisa el hash en Oracle despu�s.");
            isPasswordValid = true;
        }
        else if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _log.LogError("Error al verificar BCrypt: {Message}", ex.Message);
            }
        }

        if (!isPasswordValid)
        {
            await _repo.IncrementarIntentosFallidosAsync(user.UsuarioId);
            _log.LogWarning("Password incorrecta para {U}", req.NombreUsuario);
            return null;
        }

        // OK: emitir token + refresh
        var roles = await _repo.ObtenerRolesAsync(user.UsuarioId);
        var (token, expira) = GenerarJwt(user.UsuarioId, user.NombreUsuario, user.Email, roles, user.EmpresaId, user.EmpleadoId);
        var refreshToken = GenerarRefreshToken();
        var refreshExpira = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7"));

        await _repo.ActualizarRefreshTokenAsync(user.UsuarioId, refreshToken, refreshExpira);
        await _repo.ActualizarUltimoAccesoAsync(user.UsuarioId);

        _log.LogInformation("Login exitoso para: {U}", user.NombreUsuario);

        return new LoginResponse(token, refreshToken, user.NombreUsuario, user.Email, roles, expira);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken)
    {
        var user = await _repo.ObtenerPorRefreshTokenAsync(refreshToken);
        if (user == null) return null;

        var roles = await _repo.ObtenerRolesAsync(user.UsuarioId);
        var (newToken, expira) = GenerarJwt(user.UsuarioId, user.NombreUsuario, user.Email, roles, user.EmpresaId, user.EmpleadoId);
        var newRefresh = GenerarRefreshToken();
        var newRefreshExpira = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7"));

        await _repo.ActualizarRefreshTokenAsync(user.UsuarioId, newRefresh, newRefreshExpira);

        return new LoginResponse(newToken, newRefresh, user.NombreUsuario, user.Email, roles, expira);
    }

    private (string Token, DateTime Expira) GenerarJwt(
        int userId, string username, string email, List<string> roles, int empresaId, int? empleadoId)
    {
        var minutesStr = _config["Jwt:AccessTokenMinutes"];
        var minutes = int.TryParse(minutesStr, out int m) ? m : 60;
        var expira = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email ?? ""),
            new("EmpresaId", empresaId.ToString())
        };
        if (empleadoId.HasValue)
            claims.Add(new Claim("EmpleadoId", empleadoId.Value.ToString()));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r.ToUpper()));

        var keyStr = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(keyStr)) throw new Exception("JWT Key no configurada en appsettings.json");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }

    private static string GenerarRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}