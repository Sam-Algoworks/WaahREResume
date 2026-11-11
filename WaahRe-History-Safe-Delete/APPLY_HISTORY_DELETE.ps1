$ErrorActionPreference = 'Stop'
$patch = $PSScriptRoot
$project = Split-Path -Parent $patch
$expected = Get-Content (Join-Path $patch 'expected-source-sha256.json') -Raw | ConvertFrom-Json
$files = @(
  'backend/RoleFit.Api/Services/SqlHistoryRepository.cs',
  'backend/RoleFit.Api/Services/ResumeArchiveStore.cs',
  'backend/RoleFit.Api/Controllers/ArchiveController.cs',
  'frontend/rolefit-ui/src/api.ts',
  'frontend/rolefit-ui/src/components/TalentLibrary.tsx',
  'frontend/rolefit-ui/src/App.tsx'
)
foreach ($relative in $files) {
  $target = Join-Path $project $relative
  if (-not (Test-Path $target)) { throw "Missing file $target. Place this update inside the RoleFit-AI root." }
  $hash = (Get-FileHash $target -Algorithm SHA256).Hash
  if ($hash -ne $expected.$relative) { throw "SAFE STOP: $relative differs from the reviewed source. Nothing changed. Do not force overwrite newer code." }
}
Write-Host 'All six source files match the reviewed baseline.' -ForegroundColor Green
Write-Host 'Back up both SQL WaahReHistory and the private App_Data directory BEFORE installing.' -ForegroundColor Yellow
$answer = Read-Host 'After completing a private data backup, type YES to install'
if ($answer -cne 'YES') { Write-Host 'Cancelled'; exit 0 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $project "HistoryDeleteSourceBackup-$stamp"
New-Item -ItemType Directory $backup -Force | Out-Null
foreach ($relative in $files) {
  $destination = Join-Path $backup $relative
  New-Item -ItemType Directory (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item (Join-Path $project $relative) $destination -Force
}
foreach ($relative in $files) { Copy-Item (Join-Path $patch ('files/' + $relative)) (Join-Path $project $relative) -Force }
Write-Host "Installed. Source backup at: $backup" -ForegroundColor Green
Write-Host 'Build backend and frontend. Test only with a synthetic resume first.'
