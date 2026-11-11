@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0APPLY_REVIEW_FIX.ps1"
if errorlevel 1 (
  echo Update was not applied successfully. Check the messages above.
  pause
)
