import { useEffect, useRef, useState, type ChangeEvent, type KeyboardEvent } from "react";
import { createPortal } from "react-dom";
import {
  Check, CheckCircle2, CircleAlert, Download, FileText, LoaderCircle,
  Plus, Save, Sparkles, UploadCloud, WandSparkles, X
} from "lucide-react";
import { api, type Analysis, type Job, type ResumeProfile, type ResumeProject } from "../api";
import JDFileUpload from "./JDFileUpload";
import "./ResumeMode.css";

type Style = "algoworks" | "aligned";
type Seed = { file: File; jdText: string } | null;
const wordCount = (value: string) => value.trim() ? value.trim().split(/\s+/).length : 0;
const safeExperience = (value: number | null) => value == null ? "" : `${value} ${value === 1 ? "year" : "years"} of experience`;

type SkillGroup = { label: string; technologies: string[] };
const skillCategories: { label: string; test: RegExp }[] = [
  { label: "Languages / Web", test: /^(?:C|C\+\+|C#|Java|JavaScript|TypeScript|Python|SQL|R|HTML(?:5)?|CSS(?:3)?)$/i },
  { label: "Frameworks", test: /(?:Django|Flask|FastAPI|React|Angular|Vue|Spring|ASP\.NET|\.NET|Bootstrap|Next\.js|Node\.js|Express|Laravel)/i },
  { label: "Data / ML", test: /(?:NumPy|pandas|scikit|Plotly|Power BI|Tableau|TensorFlow|PyTorch|Machine Learning|Snowflake|Spark)/i },
  { label: "Databases", test: /(?:PostgreSQL|Redis|SQLite|SQL Server|MySQL|MongoDB|Oracle|MariaDB)/i },
  { label: "Cloud / DevOps", test: /(?:AWS|Azure|GCP|Google Cloud|Docker|Kubernetes|Terraform|Git|GitHub|Heroku|Gunicorn|Nginx|CI\/CD|Jenkins|Linux)/i }
];

function groupTechnologies(skills: string[]): SkillGroup[] {
  const groups: SkillGroup[] = skillCategories.map(({ label }) => ({ label, technologies: [] }));
  const others: string[] = [];
  for (const skill of [...new Set(skills.map(value => value.trim()).filter(Boolean))]) {
    const category = skillCategories.findIndex(({ test }) => test.test(skill));
    if (category < 0) others.push(skill);
    else groups[category].technologies.push(skill);
  }
  if (others.length) groups.push({ label: "Other Tools", technologies: others });
  return groups.filter(group => group.technologies.length > 0);
}

function StaticDocument({ profile, style }: { profile: ResumeProfile; style: Style }) {
  const company = style === "algoworks";
  return <div className={`studio-static ${company ? "company" : "standard"}`}>
    <div className="static-header">
      <img src="/algoworks-logo.png" alt="Algoworks logo" />
      {!company && <span className="resume-mode-label">ATS-FRIENDLY RESUME</span>}
    </div>
    <div className="static-green-rule"/>
    <h1>{profile.candidateName || "Candidate name not detected"}</h1>
    <p className="static-designation">{profile.designation || "Designation not provided"}
      {profile.experienceYears != null && <span> · {safeExperience(profile.experienceYears)}</span>}
    </p>
    {!company && (profile.email || profile.phone) &&
      <p className="static-contact">{[profile.email, profile.phone].filter(Boolean).join("  ·  ")}</p>}
    <section className="static-summary-section"><h2>PROFESSIONAL SUMMARY</h2>
      <p>{profile.professionalSummary || "Professional summary has not been confirmed."}</p>
    </section>
    <section className="static-technologies"><h2>{company ? "TECHNOLOGY STACK" : "CORE SKILLS"}</h2>
      <div className="static-tech-box"><div className="static-tech-categories">
        {profile.technicalSkills.length ? groupTechnologies(profile.technicalSkills).map(group =>
          <div className="static-tech-category" key={group.label}>
            <strong>{group.label}</strong><span>{group.technologies.join(", ")}</span>
          </div>) : <div className="static-tech-category"><span>Not provided</span></div>}
      </div></div>
    </section>
    <section className="static-project-section"><h2>{company ? "PROJECT EXPERIENCE" : "RELEVANT PROJECTS"}</h2>
      {profile.projects.length > 0 ? profile.projects.slice(0, 10).map((project, i) =>
        <div className="static-project" key={`${project.domain}-${i}`}>
          <div className="static-project-head">
            <strong>{i + 1}. {project.domain || project.title || "Unnamed project"}</strong><span>{project.year}</span>
          </div>
          {project.description && <p>{project.description}</p>}
          <p className="static-techused"><b>Technology Stack Used:</b> {project.technologyStack.length ? project.technologyStack.join(", ") : "Not specified"}</p>
        </div>) : <p>No separate projects could be verified from the uploaded resume.</p>}
    </section>
    {company && <footer className="static-footer"><span>Algoworks Technologies</span><span>Employee Professional Profile</span></footer>}
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
  const [printHost, setPrintHost] = useState<HTMLElement | null>(null);

  // Render the printable document directly under <body>, outside the app.
  // Printing the whole SPA caused blank pages from hidden dashboard elements.
  useEffect(() => {
    const host = document.createElement("div");
    host.id = "rolefit-print-host";
    document.body.appendChild(host);
    setPrintHost(host);
    return () => { host.remove(); };
  }, []);

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
        ? `Groq processed the resume and found ${parsed.projects.length} project(s). Check every detail before exporting.`
        : `Basic parser found ${parsed.projects.length} project(s). Configure Groq for an 85-word introduction and project enrichment.`);
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
      <h1>Resume Parser & JD Alignment</h1><p>Algoworks professional profile format: identity, 85-word introduction, skills and verified projects. Click text to edit directly.</p>
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
        {profile && <p className="studio-view-mode-hint" role="status">
          {style === "algoworks"
            ? "Algoworks corporate profile: branded format for candidate submissions."
            : analysis
              ? "JD-aligned resume: this view uses the profile generated by Align to Job. Review all suggested wording."
              : "Standard ATS-friendly resume. To tailor the content to a job, upload/paste a JD and click Align to Job."
          }
        </p>}
        {profile ? <>
          <article className={`studio-edit-sheet ${style === "algoworks" ? "company" : "standard"}`}>
            <div className="studio-company-brand">
              <img src="/algoworks-logo.png" alt="Algoworks logo"/>
              {style === "aligned" && <div className="studio-normal-banner">
                <strong>ATS-FRIENDLY RESUME</strong>
                <small>{analysis ? "JD-aligned draft" : "Standard resume format"}</small>
              </div>}
            </div>
            <div className="studio-green-rule"/>
            <input className="studio-name" aria-label="Candidate name" value={profile.candidateName} placeholder="Full name not detected"
              onChange={e => change("candidateName", e.target.value)}/>
            <div className="studio-designation-row">
              <input aria-label="Designation" value={profile.designation} placeholder="Designation from resume"
                onChange={e => change("designation", e.target.value)}/>
              <label className="studio-experience-label"><input aria-label="Experience years" type="number" min="0" max="60" step="0.5"
                value={profile.experienceYears ?? ""} placeholder="Years" onChange={e => change("experienceYears", e.target.value === "" ? null : Number(e.target.value))}/><span>years of experience</span></label>
            </div>
            {style === "aligned" && (profile.email || profile.phone) &&
              <p className="studio-normal-contact">
                {[profile.email, profile.phone].filter(Boolean).join(" · ")}
              </p>}
            <section className="studio-section"><div className="studio-section-heading"><h3>PROFESSIONAL SUMMARY</h3>
              <span className={count === 85 ? "studio-good" : "studio-caution"}>{count}/85 words {count===85 && <CheckCircle2 size={13}/>}</span></div>
              <textarea aria-label="Professional summary" rows={6} value={profile.professionalSummary}
                onChange={e=>change("professionalSummary",e.target.value.trim().split(/\s+/).filter(Boolean).length > 85
                  ? e.target.value.trim().split(/\s+/).slice(0,85).join(" ") : e.target.value)}
                placeholder="Groq-generated 85-word introduction using only resume evidence"/>
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
          </article>
          <div className="studio-info"><CircleAlert size={16}/>
            <span>{profile.parserNote} {profile.projects.length === 0 ? "Check that the resume has an identifiable Projects section; unrelated work-history bullets are not invented as projects. " : ""}
            {count !== 85 ? `Introduction has ${count} words. ${profile.aiStatus === "groq" ? "Review source evidence or regenerate with Groq." : "Configure Groq for a supported 85-word introduction."} ` : ""}
            Inline edits remain unverified drafts and never improve the ATS score automatically.</span>
          </div>
          {printHost && createPortal(<StaticDocument profile={profile} style={style}/>, printHost)}
        </> : <div className="profile-empty"><FileText size={36}/><h3>Upload your resume to begin</h3>
          <p>Parse a candidate profile, optionally align it to a JD, edit directly, then print an Algoworks-formatted PDF or a normal ATS-friendly resume.</p></div>}
      </div>
    </div>
  </div>;
}
