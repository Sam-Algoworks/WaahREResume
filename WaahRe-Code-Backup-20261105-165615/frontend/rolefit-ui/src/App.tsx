import { useCallback, useEffect, useState, type ChangeEvent, type CSSProperties, type FormEvent } from "react";
import {
  Activity, ArrowRight, ArrowUpRight, BarChart3, BrainCircuit, CheckCircle2,
  ChevronDown, CircleHelp, Database, FilePlus2, FileSearch, Files,
  FolderOpen, LayoutDashboard, ListFilter, Menu, Plus, RefreshCw,
  ShieldCheck, Sparkles, Trash2, UploadCloud, Users, X, FileText
} from "lucide-react";
import { api, type Analysis, type BatchResponse, type Job, type Stats } from "./api";
import JDFileUpload from "./components/JDFileUpload";
import ResumeParserPage from "./components/ResumeParserPage";
import TalentLibrary from "./components/TalentLibrary";
import { type ResumeProfileSummary } from "./api";

type Section = "dashboard" | "analyze" | "bulk" | "jobs" | "parser" | "candidates" | "history" | "library";
const menu: { id: Section; label: string; icon: typeof LayoutDashboard }[] = [
  { id: "dashboard", label: "Overview", icon: LayoutDashboard },
  { id: "parser", label: "Analyze & Align", icon: Sparkles },
  { id: "bulk", label: "Bulk Ranking", icon: BarChart3 },
  { id: "jobs", label: "Job Descriptions", icon: FolderOpen },
  { id: "library", label: "Talent Library", icon: Users }
];

const formatDate = (iso: string) => new Date(iso).toLocaleString(undefined, {
  month: "short", day: "numeric", hour: "2-digit", minute: "2-digit"
});

function ScoreCircle({ score, size = "normal" }: { score: number; size?: "normal" | "big" }) {
  return <div className={`score-circle ${size === "big" ? "big" : ""}`}
    style={{ "--score": `${score}%` } as CSSProperties}>
    <div className="score-inner"><strong>{score}</strong><span>/100</span></div>
  </div>;
}

function StatusPill({ connected }: { connected: boolean | null }) {
  return <span className={`status-pill ${connected === true ? "online" : connected === false ? "offline" : "pending"}`}>
    <span className="status-dot" /> {connected === null ? "Checking API" : connected ? "API Connected" : "API Offline"}
  </span>;
}

function Empty({ title, text }: { title: string; text: string }) {
  return <div className="empty"><div className="empty-icon"><Files size={24} /></div>
    <strong>{title}</strong><p>{text}</p></div>;
}

function AnalysisDetail({ result }: { result: Analysis }) {
  return <div className="analysis-detail">
    <div className="result-head">
      <ScoreCircle score={result.score} size="big" />
      <div><span className="eyebrow">Estimated JD alignment</span>
        <h3>{result.candidateName}</h3>
        <p className="muted">Compared with {result.jobTitle} · {result.fileName}</p>
        <p className="small-note">{result.notes}</p>
      </div>
    </div>
    <div className="result-columns">
      <div className="soft-panel"><h4>Matched skills <span className="count">{result.matchedSkills.length}</span></h4>
        <div className="chip-list">{result.matchedSkills.length ? result.matchedSkills.map(s => <span className="chip positive" key={s}><CheckCircle2 size={13} />{s}</span>) : <span className="muted">No exact skills evidenced</span>}</div>
      </div>
      <div className="soft-panel"><h4>Missing / unverified <span className="count">{result.missingSkills.length}</span></h4>
        <div className="chip-list">{result.missingSkills.length ? result.missingSkills.map(s => <span className="chip warning" key={s}>{s}</span>) : <span className="muted">No known skills missing from recognized vocabulary</span>}</div>
      </div>
    </div>
    <h4 className="subheading">Score calculation</h4>
    <div className="score-parts">{result.breakdown.map(part => <div className="score-row" key={part.category}>
      <div className="score-label"><span>{part.category}</span><span>{part.applicable ? `${part.awarded} / ${part.weight}` : "N/A"}</span></div>
      <div className="meter"><div style={{ width: `${part.applicable ? 100 * part.awarded / part.weight : 0}%` }} /></div>
    </div>)}</div>
    <p className="small-note">Not applicable criteria are excluded and their weights redistributed. This baseline does not determine hiring suitability.</p>
  </div>;
}

export default function App() {
  const [section, setSection] = useState<Section>("dashboard");
  const [connected, setConnected] = useState<boolean | null>(null);
  const [stats, setStats] = useState<Stats | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [analyses, setAnalyses] = useState<Analysis[]>([]);
  const [profiles, setProfiles] = useState<ResumeProfileSummary[]>([]);
  const [openAnalysisId, setOpenAnalysisId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const [jdText, setJdText] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [selectedJob, setSelectedJob] = useState("");
  const [uploadFiles, setUploadFiles] = useState<File[]>([]);
  const [result, setResult] = useState<Analysis | null>(null);
  const [batch, setBatch] = useState<BatchResponse | null>(null);
  const [parserSeed, setParserSeed] = useState<{ file?: File; jdText: string; savedProfileId?: string; id: number } | null>(null);

  const load = useCallback(async () => {
    try {
      const [health, nextStats, nextJobs, nextAnalyses, nextProfiles] = await Promise.all([
        api.health(), api.stats(), api.jobs(), api.analyses(), api.savedProfiles().catch(() => [])
      ]);
      setConnected(health.status === "Healthy");
      setStats(nextStats);
      setJobs(nextJobs);
      setAnalyses(nextAnalyses);
      setProfiles(nextProfiles);
      setError("");
    } catch (err) {
      setConnected(false);
      setError(err instanceof Error ? err.message : "API connection failed.");
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const navigate = (target: Section) => {
    setSection(target); setNavOpen(false); setError(""); setSuccess("");
  };

  function chooseJob(id: string) {
    const found = jobs.find(j => j.id === id);
    setSelectedJob(id);
    if (found) setJdText(found.description);
  }

  function chooseFiles(event: ChangeEvent<HTMLInputElement>) {
    setUploadFiles(Array.from(event.target.files ?? []));
    setResult(null); setBatch(null); setError("");
  }

  async function saveJob(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(""); setSuccess("");
    try {
      const saved = await api.createJob(jobTitle, jdText);
      await load(); setSelectedJob(saved.id); setJobTitle("");
      setSuccess(`Job description “${saved.title}” saved.`);
    } catch (err) { setError(err instanceof Error ? err.message : "Unable to save job."); }
    finally { setBusy(false); }
  }

  async function deleteJob(id: string) {
    if (!window.confirm("Delete this saved job description? Previous analyses will be retained.")) return;
    setBusy(true); setError("");
    try { await api.deleteJob(id); await load(); if (selectedJob === id) setSelectedJob(""); }
    catch (err) { setError(err instanceof Error ? err.message : "Unable to delete job."); }
    finally { setBusy(false); }
  }

  async function runAnalysis(bulkMode: boolean) {
    setBusy(true); setError(""); setSuccess(""); setResult(null); setBatch(null);
    try {
      if (jdText.trim().length < 30) throw new Error("Enter or select a job description (at least 30 characters).");
      if (uploadFiles.length === 0) throw new Error("Select at least one resume.");
      if (bulkMode) {
        if (uploadFiles.length > 10) throw new Error("Select no more than 10 resumes per batch.");
        const response = await api.bulk(jdText, uploadFiles);
        setBatch(response);
        setSuccess(`Completed ${response.results.length} candidate analyses.`);
      } else {
        const response = await api.single(jdText, uploadFiles[0]);
        setResult(response);
        setSuccess("Resume analyzed successfully.");
      }
      await load();
    } catch (err) { setError(err instanceof Error ? err.message : "Analysis failed."); }
    finally { setBusy(false); }
  }



  const knownProfilePairs = new Set(profiles.map(p => `${p.candidateName.trim().toLowerCase()}|${p.sourceFileName.trim().toLowerCase()}`));
  const additionalAnalysisCandidates = new Set(analyses.filter(a =>
    !knownProfilePairs.has(`${a.candidateName.trim().toLowerCase()}|${a.fileName.trim().toLowerCase()}`))
    .map(a => `${a.candidateName.trim().toLowerCase()}|${a.fileName.trim().toLowerCase()}`));
  const candidateCount = profiles.length + additionalAnalysisCandidates.size;

  const panelTitle = menu.find(m => m.id === section)?.label ?? "Overview";
  const goToStudio = (id: string) => {
    setParserSeed({ jdText: "", savedProfileId: id, id: Date.now() });
    navigate("parser");
  };
  const openAnalysis = (id: string) => {
    setOpenAnalysisId(id);
    navigate("library");
  };

  return <div className="app-shell">
    <div className="ambient ambient-one" /><div className="ambient ambient-two" />
    <aside className={`sidebar ${navOpen ? "nav-open" : ""}`}>
      <div className="brand"><div className="brand-mark"><BrainCircuit size={22}/></div>
        <div><strong>RoleFit <em>AI</em></strong><span>RECRUITMENT INTELLIGENCE</span></div>
        <button className="icon-button nav-close" onClick={() => setNavOpen(false)} aria-label="Close navigation"><X size={18}/></button>
      </div>
      <div className="sidebar-label">WORKSPACE</div>
      <nav className="navigation">{menu.map(item => <button key={item.id}
        className={`nav-link ${section === item.id ? "active" : ""}`} onClick={() => navigate(item.id)}>
        <item.icon size={19}/><span>{item.label}</span>{section === item.id && <span className="active-dash" />}
      </button>)}</nav>
      <div className="sidebar-spacer" />
      <div className="sidebar-card"><div className="sidebar-card-icon"><Sparkles size={19}/></div>
        <strong>Built for accuracy</strong><p>Every score is explained. Every hiring decision remains human.</p>
        <span>RoleFit workspace</span></div>
      <div className="sidebar-foot"><ShieldCheck size={16}/> Local development mode</div>
    </aside>
    {navOpen && <button className="nav-overlay" onClick={() => setNavOpen(false)} aria-label="Close menu" />}

    <div className="main-wrap">
      <header className="topbar">
        <div className="topbar-start"><button className="icon-button menu-toggle" onClick={() => setNavOpen(true)} aria-label="Open navigation"><Menu size={22}/></button>
          <span className="breadcrumb">Workspace</span><span className="breadcrumb-separator">/</span><strong>{panelTitle}</strong></div>
        <div className="topbar-end"><StatusPill connected={connected}/>
          <button className="icon-button" onClick={() => void load()} title="Refresh data" aria-label="Refresh data"><RefreshCw size={17}/></button>
          <div className="avatar" aria-label="Local development user">RF</div></div>
      </header>

      <main className="content">
        {error && <div role="alert" className="alert error-alert"><CircleHelp size={18}/><span>{error}</span><button onClick={() => setError("")} aria-label="Dismiss error"><X size={17}/></button></div>}
        {success && <div role="status" className="alert success-alert"><CheckCircle2 size={18}/><span>{success}</span><button onClick={() => setSuccess("")} aria-label="Dismiss notification"><X size={17}/></button></div>}
        {section === "dashboard" && <>
          <section className="hero">
            <div className="hero-copy"><div className="hero-badge"><span className="tiny-dot"/> INTELLIGENT RECRUITMENT PLATFORM</div>
              <h1>Better matches.<br/><span>Smarter decisions.</span></h1>
              <p>Understand candidate experience, measure alignment with job requirements, and explore evidence-based hiring insights in one place.</p>
              <div className="hero-actions"><button className="button primary" onClick={() => navigate("parser")}><FileSearch size={18}/> Analyze & Align <ArrowRight size={17}/></button>
                <button className="button secondary" onClick={() => navigate("bulk")}><Users size={18}/> Bulk ranking</button></div>
            </div>
            <div className="hero-art" aria-hidden="true"><div className="orbit orbit-a"/><div className="orbit orbit-b"/>
              <div className="hero-art-core"><BrainCircuit size={62} strokeWidth={1.2}/></div>
              <div className="art-chip chip-top"><CheckCircle2 size={16}/> Evidence-first</div>
              <div className="art-chip chip-bottom"><Activity size={16}/> Transparent scoring</div>
            </div>
          </section>
          <div className="section-heading"><div><div className="eyebrow">LIVE WORKSPACE</div><h2>Overview at a glance</h2></div><span className="subtle">Calculated from local saved data</span></div>
          <div className="stat-grid">
            {[
              { icon: Users, label: "Candidates", value: candidateCount, color: "purple" },
              { icon: FolderOpen, label: "Saved jobs", value: stats?.totalJobs ?? "—", color: "blue" },
              { icon: FileSearch, label: "Completed analyses", value: stats?.totalAnalyses ?? "—", color: "pink" },
              { icon: Activity, label: "Average alignment", value: stats && stats.totalAnalyses ? `${stats.averageScore}/100` : "—", color: "green" }
            ].map(stat => <div className="stat-card" key={stat.label}>
              <div className={`stat-icon ${stat.color}`}><stat.icon size={21}/></div>
              <span>{stat.label}</span><strong>{stat.value}</strong><div className="stat-bottom">Current workspace data</div>
            </div>)}
          </div>
          <div className="dashboard-grid">
            <section className="panel"><div className="panel-heading"><div><h3>Recent analysis activity</h3><p>Latest candidate evaluations</p></div>
              <button className="text-action" onClick={() => navigate("library")}>View library <ArrowUpRight size={16}/></button></div>
              {stats?.recent.length ? <div className="records">{stats.recent.map(a => <button className="record-row rf-record-action" key={a.id} type="button" onClick={() => openAnalysis(a.id)} title={`View ${a.candidateName} analysis`}>
                <div className="candidate-avatar">{a.candidateName.slice(0, 2).toUpperCase()}</div><div className="record-main"><strong>{a.candidateName}</strong><span>{a.jobTitle}</span></div>
                <span className="date-text">{formatDate(a.createdAt)}</span><span className="mini-score">{a.score}/100</span></button>)}</div>
                : <Empty title="No analyses yet" text="Run your first analysis to populate this activity feed."/>}
            </section>
            <section className="panel next-panel"><div className="panel-heading"><div><h3>Quick actions</h3><p>Jump into your workflow</p></div></div>
              <button className="quick-action" onClick={() => navigate("jobs")}><div className="quick-icon"><FilePlus2 size={20}/></div><div><strong>Create job description</strong><span>Save or reuse an opening</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => navigate("parser")}><div className="quick-icon"><FileSearch size={20}/></div><div><strong>Analyze & align resume</strong><span>One step to parse and evaluate</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => navigate("bulk")}><div className="quick-icon"><ListFilter size={20}/></div><div><strong>Rank multiple candidates</strong><span>Compare up to 10 resumes</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => navigate("parser")}><div className="quick-icon"><FileText size={20}/></div><div><strong>Build professional profile</strong><span>Parse and edit directly in the preview</span></div><ArrowUpRight size={17}/></button>
            </section>
          </div>
        </>}

        {section === "jobs" && <>
          <div className="page-head"><div><div className="eyebrow">JOB WORKSPACE</div><h1>Job descriptions</h1><p>Create and save job requirements, then reuse them during analysis.</p></div><span className="page-count">{jobs.length} saved jobs</span></div>
          <div className="form-layout"><form className="panel form-panel" onSubmit={e => void saveJob(e)}>
            <h3><Plus size={20}/> Create a job description</h3>
            <label htmlFor="job-title">Job title</label><input id="job-title" value={jobTitle} onChange={e => setJobTitle(e.target.value)} placeholder="e.g. Full Stack Developer" required maxLength={120}/>
            <label htmlFor="job-description">Job requirements</label>
            <JDFileUpload onText={text => { setJdText(text); setSelectedJob(""); setError(""); }} onError={setError}/>
            <textarea id="job-description" value={jdText} onChange={e => setJdText(e.target.value)} placeholder="Paste the job description, including skills, experience and responsibilities..." rows={13} required minLength={30}/>
            <button className="button primary" type="submit" disabled={busy || jobTitle.trim().length === 0 || jdText.trim().length < 30}>Save job description <ArrowRight size={17}/></button>
          </form>
          <section className="panel saved-panel"><div className="panel-heading"><div><h3>Saved job descriptions</h3><p>Use a saved JD for one or many resumes</p></div></div>
            {jobs.length ? <div className="job-list">{jobs.map(job => <div className="job-card" key={job.id}>
              <div className="job-row"><div className="job-symbol"><FolderOpen size={20}/></div><div><strong>{job.title}</strong><span>Version {job.version} · {formatDate(job.createdAt)}</span></div></div>
              <p>{job.description.slice(0, 155)}{job.description.length > 155 ? "…" : ""}</p>
              <div className="job-actions"><button onClick={() => { setParserSeed({ jdText: job.description, id: Date.now() }); navigate("parser"); }}>Analyze resumes <ArrowRight size={15}/></button>
                <button className="danger-icon" onClick={() => void deleteJob(job.id)} aria-label={`Delete ${job.title}`} title="Delete job"><Trash2 size={16}/></button></div>
            </div>)}</div> : <Empty title="No saved jobs" text="Create your first job description using the form."/>}
          </section></div>
        </>}

        {(section === "analyze" || section === "bulk") && <>
          <div className="page-head"><div><div className="eyebrow">{section === "bulk" ? "BULK CANDIDATE REVIEW" : "SINGLE CANDIDATE REVIEW"}</div>
            <h1>{section === "bulk" ? "Candidate ranking" : "Resume analyzer"}</h1>
            <p>{section === "bulk" ? "Compare up to 10 resumes against the same JD." : "Evaluate one candidate against your job requirements."}</p></div>
            <span className="page-count">Baseline evidence engine</span></div>
          <div className="workflow-grid">
            <section className="panel upload-panel"><div className="step-label">STEP 01 / JOB REQUIREMENTS</div><h3>Choose job description</h3>
              <label htmlFor="saved-jd">Use saved job (optional)</label><div className="select-wrap"><select id="saved-jd" value={selectedJob} onChange={e => chooseJob(e.target.value)}>
                <option value="">Enter a new JD below</option>{jobs.map(j => <option value={j.id} key={j.id}>{j.title}</option>)}
              </select><ChevronDown size={18}/></div>
              <label htmlFor="analysis-jd">Job description</label>
              <JDFileUpload onText={text => { setJdText(text); setSelectedJob(""); setError(""); setResult(null); setBatch(null); }} onError={setError}/>
              <textarea id="analysis-jd" rows={10} value={jdText} onChange={e => { setJdText(e.target.value); setSelectedJob(""); }} placeholder="Paste the full job description here (minimum 30 characters)." />
              <div className="step-label extra">STEP 02 / CANDIDATE DOCUMENTS</div><h3>Upload resumes</h3>
              <label className="dropzone" htmlFor="resume-upload"><UploadCloud size={31}/><strong>{uploadFiles.length ? `${uploadFiles.length} file(s) selected` : "Choose resume file(s)"}</strong>
                <span>PDF (selectable text), DOCX or TXT · 5 MB each</span><small>{section === "bulk" ? "Up to 10 files" : "One resume at a time"}</small></label>
              <input id="resume-upload" type="file" accept=".pdf,.docx,.txt" multiple={section === "bulk"} onChange={chooseFiles} className="file-input"/>
              {uploadFiles.length > 0 && <div className="file-names">{uploadFiles.map((f, index) => <div key={`${f.name}-${index}`}><Files size={15}/>{f.name}</div>)}</div>}
              <button className="button primary full-width" disabled={busy || jdText.trim().length < 30 || uploadFiles.length === 0}
                onClick={() => void runAnalysis(section === "bulk")}>
                {busy ? <><RefreshCw size={17} className="spin"/> Analyzing...</> : <><Sparkles size={18}/> {section === "bulk" ? "Analyze & rank candidates" : "Analyze resume"}<ArrowRight size={17}/></>}
              </button>
              <p className="small-note">This starter uses deterministic skill and content checks. Groq, OCR, full semantic evaluation, and official ATS scoring are not yet implemented.</p>
            </section>
            <section className="panel result-panel"><div className="panel-heading"><div><h3>{section === "bulk" ? "Ranking results" : "Alignment report"}</h3>
              <p>Results come from the ASP.NET Core API, not mock data</p></div></div>
              {section === "analyze" && (result ? <>
                <AnalysisDetail result={result}/>
                {uploadFiles.length > 0 && <button className="button primary full-width" style={{marginTop:18}}
                  onClick={() => { setParserSeed({ file: uploadFiles[0], jdText, id: Date.now() }); navigate("parser"); }}>
                  <Sparkles size={17}/> Align & build professional resume <ArrowRight size={17}/>
                </button>}
              </> : <Empty title="Ready for your first result" text="Choose a JD and upload a resume to see the evidence-based score breakdown."/>)}
              {section === "bulk" && (batch ? <>
                <div className="ranking-list">{batch.results.map((a, index) => <button type="button" className="ranking-item rf-rank-action" key={a.id} onClick={() => openAnalysis(a.id)} title={`Open ${a.candidateName} evaluation`}><span className="rank">#{index + 1}</span>
                  <div><strong>{a.candidateName}</strong><span>{a.matchedSkills.length} matched · {a.missingSkills.length} missing</span></div><strong className="mini-score">{a.score}/100</strong></button>)}</div>
                {batch.failures.length > 0 && <div className="failure-list"><h4>Unable to process</h4>{batch.failures.map(f => <p key={f.fileName}><strong>{f.fileName}:</strong> {f.error}</p>)}</div>}
                {batch.results[0] && <><h4 className="subheading">Top-ranked profile</h4><AnalysisDetail result={batch.results[0]}/></>}
              </> : <Empty title="Ranking results will appear here" text="Select the same JD and multiple candidate resumes to rank them consistently."/>)}
            </section>
          </div>
        </>}

        {section === "parser" && <ResumeParserPage key={parserSeed?.id ?? 0} jobs={jobs} seed={parserSeed} onSaved={() => void load()}/>}

        {section === "library" && <TalentLibrary profiles={profiles} analyses={analyses}
          highlightedAnalysisId={openAnalysisId} onOpenProfile={goToStudio}/>}
        <footer className="footer"><span>RoleFit AI · Local development MVP</span><span><Database size={15}/> JSON-backed starter storage · SQL Server planned</span></footer>
      </main>
    </div>
  </div>;
}
