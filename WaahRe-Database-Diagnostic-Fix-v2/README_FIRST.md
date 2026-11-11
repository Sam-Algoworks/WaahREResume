# Waah रे Resume — Database Diagnostic Fix v2

This version fixes the PowerShell Stage 2 error: `Keyword not supported: 'ConnectTimeout'`. It uses supported SQL Server connection-string keys (`Connect Timeout` and `Application Name`). The script is READ ONLY. It does not change the application, user secrets, existing data, or database configuration.

## Install and run

1. Close the previous diagnostic command window.
2. Extract this folder inside the main RoleFit-AI directory, beside `backend` and `frontend`.
3. Double-click `CHECK_DATABASE_DIAGNOSTIC.bat` in this v2 folder.
4. Observe Stage 3 (SQL connection) and Stage 4 (table counts), or share the next diagnostic error. Never share a connection string or secrets.

## Manual edit alternative

Open the existing `CHECK_DATABASE_DIAGNOSTIC.ps1` and replace:

```powershell
$csb.ConnectTimeout = 8
$csb.ApplicationName = 'WaahRe-ReadOnly-Diagnostic'
```

with:

```powershell
$csb['Connect Timeout'] = 8
$csb['Application Name'] = 'WaahRe-ReadOnly-Diagnostic'
```

The package was structurally verified; Windows PowerShell / SQL LocalDB execution is not available in the authoring environment. Reaching Stage 4 verifies the SQL endpoint and tables, but the API's actual storage mode must be tested separately.
