$ErrorActionPreference = 'Stop'

function Stage([string]$message) {
  Write-Host ''
  Write-Host ('>> ' + $message) -ForegroundColor Cyan
}
function Fail([string]$message, [int]$code=1) {
  Write-Host ('ERROR: ' + $message) -ForegroundColor Red
  exit $code
}

Write-Host 'Waah Re Resume - SQL verification diagnostic (READ ONLY)' -ForegroundColor Green
$projectRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $projectRoot 'backend\RoleFit.Api'
$projectFile = Join-Path $backend 'RoleFit.Api.csproj'
Write-Host ('Project: ' + $backend)
if (-not (Test-Path -LiteralPath $projectFile)) {
  Fail 'Put this WaahRe-Database-Diagnostic-Fix folder inside the main RoleFit-AI folder, next to backend and frontend.'
}

Stage '1/4: Finding the SQL connection setting (no secret values will be printed)'
$source = ''
$historyConnection = $env:ConnectionStrings__RoleFitHistory
if (-not [string]::IsNullOrWhiteSpace($historyConnection)) {
  $source = 'ConnectionStrings__RoleFitHistory environment variable'
} else {
  try {
    [xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
    $node = $projectXml.SelectSingleNode("//*[local-name()='UserSecretsId']")
    $secretsId = if ($null -ne $node) { [string]$node.InnerText } else { '' }
    if ([string]::IsNullOrWhiteSpace($secretsId)) {
      Fail 'No UserSecretsId in RoleFit.Api.csproj. Run dotnet user-secrets init from backend\RoleFit.Api, then set the connection string.' 2
    }
    $file = Join-Path (Join-Path (Join-Path $env:APPDATA 'Microsoft\UserSecrets') $secretsId) 'secrets.json'
    if (-not (Test-Path -LiteralPath $file)) {
      Fail 'No .NET user-secrets file for this project under the current Windows user. Set ConnectionStrings:RoleFitHistory for this project, then rerun.' 2
    }
    $data = (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json)
    if ($null -ne $data.PSObject.Properties['ConnectionStrings:RoleFitHistory']) {
      $historyConnection = [string]$data.'ConnectionStrings:RoleFitHistory'
    } elseif ($null -ne $data.ConnectionStrings -and $null -ne $data.ConnectionStrings.RoleFitHistory) {
      $historyConnection = [string]$data.ConnectionStrings.RoleFitHistory
    }
    $source = '.NET user-secrets for the backend project'
  } catch {
    Fail ('Could not read this project''s configuration: ' + $_.Exception.Message) 2
  }
}
if ([string]::IsNullOrWhiteSpace($historyConnection)) {
  Fail 'ConnectionStrings:RoleFitHistory is empty or missing. SQL is not enabled for the checker.' 2
}
Write-Host ('OK: SQL setting found via ' + $source) -ForegroundColor Green

Stage '2/4: Building a SQL connection with an 8-second connect timeout'
try {
  Add-Type -AssemblyName System.Data
  $csb = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($historyConnection)
  $csb.ConnectTimeout = 8
  $csb.ApplicationName = 'WaahRe-ReadOnly-Diagnostic'
  $safeServer = [string]$csb.DataSource
  $safeDb = [string]$csb.InitialCatalog
  Write-Host ('SQL instance: ' + $safeServer)
  Write-Host ('Database name: ' + $safeDb)
} catch {
  Fail ('Invalid SQL connection settings: ' + $_.Exception.Message) 3
}

Stage '3/4: Opening SQL connection (up to 8 seconds, potentially longer for LocalDB startup)'
$connection = $null
try {
  $connection = [System.Data.SqlClient.SqlConnection]::new($csb.ConnectionString)
  $connection.Open()
  Write-Host 'OK: SQL connection opened.' -ForegroundColor Green
} catch {
  Write-Host ('SQL CONNECTION FAILED: ' + $_.Exception.Message) -ForegroundColor Red
  Write-Host 'Check: sqllocaldb info MSSQLLocalDB, then sqllocaldb start MSSQLLocalDB' -ForegroundColor Yellow
  Write-Host 'If SQL LocalDB is running, check the server name, database availability, and the Windows account used to run this checker.' -ForegroundColor Yellow
  exit 4
}

Stage '4/4: Reading expected history tables (read-only)'
$tables = @('Jobs', 'Analyses', 'Drafts', 'Archives')
try {
  foreach ($table in $tables) {
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT COUNT_BIG(*) FROM dbo.[' + $table + ']'
    $command.CommandTimeout = 8
    $count = [long]$command.ExecuteScalar()
    Write-Host ('  ' + $table + ': ' + $count) -ForegroundColor Green
  }
  Write-Host ''
  Write-Host 'DATABASE: CONNECTED - SQL MODE VERIFIED' -ForegroundColor Green
  Write-Host 'This confirms connectivity and visible table counts, not that the API process is using this configuration or that backups can be restored.'
} catch {
  Write-Host ('SQL IS CONNECTED, BUT TABLE CHECK FAILED: ' + $_.Exception.Message) -ForegroundColor Yellow
  Write-Host 'The backend may not have initialized its SQL schema. Check backend startup logs. Do not delete the JSON App_Data folder.'
  exit 5
} finally {
  if ($null -ne $connection) { $connection.Dispose() }
}
