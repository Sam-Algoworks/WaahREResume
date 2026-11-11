$ErrorActionPreference = 'Stop'
$updateFolder = $PSScriptRoot
$projectRoot = Split-Path -Parent $updateFolder
$targetSrc = Join-Path $projectRoot 'frontend\rolefit-ui\src'
$sourceSrc = Join-Path $updateFolder 'files\frontend\rolefit-ui\src'
$files = @(
  'App.tsx',
  'main.tsx',
  'ReviewFlow.css',
  'components\CandidateReview.tsx',
  'components\ResumeParserPage.tsx',
  'components\TalentLibrary.tsx'
)
Write-Host ''
Write-Host 'Waah Re Resume - Candidate Review Frontend Update' -ForegroundColor Cyan
Write-Host "Project: $projectRoot"
if (-not (Test-Path (Join-Path $projectRoot 'backend\RoleFit.Api'))) {
  Write-Host 'STOP: Place the entire update folder inside your existing RoleFit-AI project folder.' -ForegroundColor Red
  Read-Host 'Press Enter to close'
  exit 1
}
if (-not (Test-Path (Join-Path $targetSrc 'App.tsx'))) {
  Write-Host 'STOP: Your frontend source was not found. No files were changed.' -ForegroundColor Red
  Read-Host 'Press Enter to close'
  exit 1
}
foreach ($relative in $files) {
  if (-not (Test-Path (Join-Path $sourceSrc $relative))) {
    Write-Host "STOP: Update package is missing $relative. No files were changed." -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
  }
}
Write-Host 'This will replace ONLY six frontend files (not backend, saved data, or print CSS).'
$answer = Read-Host 'Type YES to back up and apply'
if ($answer -cne 'YES') {
  Write-Host 'Cancelled. No changes made.'
  exit 0
}
$backupPath = Join-Path $projectRoot ("waahre_backups\review_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))
foreach ($relative in $files) {
  $destination = Join-Path $targetSrc $relative
  $source = Join-Path $sourceSrc $relative
  $folder = Split-Path -Parent $destination
  if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
  if (Test-Path $destination) {
    $copyBackup = Join-Path $backupPath $relative
    $backupFolder = Split-Path -Parent $copyBackup
    if (-not (Test-Path $backupFolder)) { New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null }
    Copy-Item $destination $copyBackup -Force
  }
}
try {
  foreach ($relative in $files) {
    Copy-Item (Join-Path $sourceSrc $relative) (Join-Path $targetSrc $relative) -Force
    Write-Host "Updated: $relative" -ForegroundColor Green
  }
  Write-Host "Backup: $backupPath" -ForegroundColor Cyan
  Write-Host 'Frontend update copied. Run npm.cmd run build in VS Code to verify compilation.' -ForegroundColor Green
} catch {
  Write-Host "Copy failed: $_" -ForegroundColor Red
  Write-Host "Restore backups from $backupPath before retrying." -ForegroundColor Yellow
  exit 1
}
Read-Host 'Press Enter to close'
