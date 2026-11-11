# Waah रे Resume — Phase 4: Cross-Role ATS Scoring Consistency

**Purpose:** Improve the deterministic ATS/JD-alignment estimate for both technical and nontechnical resumes without replacing the current Resume Parser, Groq service, SQL History, frontend, or exports.

## What changes

1. **Required versus preferred skills.** Sections labelled `Preferred skills`, `Nice to have`, `Bonus`, etc. are not scored as mandatory. This applies to the explicitly labelled sections that the deterministic rules recognize; free-form JDs still need human review.
2. **Bullet-list skills.** The evaluator recognizes skills in `Required skills`, `Core competencies`, `Must-haves`, etc., including multiline bullets, numbered items, and `SQL Server and MongoDB`-style conjunctive lists.
3. **Cross-profession action verbs.** Adds deterministic matching for actions such as audit, reconcile, train, teach, forecast, assess, recruit, supervise, and coordinate. Basic forms (e.g. `reconcile/reconciled`) are recognized.
4. **Negative evidence is not a match.** A resume that says `No experience with MongoDB` will not earn source-evidence credit for MongoDB just because the word occurs.
5. **Conservative plural variants.** Examples: `student assessment/assessments`, `financial reconciliation/reconciliations`.
6. **Deterministic score version v1.2.** Existing stored analyses remain historical; new runs use updated scoring.

## Installation (Windows)

1. Stop the API in Visual Studio and stop the frontend if it is running.
2. Make a private backup of the current project and your database + `App_Data` folder. Do not share candidate information or API keys.
3. Extract `WaahRe-ATS-Phase4` into the **main** `RoleFit-AI` folder, alongside `backend` and `frontend`.
4. Double-click `WaahRe-ATS-Phase4/APPLY_PHASE4.bat`.
5. If the source prechecks pass, type `YES`. The installer saves your existing `AlignmentEngine.cs` in a timestamped backup.
6. In Visual Studio Community, **Build → Rebuild Solution** and launch the existing RoleFit Local profile.
7. In a terminal at the **main** `RoleFit-AI` folder, run:

   ```powershell
   dotnet run --project .\tests\RoleFit.ScoringPhase4Checks\RoleFit.ScoringPhase4Checks.csproj
   dotnet run --project .\tests\RoleFit.ScoringChecks\RoleFit.ScoringChecks.csproj
   ```

8. Test the ATS check with the **same original fictional resume and JD** twice. Confirm that an unverified JD-only skill remains missing. Test a finance or healthcare JD with bullet lists. Your History data should remain available.

## Safe-stop behavior

The installer is specifically designed for the `AlignmentEngine.cs` installed by **Audit Fix Phase 1**. It checks the source hash and **does not overwrite** a newer customized scorer. If `SAFE STOP` appears, do not force-install; share your *current* `backend/RoleFit.Api/Services/AlignmentEngine.cs` for a targeted manual merge. This protects work performed after Phase 1.

## Rollback

Stop the backend. Locate the timestamped `backup_...` folder inside the update folder and copy its `AlignmentEngine.cs` back to `backend/RoleFit.Api/Services`. Remove the newly introduced `JdEvidenceRules.cs` and `tests/RoleFit.ScoringPhase4Checks` directory. Rebuild the API. Stored history is not modified by installation.

## Limits / safety

- This is an **estimated JD-alignment score**, not an official ATS score or a validated hiring recommendation. Exact/near-exact deterministic matching will still miss many semantic equivalents and may count generic verbs as weak evidence.
- It does not add skills to resumes or infer experiences from JDs. JD-alignment editing/verification stays as implemented in your existing app.
- It does not make the anonymous application safe for public access to actual HR data. Keep real candidates private and use only fictional data for future public demos.
- An end-to-end .NET SDK build and live validation are required on your Windows machine; the files have been statically checked and packaged here but .NET is not installed in the creation environment.
