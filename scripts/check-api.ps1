try {
    $result = Invoke-RestMethod -Uri 'http://127.0.0.1:5099/api/health' -TimeoutSec 8
    Write-Host "API status: $($result.status)" -ForegroundColor Green
    Write-Host "Service: $($result.service)"
    Write-Host "Version: $($result.version)"
} catch {
    Write-Host 'API unreachable. Start the API window and check port 5099.' -ForegroundColor Red
    Write-Host $_.Exception.Message
    exit 1
}
