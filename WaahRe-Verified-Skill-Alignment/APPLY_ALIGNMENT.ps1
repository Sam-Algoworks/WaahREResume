$ErrorActionPreference = 'Stop'
$updateDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Split-Path -Parent $updateDir
$root = Join-Path $project 'frontend\rolefit-ui\src'
if (!(Test-Path (Join-Path $project 'backend\RoleFit.Api\Program.cs')) -or !(Test-Path $root)) {
  Write-Host 'ERROR: Copy the entire update folder inside your existing RoleFit-AI project folder.' -ForegroundColor Red
  exit 1
}
$confirm = Read-Host 'Back up and apply the JD skill verification update? Type YES'
if ($confirm -cne 'YES') { Write-Host 'Cancelled.'; exit 0 }
$backup = Join-Path $project ('WaahRe_Backup_Alignment_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
$files = Get-ChildItem -Path (Join-Path $updateDir 'files') -Recurse -File
foreach ($f in $files) {
  $relative = $f.FullName.Substring((Join-Path $updateDir 'files').Length).TrimStart('\','/')
  $target = Join-Path $project $relative
  if (Test-Path $target) {
    $dest = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $dest -Force
  }
  New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
  Copy-Item -LiteralPath $f.FullName -Destination $target -Force
  Write-Host ('Updated: ' + $relative)
}
Write-Host 'Applied. Your App_Data and exported documents are unchanged.' -ForegroundColor Green
Write-Host ('Backups: ' + $backup)
