# Waah रे Resume — Phase 2: SQL Server history migration (local development)

This is a **backend-only, backup-first update** for your existing RoleFit-AI project. **No login UI is added.** Your frontend, parser, Groq, ATS scoring, alignment, and PDF/DOCX export code remain untouched.

## What is implemented

- Optional SQL Server + EF Core 10 storage for jobs, ATS analyses, editable profile drafts, and **archive metadata**.
- Existing controllers keep the same API contracts. **History IDs and content are retained.**
- On the first SQL-enabled startup, the app creates tables and imports data from:
  - `App_Data/rolefit-local.json`
  - `App_Data/profile-drafts.json`
  - `App_Data/resume-archive.json`
- Import only runs for tables that are empty, so restarting does not duplicate records.
- Old JSON files remain untouched as a migration backup.
- If no SQL connection string is configured, existing JSON storage stays active (no instant breaking change).
- Original resume files remain protected with ASP.NET Core Data Protection under `App_Data/secure-originals`. **The SQL database alone is not a complete original-file backup.** The Data Protection keys also must be preserved for decryption.

## Apply safely

1. **Stop both app processes**, and make a **private offline backup of your whole `backend/RoleFit.Api/App_Data` directory**. Never upload it to GitHub or share it in chat; it may contain sensitive candidate data.
2. Extract `WaahRe-Database-Phase2` into your `RoleFit-AI` root, so the installer is at `RoleFit-AI/WaahRe-Database-Phase2/APPLY_DATABASE_UPDATE.bat`.
3. Run `APPLY_DATABASE_UPDATE.bat`, enter `YES`. The installer checks exact hashes of all 5 existing files and refuses to overwrite newer code. It backs up existing source before copying.
4. In Visual Studio, **restore NuGet packages**, then **Build → Rebuild Solution**. .NET 10 SDK is required because the existing project targets `net10.0`.
5. Install SQL Server LocalDB or SQL Server Express (or use an existing local SQL Server instance); ensure the database engine is running.
6. In a terminal opened at `backend/RoleFit.Api`, run:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RoleFitHistory" "Server=(localdb)\MSSQLLocalDB;Database=WaahReHistory;Integrated Security=true;TrustServerCertificate=true;"
dotnet restore
dotnet build
```

> `TrustServerCertificate=true` is for **local development only**. Never commit a real SQL password or API key. If using a different SQL instance, adjust the server name.

7. Launch the `RoleFit Local` **Development** profile. The first launch creates SQL tables and imports existing JSON history. **Keep App_Data in place**, even after import, because encrypted original PDF/DOCX files are still stored there.
8. Verify history count, open an archived original, save a new parsed profile, create a new ATS evaluation, restart, and confirm that all records remain.

## Rollback

Stop the API, remove the `ConnectionStrings:RoleFitHistory` user secret (`dotnet user-secrets remove "ConnectionStrings:RoleFitHistory"`), and restore the five source files from the `DatabasePhase2Backup-...` folder if required. **Warning:** records created only in SQL after migration will not appear in old JSON mode. Export/restore SQL records before switching back if any new history exists.

## Boundaries and known limitations

- This is **local SQL Server persistence**, not a public-hosting implementation. Phase 1's production startup block and loopback-only restriction remain active.
- No sign-in was added, as requested. Without authentication, public hosting must not expose real resumes; a restricted network or synthetic-data demo is required.
- Original PDF/DOCX bytes, encryption keys, and archives must be backed up **separately**. They are not moved into SQL Server by this update.
- This MVP uses EF Core `EnsureCreated()` for a new schema; it does not ship a versioned migrations history. A later migration phase should add EF migrations, document deletion, retention controls, durable object storage, and backup restore tests.
- Single running local API instance only. Full-table job/analysis and archive updates are transactional but not optimized for concurrent multi-instance hosting.
- Source and ZIP integrity checks were performed; **.NET build and SQL import could not be executed in this container**. Confirm `dotnet build` and inspect imported records locally.

**If the installer refuses:** your current source differs from the reviewed baseline; do not force-overwrite the working application. Provide the relevant changed source files to create a safe merge.
