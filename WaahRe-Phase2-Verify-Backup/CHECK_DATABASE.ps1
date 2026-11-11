$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $projectRoot 'backend\RoleFit.Api'
$projectFile = Join-Path $backend 'RoleFit.Api.csproj'
if (-not (Test-Path $projectFile)) {
    Write-Host 'ERROR: Place the extracted WaahRe-Phase2-Verify-Backup folder inside the RoleFit-AI project root.' -ForegroundColor Red
    exit 1
}

function Get-HistoryConnection {
    if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__RoleFitHistory)) {
        return $env:ConnectionStrings__RoleFitHistory
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return '' }
    # Capture output only; never echo the secret value to console or into a file.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& dotnet user-secrets list --project $projectFile 2>$null)
        foreach ($line in $lines) {
            if ([string]$line -match '^ConnectionStrings:RoleFitHistory\s*=\s*(.+)$') {
                return $Matches[1].Trim()
            }
        }
    } finally { $ErrorActionPreference = $previousPreference }
    return ''
}

Write-Host 'Waah Re Resume - Phase 2 database verification (READ ONLY)' -ForegroundColor Cyan
Write-Host ('Project source: ' + $backend)
$historyConnection = Get-HistoryConnection
if ([string]::IsNullOrWhiteSpace($historyConnection)) {
    Write-Host 'DATABASE: NOT ENABLED. App is using its JSON fallback.' -ForegroundColor Yellow
    Write-Host 'Set ConnectionStrings:RoleFitHistory using dotnet user-secrets, as shown in README_FIRST.md.'
    exit 2
}

try {
    Add-Type -AssemblyName System.Data
    $connection = New-Object System.Data.SqlClient.SqlConnection($historyConnection)
    $connection.Open()
    $commands = @(
        'SELECT COUNT_BIG(*) FROM dbo.Jobs',
        'SELECT COUNT_BIG(*) FROM dbo.Analyses',
        'SELECT COUNT_BIG(*) FROM dbo.Drafts',
        'SELECT COUNT_BIG(*) FROM dbo.Archives'
    )
    $labels = @('Jobs', 'Analyses', 'Drafts', 'Archives')
    $counts = @{}
    for ($i = 0; $i -lt $commands.Count; $i++) {
        $command = $connection.CreateCommand()
        $command.CommandText = $commands[$i]
        $command.CommandTimeout = 15
        $counts[$labels[$i]] = [long]$command.ExecuteScalar()
    }
    Write-Host 'DATABASE: CONNECTED. All four expected tables are readable.' -ForegroundColor Green
    foreach ($label in $labels) { Write-Host ('  ' + $label + ': ' + $counts[$label]) }

    $dataDir = Join-Path $backend 'App_Data'
    $secureDir = Join-Path $dataDir 'secure-originals'
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'SELECT Id FROM dbo.Archives'
    $reader = $cmd.ExecuteReader()
    $missing = 0
    while ($reader.Read()) {
        $fileName = $reader.GetGuid(0).ToString('N') + '.bin'
        if (-not (Test-Path (Join-Path $secureDir $fileName))) { $missing++ }
    }
    $reader.Close()
    Write-Host ('Original file check: ' + ($counts['Archives'] - $missing) + ' encrypted archive files found; ' + $missing + ' metadata records missing files.')
    if ($missing -gt 0) {
        Write-Host 'WARNING: Some original documents are unavailable. Preserve App_Data and investigate before deleting or moving records.' -ForegroundColor Yellow
    }
    $keyDir = Join-Path $env:LOCALAPPDATA 'ASP.NET\DataProtection-Keys'
    if (Test-Path $keyDir) {
        $keyCount = @(Get-ChildItem -LiteralPath $keyDir -Filter '*.xml' -File -ErrorAction SilentlyContinue).Count
        Write-Host ('Local Data Protection key files: ' + $keyCount + ' (back these up privately with your data).')
    } else {
        Write-Host 'WARNING: Default local Data Protection key directory not found. Verify where this Windows user stores encryption keys.' -ForegroundColor Yellow
    }
    $connection.Close()
    Write-Host 'RESULT: SQL MODE VERIFIED. This checks connectivity and table visibility, not full data recovery.' -ForegroundColor Green
} catch {
    Write-Host ('DATABASE CHECK FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'The app may still appear to work in JSON fallback or could be using different Windows credentials.' -ForegroundColor Yellow
    exit 3
} finally {
    if ($connection) { $connection.Dispose() }
}
