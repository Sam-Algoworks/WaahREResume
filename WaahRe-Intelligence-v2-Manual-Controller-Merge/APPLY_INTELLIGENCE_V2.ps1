$ErrorActionPreference = 'Stop'
try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $src = Join-Path $PSScriptRoot 'files'
    $manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
    Write-Host ''
    Write-Host 'WAAH RE RESUME - INTELLIGENCE V2 COMPLETE FIX' -ForegroundColor Cyan
    Write-Host "Project root: $root"
    Write-Host 'This is a BACKEND-ONLY source update; no database migrations or UI replacements.'
    if (-not (Test-Path (Join-Path $root 'backend\RoleFit.Api\RoleFit.Api.csproj'))) {
        throw 'SAFE STOP: Place this extracted folder inside your main RoleFit-AI directory (beside backend and frontend).'
    }
    $newFiles = @($manifest.new)
    $allFiles = @($manifest.files)
    foreach ($relative in $allFiles) {
        $from = Join-Path $src $relative
        if (-not (Test-Path $from)) { throw "SAFE STOP: Missing replacement file $relative" }
        $target = Join-Path $root $relative
        if ($relative -in $newFiles) {
            if (Test-Path $target) { throw "SAFE STOP: $relative already exists. Do not overwrite manually." }
            continue
        }
        if (-not (Test-Path $target)) { throw "SAFE STOP: $relative is missing in the working source." }
        $actual = (Get-FileHash $target -Algorithm SHA256).Hash.ToUpperInvariant()
        $allowed = @($manifest.expected.PSObject.Properties[$relative].Value)
        if ($actual -notin $allowed) {
            Write-Host "Expected reviewed version(s): $($allowed -join ', ')" -ForegroundColor DarkYellow
            Write-Host "Found current SHA256: $actual" -ForegroundColor DarkYellow
            throw "SAFE STOP: $relative differs from the reviewed version. Nothing changed. Upload this file for a manual merge."
        }
    }
    $testDestination = Join-Path $root $manifest.testFolder
    if (Test-Path $testDestination) { throw 'SAFE STOP: IntelligenceV2Checks tests already exist. Nothing changed.' }
    $testSource = Join-Path $src $manifest.testFolder
    if (-not (Test-Path $testSource)) { throw 'SAFE STOP: Regression tests are missing from this package.' }
    Write-Host 'Source checks passed. Existing backend source matches the reviewed versions.' -ForegroundColor Green
    Write-Host 'Changes: experience evidence, project validation, contextual Groq matching, shared ranking rubric.'
    Write-Host 'Not changed: React UI, SQL history, candidate files, Groq credentials, and PDF/DOCX exporters.'
    $confirmation = Read-Host 'Type YES to back up these source files and install v2'
    if ($confirmation -cne 'YES') { Write-Host 'Cancelled. Nothing changed.'; exit 0 }

    $backup = Join-Path $PSScriptRoot ('backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    New-Item -Path $backup -ItemType Directory -Force | Out-Null
    $applied = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($relative in $allFiles) {
            if ($relative -in $newFiles) { continue }
            $target = Join-Path $root $relative
            $backupPath = Join-Path $backup $relative
            $parent = Split-Path $backupPath -Parent
            if (-not (Test-Path $parent)) { New-Item $parent -ItemType Directory -Force | Out-Null }
            Copy-Item $target $backupPath -ErrorAction Stop
        }
        foreach ($relative in $allFiles) {
            $target = Join-Path $root $relative
            $parent = Split-Path $target -Parent
            if (-not (Test-Path $parent)) { New-Item $parent -ItemType Directory -Force | Out-Null }
            Copy-Item (Join-Path $src $relative) $target -Force -ErrorAction Stop
            $applied.Add($relative)
        }
        Copy-Item $testSource $testDestination -Recurse -ErrorAction Stop
        $applied.Add($manifest.testFolder)
    } catch {
        Write-Host 'Copy failed; restoring source backups...' -ForegroundColor Yellow
        foreach ($relative in $applied) {
            $target = Join-Path $root $relative
            if ($relative -in $newFiles -or $relative -eq $manifest.testFolder) {
                if (Test-Path $target) { Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue }
            } else {
                Copy-Item (Join-Path $backup $relative) $target -Force -ErrorAction SilentlyContinue
            }
        }
        throw
    }
    Set-Content -Path (Join-Path $PSScriptRoot 'LAST_BACKUP.txt') -Value $backup -Encoding UTF8
    Write-Host "SUCCESS. Source backup created: $backup" -ForegroundColor Green
    Write-Host 'Now run the .NET build and intelligence regression tests as documented in README_FIRST.md.'
    Write-Host 'No data, SQL schema, frontend styling or credentials were modified.'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
