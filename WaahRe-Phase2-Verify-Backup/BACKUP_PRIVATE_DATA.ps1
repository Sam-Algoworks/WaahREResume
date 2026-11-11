$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $projectRoot 'backend\RoleFit.Api'
$projectFile = Join-Path $backend 'RoleFit.Api.csproj'
$dataDir = Join-Path $backend 'App_Data'
if (-not (Test-Path $projectFile)) { throw 'Move this folder into the RoleFit-AI project root.' }

function Get-HistoryConnection {
    if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__RoleFitHistory)) { return $env:ConnectionStrings__RoleFitHistory }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return '' }
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $lines = @(& dotnet user-secrets list --project $projectFile 2>$null)
        foreach ($line in $lines) {
            if ([string]$line -match '^ConnectionStrings:RoleFitHistory\s*=\s*(.+)$') { return $Matches[1].Trim() }
        }
    } finally { $ErrorActionPreference = $previousPreference }
    return ''
}

Write-Host 'PRIVATE BACKUP - contains candidate resumes and encryption keys.' -ForegroundColor Yellow
Write-Host 'STOP the ASP.NET API and Vite frontend before continuing. Use the same Windows account that runs the API.'
Write-Host 'Never upload backups to GitHub, public cloud drives, or this chat.'
$confirmation = Read-Host 'Type BACKUP to confirm both app processes are stopped'
if ($confirmation -cne 'BACKUP') { Write-Host 'Cancelled.'; exit 0 }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$baseDir = Join-Path $env:USERPROFILE 'WaahRe_PrivateBackups'
$target = Join-Path $baseDir ('Backup-' + $stamp)
New-Item -ItemType Directory -Path $target -Force | Out-Null
$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add('Waah Re Resume offline backup')
$manifest.Add('Created at: ' + (Get-Date).ToString('o'))
$manifest.Add('Project: ' + $projectRoot)

if (Test-Path $dataDir) {
    Copy-Item -LiteralPath $dataDir -Destination (Join-Path $target 'App_Data') -Recurse -Force
    $manifest.Add('App_Data: copied')
    Write-Host 'App_Data: copied.' -ForegroundColor Green
} else {
    $manifest.Add('App_Data: MISSING')
    Write-Host 'WARNING: App_Data was not found.' -ForegroundColor Yellow
}

$keyDir = Join-Path $env:LOCALAPPDATA 'ASP.NET\DataProtection-Keys'
if (Test-Path $keyDir) {
    Copy-Item -LiteralPath $keyDir -Destination (Join-Path $target 'DataProtection-Keys') -Recurse -Force
    $manifest.Add('DataProtection-Keys: copied from Windows user profile')
    Write-Host 'Local Data Protection keys: copied.' -ForegroundColor Green
} else {
    $manifest.Add('DataProtection-Keys: NOT FOUND in the default Windows location')
    Write-Host 'WARNING: Default Data Protection keys were not found. Encrypted files may be unrecoverable on another machine without those keys.' -ForegroundColor Yellow
}

$historyConnection = Get-HistoryConnection
$sqlBackupComplete = $false
if (-not [string]::IsNullOrWhiteSpace($historyConnection)) {
    try {
        Add-Type -AssemblyName System.Data
        $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($historyConnection)
        $database = $builder.InitialCatalog
        if ([string]::IsNullOrWhiteSpace($database) -or $database -match '^(master|tempdb|model|msdb)$') {
            throw 'The configured SQL connection does not select a safe application database.'
        }
        $backupPath = Join-Path $target 'WaahReHistory.bak'
        $quotedDb = '[' + $database.Replace(']', ']]') + ']'
        $quotedPath = $backupPath.Replace("'", "''")
        $query = "BACKUP DATABASE $quotedDb TO DISK = N'$quotedPath' WITH COPY_ONLY, INIT, CHECKSUM"
        $connection = New-Object System.Data.SqlClient.SqlConnection($historyConnection)
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $command.CommandTimeout = 180
        [void]$command.ExecuteNonQuery()
        $connection.Close()
        $sqlBackupComplete = Test-Path $backupPath
        if (-not $sqlBackupComplete) { throw 'SQL Server reported success but no .bak file was found at the requested location.' }
        $manifest.Add('SQL Server: .bak created')
        Write-Host 'SQL database: backed up.' -ForegroundColor Green
    } catch {
        $manifest.Add('SQL Server: FAILED - needs separate SQL Server backup')
        Write-Host ('SQL BACKUP FAILED: ' + $_.Exception.Message) -ForegroundColor Red
        Write-Host 'SQL Server service account may not have permission to write to this directory. Back up using SQL Server Management Studio and verify that .bak exists.' -ForegroundColor Yellow
    } finally {
        if ($connection) { $connection.Dispose() }
    }
} else {
    $manifest.Add('SQL Server: NOT CONFIGURED - JSON mode only')
    Write-Host 'SQL connection is absent: backed up JSON storage only.' -ForegroundColor Yellow
}

$manifest | Set-Content -LiteralPath (Join-Path $target 'BACKUP_STATUS.txt') -Encoding UTF8
Write-Host ('Backup location: ' + $target) -ForegroundColor Cyan
if ($historyConnection -and -not $sqlBackupComplete) {
    Write-Host 'BACKUP IS INCOMPLETE: SQL database backup must be completed separately.' -ForegroundColor Red
    exit 3
}
if (-not (Test-Path $keyDir)) {
    Write-Host 'BACKUP NEEDS REVIEW: Data Protection encryption keys may be missing.' -ForegroundColor Yellow
    exit 4
}
Write-Host 'Backup files created. A restore test is still required before calling recovery verified.' -ForegroundColor Green
