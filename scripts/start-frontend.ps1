$ErrorActionPreference = 'Stop'
$projectFolder = Join-Path $PSScriptRoot '..\frontend\rolefit-ui'
Set-Location $projectFolder

Write-Host 'ROLEFIT AI - React Frontend' -ForegroundColor Cyan
Write-Host 'Preferred address: http://127.0.0.1:5180' -ForegroundColor Green
Write-Host 'Port 5173 is not used by this project.' -ForegroundColor Yellow
Write-Host ''

if (-not (Test-Path '.\node_modules\vite')) {
    Write-Host 'Installing frontend dependencies (first run only)...' -ForegroundColor Cyan
    npm.cmd install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'npm install failed. Check network connectivity and your Node.js version.' -ForegroundColor Red
        exit 1
    }
}

npm.cmd run dev
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Frontend did not start. Review the error above.' -ForegroundColor Red
}
