@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0CHECK_DATABASE.ps1"
echo.
pause
