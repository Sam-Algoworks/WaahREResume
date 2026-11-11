$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $payload = Join-Path $PSScriptRoot 'files'
    $bases = Join-Path $PSScriptRoot 'reviewed_base'
    $manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json

    Write-Host ''
    Write-Host 'WAAH RE RESUME - INTELLIGENCE V2.3 - MANUAL GROQ MERGE' -ForegroundColor Cyan
    Write-Host "Project: $root"
    Write-Host 'This update will NEVER force-replace an unreviewed source file.'

    if (-not (Test-Path (Join-Path $root 'backend\RoleFit.Api\RoleFit.Api.csproj'))) {
        throw 'SAFE STOP: Extract this update beside backend and frontend, in the main RoleFit-AI folder.'
    }
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if (-not $git) { $git = Get-Command git -ErrorAction SilentlyContinue }
    if (-not $git) { throw 'SAFE STOP: Git for Windows is required for a safe three-way source merge. Install Git or upload current changed C# files for manual merging.' }

    $newFiles = @($manifest.new)
    $targetFiles = @($manifest.files)
    $testSource = Join-Path $payload $manifest.testFolder
    $testTarget = Join-Path $root $manifest.testFolder
    if (-not (Test-Path $testSource)) { throw 'SAFE STOP: Regression test files are missing from this ZIP.' }
    if (Test-Path $testTarget) { throw 'SAFE STOP: IntelligenceV2Checks tests already exist. Nothing changed.' }

    $temp = Join-Path ([IO.Path]::GetTempPath()) ('WaahReV2Merge_' + [guid]::NewGuid().ToString('N'))
    New-Item -Path $temp -ItemType Directory -Force | Out-Null
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try {
        # Fully prepare every file before asking for approval. This is read-only to the working project.
        foreach ($relative in $targetFiles) {
            if ($relative -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($relative)) {
                throw "SAFE STOP: Invalid relative path in manifest: $relative"
            }
            $source = Join-Path $payload $relative
            $target = Join-Path $root $relative
            $staged = Join-Path $temp $relative
            $parent = Split-Path $staged -Parent
            if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
            if (-not (Test-Path $source)) { throw "SAFE STOP: Package is missing $relative" }

            if ($relative -in $newFiles) {
                if (Test-Path $target) { throw "SAFE STOP: $relative already exists. No files changed." }
                Copy-Item $source $staged -Force
                Write-Host "ADD   $relative" -ForegroundColor Green
                continue
            }
            if (-not (Test-Path $target)) { throw "SAFE STOP: $relative is absent from your project. No files changed." }
            $actual = (Get-FileHash $target -Algorithm SHA256).Hash.ToUpperInvariant()
            $approved = @($manifest.expected.PSObject.Properties[$relative].Value)
            if ($actual -in $approved) {
                Copy-Item $source $staged -Force
                Write-Host "MATCH $relative" -ForegroundColor Green
                continue
            }

            # A changed source is NOT overwritten. Merge with a verified historic base.
            $baseFile = Join-Path $bases $relative
            if (-not (Test-Path $baseFile)) { throw "SAFE STOP: No reviewed base for $relative." }
            $baseHash = (Get-FileHash $baseFile -Algorithm SHA256).Hash.ToUpperInvariant()
            $approvedBase = [string]$manifest.reviewedBaseHashes.PSObject.Properties[$relative].Value
            if ($baseHash -ne $approvedBase) { throw "SAFE STOP: Reviewed baseline is damaged for $relative." }
            Write-Host "MERGE $relative (preserve your newer edits)" -ForegroundColor Yellow

            $ours = Join-Path $temp ([guid]::NewGuid().ToString('N') + '.ours')
            $common = Join-Path $temp ([guid]::NewGuid().ToString('N') + '.base')
            $theirs = Join-Path $temp ([guid]::NewGuid().ToString('N') + '.new')
            # Normalize line endings and UTF-8 BOM for consistent merging on Windows.
            $currentText = [IO.File]::ReadAllText($target)
            $currentRaw = [IO.File]::ReadAllBytes($target)
            $hasBom = $currentRaw.Length -ge 3 -and $currentRaw[0] -eq 239 -and $currentRaw[1] -eq 187 -and $currentRaw[2] -eq 191
            $crlf = $currentText.Contains("`r`n")
            [IO.File]::WriteAllText($ours, $currentText.Replace("`r`n", "`n").Replace("`r", "`n"), $utf8)
            [IO.File]::WriteAllText($common, ([IO.File]::ReadAllText($baseFile)).Replace("`r`n", "`n").Replace("`r", "`n"), $utf8)
            [IO.File]::WriteAllText($theirs, ([IO.File]::ReadAllText($source)).Replace("`r`n", "`n").Replace("`r", "`n"), $utf8)

            & $git.Source merge-file --quiet $ours $common $theirs
            $exitCode = $LASTEXITCODE
            if ($exitCode -ne 0) {
                throw "SAFE STOP: $relative has conflicting edits (git merge-file exit $exitCode). Nothing changed. Upload your CURRENT $relative for an exact manual merge."
            }
            $merged = [IO.File]::ReadAllText($ours)
            if ($merged.Contains('<<<<<<< ') -or $merged.Contains('>>>>>>> ')) {
                throw "SAFE STOP: Conflict markers in merged $relative. Nothing changed."
            }
            if ($crlf) { $merged = $merged.Replace("`n", "`r`n") }
            $encoding = New-Object System.Text.UTF8Encoding($hasBom)
            [IO.File]::WriteAllText($staged, $merged, $encoding)
        }

        Write-Host ''
        Write-Host 'PASS: Every source file is prepared in a temporary staging directory.' -ForegroundColor Green
        Write-Host 'No working project files have been modified.' -ForegroundColor Green
        Write-Host 'This is a CODE MERGE, not a verified runtime build; run dotnet build and tests afterwards.'
        $confirmation = Read-Host 'Type YES to create backups and apply all staged source files'
        if ($confirmation -cne 'YES') {
            Write-Host 'Cancelled. Nothing changed.' -ForegroundColor Yellow
            exit 0
        }
        $backup = Join-Path $PSScriptRoot ('backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        $applied = New-Object System.Collections.Generic.List[string]
        try {
            foreach ($relative in $targetFiles) {
                if ($relative -in $newFiles) { continue }
                $target = Join-Path $root $relative
                $backupFile = Join-Path $backup $relative
                $parent = Split-Path $backupFile -Parent
                if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
                Copy-Item $target $backupFile -Force -ErrorAction Stop
            }
            foreach ($relative in $targetFiles) {
                $target = Join-Path $root $relative
                Copy-Item (Join-Path $temp $relative) $target -Force -ErrorAction Stop
                $applied.Add($relative)
            }
            Copy-Item $testSource $testTarget -Recurse -ErrorAction Stop
            $applied.Add($manifest.testFolder)
        } catch {
            Write-Host 'Copy failure. Restoring original source files...' -ForegroundColor Yellow
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
        Write-Host ''
        Write-Host 'SUCCESS: Source files were merged/applied. Backup:' -ForegroundColor Green
        Write-Host $backup
        Write-Host 'NEXT: dotnet build backend\RoleFit.Api\RoleFit.Api.csproj; run IntelligenceV2Checks.'
        Write-Host 'The SQL database, App_Data, frontend and credentials were not changed.'
    } finally {
        if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
