$ErrorActionPreference = 'Stop'
try {
  $target = Split-Path -Parent $PSScriptRoot
  $apiProject = Join-Path $target 'backend\RoleFit.Api\RoleFit.Api.csproj'
  $frontendProject = Join-Path $target 'frontend\rolefit-ui\package.json'
  if (-not (Test-Path $apiProject) -or -not (Test-Path $frontendProject)) {
    throw "Put the entire RoleFit-AI-Phase5-Update folder INSIDE your existing RoleFit-AI main folder (containing backend, frontend, START_ROLEFIT.bat). Then run this script again."
  }
  $src = Join-Path $PSScriptRoot 'files'
  $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $backup = Join-Path $target "Phase5-Backup-$timestamp"
  New-Item -ItemType Directory -Force $backup | Out-Null
  Get-ChildItem -Path $src -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($src.Length).TrimStart('\', '/')
    $dest = Join-Path $target $relative
    $old = Join-Path $backup $relative
    if (Test-Path $dest) {
      New-Item -ItemType Directory -Force (Split-Path -Parent $old) | Out-Null
      Copy-Item -LiteralPath $dest -Destination $old -Force
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    Write-Host "Updated: $relative"
  }
  Write-Host ''
  Write-Host 'UPDATE APPLIED SUCCESSFULLY' -ForegroundColor Green
  Write-Host "Backup: $backup"
  Write-Host "Next: run TEST_PARSER.bat from the main project folder; then start START_ROLEFIT.bat."
} catch {
  Write-Host "UPDATE FAILED: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
}
