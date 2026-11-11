# Waah re Resume - targeted SQL cancellation fix (no data/schema changes)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'backend\RoleFit.Api'
$repoPath = Join-Path $api 'Services\SqlHistoryRepository.cs'
$programPath = Join-Path $api 'Program.cs'
$basePath = Join-Path $PSScriptRoot 'reference\SqlHistoryRepository.before.cs'
$patchedPath = Join-Path $PSScriptRoot 'reference\SqlHistoryRepository.after.cs'
$programBasePath = Join-Path $PSScriptRoot 'reference\Program.before.cs'
$programPatchedPath = Join-Path $PSScriptRoot 'reference\Program.after.cs'

function Normalize-Lines([string]$text) { return $text.Replace("`r`n", "`n") }
function Get-Section([string]$text, [string]$start, [string]$end) {
    $a = $text.IndexOf($start, [StringComparison]::Ordinal)
    if ($a -lt 0) { throw "SAFE STOP: expected method start is absent: $start" }
    $b = $text.IndexOf($end, $a + $start.Length, [StringComparison]::Ordinal)
    if ($b -lt 0) { throw "SAFE STOP: expected method end is absent: $end" }
    return $text.Substring($a, $b - $a)
}
function Read-Normal([string]$p) { return Normalize-Lines ([IO.File]::ReadAllText($p)) }

try {
    foreach ($path in @($repoPath,$programPath,$basePath,$patchedPath,$programBasePath,$programPatchedPath)) {
        if (!(Test-Path -LiteralPath $path)) { throw "SAFE STOP: cannot find $path . Extract the entire update folder inside RoleFit-AI." }
    }
    $repoCurrent = Read-Normal $repoPath
    $programCurrent = Read-Normal $programPath
    if ($repoCurrent.Contains('SQL history read cancelled by request') -or $programCurrent.Contains('Browser cancelled {Method} {Path}')) {
        throw 'SAFE STOP: this update looks partly or fully applied already. No changes made; inspect the files before retrying.'
    }
    $before = Read-Normal $basePath
    $after = Read-Normal $patchedPath
    $programBefore = Read-Normal $programBasePath
    $programAfter = Read-Normal $programPatchedPath
    $start = '    public async Task<StoredData> ReadLocalAsync(CancellationToken ct)'
    $end = '    public async Task<T> UpdateLocalAsync<T>(Func<StoredData, T> update, CancellationToken ct)'
    $oldMethod = Get-Section $before $start $end
    $newMethod = Get-Section $after '    // Only loads the JSON fields needed for this read.' $end
    if (!$repoCurrent.Contains($oldMethod)) {
        throw 'SAFE STOP: SqlHistoryRepository.ReadLocalAsync has changed since the reviewed version. Upload your current SqlHistoryRepository.cs for a manual merge.'
    }
    $oldConstructor = 'public sealed class SqlHistoryRepository(IDbContextFactory<HistoryDbContext> factory, IWebHostEnvironment environment)'
    $newConstructor = 'public sealed class SqlHistoryRepository(IDbContextFactory<HistoryDbContext> factory, IWebHostEnvironment environment, ILogger<SqlHistoryRepository> logger)'
    if (!$repoCurrent.Contains($oldConstructor)) {
        throw 'SAFE STOP: SqlHistoryRepository constructor differs from the reviewed version. Upload the current file for a manual merge.'
    }
    $newRepo = $repoCurrent.Replace($oldMethod, $newMethod).Replace($oldConstructor, $newConstructor)
    if (!$programCurrent.Contains('app.UseRateLimiter();')) {
        throw 'SAFE STOP: Program.cs no longer contains the reviewed middleware anchor.'
    }
    $programAnchor = 'app.UseRateLimiter();'
    $programInsert = Get-Section $programAfter '// Handles normal browser-aborted requests without turning them into an app error.' 'app.UseRateLimiter();'
    $newProgram = $programCurrent.Replace($programAnchor, $programInsert + $programAnchor)
    if ($newRepo -eq $repoCurrent -or $newProgram -eq $programCurrent) { throw 'SAFE STOP: no source changes were generated.' }

    Write-Host 'Source checks passed. This update modifies ONLY:' -ForegroundColor Green
    Write-Host ' - backend/RoleFit.Api/Services/SqlHistoryRepository.cs'
    Write-Host ' - backend/RoleFit.Api/Program.cs'
    Write-Host 'No candidate data, SQL table, frontend, or export file will be changed.'
    $answer = Read-Host 'Type YES to back up and apply'
    if ($answer -cne 'YES') { Write-Host 'Cancelled. Nothing changed.'; exit 0 }

    $backup = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $repoPath -Destination (Join-Path $backup 'SqlHistoryRepository.cs') -ErrorAction Stop
    Copy-Item -LiteralPath $programPath -Destination (Join-Path $backup 'Program.cs') -ErrorAction Stop
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $repoCrlf = [IO.File]::ReadAllText($repoPath).Contains("`r`n")
    $programCrlf = [IO.File]::ReadAllText($programPath).Contains("`r`n")
    if ($repoCrlf) { $newRepo = $newRepo.Replace("`n", "`r`n") }
    if ($programCrlf) { $newProgram = $newProgram.Replace("`n", "`r`n") }
    try {
        [IO.File]::WriteAllText($repoPath, $newRepo, $utf8)
        [IO.File]::WriteAllText($programPath, $newProgram, $utf8)
    } catch {
        Copy-Item -LiteralPath (Join-Path $backup 'SqlHistoryRepository.cs') -Destination $repoPath -Force
        Copy-Item -LiteralPath (Join-Path $backup 'Program.cs') -Destination $programPath -Force
        throw ('Update failed; source restored from backup. Details: ' + $_.Exception.Message)
    }
    Write-Host "APPLIED. Source backups: $backup" -ForegroundColor Green
    Write-Host 'Now rebuild the backend in Visual Studio and test History.'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
