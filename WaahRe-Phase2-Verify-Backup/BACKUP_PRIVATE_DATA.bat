@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BACKUP_PRIVATE_DATA.ps1"
echo.
pause
