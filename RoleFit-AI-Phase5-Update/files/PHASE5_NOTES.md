# RoleFit AI — Phase 5 parser and layout corrections

These changes address the specific reference-profile issues reported in the previous build.

## What changed

- Recognizes `SELECTED PROJECTS`, `KEY PROJECTS`, `ACADEMIC PROJECTS` and additional project-section headings.
- Groups numbered and multiline project records into real projects rather than counting each line as a project.
- Separates an em-dash project heading (`Project Name — Java, React, ...`) into the actual title and project-local technologies.
- Supports verified project-specific technology extraction, generic functional domains, last year of an explicit project date range, and a cap of 10 projects.
- Ensures Groq cannot silently erase projects already found by the deterministic parser.
- Shows a compact Algoworks logo at the top left, name/designation/years, summary, compact two-column tech stack in PDF, and project experience only.
- Removes Work Experience, Education, Certifications, and the project Responsibility line from the visible Algoworks profile/print export. Other internal data remains available to the backend without being printed.
- Caps the summary at 85 whitespace-separated words. With a Groq key, an 85-word factual summary is requested and correction is attempted; when factual evidence is insufficient, an accurate shorter summary is shown. The UI never pretends a 38-word summary is 85 words.
- Preserves the working dashboard, single analyzer, bulk ranking and JD inputs.

## Installation

1. Stop both RoleFit AI PowerShell servers, or close their windows.
2. Place the entire `RoleFit-AI-Phase5-Update` folder **inside** the folder that contains `START_ROLEFIT.bat`.
3. Run `APPLY_PHASE5.bat` from the update folder, and check for `UPDATE APPLIED SUCCESSFULLY`.
4. Run `TEST_PARSER.bat` from the main project folder to check the included synthetic regressions.
5. Start the main `START_ROLEFIT.bat` and use the **Local** address shown by Vite.
6. Re-upload Aarav's resume and press **Parse Resume** again. Old saved drafts do not automatically update.

## Groq

In the user's previous screenshot the frontend displayed `Groq AI not configured`. To generate an AI introduction you must run `CONFIGURE_GROQ.bat` from your project root, enter a valid Groq API key and **restart the backend**. The API key remains out of the source tree; never send it to ChatGPT or commit it to Git. With Groq enabled and permission to process the resume, the candidate text is sent to Groq's third-party servers.

## Limitations

This is not a confirmed live Windows build: .NET 10 is unavailable in the packaging environment, and npm package installation timed out. The updated TSX was syntax checked. A complete .NET compile, the parser regression run, live Groq call, and browser print preview must be checked on your PC. DOCX export, scanned PDF OCR and SQL Server persistence are not implemented in this patch. PDFs must contain selectable text.

The regression fixture `selected-projects-multiline.txt` contains **fictional synthetic data** modeled on the observed resume structure; it is not the user's private PDF. Candidate files are not packaged.
