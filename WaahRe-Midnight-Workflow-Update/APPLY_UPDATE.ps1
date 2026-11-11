$ErrorActionPreference = 'Stop'
try {
  $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
  $source = Join-Path $PSScriptRoot 'files'
  $check = Join-Path $root 'frontend\rolefit-ui\src\App.tsx'
  if (!(Test-Path -LiteralPath $check)) {
    throw "The update folder must be placed INSIDE the existing project folder containing frontend and backend. Missing: $check"
  }
  $files = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'FILE_LIST.txt') | Where-Object { $_.Trim() -ne '' }
  foreach ($file in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file))) {throw "Update file missing: $file"}
  }
  Write-Host "Project root: $root" -ForegroundColor Cyan
  Write-Host "Will update $($files.Count) code/style files. Saved resume records and credentials are not touched." -ForegroundColor Yellow
  $confirm = Read-Host "Type YES to create backups and apply"
  if ($confirm -cne 'YES') { Write-Host 'Cancelled. No project files were changed.'; exit 0 }
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $backup = Join-Path $root ("WAAHRE_SOURCE_BACKUP_" + $stamp)
  New-Item -ItemType Directory -Path $backup -Force | Out-Null
  foreach ($file in $files) {
    $dst = Join-Path $root $file
    $src = Join-Path $source $file
    if (Test-Path -LiteralPath $dst) {
      $old = Join-Path $backup $file
      New-Item -ItemType Directory -Path (Split-Path $old -Parent) -Force | Out-Null
      Copy-Item -LiteralPath $dst -Destination $old -Force
    }
    New-Item -ItemType Directory -Path (Split-Path $dst -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
    Write-Host "Updated $file" -ForegroundColor Green
  }
  Write-Host "`nUPDATE APPLIED. Source backup: $backup" -ForegroundColor Green
  Write-Host 'Restart the ASP.NET Core backend and the Vite frontend after building both.'
} catch {
  Write-Host "UPDATE FAILED: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
}
