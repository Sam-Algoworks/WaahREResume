@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_INTELLIGENCE_V2.ps1"
set "exitcode=%errorlevel%"
echo.
pause
exit /b %exitcode%
