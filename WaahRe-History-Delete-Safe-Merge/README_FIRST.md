# Waah रे Resume — History delete safe-merge installer (v2)

**Why this package exists:** The first installer refused to replace `src/components/TalentLibrary.tsx` because it did not match an older source hash. That was the correct behavior. **Do not overwrite the newer file with the previous ZIP.**

This installer compares **your current source**, the **reviewed older base** and the **new History deletion changes**, using `git merge-file` to preserve non-conflicting local edits. It checks all six files before editing *any* of them. If any changes conflict, it stops with `SAFE STOP` and leaves your source untouched. After a clean preparation and explicit YES, it creates a dated backup of affected source files, then applies the merged version.

## Before running

1. **Confirm SQL MODE VERIFIED with table counts**, not only that the website loads.
2. Back up `WaahReHistory` and the entire `backend/RoleFit.Api/App_Data` privately. Do not commit backups, secrets or real resumes to GitHub.
3. Stop the backend and frontend.
4. Make a copy of your project source; use Git for Windows (`git --version`).
5. Extract this ZIP into the **RoleFit-AI project root** (the folder with `backend` and `frontend`).
6. Double-click `WaahRe-History-Delete-Safe-Merge/APPLY_SAFE_MERGE.bat`. Review its output and type `YES` only after backup.

## What changes

- Adds a local-development-only SQL DELETE endpoint to `ArchiveController.cs` and corresponding SQL transaction in `SqlHistoryRepository.cs`.
- Deletes one archive, *unshared* associated draft and evaluation records, and then attempts to delete the encrypted original file. JDs remain.
- Adds a Delete Record button, confirmation and error status to your **existing** History panel without replacing unrelated UI changes.
- Stops automatic re-import of old JSON history on SQL startup: otherwise, deleting the last record could make its old metadata reappear after a restart. **This change assumes the initial Phase 2 SQL migration was already completed**, as confirmed in your workflow.
- Does **not** enable deletion without SQL or outside local Development loopback. Does **not** add login, change parser, scoring, layout theme or exports.

## Test and recovery

Run `dotnet build` in `backend/RoleFit.Api` and `npm.cmd run build` in `frontend/rolefit-ui`. If any build fails, restore the dated `HistoryDeleteSourceBackup-*` files from the project root. Then test with a synthetic resume. Delete one record; confirm that the linked history vanishes but other records remain; restart and confirm it stays deleted. If the API reports file cleanup warning, inspect the encrypted original path privately. DB record deletion is not reversible without a SQL + App_Data backup.

If `SAFE STOP` reports merge conflicts, **do not force overwrite**. Upload your current `src/components/TalentLibrary.tsx` (and `src/App.tsx` if that is also flagged) for a manual patch preserving your latest UI.

This package was statically checked and simulated with a modified UI file. It has **not** been executed against your Windows SQL Server or built with your .NET SDK. Do not deploy the endpoint publicly; it is deliberately limited to local Development loopback.
