$ErrorActionPreference = 'Stop'
$projectFolder = Join-Path $PSScriptRoot '..\backend\RoleFit.Api'
Set-Location $projectFolder

Write-Host 'ROLEFIT AI - ASP.NET Core Backend' -ForegroundColor Cyan
Write-Host 'API endpoint: http://127.0.0.1:5099/api/health' -ForegroundColor Green
Write-Host 'If port 5099 is already in use, stop the other API instance before running this one.' -ForegroundColor Yellow
Write-Host ''

$env:ASPNETCORE_URLS = 'http://127.0.0.1:5099'
dotnet run --no-launch-profile
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Backend did not start. Review the error above. The first run may need a NuGet restore.' -ForegroundColor Red
}
