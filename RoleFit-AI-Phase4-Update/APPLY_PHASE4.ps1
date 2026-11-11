$ErrorActionPreference = 'Stop'
$update = $PSScriptRoot
$source = Join-Path $update 'files'
$project = [System.IO.Path]::GetFullPath((Join-Path $update '..'))
if (-not (Test-Path (Join-Path $project 'backend\RoleFit.Api\RoleFit.Api.csproj'))) {
  Write-Host 'ERROR: Put the entire extracted RoleFit-AI-Phase4-Update folder INSIDE your existing RoleFit-AI folder.' -ForegroundColor Red
  Write-Host 'Expected: RoleFit-AI\RoleFit-AI-Phase4-Update\APPLY_PHASE4.bat' -ForegroundColor Yellow
  exit 1
}
Write-Host "Project found: $project" -ForegroundColor Cyan
Write-Host 'CLOSE both running RoleFit PowerShell windows before applying this update.' -ForegroundColor Yellow
$answer = Read-Host 'Have you closed the API and frontend? Type YES to continue'
if ($answer -ne 'YES') { Write-Host 'Cancelled; no files changed.'; exit 1 }
$backup = Join-Path $project ('Phase4_Backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
$sourceDir = (Get-Item $source).FullName.TrimEnd('\')
foreach ($item in Get-ChildItem $source -File -Recurse) {
  $relative = $item.FullName.Substring($sourceDir.Length + 1)
  $destination = Join-Path $project $relative
  if (Test-Path $destination) {
    $old = Join-Path $backup $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $old -Parent) | Out-Null
    Copy-Item -LiteralPath $destination -Destination $old -Force
  }
  New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
  Copy-Item -LiteralPath $item.FullName -Destination $destination -Force
  Write-Host "Updated: $relative" -ForegroundColor DarkGray
}
Write-Host 'UPDATE APPLIED SUCCESSFULLY' -ForegroundColor Green
Write-Host "Old affected files were backed up to $backup" -ForegroundColor Gray
Write-Host 'Your saved App_Data files were not changed.' -ForegroundColor Green
Write-Host 'Optional: double-click CONFIGURE_GROQ.bat in your main RoleFit-AI folder.' -ForegroundColor Cyan
Write-Host 'Then double-click START_ROLEFIT.bat.' -ForegroundColor Cyan
