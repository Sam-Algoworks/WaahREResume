@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_PHASE4.ps1"
echo.
if errorlevel 1 echo Installation stopped safely. Read the error above.
pause
