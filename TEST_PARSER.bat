@echo off
setlocal
cd /d "%~dp0"
echo Testing RoleFit AI parser regression cases...
echo.
dotnet run --project "tests\RoleFit.ParserChecks\RoleFit.ParserChecks.csproj"
if errorlevel 1 (
 echo.
 echo Parser test FAILED. Save the full error message for review.
) else (
 echo.
 echo Parser checks PASSED.
)
pause
