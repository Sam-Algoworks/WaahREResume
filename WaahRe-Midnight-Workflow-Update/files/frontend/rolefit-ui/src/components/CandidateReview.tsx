import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, ArrowRight, CheckCircle2, Download, FileText, Info, LoaderCircle, ShieldCheck, Sparkles } from "lucide-react";
import { api, type Analysis, type ArchiveEntry, type ResumeProfile, type ResumeProfileSummary } from "../api";
import ScoreRecommendations from "./ScoreRecommendations";

type Props = {
  result: Analysis;
  original: File | null;
  archives: ArchiveEntry[];
  profiles: ResumeProfileSummary[];
  onBack: () => void;
  onBuildProfile: (source: File | null, jdText: string, savedProfileId?: string) => void;
  onAlignResume: (file: File, jdText: string) => Promise<void>;
  jdText: string;
  profileId?: string;
};

const isPdf = (name: string) => name.toLowerCase().endsWith(".pdf");
const isTxt = (name: string) => name.toLowerCase().endsWith(".txt");

export default function CandidateReview({ result, original, archives, profiles, onBack, onBuildProfile, onAlignResume, jdText, profileId }: Props) {
  const [source, setSource] = useState<File | null>(original);
  const [sourceUrl, setSourceUrl] = useState<string | null>(null);
  const [sourceText, setSourceText] = useState<string>("");
  const [sourceState, setSourceState] = useState<"loading" | "ready" | "missing">(original ? "ready" : "loading");
  const [tab, setTab] = useState<"original" | "parsed">("original");
  const [savedProfile, setSavedProfile] = useState<ResumeProfile | null>(null);
  const [profileState, setProfileState] = useState<"loading" | "ready" | "missing">("loading");
  const [alignBusy, setAlignBusy] = useState(false);
  const [alignError, setAlignError] = useState("");

  const archive = useMemo(() => archives.find(a => a.analysisIds.includes(result.id) && a.hasOriginal), [archives, result.id]);
  const matchingProfileId = profileId ?? archive?.profileId ?? profiles.find(p =>
    p.sourceFileName.toLowerCase() === result.fileName.toLowerCase() &&
    p.candidateName.toLowerCase() === result.candidateName.toLowerCase())?.id;
  const deductions = result.breakdown.filter(p => p.applicable && p.awarded < p.weight);
  const hasGaps = result.score < 100 && (result.missingSkills.length > 0 || deductions.length > 0);

  useEffect(() => {
    let cancelled = false;
    setSource(original);
    setSourceState(original ? "ready" : "loading");
    if (original) return () => { cancelled = true; };
    if (!archive) { setSourceState("missing"); return; }
    void fetch(api.originalDownloadUrl(archive.id))
      .then(async response => {
        if (!response.ok) throw new Error("Archived original could not be loaded");
        const blob = await response.blob();
        if (!cancelled) { setSource(new File([blob], archive.fileName, { type: blob.type })); setSourceState("ready"); }
      })
      .catch(() => { if (!cancelled) setSourceState("missing"); });
    return () => { cancelled = true; };
  }, [result.id, original, archive]);

  useEffect(() => {
    if (!source) { setSourceUrl(null); setSourceText(""); return; }
    const url = URL.createObjectURL(source);
    let active = true;
    setSourceUrl(url);
    setSourceText("");
    if (isTxt(source.name)) {
      void source.text().then(text => { if (active) setSourceText(text); }).catch(() => { if (active) setSourceText("Text preview unavailable."); });
    } else if (!isPdf(source.name)) {
      void api.readDocument(source).then(v => { if (active) setSourceText(v.text); })
        .catch(() => { if (active) setSourceText("Word text preview unavailable."); });
    }
    return () => { active = false; URL.revokeObjectURL(url); };
  }, [source]);

  useEffect(() => {
    let active = true;
    setSavedProfile(null);
    setProfileState(matchingProfileId ? "loading" : "missing");
    if (!matchingProfileId) return;
    void api.savedProfile(matchingProfileId)
      .then(v => { if (active) { setSavedProfile(v); setProfileState("ready"); } })
      .catch(() => { if (active) setProfileState("missing"); });
    return () => { active = false; };
  }, [matchingProfileId]);

  return <section className="waah-review-page" aria-label={`${result.candidateName} candidate evaluation`}>
    <header className="waah-review-header">
      <div className="waah-review-heading"><button type="button" className="waah-review-back" onClick={onBack} aria-label="Back to results"><ArrowLeft size={19}/></button>
        <div><span className="eyebrow">CANDIDATE REVIEW</span><h1>{result.candidateName}</h1>
          <p>{result.fileName} · Compared against {result.jobTitle}</p></div></div>
      <span className="waah-review-footnote"><ShieldCheck size={17}/> Source-based alignment estimate · Human review required</span>
    </header>
    <div className="waah-review-columns">
      <section className="waah-review-document panel">
        <div className="waah-review-tabs" role="tablist" aria-label="Resume view">
          <button type="button" role="tab" aria-selected={tab === "original"} className={tab === "original" ? "active" : ""} onClick={() => setTab("original")}>Original resume</button>
          <button type="button" role="tab" aria-selected={tab === "parsed"} className={tab === "parsed" ? "active" : ""} onClick={() => setTab("parsed")}>Parsed profile</button>
        </div>
        {tab === "original" ? <>
          <div className="waah-review-filebar"><span><FileText size={18}/> {source?.name ?? result.fileName}</span>
            {sourceUrl && source && <a href={sourceUrl} download={source.name} className="waah-review-download"><Download size={16}/> Original</a>}</div>
          {sourceState === "loading" ? <div className="waah-review-empty"><LoaderCircle className="waah-review-spin" size={27}/><p>Loading saved original…</p></div> :
           !source ? <div className="waah-review-empty"><Info size={26}/><h3>Original file unavailable</h3><p>This evaluation was saved before the original-file archive was enabled, or the file cannot be retrieved. Re-upload it to compare side by side.</p></div> :
           isPdf(source.name) && sourceUrl ? <iframe className="waah-review-pdf" src={`${sourceUrl}#toolbar=1`} title={`Original resume for ${result.candidateName}`} /> :
           <div className="waah-review-text">{!isTxt(source.name) && <p className="waah-review-note">DOCX preview is extracted text, not the original Word page layout.</p>}<pre>{sourceText || "Loading extracted text…"}</pre></div>}
        </> : <div className="waah-review-structured">
          {profileState === "loading" && <div className="waah-review-empty"><LoaderCircle size={24} className="waah-review-spin"/> Loading parsed profile…</div>}
          {profileState === "missing" && <div className="waah-review-empty"><Info size={26}/><h3>No parsed profile saved for this evaluation</h3><p>The ranking engine stores the original evidence and score. Open the resume studio to build the editable professional profile.</p></div>}
          {savedProfile && <article className="waah-review-profile-summary"><h2>{savedProfile.candidateName}</h2><p>{savedProfile.designation}</p><h3>Professional summary</h3><p>{savedProfile.professionalSummary}</p><h3>Technology stack</h3><div className="waah-review-skill-list">{savedProfile.technicalSkills.map(s=><span key={s}>{s}</span>)}</div><h3>Projects ({savedProfile.projects.length})</h3>{savedProfile.projects.map((p,i)=><div className="waah-review-profile-project" key={i}><strong>{i+1}. {p.domain || p.title}</strong><p>{p.description}</p></div>)}</article>}
        </div>}
      </section>
      <section className="waah-review-score panel">
        <div className="waah-review-score-top"><div><span className="eyebrow">ESTIMATED JD ALIGNMENT</span><div className="waah-review-number">{result.score}<small>/100</small></div></div><div className="waah-review-score-summary"><strong>{result.score === 100 ? "All measured criteria met" : hasGaps ? "Some requirements need review" : "Review alignment evidence"}</strong><p>{result.score === 100 ? "No deductions in this scoring model. Recruiter verification is still required." : "The score is based on original resume evidence, not on AI-edited claims."}</p></div></div>
        <div className="waah-review-progress" role="progressbar" aria-label="Estimated JD alignment" aria-valuemin={0} aria-valuemax={100} aria-valuenow={result.score}><span style={{width:`${Math.max(0,Math.min(100,result.score))}%`}}/></div>
        <div className="waah-review-matches"><h3><CheckCircle2 size={19}/> Matched requirements ({result.matchedSkills.length})</h3><div className="waah-review-skill-list">{result.matchedSkills.map(s=><span key={s}>{s}</span>)}{!result.matchedSkills.length && <p>None confirmed by the current skill matcher.</p>}</div></div>
        {hasGaps && <div className="waah-review-gaps"><h3>Missing or weak evidence ({result.missingSkills.length + deductions.length})</h3>{result.missingSkills.length>0 && <div className="waah-review-skill-list">{result.missingSkills.map(s=><span key={s}>{s}</span>)}</div>}
          <div className="waah-review-deductions">{deductions.map(p=><div key={p.category}><span>{p.category}</span><strong>{p.awarded}/{p.weight}</strong></div>)}</div></div>}
        {hasGaps && <ScoreRecommendations result={result}/>}
        <div className="waah-review-cta">
          <div className="waah-review-action-grid">
            <button type="button" className="button primary" disabled={alignBusy || !source || jdText.trim().length < 30}
              title={jdText.trim().length < 30 ? "Original JD is unavailable. Run ATS Check with a JD to align." : "Align evidence without changing experience years"}
              onClick={() => {
                if (!source) return;
                setAlignBusy(true); setAlignError("");
                void onAlignResume(source, jdText).catch(e => setAlignError(e instanceof Error ? e.message : "Unable to align resume.")).finally(()=>setAlignBusy(false));
              }}>
              <Sparkles size={17}/>{alignBusy ? "Aligning…" : "Align to JD"}<ArrowRight size={17}/>
            </button>
            <button type="button" className="button secondary" onClick={()=>onBuildProfile(source,"",matchingProfileId)}>
              <FileText size={17}/> {matchingProfileId ? "Resume Parser" : "Parse separately"}
            </button>
          </div>
          {alignError && <p role="alert" className="waah-review-align-error">{alignError}</p>}
          {jdText.trim().length < 30 && <p className="waah-review-disclaimer">To align this saved record, rerun ATS Check with its original JD. No unverified JD is substituted automatically.</p>}
        </div>
        <p className="waah-review-disclaimer">Alignment can reorder or clarify supported skills and projects. It never changes verified experience years, candidate identity or the score based on the original. Scores are estimates, not hiring decisions.</p>
      </section>
    </div>
  </section>;
}
