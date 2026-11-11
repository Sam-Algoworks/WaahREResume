$ErrorActionPreference = "Stop"
$folder = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $folder
$source = Join-Path $folder "files"
$backup = Join-Path $folder "backup"
if (-not (Test-Path (Join-Path $root "backend/RoleFit.Api/Program.cs")) -or
    -not (Test-Path (Join-Path $root "frontend/rolefit-ui/src/App.tsx"))) {
    Write-Host "ERROR: Place the extracted update folder INSIDE your existing RoleFit-AI folder." -ForegroundColor Red
    exit 1
}
Write-Host "RoleFit UX Refresh" -ForegroundColor Cyan
Write-Host "Target: $root"
Write-Host "Backend data, existing Groq code and print CSS will NOT be replaced."
$reply = Read-Host "Type YES to apply the update"
if ($reply -ne "YES") { Write-Host "Cancelled."; exit 0 }
$relativePaths = @(
    "backend/RoleFit.Api/Controllers/ResumesController.cs",
    "backend/RoleFit.Api/Services/ProfileDraftStore.cs",
    "frontend/rolefit-ui/src/App.tsx",
    "frontend/rolefit-ui/src/main.tsx",
    "frontend/rolefit-ui/src/api.ts",
    "frontend/rolefit-ui/src/components/ResumeParserPage.tsx",
    "frontend/rolefit-ui/src/components/TalentLibrary.tsx",
    "frontend/rolefit-ui/src/Experience.css"
)
foreach ($rel in $relativePaths) {
    $from = Join-Path $source $rel
    $to = Join-Path $root $rel
    if (-not (Test-Path $from)) { throw "Missing update file: $from" }
    $toDir = Split-Path -Parent $to
    New-Item -ItemType Directory -Force -Path $toDir | Out-Null
    if (Test-Path $to) {
        $back = Join-Path $backup $rel
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $back) | Out-Null
        Copy-Item -LiteralPath $to -Destination $back -Force
    }
    Copy-Item -LiteralPath $from -Destination $to -Force
    Write-Host "Updated $rel" -ForegroundColor Green
}
Write-Host "Update applied. Backups are in $backup" -ForegroundColor Cyan
Write-Host "Rebuild Visual Studio backend and run npm.cmd run build in VS Code." 
