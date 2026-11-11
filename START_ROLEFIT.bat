@echo off
setlocal
cd /d "%~dp0"
title RoleFit AI Launcher

echo ======================================================
echo          ROLEFIT AI - LOCAL DEVELOPMENT

echo ======================================================
echo.
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET 10 SDK is not installed or not on PATH.
    echo Install from https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)
where node >nul 2>&1
if errorlevel 1 (
    echo ERROR: Node.js is not installed or not on PATH.
    echo Install from https://nodejs.org/
    pause
    exit /b 1
)
where npm.cmd >nul 2>&1
if errorlevel 1 (
    echo ERROR: npm.cmd was not found on PATH.
    pause
    exit /b 1
)
echo Starting API and frontend in separate PowerShell windows...
echo First launch needs an internet connection for NuGet and npm packages.
echo.
start "RoleFit AI API" powershell.exe -NoExit -ExecutionPolicy Bypass -File "%~dp0scripts\start-backend.ps1"
timeout /t 2 /nobreak >nul
start "RoleFit AI Frontend" powershell.exe -NoExit -ExecutionPolicy Bypass -File "%~dp0scripts\start-frontend.ps1"
echo.
echo Once the frontend terminal says ready, open:
echo   http://127.0.0.1:5180

echo If Vite says 5180 is occupied, use the URL it prints instead.
echo API health check: http://127.0.0.1:5099/api/health
echo.
echo You can close this launcher window. Leave the two server windows open.
pause
