$ErrorActionPreference = 'Stop'
$patch = $PSScriptRoot
$project = Split-Path -Parent $patch
$expected = Get-Content (Join-Path $patch 'expected-source-sha256.json') -Raw | ConvertFrom-Json
$files = @(
  'backend/RoleFit.Api/Program.cs',
  'backend/RoleFit.Api/RoleFit.Api.csproj',
  'backend/RoleFit.Api/Services/LocalStore.cs',
  'backend/RoleFit.Api/Services/ProfileDraftStore.cs',
  'backend/RoleFit.Api/Services/ResumeArchiveStore.cs'
)
foreach ($relative in $files) {
  $target = Join-Path $project $relative
  if (-not (Test-Path $target)) { throw "Cannot find $target. Put this update inside RoleFit-AI root." }
  $hash = (Get-FileHash $target -Algorithm SHA256).Hash
  if ($hash -ne $expected.$relative) {
    throw "SAFE STOP: $relative has changed from the reviewed version. No changes applied. Merge this update with your newer code instead of overwriting it."
  }
}
$newFiles = @('backend/RoleFit.Api/Services/HistoryDbContext.cs','backend/RoleFit.Api/Services/SqlHistoryRepository.cs')
foreach ($relative in $newFiles) {
  if (Test-Path (Join-Path $project $relative)) { throw "SAFE STOP: $relative already exists. Nothing has been changed." }
}
$answer = Read-Host 'Ready to back up and update SQL storage. Type YES to proceed'
if ($answer -cne 'YES') { Write-Host 'Cancelled.'; exit 0 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $project "DatabasePhase2Backup-$stamp"
New-Item -ItemType Directory $backup -Force | Out-Null
foreach ($relative in $files) {
  $destination = Join-Path $backup $relative
  New-Item -ItemType Directory (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item (Join-Path $project $relative) $destination -Force
}
# App_Data is NOT overwritten, relocated, or deleted. Back up it independently before SQL import.
foreach ($relative in ($files + $newFiles)) {
  Copy-Item (Join-Path $patch ('files/' + $relative)) (Join-Path $project $relative) -Force
}
Write-Host "Source files updated. Backup saved at $backup" -ForegroundColor Green
Write-Host 'Next: dotnet restore; dotnet build; set ConnectionStrings:RoleFitHistory using user-secrets.'
Write-Host 'SQL import happens only after connection string is configured. Old JSON stays untouched.'
