@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_SAFE_MERGE.ps1"
if errorlevel 1 (echo. & echo Installation did NOT complete. Read the reason above.)
pause
