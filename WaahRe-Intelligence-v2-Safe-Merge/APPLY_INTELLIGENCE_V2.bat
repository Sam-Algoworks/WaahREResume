@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_INTELLIGENCE_V2.ps1"
set CODE=%ERRORLEVEL%
echo.
if not "%CODE%"=="0" echo No changes should have been made. Read SAFE STOP above.
pause
exit /b %CODE%
