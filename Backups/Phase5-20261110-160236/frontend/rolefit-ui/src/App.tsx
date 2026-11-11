import { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import {
  Activity, ArrowRight, ArrowUpRight, BarChart3, CheckCircle2,
  ChevronDown, CircleHelp, Database, FilePlus2, FileSearch, Files,
  FolderOpen, LayoutDashboard, ListFilter, Menu, Plus, RefreshCw,
  Sparkles, Trash2, UploadCloud, Users, X, FileText
} from "lucide-react";
import { api, type Analysis, type ArchiveEntry, type BatchResponse, type Job, type Stats } from "./api";
import JDFileUpload from "./components/JDFileUpload";
import ResumeParserPage from "./components/ResumeParserPage";
import TalentLibrary from "./components/TalentLibrary";
import CandidateReview from "./components/CandidateReview";
import { type ResumeProfileSummary } from "./api";

type Section = "dashboard" | "analyze" | "bulk" | "jobs" | "parser" | "candidates" | "history" | "library" | "review";
const menu: { id: Section; label: string; icon: typeof LayoutDashboard }[] = [
  { id: "dashboard", label: "Home", icon: LayoutDashboard },
  { id: "analyze", label: "ATS Check", icon: FileSearch },
  { id: "bulk", label: "Rankings", icon: BarChart3 },
  { id: "parser", label: "Resume Parser", icon: FileText },
  { id: "jobs", label: "Jobs", icon: FolderOpen },
  { id: "library", label: "History", icon: Users }
];

const formatDate = (iso: string) => new Date(iso).toLocaleString(undefined, {
  month: "short", day: "numeric", hour: "2-digit", minute: "2-digit"
});

function Empty({ title, text }: { title: string; text: string }) {
  return <div className="empty"><div className="empty-icon"><Files size={24} /></div>
    <strong>{title}</strong><p>{text}</p></div>;
}

export default function App() {
  const [section, setSection] = useState<Section>("dashboard");
  const [stats, setStats] = useState<Stats | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [analyses, setAnalyses] = useState<Analysis[]>([]);
  const [profiles, setProfiles] = useState<ResumeProfileSummary[]>([]);
  const [archives, setArchives] = useState<ArchiveEntry[]>([]);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const [jdText, setJdText] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [selectedJob, setSelectedJob] = useState("");
  const [uploadFiles, setUploadFiles] = useState<File[]>([]);
  const [, setResult] = useState<Analysis | null>(null);
  const [batch, setBatch] = useState<BatchResponse | null>(null);
  const [parserSeed, setParserSeed] = useState<{ file?: File; jdText: string; savedProfileId?: string; id: number } | null>(null);
  const [review, setReview] = useState<{ result: Analysis; original: File | null; back: Section; jdText: string; profileId?: string } | null>(null);

  const load = useCallback(async () => {
    try {
      const [, nextStats, nextJobs, nextAnalyses, nextProfiles, nextArchives] = await Promise.all([
        api.health(), api.stats(), api.jobs(), api.analyses(), api.savedProfiles().catch(() => []), api.archive().catch(() => [])
      ]);
      setStats(nextStats);
      setJobs(nextJobs);
      setAnalyses(nextAnalyses);
      setProfiles(nextProfiles);
      setArchives(nextArchives);
      setError("");
    } catch (err) {
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
    const chosen = Array.from(event.target.files ?? []);
    setUploadFiles(previous => {
      const merged = [...previous];
      for (const file of chosen) {
        if (!merged.some(old => old.name === file.name && old.size === file.size && old.lastModified === file.lastModified)) merged.push(file);
      }
      return merged.slice(0, 10);
    });
    event.target.value = "";
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

  // Called only after the user confirms deletion inside the History drawer.
  async function deleteArchivedResume(archiveId: string): Promise<void> {
    const result = await api.deleteArchive(archiveId);
    if (!result.deleted) throw new Error("The resume record could not be deleted.");
    await load();
    if (!result.originalFileDeleted) {
      setError(result.warning || "The History entry was deleted, but an encrypted original still needs local cleanup.");
      setSuccess("Resume history deleted; original-file cleanup needs attention.");
    } else {
      setSuccess("Saved resume and its linked records were deleted.");
    }
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
        reviewCandidate(response, uploadFiles[0], "analyze");
        setSuccess("Resume analyzed successfully.");
      }
      await load();
    } catch (err) { setError(err instanceof Error ? err.message : "Analysis failed."); }
    finally { setBusy(false); }
  }



  const archivePairs = new Set(archives.map(a => `${a.candidateName.trim().toLowerCase()}|${a.fileName.trim().toLowerCase()}`));
  const knownPairs = new Set([
    ...profiles.map(p => `${p.candidateName.trim().toLowerCase()}|${p.sourceFileName.trim().toLowerCase()}`),
    ...analyses.map(a => `${a.candidateName.trim().toLowerCase()}|${a.fileName.trim().toLowerCase()}`)
  ]);
  const candidateCount = archives.length + [...knownPairs].filter(key => !archivePairs.has(key)).length;
  const latestEvaluations = Array.from(analyses.reduce((map, item) => {
    const key = `${item.candidateName.trim().toLowerCase()}|${item.fileName.trim().toLowerCase()}`;
    const previous = map.get(key);
    if (!previous || item.createdAt > previous.createdAt) map.set(key, item);
    return map;
  }, new Map<string, Analysis>()).values()).sort((a,b) => b.createdAt.localeCompare(a.createdAt)).slice(0,6);

  const goToStudio = (id: string) => {
    setParserSeed({ jdText: "", savedProfileId: id, id: Date.now() });
    navigate("parser");
  };
  const openAnalysis = (id: string) => {
    const found = analyses.find(a => a.id === id);
    if (found) {
      setReview({ result: found, original: null, back: section, jdText: "" });
      navigate("review");
    }
  };
  const reviewCandidate = (value: Analysis, file: File | null, back: Section, profileId?: string) => {
    // Preserve the current resume/role when returning to the editor from the review page.
    if (back === "parser") setParserSeed({ file: file ?? undefined, jdText, savedProfileId: profileId, id: Date.now() });
    setReview({ result: value, original: file, back, jdText, profileId });
    navigate("review");
  };
  const buildFromReview = (file: File | null, _sourceJd: string, savedProfileId?: string) => {
    if (!file && !savedProfileId) { setError("Original file is unavailable. Upload the resume again to open the parser."); return; }
    setParserSeed({ file: file ?? undefined, jdText: "", savedProfileId, id: Date.now() });
    navigate("parser");
  };

  // Explicitly requested, never called as part of ATS scoring or standalone parsing.
  const alignFromReview = async (file: File, sourceJd: string) => {
    if (sourceJd.trim().length < 30) throw new Error("The original JD is unavailable for this record. Run ATS Check again with a JD to align it.");
    const response = await api.alignResume(file, sourceJd);
    setParserSeed({ file, jdText: sourceJd, savedProfileId: response.profile.id, id: Date.now() });
    navigate("parser");
    setSuccess("JD-prioritized draft ready for review. Verified experience years were preserved; ATS scoring still uses the original source.");
    void load();
  };

  return <div className="app-shell waah-shell">
    <div className="ambient ambient-one"/><div className="ambient ambient-two"/>
    <div className="main-wrap">
      <header className="waah-header">
        <div className="waah-header-inner">
          <button type="button" className="waah-brand" onClick={() => navigate("dashboard")} aria-label="Waah रे Resume home">
            <span className="waah-brand-symbol"><Sparkles size={25}/></span>
            <span className="waah-brand-text">Waah <em>रे</em> Resume<small>HIRING, MADE HUMAN.</small></span>
          </button>
          <nav className={`waah-nav ${navOpen ? "waah-nav-open" : ""}`} aria-label="Main navigation">
            {menu.map(item => <button type="button" key={item.id}
              className={`waah-nav-link ${section === item.id ? "active" : ""}`}
              onClick={() => { if (item.id === "parser") setParserSeed(null); navigate(item.id); }} aria-current={section === item.id ? "page" : undefined}>
              <item.icon size={17}/>{item.label}
            </button>)}
          </nav>
          <div className="waah-header-right">
            <button className="waah-mobile-menu" type="button" onClick={() => setNavOpen(!navOpen)}
              aria-label={navOpen ? "Close navigation" : "Open navigation"} aria-expanded={navOpen}>
              {navOpen ? <X size={22}/> : <Menu size={22}/>}
            </button>
          </div>
        </div>
      </header>
      <main className="content">
        {error && <div role="alert" className="alert error-alert"><CircleHelp size={18}/><span>{error}</span><button onClick={() => setError("")} aria-label="Dismiss error"><X size={17}/></button></div>}
        {success && <div role="status" className="alert success-alert"><CheckCircle2 size={18}/><span>{success}</span><button onClick={() => setSuccess("")} aria-label="Dismiss notification"><X size={17}/></button></div>}
        {section === "dashboard" && <>
          <section className="hero waah-hero">
            <div className="hero-copy"><div className="hero-badge"><span className="tiny-dot"/> THE NEW WAY TO HIRE</div>
              <h1>Find the right fit.<br/><span>Feel the waah.</span></h1>
              <p>Beautifully simple resume analysis, thoughtful candidate comparisons, and professional profiles—without the busywork.</p>
              <div className="hero-actions"><button className="button primary" onClick={() => navigate("analyze")}><FileSearch size={18}/> Check ATS score <ArrowRight size={17}/></button>
                <button className="button secondary" onClick={() => {setParserSeed(null); navigate("parser");}}><FileText size={18}/> Parse a resume</button>
                <button className="button secondary" onClick={() => navigate("bulk")}><Users size={18}/> Rank candidates</button></div>
            </div>
            <div className="hero-art waah-hero-art" aria-hidden="true">
              <div className="waah-paper waah-paper-back"/>
              <div className="waah-paper waah-paper-front"><div className="waah-paper-top"><span className="waah-paper-avatar"><Users size={25}/></span><span>Candidate profile</span></div>
                <i/><i/><i className="short"/><div className="waah-paper-score"><Sparkles size={19}/> A clearer picture</div></div>
              <div className="art-chip chip-top"><CheckCircle2 size={16}/> Evidence-first</div>
              <div className="art-chip chip-bottom"><Activity size={16}/> Human-approved</div>
            </div>
          </section>
          <div className="section-heading"><div><div className="eyebrow">YOUR SPACE</div><h2>Everything, in one glance</h2></div><span className="subtle">Based on your saved records</span></div>
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
            <section className="panel"><div className="panel-heading"><div><h3>Recently reviewed</h3><p>Your newest candidate insights</p></div>
              <button className="text-action" onClick={() => navigate("library")}>Open history <ArrowUpRight size={16}/></button></div>
              {latestEvaluations.length ? <div className="records">{latestEvaluations.map(a => <button className="record-row rf-record-action" key={a.id} type="button" onClick={() => openAnalysis(a.id)} title={`View ${a.candidateName} analysis`}>
                <div className="candidate-avatar">{a.candidateName.slice(0, 2).toUpperCase()}</div><div className="record-main"><strong>{a.candidateName}</strong><span>{a.jobTitle}</span></div>
                <span className="date-text">{formatDate(a.createdAt)}</span><span className="mini-score">{a.score}/100</span></button>)}</div>
                : <Empty title="No analyses yet" text="Run your first analysis to populate this activity feed."/>}
            </section>
            <section className="panel next-panel"><div className="panel-heading"><div><h3>What would you like to do?</h3><p>Start with one click</p></div></div>
              <button className="quick-action" onClick={() => navigate("jobs")}><div className="quick-icon"><FilePlus2 size={20}/></div><div><strong>Create job description</strong><span>Save or reuse an opening</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => navigate("analyze")}><div className="quick-icon"><FileSearch size={20}/></div><div><strong>Check ATS score</strong><span>Upload a resume and required JD</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => navigate("bulk")}><div className="quick-icon"><ListFilter size={20}/></div><div><strong>Rank multiple candidates</strong><span>Compare up to 10 resumes</span></div><ArrowUpRight size={17}/></button>
              <button className="quick-action" onClick={() => {setParserSeed(null);navigate("parser");}}><div className="quick-icon"><FileText size={20}/></div><div><strong>Resume Parser</strong><span>No job description needed</span></div><ArrowUpRight size={17}/></button>
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
              <div className="job-actions"><button onClick={() => { setJdText(job.description); setSelectedJob(job.id); navigate("analyze"); }}>Check ATS <ArrowRight size={15}/></button>
                <button className="danger-icon" onClick={() => void deleteJob(job.id)} aria-label={`Delete ${job.title}`} title="Delete job"><Trash2 size={16}/></button></div>
            </div>)}</div> : <Empty title="No saved jobs" text="Create your first job description using the form."/>}
          </section></div>
        </>}

        {(section === "analyze" || section === "bulk") && <>
          <div className="page-head"><div><div className="eyebrow">{section === "bulk" ? "COMPARE CANDIDATES" : "CHECK YOUR MATCH"}</div>
            <h1>{section === "bulk" ? "Candidate rankings" : "ATS score checker"}</h1>
            <p>{section === "bulk" ? "Compare up to 10 resumes using the same required job description." : "Upload a resume and a required JD. Review the score first; align the resume only if you choose."}</p></div>
            <span className="page-count">Original-resume evidence</span></div>
          <div className="workflow-grid">
            <section className="panel upload-panel"><div className="step-label">STEP 01 / JOB REQUIREMENTS</div><h3>Choose job description</h3>
              <label htmlFor="saved-jd">Use a saved JD, or paste one below</label><div className="select-wrap"><select id="saved-jd" value={selectedJob} onChange={e => chooseJob(e.target.value)}>
                <option value="">Enter a new JD below</option>{jobs.map(j => <option value={j.id} key={j.id}>{j.title}</option>)}
              </select><ChevronDown size={18}/></div>
              <label htmlFor="analysis-jd">Job description <strong className="required-star">Required for scoring</strong></label>
              <JDFileUpload onText={text => { setJdText(text); setSelectedJob(""); setError(""); setResult(null); setBatch(null); }} onError={setError}/>
              <textarea id="analysis-jd" rows={10} value={jdText} onChange={e => { setJdText(e.target.value); setSelectedJob(""); }} placeholder="Paste the full job description here (minimum 30 characters)." />
              <div className="step-label extra">STEP 02 / CANDIDATE DOCUMENTS</div><h3>Upload resumes</h3>
              <label className="dropzone" htmlFor="resume-upload"><UploadCloud size={31}/><strong>{uploadFiles.length ? `${uploadFiles.length} file(s) selected` : "Choose resume file(s)"}</strong>
                <span>PDF (selectable text), DOCX or TXT · 5 MB each</span><small>{section === "bulk" ? "Up to 10 files" : "One resume at a time"}</small></label>
              <input id="resume-upload" type="file" accept=".pdf,.docx,.txt" multiple={section === "bulk"} onChange={chooseFiles} className="file-input"/>
              {uploadFiles.length > 0 && <><div className="file-names">{uploadFiles.map((f, index) => <div key={`${f.name}-${index}`}><Files size={15}/>{f.name}<button type="button" aria-label={`Remove ${f.name}`} onClick={() => setUploadFiles(prev=>prev.filter((_,i)=>i!==index))}><X size={13}/></button></div>)}</div><button type="button" className="button secondary" onClick={()=>document.getElementById("resume-upload")?.click()}><Plus size={15}/> Add more resumes</button></>}
              <button className="button primary full-width" disabled={busy || jdText.trim().length < 30 || uploadFiles.length === 0}
                onClick={() => void runAnalysis(section === "bulk")}>
                {busy ? <><RefreshCw size={17} className="spin"/> Analyzing...</> : <><Sparkles size={18}/> {section === "bulk" ? "Check & rank" : "Check ATS Score"}<ArrowRight size={17}/></>}
              </button>
              <p className="small-note">Scores are internal estimates based on the supplied resume and job description, never an automated hiring decision.</p>
            </section>
            <section className="panel result-panel"><div className="panel-heading"><div><h3>{section === "bulk" ? "Ranking results" : "Alignment report"}</h3>
              <p>Results come from the ASP.NET Core API, not mock data</p></div></div>
              {section === "analyze" && <div className="ats-onboarding">
                <div className="ats-step"><span>01</span><div><strong>Choose a job description</strong><p>ATS comparison requires a JD. Your original document is the source of truth.</p></div></div>
                <div className="ats-step"><span>02</span><div><strong>Check the estimated ATS score</strong><p>We show your original resume alongside matched and unverified requirements.</p></div></div>
                <div className="ats-step"><span>03</span><div><strong>Align only when you want to</strong><p>Alignment may improve how verified evidence is presented. It never changes stated experience years.</p></div></div>
                <div className="ats-step"><span>04</span><div><strong>Or use Resume Parser independently</strong><p>Generate an editable Algoworks profile without any JD or score.</p></div></div>
              </div>}
              {section === "bulk" && (batch ? <>
                <div className="waah-rank-board"><div className="waah-rank-board-list">{batch.results.map((a, index) => <button type="button" className="waah-rank-row" key={a.id} onClick={() => reviewCandidate(a, uploadFiles.find(f => f.name === a.fileName) ?? null, "bulk")} title={`Review ${a.candidateName} against the original resume`}><span className="waah-rank-order">#{index + 1}</span>
                  <span className="waah-rank-person"><strong>{a.candidateName}</strong><small>{a.fileName} · {a.matchedSkills.length} matched · {a.missingSkills.length} missing</small></span><strong className="waah-rank-score">{a.score}<small>/100</small></strong><span className="waah-rank-view">View details <ArrowRight size={16}/></span></button>)}</div></div>
                {batch.failures.length > 0 && <div className="failure-list"><h4>Unable to process</h4>{batch.failures.map(f => <p key={f.fileName}><strong>{f.fileName}:</strong> {f.error}</p>)}</div>}
                <p className="waah-rank-availability">Select a candidate to inspect the original resume and see why their score is lower. All scores are based on uploaded source evidence.</p>
              </> : <Empty title="Ranking results will appear here" text="Select the same JD and multiple candidate resumes to rank them consistently."/>)}
            </section>
          </div>
        </>}

        {section === "review" && review && <CandidateReview key={review.result.id} result={review.result} original={review.original} archives={archives} profiles={profiles} jdText={review.jdText} profileId={review.profileId}
          onBack={() => navigate(review.back)} onBuildProfile={buildFromReview} onAlignResume={alignFromReview}/>}

        {section === "parser" && <ResumeParserPage key={parserSeed?.id ?? 0} seed={parserSeed} onSaved={() => void load()}/>}

        {section === "library" && <TalentLibrary archives={archives} profiles={profiles} analyses={analyses}
          onOpenProfile={goToStudio} onDeleteArchive={deleteArchivedResume}/>}
        <footer className="footer"><span>Waah रे Resume · Private local preview</span><span><Database size={15}/> Human review always comes first</span></footer>
      </main>
    </div>
  </div>;
}
