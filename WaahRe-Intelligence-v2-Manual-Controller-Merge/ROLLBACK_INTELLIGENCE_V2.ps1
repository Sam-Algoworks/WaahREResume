$ErrorActionPreference = 'Stop'
try {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $marker = Join-Path $PSScriptRoot 'LAST_BACKUP.txt'
    if (-not (Test-Path $marker)) { throw 'No backup marker found. Rollback not available.' }
    $backup = (Get-Content $marker -Raw).Trim()
    if (-not (Test-Path $backup)) { throw 'The recorded backup folder is missing. Nothing changed.' }
    $manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
    Write-Host "Source backup: $backup"
    $answer = Read-Host 'Type ROLLBACK to restore the pre-update backend source'
    if ($answer -cne 'ROLLBACK') { Write-Host 'Cancelled'; exit 0 }
    foreach ($relative in @($manifest.files)) {
        $target = Join-Path $root $relative
        if ($relative -in @($manifest.new)) {
            if (Test-Path $target) { Remove-Item $target -Force }
        } else {
            $source = Join-Path $backup $relative
            if (-not (Test-Path $source)) { throw "Missing backup $relative" }
            Copy-Item $source $target -Force
        }
    }
    $tests = Join-Path $root $manifest.testFolder
    if (Test-Path $tests) { Remove-Item $tests -Force -Recurse }
    Write-Host 'Previous backend source restored. Candidate history and database unchanged.' -ForegroundColor Green
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
