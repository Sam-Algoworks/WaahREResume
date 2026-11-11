import { useEffect, useState, type ChangeEvent, type KeyboardEvent } from "react";
import {
  Check, CheckCircle2, CircleAlert, Download, FileText, LoaderCircle,
  Plus, Save, Sparkles, UploadCloud, X
} from "lucide-react";
import { api, type ResumeProfile, type ResumeProject } from "../api";
import "./ResumeMode.css";
import { printResumePdf, downloadResumeDocx } from "../utils/resumeExport";

type Style = "algoworks" | "original";
type Seed = { file?: File; jdText: string; savedProfileId?: string } | null;
const publicDesignation = (value: string) => /@|https?:|(?:\+\d|\b\d[\d .()\-]{8,}\d\b)/i.test(value) ? "" : value;
const wordCount = (value: string) => value.trim() ? value.trim().split(/\s+/).length : 0;

export default function ResumeParserPage({ seed, onSaved }: { seed?: Seed; onSaved?: () => void }) {
  const [resume, setResume] = useState<File | null>(seed?.file ?? null);

  const [profile, setProfile] = useState<ResumeProfile | null>(null);
  const [style, setStyle] = useState<Style>("algoworks");
  const [sourceUrl, setSourceUrl] = useState<string | null>(null);
  const [sourceText, setSourceText] = useState<string | null>(null);
  const [newSkill, setNewSkill] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [changed, setChanged] = useState(false);

  const [wordBusy, setWordBusy] = useState(false);

  // Original is local to this browser session, not embedded in edited drafts.
  useEffect(() => {
    if (!resume) { setSourceUrl(null); setSourceText(null); return; }
    const url = URL.createObjectURL(resume);
    setSourceUrl(url);
    if (resume.name.toLowerCase().endsWith(".pdf")) {
      setSourceText(null);
    } else if (resume.name.toLowerCase().endsWith(".txt")) {
      void resume.text().then(setSourceText).catch(() => setSourceText("Unable to read text."));
    } else {
      void api.readDocument(resume).then(v => setSourceText(v.text))
        .catch(() => setSourceText("DOCX text preview could not be loaded."));
    }
    return () => URL.revokeObjectURL(url);
  }, [resume]);

  useEffect(() => {
    if (!seed?.savedProfileId) return;
    let cancelled = false;
    void api.savedProfile(seed.savedProfileId).then(saved => {
      if (cancelled) return;
      setProfile(saved); setStyle("algoworks");
      void api.archivedOriginalForProfile(saved.id, saved.sourceFileName).then(file => {
        if (cancelled) return;
        if (file) { setResume(file); setNotice("Saved profile and original resume restored from the local protected archive."); }
        else setNotice("Saved profile opened. The original document predates protected local file storage.");
      }).catch(() => {
        if (!cancelled) setNotice("Saved profile opened, but its original document is unavailable.");
      });
    }).catch(e => { if (!cancelled) setError(e instanceof Error ? e.message : "Could not open profile."); });
    return () => { cancelled = true; };
  }, [seed?.savedProfileId]);

  function change<K extends keyof ResumeProfile>(key: K, value: ResumeProfile[K]) {
    setProfile(current => current ? { ...current, [key]: value } : current);
    setChanged(true); setNotice("");
  }
  function changeProject(index: number, patch: Partial<ResumeProject>) {
    if (!profile) return;
    change("projects", profile.projects.map((p, i) => i === index ? { ...p, ...patch } : p));
  }
  function chooseResume(event: ChangeEvent<HTMLInputElement>) {
    setResume(event.target.files?.[0] ?? null); setProfile(null);
    setError(""); setNotice(""); setChanged(false);
  }
  async function processResume() {
    if (!resume) { setError("Choose a resume to continue."); return; }
    if (changed && !window.confirm("Running again will regenerate the profile from the uploaded resume and discard unsaved edits. Continue?")) return;
    setBusy(true); setError(""); setNotice("");
    try {
      // Standalone profile generation never calculates an ATS score or aligns to a JD.
      const parsed = await api.parseResume(resume);
      setProfile(parsed);
      setNotice(`Resume parsed. ${parsed.projects.length} genuine project(s) found. Review source evidence before export.`);
      setStyle("algoworks"); setChanged(false);
      onSaved?.();
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to process resume."); }
    finally { setBusy(false); }
  }
  async function save() {
    if (!profile) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const saved = await api.saveResumeDraft(profile);
      setProfile(saved); setChanged(false);
      setNotice("Draft saved locally. Edited information requires verification before recruitment decisions.");
      onSaved?.();
    } catch (e) { setError(e instanceof Error ? e.message : "Could not save draft."); }
    finally { setBusy(false); }
  }
  function print() {
    if (!profile) return;
    printResumePdf(profile);
  }
  async function downloadWord() {
    if (!profile) return;
    setWordBusy(true); setError("");
    try {
      await downloadResumeDocx(profile);
      setNotice("Editable Word (.docx) file downloaded. Review the profile before sharing.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Word export failed.");
    } finally {
      setWordBusy(false);
    }
  }
  function addSkill(e?: KeyboardEvent<HTMLInputElement>) {
    if (e && e.key !== "Enter") return;
    e?.preventDefault();
    const skill = newSkill.trim();
    if (!profile || !skill || profile.technicalSkills.some(v => v.toLowerCase() === skill.toLowerCase())) return;
    change("technicalSkills", [...profile.technicalSkills, skill]); setNewSkill("");
  }
  const count = wordCount(profile?.professionalSummary ?? "");
  const summaryComplete = /[.!?]$/.test((profile?.professionalSummary ?? "").trim());
  const summaryValid = count >= 80 && count <= 85 && summaryComplete;

  return <div className="resume-parser-page studio-page">
    <div className="page-head"><div><div className="eyebrow">STANDALONE RESUME PARSER</div>
      <h1>Make a beautiful profile.</h1><p>No job description needed. Upload, parse, edit directly and export your Algoworks professional profile.</p>
    </div><span className="page-count">Source-grounded · 10 projects maximum</span></div>
    <div className="studio-layout">
      <aside className="panel studio-controls">
        <div className="step-label">01 / UPLOAD RESUME</div>
        <h3>Candidate source</h3>
        <label className="dropzone" htmlFor="studio-file"><UploadCloud size={27}/>
          <strong>{resume?.name ?? "Choose a candidate resume"}</strong><span>PDF (text), DOCX or TXT · max 5 MB</span></label>
        <input className="file-input" id="studio-file" type="file" accept=".pdf,.docx,.txt" onChange={chooseResume}/>

        <div className="studio-buttons rf-single-action">
          <button className="button primary full-width" disabled={!resume || busy} onClick={() => void processResume()}>
            {busy ? <LoaderCircle size={17} className="spin"/> : <Sparkles size={17}/>} {busy ? "Parsing…" : "Parse Resume"}</button>
        </div>
        {profile && <></>}
        {error && <p role="alert" className="parser-message parser-error"><CircleAlert size={16}/>{error}</p>}
        {notice && <p role="status" className="parser-message parser-notice"><Check size={16}/>{notice}</p>}
      </aside>

      <div className="studio-view">
        <div className="studio-toolbar"><div><FileText size={17}/> {style === "original" ? "Original uploaded document" : "A4-style editable preview"}</div>
          <div className="studio-switch" role="group" aria-label="Resume output format">
            <button className={style === "algoworks" ? "active" : ""} onClick={() => setStyle("algoworks")} type="button">Algoworks</button>
            <button className={style === "original" ? "active" : ""} onClick={() => setStyle("original")} type="button">Original resume</button>
          </div>
          {profile && <div className="studio-floating-actions">
            <button type="button" className="button secondary" disabled={busy} onClick={() => void save()}><Save size={16}/> Save draft {changed ? "•" : ""}</button>
            <button type="button" className="button secondary" disabled={wordBusy} onClick={() => void downloadWord()}><FileText size={16}/> {wordBusy ? "Preparing Word…" : "Word (.docx)"}</button>
            <button type="button" className="button primary" onClick={print}><Download size={16}/> Save PDF</button>
          </div>}
        </div>
        {profile && <p className="studio-view-mode-hint" role="status">
          {style === "algoworks"
            ? "Editable Algoworks professional profile. All fields require review against the original source."
            : "Original resume: read-only source document, no Algoworks branding or AI rewriting."
          }
        </p>}
        {profile ? <>
          {style === "original" ?
            <div className="rf-original-document">
              <div className="rf-original-bar"><div><FileText size={18}/><strong>{resume?.name ?? profile.sourceFileName}</strong></div>
                {resume && sourceUrl && <a className="button secondary" href={sourceUrl} download={resume.name}><Download size={16}/> Download original</a>}</div>
              {!resume ? <div className="rf-original-empty"><CircleAlert size={30}/><h3>Original source not available</h3><p>This older record has no archived file. Upload the original again to preview it.</p></div> :
                resume.name.toLowerCase().endsWith(".pdf") && sourceUrl ?
                  <iframe title="Original uploaded resume" src={sourceUrl} className="rf-original-pdf"/> :
                  <div className="rf-original-text"><p>{resume.name.toLowerCase().endsWith(".docx") ? "Text extracted from original DOCX (exact Word layout is not reproduced)." : "Original TXT content"}</p><pre>{sourceText ?? "Loading original content…"}</pre></div>}
            </div> :
          <article className="studio-edit-sheet company" aria-label="Editable A4-style resume page">
            <div className="studio-company-brand">
              <img src="/algoworks-logo.png" alt="Algoworks logo"/>

            </div>
            <div className="studio-green-rule"/>
            <input className="studio-name" aria-label="Candidate name" value={profile.candidateName} placeholder="Full name not detected"
              onChange={e => change("candidateName", e.target.value)}/>
            <div className="studio-designation-row">
              <input aria-label="Designation" value={publicDesignation(profile.designation)} placeholder="Designation from resume"
                onChange={e => change("designation", e.target.value)}/>
              <label className="studio-experience-label"><input aria-label="Experience years" type="number" min="0" max="60" step="0.5"
                value={profile.experienceYears ?? ""} placeholder="Years" onChange={e => change("experienceYears", e.target.value === "" ? null : Number(e.target.value))}/><span>years of experience</span></label>
            </div>
            <section className="studio-section"><div className="studio-section-heading"><h3>PROFESSIONAL SUMMARY</h3>
              <span className={summaryValid ? "studio-good" : "studio-caution"}>
                {count}/85 words {summaryValid && <CheckCircle2 size={13}/>} 
              </span></div>
              <textarea
                aria-label="Professional summary"
                rows={6}
                value={profile.professionalSummary}
                onChange={e => change("professionalSummary", e.target.value)}
                placeholder="Write 80–85 complete, source-supported words"
              />
            </section>
            <section className="studio-section"><h3>{style === "algoworks" ? "TECHNOLOGY STACK" : "CORE SKILLS"}</h3>
              <div className="studio-skills">
                {profile.technicalSkills.map((skill,i)=><span key={`${skill}-${i}`}>{skill}
                  <button type="button" title={`Remove ${skill}`} onClick={()=>change("technicalSkills", profile.technicalSkills.filter((_,n)=>n!==i))}><X size={12}/></button></span>)}
                {profile.technicalSkills.length===0 && <em>No skills evidenced</em>}</div>
              <div className="studio-add-skill"><input value={newSkill} onChange={e=>setNewSkill(e.target.value)}
                onKeyDown={addSkill} placeholder="Add verified technology" aria-label="Add a technology"/>
                <button aria-label="Add technology" onClick={()=>addSkill()} type="button"><Plus size={16}/></button></div>
            </section>
            <section className="studio-section"><div className="studio-section-heading"><h3>{style === "algoworks" ? "PROJECT EXPERIENCE" : "RELEVANT PROJECTS"}</h3><span>{profile.projects.length}/10 projects</span></div>
              {profile.projects.map((project,i)=><div className="studio-project" key={i}>
                <div className="studio-project-top"><span>PROJECT {String(i+1).padStart(2,"0")}</span>
                  <button type="button" title="Remove project" onClick={()=>change("projects",profile.projects.filter((_,n)=>n!==i))}><X size={14}/></button></div>
                <div className="studio-project-head"><input aria-label={`Project ${i+1} domain`} value={project.domain || project.title}
                    placeholder="Generic domain name" onChange={e=>changeProject(i,{domain:e.target.value,title:e.target.value})}/>
                  <input aria-label={`Project ${i+1} year`} value={project.year ?? ""} placeholder="Year" onChange={e=>changeProject(i,{year:e.target.value})}/></div>
                <label>Short project introduction</label><textarea aria-label={`Project ${i+1} introduction`} rows={3} value={project.description}
                  placeholder="Concise, source-supported project description" onChange={e=>changeProject(i,{description:e.target.value})}/>
                <label>Technology Stack Used</label><input aria-label={`Project ${i+1} technology stack`} value={(project.technologyStack ?? []).join(", ")}
                  placeholder="Technologies used in this project" onChange={e=>changeProject(i,{technologyStack:e.target.value.split(",").map(s=>s.trim()).filter(Boolean)})}/>
              </div>)}
              {profile.projects.length<10 && <button className="studio-quiet" type="button" onClick={()=>change("projects",[...profile.projects,{ title:"",domain:"",year:"",description:"",responsibilities:"",technologyStack:[],evidence:"" }])}><Plus size={14}/> Add a verified project</button>}
            </section>
            <div className="studio-edit-footer">Editable draft · {profile.sourceFileName} · {profile.aiStatus === "groq" ? "Groq-assisted" : "Basic extraction"}</div>
          </article>}
          <div className="studio-info"><CircleAlert size={16}/>
            <span>{profile.parserNote} {profile.projects.length === 0 ? "Check that the resume has an identifiable Projects section; unrelated work-history bullets are not invented as projects. " : ""}
            {!summaryValid ? `Summary: ${count} words. Aim for 80–85 words and finish with a complete sentence. ` : ""}
            Inline edits remain unverified drafts and never improve the ATS score automatically.</span>
          </div>
        </> : <div className="profile-empty"><FileText size={36}/><h3>Upload your resume to begin</h3>
          <p>Parse the resume separately, edit the Algoworks profile inline, or inspect the original source. For JD matching, use ATS Check.</p></div>}
      </div>
    </div>
  </div>;
}
