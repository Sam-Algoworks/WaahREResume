import { useState, type ChangeEvent, type KeyboardEvent } from "react";
import {
  Check, CircleAlert, FileText, LoaderCircle, Plus, Save, Sparkles, UploadCloud, X
} from "lucide-react";
import { api, type Job, type ResumeProfile } from "../api";
import JDFileUpload from "./JDFileUpload";

function EditableLines({ title, lines, onChange }: {
  title: string;
  lines: string[];
  onChange: (lines: string[]) => void;
}) {
  return <section className="profile-section">
    <h3>{title}</h3>
    {lines.map((line, index) => <div className="profile-line-row" key={index}>
      <input aria-label={`${title} item ${index + 1}`} value={line}
        placeholder={`Add ${title.toLowerCase()} detail`}
        onChange={e => onChange(lines.map((value, i) => i === index ? e.target.value : value))}/>
      <button type="button" className="profile-remove" title="Remove item"
        onClick={() => onChange(lines.filter((_, i) => i !== index))}><X size={14}/></button>
    </div>)}
    <button className="profile-add" type="button" onClick={() => onChange([...lines, ""])}><Plus size={14}/> Add item</button>
  </section>;
}

export default function ResumeParserPage({ jobs }: { jobs: Job[] }) {
  const [resume, setResume] = useState<File | null>(null);
  const [selectedJob, setSelectedJob] = useState("");
  const [jdText, setJdText] = useState("");
  const [profile, setProfile] = useState<ResumeProfile | null>(null);
  const [newSkill, setNewSkill] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [changed, setChanged] = useState(false);

  function change<K extends keyof ResumeProfile>(key: K, value: ResumeProfile[K]) {
    setProfile(current => current ? { ...current, [key]: value } : current);
    setChanged(true);
    setNotice("");
  }

  function changeResume(e: ChangeEvent<HTMLInputElement>) {
    const selected = e.target.files?.[0] ?? null;
    setResume(selected);
    setProfile(null);
    setChanged(false);
    setError("");
    setNotice("");
  }

  function chooseJob(id: string) {
    setSelectedJob(id);
    setJdText(jobs.find(j => j.id === id)?.description ?? "");
  }

  async function parseResume() {
    if (!resume) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const parsed = await api.parseResume(resume);
      setProfile(parsed); setChanged(false);
      setNotice("Profile extracted. Click any field in the preview to correct it; save a draft when ready.");
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to parse this resume."); }
    finally { setBusy(false); }
  }

  async function saveDraft() {
    if (!profile) return;
    setBusy(true); setError(""); setNotice("");
    try {
      const saved = await api.saveResumeDraft(profile);
      setProfile(saved); setChanged(false);
      setNotice("Draft saved locally. Edited content is not treated as verified ATS evidence.");
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to save the draft."); }
    finally { setBusy(false); }
  }

  function addSkill(e?: KeyboardEvent<HTMLInputElement>) {
    if (e && e.key !== "Enter") return;
    e?.preventDefault();
    const skill = newSkill.trim();
    if (!skill || !profile) return;
    if (!profile.technicalSkills.some(s => s.toLowerCase() === skill.toLowerCase()))
      change("technicalSkills", [...profile.technicalSkills, skill]);
    setNewSkill("");
  }

  return <div className="resume-parser-page">
    <div className="page-head">
      <div><div className="eyebrow">DOCUMENT STUDIO / NEW</div>
        <h1>Resume parser & profile studio</h1>
        <p>Upload a resume, review the extracted details, and edit directly within an Algoworks-style profile.</p>
      </div><span className="page-count">Inline editing · development preview</span>
    </div>

    <div className="parser-workspace">
      <section className="panel parser-controls">
        <div className="step-label">STEP 01 / SOURCE DOCUMENT</div>
        <h3>Upload candidate resume</h3>
        <label className="dropzone" htmlFor="profile-resume-upload">
          <UploadCloud size={30}/>
          <strong>{resume?.name ?? "Choose a resume"}</strong>
          <span>PDF with selectable text, DOCX or TXT · up to 5 MB</span>
        </label>
        <input className="file-input" id="profile-resume-upload" type="file"
          accept=".pdf,.docx,.txt" onChange={changeResume}/>
        <div className="step-label extra">STEP 02 / TARGET JOB (OPTIONAL)</div>
        <h3>Job description</h3>
        <label htmlFor="parser-saved-jd">Use saved JD</label>
        <select id="parser-saved-jd" value={selectedJob} onChange={e => chooseJob(e.target.value)}>
          <option value="">Paste or upload a JD</option>
          {jobs.map(job => <option key={job.id} value={job.id}>{job.title}</option>)}
        </select>
        <JDFileUpload onText={(text) => { setJdText(text); setSelectedJob(""); setNotice("JD uploaded. You can edit its text below."); }} onError={setError}/>
        <label htmlFor="parser-jd-text">JD text</label>
        <textarea id="parser-jd-text" rows={7} value={jdText}
          onChange={e => { setJdText(e.target.value); setSelectedJob(""); }}
          placeholder="Paste the JD or upload a file. Optional for profile extraction."/>
        <p className="small-note">This phase extracts the profile from the resume only. JD-aware AI suggestions and re-scoring will be added later; entering a JD does not add skills to the candidate.</p>
        <button type="button" className="button primary full-width" disabled={!resume || busy}
          onClick={() => void parseResume()}>
          {busy ? <LoaderCircle className="spin" size={17}/> : <Sparkles size={17}/>}
          {busy ? "Working..." : "Parse resume"}
        </button>
        {profile && <button type="button" className="button secondary full-width" disabled={busy}
          onClick={() => void saveDraft()}><Save size={16}/> Save profile draft {changed ? "· unsaved changes" : ""}</button>}
        {error && <div className="parser-message parser-error"><CircleAlert size={17}/>{error}</div>}
        {notice && <div role="status" className="parser-message parser-notice"><Check size={17}/>{notice}</div>}
      </section>

      <div className="profile-preview-area">
        <div className="preview-toolbar"><div><FileText size={17}/> Professional profile preview</div>
          <span>{profile ? (changed ? "Unsaved draft" : profile.status) : "Upload a resume to begin"}</span></div>
        {profile ? <article className="profile-sheet">
          <div className="profile-company-header">
            <div className="algo-identity" aria-label="Algoworks company branding">
              <div className="algo-mark" aria-hidden="true">a<span>w</span></div>
              <strong>ALGOWORKS</strong>
            </div>
            <span className="profile-document-type">PROFESSIONAL PROFILE</span>
          </div>
          <div className="profile-intro">
            <label className="profile-hint">Candidate name</label>
            <input className="profile-name-input" value={profile.candidateName}
              placeholder="Candidate name not detected" aria-label="Candidate name"
              onChange={e => change("candidateName", e.target.value)}/>
            <label className="profile-hint">Designation</label>
            <input className="profile-role-input" value={profile.designation}
              placeholder="Designation not detected" aria-label="Designation"
              onChange={e => change("designation", e.target.value)}/>
            <div className="profile-metadata">
              <label>Email <input type="email" value={profile.email} placeholder="Not found"
                onChange={e => change("email", e.target.value)}/></label>
              <label>Phone <input value={profile.phone} placeholder="Not found"
                onChange={e => change("phone", e.target.value)}/></label>
              <label>Years of experience <input type="number" min="0" max="60" step="0.5"
                value={profile.experienceYears ?? ""} placeholder="Not found"
                onChange={e => change("experienceYears", e.target.value === "" ? null : Number(e.target.value))}/></label>
            </div>
          </div>
          <section className="profile-section">
            <h3>Professional summary</h3>
            <textarea aria-label="Professional summary" rows={5} value={profile.professionalSummary}
              placeholder="No summary found in the resume. Add only confirmed information."
              onChange={e => change("professionalSummary", e.target.value)}/>
          </section>
          <section className="profile-section">
            <h3>Technology stack</h3>
            <div className="profile-skills">
              {profile.technicalSkills.map(skill => <span className="profile-skill" key={skill}>{skill}
                <button type="button" title={`Remove ${skill}`} aria-label={`Remove ${skill}`}
                  onClick={() => change("technicalSkills", profile.technicalSkills.filter(s => s !== skill))}><X size={12}/></button></span>)}
              {!profile.technicalSkills.length && <span className="profile-none">No technology evidence extracted</span>}
            </div>
            <div className="profile-add-skill"><input value={newSkill} placeholder="Add verified skill..."
              aria-label="New skill" onChange={e => setNewSkill(e.target.value)}
              onKeyDown={addSkill}/><button type="button" aria-label="Add skill" onClick={() => addSkill()}><Plus size={17}/></button></div>
          </section>
          <EditableLines title="Employment history" lines={profile.employmentHistory}
            onChange={value => change("employmentHistory", value)}/>
          <section className="profile-section">
            <h3>Project experience</h3>
            {profile.projects.map((project, index) => <div key={index} className="profile-project">
              <div className="profile-project-top"><span>PROJECT {String(index + 1).padStart(2, "0")}</span>
                <button type="button" title="Remove project" onClick={() => change("projects", profile.projects.filter((_, i) => i !== index))}><X size={14}/></button></div>
              <input aria-label={`Project ${index + 1} title`} value={project.title} placeholder="Project title"
                onChange={e => change("projects", profile.projects.map((p, i) => i === index ? { ...p, title: e.target.value } : p))}/>
              <textarea rows={2} aria-label={`Project ${index + 1} description`} value={project.description} placeholder="Project description (verified only)"
                onChange={e => change("projects", profile.projects.map((p, i) => i === index ? { ...p, description: e.target.value } : p))}/>
            </div>)}
            <button type="button" className="profile-add" onClick={() => change("projects", [...profile.projects, { title: "", description: "" }])}><Plus size={14}/> Add project</button>
          </section>
          <EditableLines title="Education" lines={profile.education} onChange={value => change("education", value)}/>
          <EditableLines title="Certifications" lines={profile.certifications} onChange={value => change("certifications", value)}/>
          <div className="profile-footer"><span>ALGOWORKS · PROFESSIONAL CANDIDATE PROFILE</span>
            <span>Draft for verification · {profile.sourceFileName}</span></div>
        </article> : <div className="profile-empty">
          <div className="profile-empty-symbol"><FileText size={34}/></div>
          <h3>Your candidate profile will appear here</h3>
          <p>Upload a resume and select Parse resume. You can then click into any field and edit the profile without entering a separate edit mode.</p>
        </div>}
        <p className="preview-disclaimer">Development preview. This uses baseline rule-based extraction, not Groq AI. The Algoworks heading is a temporary styled wordmark; replace it with an authorized official logo asset before external use. Edited or added claims require verification and do not automatically change ATS scores.</p>
      </div>
    </div>
  </div>;
}
