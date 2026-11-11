$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $root 'backend/RoleFit.Api/RoleFit.Api.csproj'))) {
  throw 'Place WaahRe-Phase5-UX-Export-QA beside backend and frontend in the RoleFit-AI root.'
}
$expected = Get-Content -Raw (Join-Path $PSScriptRoot 'EXPECTED_HASHES.json') | ConvertFrom-Json
$relativePaths = @('frontend/rolefit-ui/src/App.tsx','frontend/rolefit-ui/src/components/TalentLibrary.tsx')
foreach ($relative in $relativePaths) {
  $target = Join-Path $root $relative
  if (-not (Test-Path $target)) { throw "SAFE STOP: Missing $relative" }
  $propertyName = $relative
  $expectedHash = $expected.PSObject.Properties[$propertyName].Value
  $actualHash = (Get-FileHash -Algorithm SHA256 -Path $target).Hash.ToLowerInvariant()
  if ($actualHash -ne $expectedHash) { throw "SAFE STOP: $relative differs from reviewed source. Your files were NOT changed. Upload your latest file for manual merge." }
}
$qaTarget = Join-Path $root 'tests/RoleFit.ExportRoundtripChecks'
if (Test-Path $qaTarget) { throw 'SAFE STOP: Export test folder already exists. Nothing changed. Do not overwrite your existing tests.' }
Write-Host ''
Write-Host 'Exact-source checks passed. This update changes two frontend UI files and installs a separate read-only export test.' -ForegroundColor Cyan
$confirm = Read-Host 'Type YES to apply Phase 5'
if ($confirm -cne 'YES') { Write-Host 'Cancelled. Nothing changed.'; exit 0 }
$backups = Join-Path $root ('Backups/Phase5-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $backups | Out-Null
foreach ($relative in $relativePaths) {
  $dest = Join-Path $backups $relative
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
  Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $dest -ErrorAction Stop
}
Write-Host "Source backups created: $backups" -ForegroundColor Green
try {
  foreach ($relative in $relativePaths) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('files/' + $relative)) -Destination (Join-Path $root $relative) -Force -ErrorAction Stop
  }
  New-Item -ItemType Directory -Force -Path $qaTarget | Out-Null
  Copy-Item -Path (Join-Path $PSScriptRoot 'tests/RoleFit.ExportRoundtripChecks/*') -Destination $qaTarget -Force -ErrorAction Stop
  Write-Host 'PHASE 5 APPLIED SUCCESSFULLY' -ForegroundColor Green
} catch {
  Write-Host 'Copy failed. Restoring both frontend files from backups.' -ForegroundColor Red
  foreach ($relative in $relativePaths) {
    Copy-Item -LiteralPath (Join-Path $backups $relative) -Destination (Join-Path $root $relative) -Force
  }
  throw
}
Write-Host 'Next: cd frontend/rolefit-ui ; npm.cmd run build'
Write-Host 'For PDF/DOCX: see README_FIRST.md for the exact run command.'
