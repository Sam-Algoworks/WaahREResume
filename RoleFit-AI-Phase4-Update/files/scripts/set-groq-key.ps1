$ErrorActionPreference = 'Stop'
Write-Host 'ROLEFIT AI - LOCAL GROQ KEY SETUP' -ForegroundColor Cyan
Write-Host 'Get an API key at https://console.groq.com/keys' -ForegroundColor Gray
Write-Host 'The key is stored for your Windows user, not saved in project files.' -ForegroundColor Yellow
$secure = Read-Host 'Paste your Groq API key' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $key = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    if ([string]::IsNullOrWhiteSpace($key)) { throw 'No API key provided.' }
    [Environment]::SetEnvironmentVariable('GROQ_API_KEY', $key, 'User')
    Write-Host 'Groq key saved for this Windows user.' -ForegroundColor Green
    Write-Host 'Close the API PowerShell window, then restart START_ROLEFIT.bat.' -ForegroundColor Yellow
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $key = $null
}
