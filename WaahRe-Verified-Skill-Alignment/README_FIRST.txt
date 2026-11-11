Waah रे Resume – Dynamic JD Competency Suggestions (Verification-First)

WHAT CHANGES
- Align to JD extracts explicit requested competencies from ANY job field via Groq.
- Missing requirements appear automatically in a separate “JD requirements to verify” panel.
- They are NOT added to claimed skills, ATS score, PDF or Word until verified.
- “Confirm & add” requires HR to confirm the candidate actually knows that competency.
- Standalone Resume Parser remains unchanged. Experience years, designation, projects, history, exports are preserved.

INSTALL
1. BACK UP your working RoleFit-AI folder. Stop the frontend/backend.
2. Extract this ZIP. Move its entire folder inside your existing RoleFit-AI folder.
3. Run APPLY_ALIGNMENT.bat and type YES.
4. Visual Studio: rebuild backend/RoleFit.slnx and start RoleFit Local.
5. VS Code: npm.cmd run build; npm.cmd run dev inside frontend/rolefit-ui.
6. ATS Check with a JD, then Align to JD; Resume Studio shows unverified JD requirements.
7. Confirm skills only after evidence or candidate verification, then Save draft.

EXAMPLE
JD requires MongoDB, candidate resume mentions SQL only:
MongoDB appears under “JD REQUIREMENTS TO VERIFY”, not as a claimed skill.
After HR verifies MongoDB competence, Confirm & add puts it in the editable skill list.
If not genuinely possessed, dismiss and leave it out.

NOTES
- Exact JD wording must be present; AI-proposed terms not in the JD are rejected.
- Generic across industries, but no extraction can guarantee every competency in every JD.
- AI fallback extracts clearly written list items if the JD extraction request fails.
- ATS scoring always uses the original source file, not the edited aligned profile.
- This patch was checked for file structure and TypeScript syntax. A Windows .NET and Vite build are still required.
