$ErrorActionPreference = 'Stop'
try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $apiDir = Join-Path $root 'backend\RoleFit.Api'
    $groq = Join-Path $apiDir 'Services\GroqProfileService.cs'
    $guard = Join-Path $apiDir 'Services\ProjectHeadingEvidence.cs'
    $checkTarget = Join-Path $root 'tests\RoleFit.ProjectEvidenceChecks'
    $guardSource = Join-Path $PSScriptRoot 'files\backend\RoleFit.Api\Services\ProjectHeadingEvidence.cs'
    $checkSource = Join-Path $PSScriptRoot 'files\tests\RoleFit.ProjectEvidenceChecks'
    $expected = 'if (anchor.Length < 25 || !ContainsNormalized(sourceText, anchor)) continue;'
    $newCall = 'if (!ProjectHeadingEvidence.HasIndependentHeading(sourceText, candidateTitle, selected.Select(p => p.Evidence))) continue;'

    Write-Host 'WAAH RE RESUME - PHASE 3 PROJECT EVIDENCE PATCH' -ForegroundColor Cyan
    Write-Host "Project root: $root"
    if (-not (Test-Path $groq)) { throw "SAFE STOP: Missing backend service at $groq" }
    if (-not (Test-Path (Join-Path $apiDir 'RoleFit.Api.csproj'))) { throw 'SAFE STOP: ASP.NET Core project missing.' }
    if (-not (Test-Path $guardSource)) { throw 'SAFE STOP: Project evidence guard is missing from this update.' }
    if (Test-Path $guard) { throw 'SAFE STOP: A ProjectHeadingEvidence.cs file already exists. Do not replace it blindly.' }
    if (Test-Path $checkTarget) { throw 'SAFE STOP: Existing ProjectEvidenceChecks test folder found. Nothing changed.' }

    $original = [System.IO.File]::ReadAllText($groq)
    if ($original.Contains('ProjectHeadingEvidence.HasIndependentHeading(')) {
        throw 'SAFE STOP: Evidence gate already appears in GroqProfileService.cs. Nothing changed.'
    }
    if (([regex]::Matches($original, [regex]::Escape($expected))).Count -ne 1) {
        throw 'SAFE STOP: GroqProfileService.cs has a different merge structure. No files changed. Upload your latest GroqProfileService.cs for a manual merge.'
    }

    $lineEnding = if ($original.Contains("`r`n")) { "`r`n" } else { "`n" }
    $addition = $expected + $lineEnding + '                // Only an independent source project heading can add a new AI project.' + $lineEnding + '                ' + $newCall
    $updated = $original.Replace($expected, $addition)
    if ($updated -eq $original) { throw 'SAFE STOP: Could not identify the Groq project evidence insertion point.' }

    Write-Host 'Prechecks passed. This update changes only Groq project recovery and adds a test project.' -ForegroundColor Green
    Write-Host 'No database, History, ATS score, frontend, or PDF/Word source files will change.'
    $answer = Read-Host 'Type YES to back up GroqProfileService.cs and apply the change'
    if ($answer -cne 'YES') { Write-Host 'Cancelled. Nothing changed.'; exit 0 }

    $backup = Join-Path $PSScriptRoot ('backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item $groq (Join-Path $backup 'GroqProfileService.cs')
    # First install new files. The original service is changed last.
    try {
        Copy-Item $guardSource $guard -ErrorAction Stop
        Copy-Item $checkSource $checkTarget -Recurse -ErrorAction Stop
        [System.IO.File]::WriteAllText($groq, $updated, (New-Object System.Text.UTF8Encoding($false)))
    } catch {
        [System.IO.File]::WriteAllText($groq, $original, (New-Object System.Text.UTF8Encoding($false)))
        if (Test-Path $guard) { Remove-Item $guard -Force }
        if (Test-Path $checkTarget) { Remove-Item $checkTarget -Recurse -Force }
        throw
    }
    Write-Host "SUCCESS: Phase 3 patch installed. Original service backed up in: $backup" -ForegroundColor Green
    Write-Host 'Next: dotnet build backend\RoleFit.Api\RoleFit.Api.csproj'
    Write-Host 'Then: dotnet run --project tests\RoleFit.ProjectEvidenceChecks\RoleFit.ProjectEvidenceChecks.csproj'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
