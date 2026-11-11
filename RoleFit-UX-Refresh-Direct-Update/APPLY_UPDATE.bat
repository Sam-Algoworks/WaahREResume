@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_UPDATE.ps1"
if errorlevel 1 echo Update failed. Read the error above.
pause
