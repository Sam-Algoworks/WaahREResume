@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_ACCURACY_FIX.ps1"
if errorlevel 1 echo Installation stopped safely. Check the error before doing anything else.
pause
