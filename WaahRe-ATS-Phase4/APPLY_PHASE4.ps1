$ErrorActionPreference = 'Stop'
try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $apiDir = Join-Path $root 'backend\RoleFit.Api'
    $target = Join-Path $apiDir 'Services\AlignmentEngine.cs'
    $helper = Join-Path $apiDir 'Services\JdEvidenceRules.cs'
    $testTarget = Join-Path $root 'tests\RoleFit.ScoringPhase4Checks'
    $sourceRoot = Join-Path $PSScriptRoot 'files'
    $sourceEngine = Join-Path $sourceRoot 'backend\RoleFit.Api\Services\AlignmentEngine.cs'
    $sourceHelper = Join-Path $sourceRoot 'backend\RoleFit.Api\Services\JdEvidenceRules.cs'
    $sourceTests = Join-Path $sourceRoot 'tests\RoleFit.ScoringPhase4Checks'
    $expected = 'E93A37C083CB875A7441134D8F5FBE9DBB43E9943FBC2FB43FD56629DB9C9E3A'

    Write-Host 'WAAH RE RESUME - PHASE 4 ATS SCORING UPDATE' -ForegroundColor Cyan
    Write-Host "Project root: $root"
    if (-not (Test-Path (Join-Path $apiDir 'RoleFit.Api.csproj'))) {
        throw 'SAFE STOP: ASP.NET Core API project was not found. Extract this folder inside your main RoleFit-AI directory.'
    }
    foreach ($file in @($target, $sourceEngine, $sourceHelper, $sourceTests)) {
        if (-not (Test-Path $file)) { throw "SAFE STOP: Missing expected source: $file" }
    }
    if (Test-Path $helper) { throw 'SAFE STOP: JdEvidenceRules.cs already exists. This update may already be installed. No changes made.' }
    if (Test-Path $testTarget) { throw 'SAFE STOP: ScoringPhase4Checks already exists. No changes made.' }
    $hash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
    if ($hash -ne $expected) {
        throw "SAFE STOP: AlignmentEngine.cs is different from Phase 1. Your project is unchanged. Upload your CURRENT backend/RoleFit.Api/Services/AlignmentEngine.cs so I can merge Phase 4 without losing newer edits."
    }
    Write-Host 'PRECHECKS PASSED: Phase 1 scorer found. Only scoring backend and a new test project will change.' -ForegroundColor Green
    Write-Host 'Existing SQL history, Groq, parsing, React UI and PDF/Word exports are NOT modified.'
    $answer = Read-Host 'Type YES to back up AlignmentEngine.cs and install Phase 4'
    if ($answer -cne 'YES') { Write-Host 'Cancelled. No changes made.'; exit 0 }

    $backup = Join-Path $PSScriptRoot ('backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    Copy-Item $target (Join-Path $backup 'AlignmentEngine.cs') -ErrorAction Stop
    try {
        # Additive files first; change the existing scorer last. Roll back on IO errors.
        Copy-Item $sourceHelper $helper -ErrorAction Stop
        Copy-Item $sourceTests $testTarget -Recurse -ErrorAction Stop
        Copy-Item $sourceEngine $target -Force -ErrorAction Stop
    } catch {
        Copy-Item (Join-Path $backup 'AlignmentEngine.cs') $target -Force
        if (Test-Path $helper) { Remove-Item $helper -Force }
        if (Test-Path $testTarget) { Remove-Item $testTarget -Recurse -Force }
        throw
    }
    Write-Host "SUCCESS: ATS Phase 4 installed. Backup: $backup" -ForegroundColor Green
    Write-Host 'NEXT: Rebuild Visual Studio RoleFit.Api and run the Phase 4 regression tests.' -ForegroundColor Yellow
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
