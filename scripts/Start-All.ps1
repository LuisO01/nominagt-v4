# ============================================================================
# NominaGT v4 - Start-All.ps1
# Levanta el backend (.NET) y el frontend (Vite) en ventanas separadas
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  NominaGT v4 - Iniciando aplicacion" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$rootPath     = Split-Path $PSScriptRoot -Parent
$backendPath  = Join-Path $rootPath "02_backend\NominaGT.API"
$frontendPath = Join-Path $rootPath "03_frontend"

# Validaciones
if (-not (Test-Path $backendPath)) {
    Write-Host "No encontre el backend en $backendPath" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $frontendPath)) {
    Write-Host "No encontre el frontend en $frontendPath" -ForegroundColor Red
    exit 1
}

# Verificar dotnet
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "dotnet no encontrado. Instala .NET 8 SDK." -ForegroundColor Red
    exit 1
}

# Verificar npm
$npm = Get-Command npm -ErrorAction SilentlyContinue
if (-not $npm) {
    Write-Host "npm no encontrado. Instala Node.js 18+." -ForegroundColor Red
    exit 1
}

# ────── Restaurar dependencias frontend si es la primera vez ──────
$nodeModules = Join-Path $frontendPath "node_modules"
if (-not (Test-Path $nodeModules)) {
    Write-Host "[1/3] Instalando dependencias del frontend (npm install)..." -ForegroundColor Yellow
    Push-Location $frontendPath
    npm install
    Pop-Location
} else {
    Write-Host "[1/3] node_modules ya existe, omitiendo npm install." -ForegroundColor Gray
}

# ────── Lanzar backend en ventana nueva ──────
Write-Host "[2/3] Iniciando backend .NET en https://localhost:5001..." -ForegroundColor Yellow
$backendCmd = "cd '$backendPath'; dotnet run"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendCmd -WindowStyle Normal

Start-Sleep -Seconds 3

# ────── Lanzar frontend en ventana nueva ──────
Write-Host "[3/3] Iniciando frontend Vite en http://localhost:5173..." -ForegroundColor Yellow
$frontendCmd = "cd '$frontendPath'; npm run dev"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $frontendCmd -WindowStyle Normal

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  NominaGT v4 - Iniciado" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Backend:   https://localhost:5001/swagger" -ForegroundColor Cyan
Write-Host "  Frontend:  http://localhost:5173" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Usuarios demo:" -ForegroundColor Yellow
Write-Host "    admin      / admin123      (ADMIN)" -ForegroundColor White
Write-Host "    rrhh.op1   / rrhh123       (RRHH)" -ForegroundColor White
Write-Host "    nomina.op1 / nomina123     (NOMINA)" -ForegroundColor White
Write-Host ""
Write-Host "  Para detener: cierra las dos ventanas de PowerShell." -ForegroundColor Gray
Write-Host ""
