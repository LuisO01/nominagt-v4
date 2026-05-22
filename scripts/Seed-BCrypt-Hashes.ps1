# ============================================================================
# NominaGT v4 - Seed-BCrypt-Hashes.ps1
# CRITICO: regenera hashes BCrypt REALES y los actualiza en la BD.
#
# Por que: el archivo 11_users_bcrypt.sql tiene hashes placeholder que NO
# son verificables por BCrypt.Verify. Sin este script, el login fallara.
#
# Uso: ejecutar UNA VEZ despues de Setup-Database.ps1
#   .\Seed-BCrypt-Hashes.ps1
# ============================================================================

param(
    [string]$NominaPassword = "NominaGT2026#",
    [string]$Service = "XEPDB1",
    [string]$DBHost = "localhost",
    [int]$Port = 1521
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  NominaGT v4 - Regenerar hashes BCrypt" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Verificar dotnet
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "dotnet no encontrado. Instala .NET 8 SDK." -ForegroundColor Red
    exit 1
}

# Verificar sqlplus
$sqlplus = Get-Command sqlplus -ErrorAction SilentlyContinue
if (-not $sqlplus) {
    Write-Host "sqlplus no encontrado en PATH." -ForegroundColor Red
    exit 1
}

$rootPath = Split-Path $PSScriptRoot -Parent
$tempDir  = Join-Path $env:TEMP "NominaGT_BCryptSeeder"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

Push-Location $tempDir
try {
    # ────── Crear miniproyecto .NET que use BCrypt.Net ──────
    Write-Host "[1/4] Creando proyecto temporal en $tempDir..." -ForegroundColor Yellow
    dotnet new console -n BCryptSeeder --force | Out-Null
    Push-Location BCryptSeeder
    dotnet add package BCrypt.Net-Next --version 4.0.3 | Out-Null

    $code = @'
using BC = BCrypt.Net.BCrypt;

string[] passwords = { "admin123", "rrhh123", "nomina123", "empleado123" };
string[] usuarios  = { "admin",    "rrhh.op1", "nomina.op1", "empleado" };

Console.WriteLine("-- Hashes generados con BCrypt cost=11");
for (int i = 0; i < usuarios.Length; i++)
{
    var hash = BC.HashPassword(passwords[i], 11);
    Console.WriteLine($"UPDATE usuarios SET password_hash = '{hash}' WHERE nombre_usuario = '{usuarios[i]}';");
}
Console.WriteLine("COMMIT;");
'@
    Set-Content -Path "Program.cs" -Value $code

    # ────── Generar el SQL ──────
    Write-Host "[2/4] Generando hashes con BCrypt.Net..." -ForegroundColor Yellow
    $sqlOutput = dotnet run --no-build 2>$null
    if (-not $sqlOutput) { dotnet build | Out-Null; $sqlOutput = dotnet run 2>$null }

    $sqlFile = Join-Path $tempDir "update_hashes.sql"
    Set-Content -Path $sqlFile -Value ($sqlOutput -join "`n")

    Write-Host "[3/4] SQL generado:" -ForegroundColor Yellow
    Get-Content $sqlFile | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }

    Pop-Location

    # ────── Aplicar a la BD ──────
    Write-Host "[4/4] Aplicando a Oracle..." -ForegroundColor Yellow
    $conn = "nominagt/$NominaPassword@$DBHost`:$Port/$Service"
    & sqlplus -S $conn "@$sqlFile"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error al ejecutar SQL." -ForegroundColor Red
        exit 1
    }

} finally {
    Pop-Location
    # Limpiar
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  Hashes BCrypt actualizados correctamente" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Ya puedes iniciar sesion con:" -ForegroundColor Cyan
Write-Host "  admin      / admin123" -ForegroundColor White
Write-Host "  rrhh.op1   / rrhh123" -ForegroundColor White
Write-Host "  nomina.op1 / nomina123" -ForegroundColor White
Write-Host "  empleado   / empleado123" -ForegroundColor White
Write-Host ""
