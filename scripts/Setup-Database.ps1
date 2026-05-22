# ============================================================================
# NominaGT v4 - Setup-Database.ps1
# Ejecuta los 12 scripts SQL en orden contra Oracle XE
# Uso: PowerShell como administrador
#   .\Setup-Database.ps1
# ============================================================================

param(
    [string]$SystemPassword = "",
    [string]$NominaPassword = "NominaGT2026#",
    [string]$Service = "XEPDB1",
    [string]$Host = "localhost",
    [int]$Port = 1521
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  NominaGT v4 - Setup de Base de Datos" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# Verificar sqlplus
$sqlplus = Get-Command sqlplus -ErrorAction SilentlyContinue
if (-not $sqlplus) {
    Write-Host "sqlplus no encontrado en PATH. Instala Oracle XE Client." -ForegroundColor Red
    exit 1
}

# Pedir SYSTEM password si no fue pasada
if (-not $SystemPassword) {
    $secure = Read-Host "Password de SYSTEM" -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $SystemPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
}

$systemConn  = "system/$SystemPassword@$Host`:$Port/$Service"
$nominaConn  = "nominagt/$NominaPassword@$Host`:$Port/$Service"

# Path a los scripts
$scriptsPath = Join-Path $PSScriptRoot "..\01_database"
if (-not (Test-Path $scriptsPath)) {
    Write-Host "No encontre la carpeta 01_database en $scriptsPath" -ForegroundColor Red
    exit 1
}

# Scripts a ejecutar como SYSTEM
$systemScripts = @("00_drop_all.sql", "01_create_user.sql")

# Scripts a ejecutar como NOMINAGT
$nominaScripts = @(
    "02_tables_rrhh.sql",
    "03_tables_nomina.sql",
    "04_tables_contab.sql",
    "05_tables_seguridad.sql",
    "06_indexes.sql",
    "07_views_powerbi.sql",
    "08_procedures.sql",
    "09_triggers_audit.sql",
    "10_seed_data.sql",
    "11_users_bcrypt.sql"
)

function Run-Sql([string]$conn, [string]$file) {
    Write-Host "  -> $file" -ForegroundColor Yellow
    $full = Join-Path $scriptsPath $file
    if (-not (Test-Path $full)) {
        Write-Host "     NO ENCONTRADO" -ForegroundColor Red
        return
    }
    & sqlplus -S $conn "@$full"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "     FALLO con codigo $LASTEXITCODE" -ForegroundColor Red
        throw "Script $file fallo"
    }
}

# Fase 1: como SYSTEM
Write-Host "`n[1/2] Ejecutando scripts SYSTEM..." -ForegroundColor Green
foreach ($s in $systemScripts) { Run-Sql $systemConn $s }

# Fase 2: como NOMINAGT
Write-Host "`n[2/2] Ejecutando scripts NOMINAGT..." -ForegroundColor Green
foreach ($s in $nominaScripts) { Run-Sql $nominaConn $s }

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host "  Setup completo." -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Importante: regenerar hashes BCrypt antes de produccion:" -ForegroundColor Yellow
Write-Host "  .\Seed-BCrypt-Hashes.ps1" -ForegroundColor Yellow
Write-Host ""
