# Waah रे Resume — Safe History Deletion (SQL mode, local development only)

This update is **optional and irreversible for deleted records**. It adds a Delete record button to the existing History details drawer. It does not change resume extraction, scoring, Groq, alignment, or export.

## Preconditions
1. SQL verification must say `DATABASE: CONNECTED - SQL MODE VERIFIED`.
2. Stop both frontend and backend.
3. Make and verify a **private SQL database backup**, and also copy `backend/RoleFit.Api/App_Data` including `secure-originals` and Data Protection keys. Keep backups outside the repository and never commit them. Backups retain personal data until separately destroyed.
4. Put this extracted folder inside the main `RoleFit-AI` root and run `APPLY_HISTORY_DELETE.bat`. The installer checks six **exact source hashes**. If a file differs, it stops rather than overwrite newer code. It also creates a dated backup of affected source files.
5. Rebuild the backend in Visual Studio and run `npm.cmd run build` in `frontend/rolefit-ui`. Start both servers.
6. Use a **throwaway synthetic resume** first. Save one, open History, click Delete record, confirm, then check History and re-run SQL counts. Test real deletion only after checking all backup/recovery assumptions.

## Semantics and limitations
- **Only archived entries** can be deleted from the UI. Legacy-only records (without an archived original) are not touched by this update.
- DELETE `/api/archive/{id}` works **only in Development from loopback**, and **only when SQL history mode is enabled**. No login is added.
- The SQL transaction removes the chosen archive's metadata, its associated draft if no other archive references it, and its linked ATS analysis rows if no other archive references them. Saved JDs remain.
- The encrypted file `App_Data/secure-originals/{archiveId:N}.bin` is deleted after SQL commit. If filesystem deletion fails, the API explicitly returns a warning; clean up the leftover encrypted file privately after checking the exact ID. This cross-resource deletion is not fully atomic.
- The archive is deduplicated by content SHA256: one stored original can have multiple associated ATS analyses, and deleting that archive removes **all** associated unshared analyses. Backups may still contain the original.
- No deletion of shared IDs, no changes to schema, no automatic purging of old JSON fallback backups, and no public deployment permission. This is a local development-only feature.
- Cannot test against your Windows .NET SDK and SQL Server here; compile and run the smoke test locally. If installer says SAFE STOP, share current file versions for merging; do not force overwrite.

## Rollback
Stop the servers, restore source files from the installer-created `HistoryDeleteSourceBackup-...` folder, and rebuild. This reverses the code change only; to recover deleted SQL records or encrypted originals, you must restore from the private data backups.
