# ============================================================================
# NominaGT v4 - Build-Deploy.ps1
#
# Compila el frontend (Vite) y lo copia al wwwroot del backend, de modo que
# el backend sirva TODO desde un solo origen (puerto 5000). Util para:
#   * Compartir la app con un tunel HTTPS (ngrok, Cloudflare Tunnel)
#   * Pruebas integradas como en produccion
#   * Demos sin necesitar dos terminales
#
# Uso:
#   cd scripts
#   .\Build-Deploy.ps1
#
# Despues:
#   cd ..\02_backend\NominaGT.API
#   dotnet run
#   # Y abre http://localhost:5000  (todo el sitio: UI + API)
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  NominaGT v4 - Build & Deploy" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$rootPath     = Split-Path $PSScriptRoot -Parent
$frontendPath = Join-Path $rootPath "03_frontend"
$wwwrootPath  = Join-Path $rootPath "02_backend\NominaGT.API\wwwroot"

# ─── 1. Build del frontend ───
Write-Host "[1/3] Compilando frontend (Vite)..." -ForegroundColor Yellow
Push-Location $frontendPath
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "      node_modules no existe; ejecutando npm install..." -ForegroundColor Gray
        npm install
    }
    npm run build
} finally {
    Pop-Location
}

$distPath = Join-Path $frontendPath "dist"
if (-not (Test-Path $distPath)) {
    Write-Host "ERROR: El build no genero $distPath" -ForegroundColor Red
    exit 1
}

# ─── 2. Limpiar wwwroot anterior ───
Write-Host "[2/3] Limpiando wwwroot anterior..." -ForegroundColor Yellow
if (Test-Path $wwwrootPath) {
    Remove-Item $wwwrootPath -Recurse -Force
}
New-Item -ItemType Directory -Path $wwwrootPath | Out-Null

# ─── 3. Copiar dist al wwwroot ───
Write-Host "[3/3] Copiando dist al wwwroot del backend..." -ForegroundColor Yellow
Copy-Item -Path "$distPath\*" -Destination $wwwrootPath -Recurse -Force

$files = Get-ChildItem $wwwrootPath -Recurse -File | Measure-Object Length -Sum
Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  Listo. $($files.Count) archivos, $([math]::Round($files.Sum / 1KB, 1)) KB" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Siguiente paso:" -ForegroundColor Cyan
Write-Host "    cd ..\02_backend\NominaGT.API" -ForegroundColor White
Write-Host "    dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "  Luego abre:" -ForegroundColor Cyan
Write-Host "    http://localhost:5000  (UI + API en un solo puerto)" -ForegroundColor White
Write-Host ""
Write-Host "  Para compartirlo en internet con un tunel HTTPS:" -ForegroundColor Cyan
Write-Host "    ngrok http 5000" -ForegroundColor White
Write-Host "    # o" -ForegroundColor Gray
Write-Host "    cloudflared tunnel --url http://localhost:5000" -ForegroundColor White
Write-Host ""
