# Waah रे Resume — Phase 2 verification & private recovery checkpoint

This is a **verification-and-backup package, not a code replacement**. It deliberately leaves the working parser, ATS, Groq, frontend, PDF/DOCX exports, and database implementation unchanged. No login is added.

## Why this is the next step

The last SQL Server patch was **opt-in**. The app can still run in the original JSON mode if `ConnectionStrings:RoleFitHistory` is not configured. Therefore “the site still works” does **not** prove it is using SQL Server. We must confirm the four tables and prepare recoverable copies before implementing deletion or retention.

## How to use

1. Put the entire `WaahRe-Phase2-Verify-Backup` folder into the main `RoleFit-AI` project root, next to `backend` and `frontend`.
2. Run `CHECK_DATABASE.bat` under the same Windows account you use for Visual Studio. The script is **read-only** and never prints your connection string.
3. You should see `DATABASE: CONNECTED`, plus counts for `Jobs`, `Analyses`, `Drafts`, and `Archives`.
4. In the application, save one **synthetic/sample** resume profile and ATS analysis, then rerun the checker. Confirm the SQL counts increase and the archive file check reports no missing originals.
5. Shut down **both** app processes. Run `BACKUP_PRIVATE_DATA.bat`. Type `BACKUP` to confirm they are stopped. This backs up `App_Data` and the default Windows Data Protection keys. If a SQL connection is configured, it also **attempts** `BACKUP DATABASE`.
6. Only proceed to record deletion after the backup status says SQL is complete and the encryption key location has been checked. Backups must be kept **outside GitHub** and never sent in chat.

## If the checker reports NOT ENABLED

Open a terminal at `backend/RoleFit.Api`:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RoleFitHistory" "Server=(localdb)\MSSQLLocalDB;Database=WaahReHistory;Integrated Security=true;TrustServerCertificate=true;"
```

Use the connection details for **your actual SQL Server instance**, if different. Restart the API, then run the checker again. Existing JSON imports happen at first SQL-enabled startup. Never overwrite existing candidate records to force migration.

## If SQL backup fails

The SQL Server service account might not be allowed to write inside your Windows user folder. Use SQL Server Management Studio **Back Up Database** to create and verify a `.bak` file in a directory accessible to SQL Server. The backup is **incomplete** until both the database and original encrypted files are backed up. The script prints a warning rather than falsely claiming recovery is complete.

## Privacy and recovery limits

- Backups contain sensitive resumes and encryption keys. Keep them in a secure, access-restricted offline location, never in the source repository or on public cloud hosting.
- The original file bytes live in `backend/RoleFit.Api/App_Data/secure-originals` and are encrypted. SQL contains metadata and structured profiles—not the original bytes.
- Windows Data Protection keys **usually** live in `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` for this deployment setup. This script checks that path. If you configured a different key ring location, you must back up that location instead.
- A backup is **not validated** until successfully restored in a safe isolated test environment. Never run restore over the active application without a separate recovery plan.
- Without login, this stays a **local-only application** or a demo with synthetic data. Do not expose real HR candidate data via anonymous public endpoints.

## Next implementation after this checkpoint

A safe, locally restricted **Delete history record** action that removes only the selected candidate's archive metadata, encrypted original, profile draft, and linked ATS evaluations. It must respect file deduplication, require explicit confirmation, and be regression-tested against both SQL and JSON modes.
