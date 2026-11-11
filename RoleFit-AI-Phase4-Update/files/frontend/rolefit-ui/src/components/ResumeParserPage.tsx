import { useEffect, useRef, useState, type ChangeEvent, type KeyboardEvent } from "react";
import {
  Check, CheckCircle2, CircleAlert, Download, FileText, LoaderCircle,
  Plus, Save, Sparkles, UploadCloud, WandSparkles, X
} from "lucide-react";
import { api, type Analysis, type Job, type ResumeProfile, type ResumeProject } from "../api";
import JDFileUpload from "./JDFileUpload";

type Style = "algoworks" | "aligned";
type Seed = { file: File; jdText: string } | null;
const wordCount = (value: string) => value.trim() ? value.trim().split(/\s+/).length : 0;
const safeExperience = (value: number | null) => value == null ? "" : `${value} ${value === 1 ? "year" : "years"} of experience`;

function EditableLines({ title, lines, onChange }: {
  title: string; lines: string[]; onChange: (value: string[]) => void;
}) {
  return <section className="studio-section editor-only">
    <h3>{title}</h3>
    {lines.map((line, i) => <div key={i} className="studio-line">
      <input aria-label={`${title} detail ${i + 1}`} value={line} placeholder="Add verified information"
        onChange={e => onChange(lines.map((v, n) => n === i ? e.target.value : v))}/>
      <button type="button" aria-label="Remove line" onClick={() => onChange(lines.filter((_, n) => n !== i))}><X size={14}/></button>
    </div>)}
    <button className="studio-quiet" type="button" onClick={() => onChange([...lines, ""])}><Plus size={13}/> Add item</button>
  </section>;
}

function StaticDocument({ profile, style }: { profile: ResumeProfile; style: Style }) {
  const company = style === "algoworks";
  const projects = profile.projects.slice(0, 10);
  return <div className={`studio-static ${company ? "company" : "standard"}`}>
    {company ? <div className="static-brand"><img src="/algoworks-logo.png" alt="Algoworks" /></div>
      : <div className="static-standard-label">PROFESSIONAL RESUME</div>}
    <div className="static-green-rule"/>
    <h1>{profile.candidateName || "Candidate name not provided"}</h1>
    <p className="static-designation">{profile.designation || "Designation not provided"}
      {profile.experienceYears != null && <span> · {safeExperience(profile.experienceYears)}</span>}
    </p>
    {!company && (profile.email || profile.phone) && <p className="static-contact">{[profile.email, profile.phone].filter(Boolean).join("  ·  ")}</p>}
    <section><h2>PROFESSIONAL SUMMARY</h2><p>{profile.professionalSummary || "No verified summary available."}</p></section>
    <section><h2>TECHNOLOGY STACK</h2>
      <div className="static-tech-box"><div className="static-tech-grid">
        {profile.technicalSkills.length ? profile.technicalSkills.map((skill, i) => <div key={`${skill}-${i}`}>{skill}</div>)
          : <span>Not specified in source resume</span>}
      </div></div>
    </section>
    {profile.employmentHistory.length > 0 && <section><h2>WORK EXPERIENCE</h2>
      {profile.employmentHistory.map((item, i) => <p className="static-line" key={i}>{item}</p>)}</section>}
    <section><h2>PROJECT EXPERIENCE</h2>
      {projects.length ? projects.map((project, i) => <div className="static-project" key={i}>
        <div className="static-project-head"><strong>{String(i + 1).padStart(2, "0")}. {project.domain || project.title || "Project"}</strong><span>{project.year}</span></div>
        {project.responsibilities && <p><b>Responsibility:</b> {project.responsibilities}</p>}
        {project.description && <p>{project.description}</p>}
        {project.technologyStack?.length > 0 && <p className="static-techused"><b>Technology Stack Used:</b> {project.technologyStack.join(", ")}</p>}
      </div>) : <p>No projects evidenced in this resume.</p>}
    </section>
    {profile.education.length > 0 && <section><h2>EDUCATION</h2>{profile.education.map((value,i)=><p key={i}>{value}</p>)}</section>}
    {profile.certifications.length > 0 && <section><h2>CERTIFICATIONS</h2>{profile.certifications.map((value,i)=><p key={i}>{value}</p>)}</section>}
  </div>;
}

export default function ResumeParserPage({ jobs, seed }: { jobs: Job[]; seed?: Seed }) {
  const [resume, setResume] = useState<File | null>(seed?.file ?? null);
  const [selectedJob, setSelectedJob] = useState("");
  const [jdText, setJdText] = useState(seed?.jdText ?? "");
  const [profile, setProfile] = useState<ResumeProfile | null>(null);
  const [style, setStyle] = useState<Style>("algoworks");
  const [newSkill, setNewSkill] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [changed, setChanged] = useState(false);
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [aiConfigured, setAiConfigured] = useState<boolean | null>(null);
  const [showInternal, setShowInternal] = useState(false);
  const firstRender = useRef(true);

  useEffect(() => {
    if (!firstRender.current) return;
    firstRender.current = false;
    void api.aiStatus().then(v => setAiConfigured(v.configured)).catch(() => setAiConfigured(null));
  }, []);

  function change<K extends keyof ResumeProfile>(key: K, value: ResumeProfile[K]) {
    setProfile(current => current ? { ...current, [key]: value } : current);
    setChanged(true); setNotice("");
  }
  function changeProject(index: number, patch: Partial<ResumeProject>) {
    if (!profile) return;
    change("projects", profile.projects.map((p, i) => i === index ? { ...p, ...patch } : p));
  }
  function chooseResume(event: ChangeEvent<HTMLInputElement>) {
    setResume(event.target.files?.[0] ?? null); setProfile(null); setAnalysis(null);
    setError(""); setNotice(""); setChanged(false);
  }
  function chooseJob(id: string) {
    setSelectedJob(id); setJdText(jobs.find(j => j.id === id)?.description ?? "");
  }
  async function parse() {
    if (!resume) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const parsed = await api.parseResume(resume);
      setProfile(parsed); setStyle("algoworks"); setAnalysis(null); setChanged(false);
      setNotice(parsed.aiStatus === "groq"
        ? "Groq parsed the resume. Check every detail and edit directly on the document."
        : "Basic parser completed. Configure Groq for 85-word introductions and improved project grouping.");
    } catch (e) { setError(e instanceof Error ? e.message : "Cannot parse this resume."); }
    finally { setBusy(false); }
  }
  async function align() {
    if (!resume) { setError("Select a candidate resume first."); return; }
    if (jdText.trim().length < 30) { setError("Upload or paste a JD of at least 30 characters."); return; }
    if (changed && !window.confirm("Aligning will regenerate the profile from the original resume and discard unsaved edits. Continue?")) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const response = await api.alignResume(resume, jdText);
      setProfile(response.profile); setAnalysis(response.analysis); setStyle("aligned"); setChanged(false);
      setNotice("JD alignment complete. ATS score comes from the ORIGINAL resume, not AI rewriting. Choose the Algoworks or normal document view.");
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to align resume."); }
    finally { setBusy(false); }
  }
  async function save() {
    if (!profile) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const saved = await api.saveResumeDraft(profile);
      setProfile(saved); setChanged(false);
      setNotice("Draft saved locally. Edited information requires verification before recruitment decisions.");
    } catch (e) { setError(e instanceof Error ? e.message : "Could not save draft."); }
    finally { setBusy(false); }
  }
  function print() {
    if (!profile) return;
    window.print();
  }
  function addSkill(e?: KeyboardEvent<HTMLInputElement>) {
    if (e && e.key !== "Enter") return;
    e?.preventDefault();
    const skill = newSkill.trim();
    if (!profile || !skill || profile.technicalSkills.some(v => v.toLowerCase() === skill.toLowerCase())) return;
    change("technicalSkills", [...profile.technicalSkills, skill]); setNewSkill("");
  }
  const count = wordCount(profile?.professionalSummary ?? "");

  return <div className="resume-parser-page studio-page">
    <div className="page-head"><div><div className="eyebrow">ROLEFIT / PROFESSIONAL PROFILE STUDIO</div>
      <h1>Resume Parser & JD Alignment</h1><p>Generate an Algoworks-style profile or a conventional JD-aligned resume. Click any field to edit directly.</p>
    </div><span className="page-count">Source-grounded · 10 projects maximum</span></div>
    <div className="studio-layout">
      <aside className="panel studio-controls">
        <div className="step-label">01 / UPLOAD RESUME</div>
        <h3>Candidate source</h3>
        <label className="dropzone" htmlFor="studio-file"><UploadCloud size={27}/>
          <strong>{resume?.name ?? "Choose a candidate resume"}</strong><span>PDF (text), DOCX or TXT · max 5 MB</span></label>
        <input className="file-input" id="studio-file" type="file" accept=".pdf,.docx,.txt" onChange={chooseResume}/>
        <div className="step-label extra">02 / TARGET ROLE (OPTIONAL)</div>
        <h3>Job description</h3>
        <label htmlFor="studio-job">Saved JD</label>
        <select id="studio-job" value={selectedJob} onChange={e => chooseJob(e.target.value)}>
          <option value="">Paste or upload a JD</option>
          {jobs.map(job => <option key={job.id} value={job.id}>{job.title}</option>)}
        </select>
        <JDFileUpload onText={text => { setJdText(text); setSelectedJob(""); }} onError={setError}/>
        <label htmlFor="studio-jd">JD text</label>
        <textarea id="studio-jd" value={jdText} rows={6} placeholder="Paste JD to activate Align to Job"
          onChange={e => { setJdText(e.target.value); setSelectedJob(""); }}/>
        <div className="studio-buttons">
          <button className="button primary full-width" disabled={!resume || busy} onClick={() => void parse()}>
            {busy ? <LoaderCircle size={17} className="spin"/> : <Sparkles size={17}/>} Parse Resume</button>
          <button className="button secondary full-width" disabled={!resume || busy || jdText.trim().length < 30}
            onClick={() => void align()}><WandSparkles size={16}/> Align to Job</button>
        </div>
        <div className="studio-ai-line"><span className={`studio-led ${aiConfigured ? "on" : ""}`}/>
          {aiConfigured ? "Groq AI enabled" : "Groq AI not configured · basic parser available"}</div>
        <p className="small-note">Alignment reorganizes evidenced information only. Missing skills are not invented. Original files remain unchanged.</p>
        {analysis && <div className="studio-match-result"><span>Estimated JD alignment</span><strong>{analysis.score}<small>/100</small></strong>
          <p>{analysis.matchedSkills.length} matched skill(s) · {analysis.missingSkills.length} not evidenced</p>
          {analysis.missingSkills.length > 0 && <div className="studio-gaps">{analysis.missingSkills.slice(0, 6).join(" · ")}</div>}</div>}
        {profile && <><div className="studio-after"><button className="button secondary full-width" disabled={busy} onClick={() => void save()}><Save size={16}/> Save Profile Draft {changed ? "• unsaved" : ""}</button>
          <button className="button primary full-width" onClick={print}><Download size={16}/> Print / Save PDF</button></div>
          <details className="studio-internal" open={showInternal} onToggle={e=>setShowInternal(e.currentTarget.open)}>
            <summary>Internal contact details (not on Algoworks PDF)</summary>
            <label>Name/email/phone are editable here for internal notes</label>
            <input aria-label="Candidate email" value={profile.email} placeholder="Email" onChange={e=>change("email",e.target.value)}/>
            <input aria-label="Candidate phone" value={profile.phone} placeholder="Phone" onChange={e=>change("phone",e.target.value)}/>
          </details></>}
        {error && <p role="alert" className="parser-message parser-error"><CircleAlert size={16}/>{error}</p>}
        {notice && <p role="status" className="parser-message parser-notice"><Check size={16}/>{notice}</p>}
      </aside>

      <div className="studio-view">
        <div className="studio-toolbar"><div><FileText size={17}/> Live editable profile</div>
          <div className="studio-switch" role="group" aria-label="Resume output format">
            <button className={style === "algoworks" ? "active" : ""} onClick={() => setStyle("algoworks")} type="button">Algoworks</button>
            <button className={style === "aligned" ? "active" : ""} onClick={() => setStyle("aligned")} type="button">Normal / JD aligned</button>
          </div></div>
        {profile ? <>
          <article className={`studio-edit-sheet ${style === "algoworks" ? "company" : "standard"}`}>
            {style === "algoworks" ? <div className="studio-company-brand"><img src="/algoworks-logo.png" alt="Algoworks logo"/></div>
              : <div className="studio-conventional-head">ATS-FRIENDLY CANDIDATE RESUME</div>}
            <div className="studio-green-rule"/>
            <input className="studio-name" aria-label="Candidate name" value={profile.candidateName} placeholder="Full name not detected"
              onChange={e => change("candidateName", e.target.value)}/>
            <div className="studio-designation-row">
              <input aria-label="Designation" value={profile.designation} placeholder="Designation from resume"
                onChange={e => change("designation", e.target.value)}/>
              <label className="studio-experience-label"><input aria-label="Experience years" type="number" min="0" max="60" step="0.5"
                value={profile.experienceYears ?? ""} placeholder="Years" onChange={e => change("experienceYears", e.target.value === "" ? null : Number(e.target.value))}/><span>years of experience</span></label>
            </div>
            <section className="studio-section"><div className="studio-section-heading"><h3>PROFESSIONAL SUMMARY</h3>
              <span className={count === 85 ? "studio-good" : "studio-caution"}>{count}/85 words {count===85 && <CheckCircle2 size={13}/>}</span></div>
              <textarea aria-label="Professional summary" rows={6} value={profile.professionalSummary}
                onChange={e=>change("professionalSummary",e.target.value)} placeholder="AI-generated verified summary"/>
            </section>
            <section className="studio-section"><h3>TECHNOLOGY STACK</h3>
              <div className="studio-skills">
                {profile.technicalSkills.map((skill,i)=><span key={`${skill}-${i}`}>{skill}
                  <button type="button" title={`Remove ${skill}`} onClick={()=>change("technicalSkills", profile.technicalSkills.filter((_,n)=>n!==i))}><X size={12}/></button></span>)}
                {profile.technicalSkills.length===0 && <em>No skills evidenced</em>}</div>
              <div className="studio-add-skill"><input value={newSkill} onChange={e=>setNewSkill(e.target.value)}
                onKeyDown={addSkill} placeholder="Add verified technology" aria-label="Add a technology"/>
                <button aria-label="Add technology" onClick={()=>addSkill()} type="button"><Plus size={16}/></button></div>
            </section>
            <section className="studio-section"><div className="studio-section-heading"><h3>PROJECT EXPERIENCE</h3><span>{profile.projects.length}/10 projects</span></div>
              {profile.projects.map((project,i)=><div className="studio-project" key={i}>
                <div className="studio-project-top"><span>PROJECT {String(i+1).padStart(2,"0")}</span>
                  <button type="button" title="Remove project" onClick={()=>change("projects",profile.projects.filter((_,n)=>n!==i))}><X size={14}/></button></div>
                <div className="studio-project-head"><input aria-label={`Project ${i+1} domain`} value={project.domain || project.title}
                    placeholder="Generic domain name" onChange={e=>changeProject(i,{domain:e.target.value,title:e.target.value})}/>
                  <input aria-label={`Project ${i+1} year`} value={project.year ?? ""} placeholder="Year" onChange={e=>changeProject(i,{year:e.target.value})}/></div>
                <label>Responsibility</label><input aria-label={`Project ${i+1} responsibility`} value={project.responsibilities ?? ""}
                  placeholder="Only if evidenced in resume" onChange={e=>changeProject(i,{responsibilities:e.target.value})}/>
                <label>Short project introduction</label><textarea aria-label={`Project ${i+1} introduction`} rows={3} value={project.description}
                  placeholder="Concise, source-supported project description" onChange={e=>changeProject(i,{description:e.target.value})}/>
                <label>Technology Stack Used</label><input aria-label={`Project ${i+1} technology stack`} value={(project.technologyStack ?? []).join(", ")}
                  placeholder="Technologies used in this project" onChange={e=>changeProject(i,{technologyStack:e.target.value.split(",").map(s=>s.trim()).filter(Boolean)})}/>
              </div>)}
              {profile.projects.length<10 && <button className="studio-quiet" type="button" onClick={()=>change("projects",[...profile.projects,{ title:"",domain:"",year:"",description:"",responsibilities:"",technologyStack:[],evidence:"" }])}><Plus size={14}/> Add a verified project</button>}
            </section>
            <EditableLines title="WORK EXPERIENCE" lines={profile.employmentHistory} onChange={v=>change("employmentHistory",v)}/>
            <EditableLines title="EDUCATION" lines={profile.education} onChange={v=>change("education",v)}/>
            <EditableLines title="CERTIFICATIONS" lines={profile.certifications} onChange={v=>change("certifications",v)}/>
            <div className="studio-edit-footer">Editable draft · {profile.sourceFileName} · {profile.aiStatus === "groq" ? "Groq-assisted" : "Basic extraction"}</div>
          </article>
          <div className="studio-info"><CircleAlert size={16}/>{profile.parserNote} Inline edits are unverified drafts and do not automatically improve ATS scores.</div>
          <div className="profile-print"><StaticDocument profile={profile} style={style}/></div>
        </> : <div className="profile-empty"><FileText size={36}/><h3>Upload your resume to begin</h3>
          <p>Parse a candidate profile, optionally align it to a JD, edit directly, then print an Algoworks-formatted PDF or a normal ATS-friendly resume.</p></div>}
      </div>
    </div>
  </div>;
}
