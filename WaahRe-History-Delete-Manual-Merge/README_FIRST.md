# Waah रे Resume — exact manual merge: History deletion

This update is based on the **two actual current source files** you uploaded on November 10, 2026. It preserves the current App navigation and the portal-based History drawer. It is **not** the earlier conflicting generic merge.

## Apply safely

1. **Keep using your current working project.** Close Visual Studio debugging and stop the Vite frontend.
2. Verify `WaahReHistory` contains your expected records. **Make an independent, private SQL backup** AND back up the entire `backend/RoleFit.Api/App_Data` directory, including encrypted original files and data-protection keys (keys may also be stored in the Windows user profile). Do not upload real candidate data or backup files to GitHub.
3. Extract this folder into the **RoleFit-AI project root**, alongside `backend` and `frontend`.
4. Double-click **`APPLY_MANUAL_MERGE.bat`**. It verifies your exact `App.tsx` and `TalentLibrary.tsx` versions using hashes derived from the uploads. For the other four files it uses known baseline replacement or a clean three-way merge. If a file has changed or conflicts, the installer **stops with nothing changed**.
5. After it passes prechecks and you verified the backups, enter `YES`. The installer creates a `HistoryManualMergeBackup-...` source backup.
6. In `backend/RoleFit.Api`, run `dotnet build` and start **RoleFit Local**. In `frontend/rolefit-ui`, run `npm.cmd run build`, then `npm.cmd run dev`.
7. Test ONLY with a new fictional/test candidate: save a resume, go to History, open it, click **Delete record**, confirm. Refresh History and SQL table counts, then restart backend. Other records and JDs must remain.

## What changes

Six source files. The two UI files are exact edits of your uploads. Four backend/API files come from the previous reviewed History Deletion patch, guarded against conflicts:

- `frontend/rolefit-ui/src/App.tsx` — passes the deletion callback to History, refreshes after deletion, shows warning if encrypted-file cleanup fails.
- `frontend/rolefit-ui/src/components/TalentLibrary.tsx` — Delete record button with confirmation/disabled state/error display inside your current drawer; only archived records can be deleted.
- `frontend/rolefit-ui/src/api.ts` — DELETE `/api/archive/{id}` request.
- `backend/RoleFit.Api/Controllers/ArchiveController.cs` — local-Development and loopback-only endpoint.
- `backend/RoleFit.Api/Services/ResumeArchiveStore.cs` — SQL deletion then encrypted-original cleanup.
- `backend/RoleFit.Api/Services/SqlHistoryRepository.cs` — transaction deletes archive, linked *unshared* analyses/draft; disables old JSON re-import on every startup (otherwise deleted SQL history could reappear).

## Important limitations

- **No login added.** Deletion is intentionally restricted to local Development on the same computer, using SQL mode. Do not expose this backend on the public Internet.
- **Legacy-only rows** without an archived-original ID do not get the delete action. This does not clear source JSON backups or copies of candidate data elsewhere.
- One archived original may have **several** related ATS evaluations; deleting that original can delete those linked results if not referenced elsewhere. Saving backups first is required. Encrypted-file removal happens after SQL commit (not a single atomic operation). If the file cannot be removed, the UI displays a warning but correctly closes the deleted record.
- **The SQL import must be complete before applying**, because this update removes repeated JSON-to-SQL startup import. Confirm the Jobs/Analyses/Drafts/Archives counts match expectations before proceeding.
- I validated script/package structure and TypeScript parsing here; I cannot run your Windows SQL Server or .NET build. A clean merge is not proof of runtime correctness. If a new `SAFE STOP` appears, do not force the installer.

## Rollback

Stop both processes. Restore the changed source files from the dated `HistoryManualMergeBackup-...` directory and rebuild. **Source rollback does not restore deleted history.** Deleted SQL records and encrypted files require restoring the **private database and original-file backups**.
