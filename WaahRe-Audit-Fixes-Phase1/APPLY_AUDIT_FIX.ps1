$ErrorActionPreference = 'Stop'
$patch = $PSScriptRoot
$project = Split-Path -Parent $patch
$targets = @('backend/RoleFit.Api/Program.cs','backend/RoleFit.Api/Services/AlignmentEngine.cs')
$expected = Get-Content (Join-Path $patch 'original-sha256.json') -Raw | ConvertFrom-Json
foreach ($relative in $targets) {
  $full = Join-Path $project $relative
  if (!(Test-Path $full)) { throw "Missing required project file: $full. Move the update folder into the RoleFit-AI project root." }
  $expectedHash = $expected.$relative
  $actualHash = (Get-FileHash -Path $full -Algorithm SHA256).Hash
  if ($actualHash -ne $expectedHash) { throw "STOP: Your local $relative differs from the reviewed source. Nothing has been changed. Do not replace newer working files blindly." }
}
$answer = Read-Host 'The two files match the reviewed baseline. Apply backup-first patch? Type YES'
if ($answer -cne 'YES') { Write-Host 'Cancelled.'; exit 0 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $project "AuditFixBackup-$stamp"
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach ($relative in $targets) {
  $original = Join-Path $project $relative
  $destination = Join-Path $backup $relative
  New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
  Copy-Item $original $destination
  Copy-Item (Join-Path (Join-Path $patch 'files') $relative) $original -Force
}
$testRelative = 'tests/RoleFit.ScoringChecks'
$testTarget = Join-Path $project $testRelative
if (Test-Path $testTarget) {
  Write-Warning "ScoringChecks already exists. Leaving it untouched."
} else {
  Copy-Item (Join-Path (Join-Path $patch 'files') $testRelative) $testTarget -Recurse
}
Write-Host "Completed source update. Backup: $backup" -ForegroundColor Green
Write-Host 'Next: Rebuild Solution, run RoleFit.ScoringChecks, then RoleFit.ParserChecks.'
