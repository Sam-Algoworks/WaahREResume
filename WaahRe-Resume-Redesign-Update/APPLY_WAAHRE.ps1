$ErrorActionPreference = 'Stop'
$updateFolder = $PSScriptRoot
$project = Split-Path -Parent $updateFolder
$mustExist = @(
 'backend\RoleFit.Api\RoleFit.Api.csproj',
 'frontend\rolefit-ui\package.json',
 'frontend\rolefit-ui\src\App.tsx',
 'frontend\rolefit-ui\src\components\ResumeParserPage.tsx'
)
foreach ($name in $mustExist) {
 if (-not (Test-Path (Join-Path $project $name))) {
  throw "Could not find $name under $project. Place the entire update folder directly inside your RoleFit-AI project."
 }
}
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = Join-Path $project "WaahRe-Code-Backup-$timestamp"
$files = Get-ChildItem -LiteralPath (Join-Path $updateFolder 'files') -File -Recurse
foreach ($file in $files) {
 $relative = $file.FullName.Substring((Join-Path $updateFolder 'files').Length).TrimStart([char]'\', [char]'/')
 $destination = Join-Path $project $relative
 $backupDestination = Join-Path $backup $relative
 if (Test-Path $destination) {
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupDestination) | Out-Null
  Copy-Item -LiteralPath $destination -Destination $backupDestination -Force
 }
 New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
 Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
 Write-Host "Updated: $relative"
}
Write-Host ''
Write-Host 'WAAH RE RESUME UI UPDATE APPLIED.' -ForegroundColor Green
Write-Host "Backup of affected existing code: $backup"
Write-Host 'Your App_Data and existing saved records were not changed.'
Write-Host 'Next: Visual Studio -> Rebuild Solution; VS Code -> npm.cmd run build.'
