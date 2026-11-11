@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_DATABASE_UPDATE.ps1"
echo.
pause
