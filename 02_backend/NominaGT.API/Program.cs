using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NominaGT.API.Data;
using NominaGT.API.Middleware;
using NominaGT.API.Repositories;
using NominaGT.API.Services;
using NominaGT.API.Reports; // Aseg�rate de que este namespace sea el correcto

var builder = WebApplication.CreateBuilder(args);

// ============================================================
//  URLs de escucha
// ============================================================
// Por defecto escuchamos en HTTPS local (5001) y HTTP en TODAS las interfaces (5000).
// El HTTP en 0.0.0.0:5000 permite que un tunel (ngrok/cloudflare) pueda llegar
// desde otra computadora o desde internet a traves del SSL del tunel.
builder.WebHost.UseUrls("https://localhost:5001", "http://0.0.0.0:5000");

// ============================================================
//  Dapper: snake_case -> PascalCase
// ============================================================
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// ============================================================
//  Data layer
// ============================================================
builder.Services.AddSingleton<DapperContext>();

// ============================================================
//  Repositories
// ============================================================
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IEmpleadoRepository, EmpleadoRepository>();
builder.Services.AddScoped<INominaRepository, NominaRepository>();
builder.Services.AddScoped<IReporteRepository, ReporteRepository>();
builder.Services.AddScoped<ICatalogoRepository, CatalogoRepository>();
builder.Services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
builder.Services.AddScoped<IVacacionRepository, VacacionRepository>();
builder.Services.AddScoped<ILiquidacionRepository, LiquidacionRepository>();

// ============================================================
//  Infraestructura de Reportes (CORRECCI�N AQU�)
// ============================================================
// Registramos los generadores de archivos que ReporteService requiere
builder.Services.AddScoped<ExcelReportGenerator>();
builder.Services.AddScoped<PdfReportGenerator>();

// ============================================================
//  Business services
// ============================================================
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmpleadoService>();
builder.Services.AddScoped<NominaService>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<VacacionService>();
builder.Services.AddScoped<LiquidacionService>();
builder.Services.AddScoped<EmailService>();

// ============================================================
//  FluentValidation
// ============================================================
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ============================================================
//  JWT Authentication
// ============================================================
var jwtConfig = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            ClockSkew = TimeSpan.FromMinutes(2),
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtConfig["Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ============================================================
//  Swagger con soporte JWT
// ============================================================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NominaGT API v4",
        Version = "v4.0",
        Description = "Sistema de Nominas Empresarial - Guatemala",
        Contact = new OpenApiContact { Name = "Estanly", Email = "soporte@nominagt.gt" }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega solo el token (sin la palabra 'Bearer'). Swagger lo agrega solo."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
//  CORS
// ============================================================
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactDev", policy =>
        policy.SetIsOriginAllowed(origin =>
              {
                  // Permite localhost en cualquier puerto + dominios de tuneles publicos
                  if (allowedOrigins.Contains(origin)) return true;
                  var host = new Uri(origin).Host;
                  return host == "localhost"
                      || host == "127.0.0.1"
                      || host.EndsWith(".ngrok-free.app")
                      || host.EndsWith(".ngrok.io")
                      || host.EndsWith(".trycloudflare.com")
                      || host.EndsWith(".loca.lt")        // localtunnel
                      || host.EndsWith(".lhr.life");       // localhost.run
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// ============================================================
//  Middleware pipeline
// ============================================================
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NominaGT API v4");
    c.RoutePrefix = "swagger";
});

// Respetar headers X-Forwarded-* (importante cuando un tunel/proxy HTTPS termina
// el SSL al frente y comunica con el backend en HTTP).
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost,
};
// Aceptamos cualquier proxy (sin lista blanca) porque corremos en LAN/tunel privado.
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

// NO usamos UseHttpsRedirection: el HTTP en :5000 debe ser accesible para
// permitir que los tuneles HTTPS (ngrok, Cloudflare) hagan forward sin loop.

app.UseCors("ReactDev");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

// ─── Servir el frontend (build de Vite copiado a wwwroot) ───
// Sirve archivos estaticos /assets/* y al final, cualquier ruta no manejada
// por la API cae al index.html (fallback de SPA con React Router).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Health check de la API (solo si /api/health, no choca con el SPA)
app.MapGet("/api/health", () => Results.Ok(new
{
    api = "NominaGT",
    version = "4.0",
    status = "running",
    swagger = "/swagger",
    timestamp = DateTime.Now
}));

// Fallback de SPA: cualquier ruta que no sea /api/*, /swagger* o un archivo
// estatico, devuelve index.html. Asi React Router maneja /empleados, /nomina, etc.
app.MapFallbackToFile("index.html");

Console.WriteLine();
Console.WriteLine("=========================================");
Console.WriteLine("  NominaGT API v4 - INICIADA");
Console.WriteLine("=========================================");
Console.WriteLine("  Swagger:  https://localhost:5001/swagger");
Console.WriteLine("  Health:   https://localhost:5001/");
Console.WriteLine("=========================================");
Console.WriteLine();

app.Run();