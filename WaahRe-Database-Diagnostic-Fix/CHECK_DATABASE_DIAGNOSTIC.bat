@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0CHECK_DATABASE_DIAGNOSTIC.ps1"
echo.
echo Diagnostic finished. Share a screenshot of the output (do not share passwords or a full connection string).
pause
