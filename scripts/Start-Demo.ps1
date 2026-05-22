# ============================================================================
# NominaGT v4 - Start-Demo.ps1
#
# Arranca todo lo necesario para una demo publica con ngrok:
#   1. Verifica/inicia Oracle XE
#   2. Inicia el backend .NET en una terminal nueva
#   3. Inicia ngrok con la URL fija en otra terminal nueva
#
# CONFIGURA primero tu URL fija de ngrok abajo (variable $NgrokDomain).
# Si la dejas vacia, ngrok te dara una URL random nueva cada vez.
#
# Uso:
#   Doble click en este archivo, o desde PowerShell:
#   .\Start-Demo.ps1
# ============================================================================

# ─── CONFIGURACION ──────────────────────────────────────────────────────────
$NgrokDomain = "snooper-outlying-dugout.ngrok-free.dev"
# Si lo dejas vacio "", ngrok asignara una URL nueva cada vez.

# ─── RUTAS ──────────────────────────────────────────────────────────────────
$rootPath    = Split-Path $PSScriptRoot -Parent
$backendPath = Join-Path $rootPath "02_backend\NominaGT.API"
$wwwrootPath = Join-Path $backendPath "wwwroot"

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  NominaGT v4 - Demo Launcher" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# ─── 1. Verificar/iniciar Oracle XE ─────────────────────────────────────────
Write-Host "[1/3] Verificando Oracle XE..." -ForegroundColor Yellow
$oracleSvc = Get-Service -Name "OracleServiceXE" -ErrorAction SilentlyContinue
if (-not $oracleSvc) {
    Write-Host "      ERROR: OracleServiceXE no instalado." -ForegroundColor Red
    Read-Host "Presiona Enter para salir"
    exit 1
}
if ($oracleSvc.Status -ne "Running") {
    Write-Host "      Oracle XE detenido. Iniciando..." -ForegroundColor Yellow
    Start-Service OracleServiceXE
    Start-Sleep -Seconds 8
    $listenerSvc = Get-Service "OracleOraDB21Home1TNSListener" -ErrorAction SilentlyContinue
    if ($listenerSvc -and $listenerSvc.Status -ne "Running") {
        Start-Service "OracleOraDB21Home1TNSListener"
    }
    Write-Host "      OK: Oracle XE iniciado." -ForegroundColor Green
} else {
    Write-Host "      OK: Oracle XE ya esta corriendo." -ForegroundColor Green
}

# ─── 2. Verificar que wwwroot tiene el build del frontend ───────────────────
if (-not (Test-Path (Join-Path $wwwrootPath "index.html"))) {
    Write-Host "[2/3] wwwroot vacio. Compilando frontend..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "Build-Deploy.ps1")
} else {
    Write-Host "[2/3] OK: wwwroot ya tiene el build del frontend." -ForegroundColor Green
}

# ─── 3. Iniciar backend en ventana nueva ────────────────────────────────────
Write-Host "[3/3] Iniciando backend y ngrok..." -ForegroundColor Yellow

$backendCmd = "Set-Location '$backendPath'; Write-Host '===== BACKEND =====' -ForegroundColor Cyan; dotnet run"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendCmd -WindowStyle Normal

Write-Host "      Esperando que el backend este listo (15s)..." -ForegroundColor Gray
Start-Sleep -Seconds 15

# ─── 4. Iniciar ngrok en ventana nueva ──────────────────────────────────────
$ngrokArgs = if ($NgrokDomain -and $NgrokDomain -notlike "*REEMPLAZA*") {
    "http --url=$NgrokDomain 5000"
} else {
    "http 5000"
}
$ngrokCmd = "Write-Host '===== NGROK TUNNEL =====' -ForegroundColor Cyan; ngrok $ngrokArgs"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $ngrokCmd -WindowStyle Normal

Start-Sleep -Seconds 3

# ─── Resumen ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  NominaGT v4 - LISTO" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Local:   http://localhost:5000" -ForegroundColor Cyan
if ($NgrokDomain -and $NgrokDomain -notlike "*REEMPLAZA*") {
    Write-Host "  Publica: https://$NgrokDomain" -ForegroundColor Cyan
} else {
    Write-Host "  Publica: revisa la ventana de ngrok (URL random)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Usuarios demo:" -ForegroundColor Yellow
Write-Host "    admin      / admin       (ADMIN)" -ForegroundColor White
Write-Host "    rrhh.op1   / rrhh123     (RRHH)" -ForegroundColor White
Write-Host "    nomina.op1 / nomina123   (NOMINA)" -ForegroundColor White
Write-Host "    empleado   / empleado123 (EMPLEADO)" -ForegroundColor White
Write-Host ""
Write-Host "  Para detener todo: cierra las dos ventanas de PowerShell." -ForegroundColor Gray
Write-Host ""
Read-Host "Presiona Enter para cerrar esta ventana (las otras seguiran corriendo)"
