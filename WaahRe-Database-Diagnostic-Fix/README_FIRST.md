# Waah Re Resume — Database checker diagnostic fix

This package **does not modify your application or database**. It replaces the silent diagnostic with step-by-step progress reporting.

## Install and run

1. Extract this entire `WaahRe-Database-Diagnostic-Fix` folder into the existing `RoleFit-AI` root (beside `backend` and `frontend`).
2. Run `CHECK_DATABASE_DIAGNOSTIC.bat` by double-clicking it.
3. The checker prints stages 1 through 4. Share the first error and the last stage displayed, without sharing secrets.

## What the checker does

- Reads the `ConnectionStrings:RoleFitHistory` setting from the current Windows user's .NET user-secrets file, using the exact `UserSecretsId` in `RoleFit.Api.csproj`, **without launching the `dotnet` command**. This avoids one possible source of the previous checker hanging. Also supports `ConnectionStrings__RoleFitHistory` environment variable.
- Opens SQL with an 8-second connection timeout and runs read-only counts on `dbo.Jobs`, `dbo.Analyses`, `dbo.Drafts`, and `dbo.Archives`.
- Never prints the full connection string or passwords and never imports, edits, deletes, or backs up records.

If it reaches `DATABASE: CONNECTED - SQL MODE VERIFIED`, test that the backend also uses SQL and that your saved history appears after restart. The next step after that is backup verification, not deletion yet.

If Stage 1 fails, open PowerShell at `backend/RoleFit.Api` and run:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RoleFitHistory" "Server=(localdb)\MSSQLLocalDB;Database=WaahReHistory;Integrated Security=true;TrustServerCertificate=true;"
```

Do not include connection strings, user secrets, private candidate resumes, or `App_Data` when sharing troubleshooting screenshots.
