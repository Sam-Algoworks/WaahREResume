@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_PHASE5.ps1"
if errorlevel 1 (echo. & echo UPDATE DID NOT APPLY. See the error above.)
echo. & pause
