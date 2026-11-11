import ScoreRecommendations from "./ScoreRecommendations";
import { useEffect, useMemo, useState } from "react";
import { ArrowRight, ArrowUpRight, CheckCircle2, ChevronRight, FileDown, FileText, FolderOpen, Search, ShieldCheck, Trash2, X } from "lucide-react";
import { api, type Analysis, type ArchiveEntry, type ResumeProfileSummary } from "../api";

type Props = {
  archives: ArchiveEntry[];
  profiles: ResumeProfileSummary[];
  analyses: Analysis[];
  onOpenProfile: (id: string) => void;
  onDeleteArchive: (archiveId: string) => Promise<void>;
  highlightedAnalysisId?: string | null;
};

type SavedItem = {
  id: string;
  name: string;
  designation: string;
  fileName: string;
  updatedAt: string;
  profileId: string | null;
  archiveId: string | null;
  evaluations: Analysis[];
  source: "archived" | "legacy";
};

const pair = (name: string, file: string) => `${name.trim().toLowerCase()}|${file.trim().toLowerCase()}`;
const date = (value: string) => new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });

export default function TalentLibrary({ archives, profiles, analyses, onOpenProfile, onDeleteArchive, highlightedAnalysisId }: Props) {
  const [search, setSearch] = useState("");
  const [openedId, setOpenedId] = useState<string | null>(null);
  const [selectedAnalysis, setSelectedAnalysis] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState("");

  const records = useMemo(() => {
    const usedProfileIds = new Set<string>();
    const usedAnalysisIds = new Set<string>();
    const result: SavedItem[] = [];

    for (const archive of archives) {
      const linked = analyses.filter(a => archive.analysisIds.includes(a.id)).sort((a,b) => b.createdAt.localeCompare(a.createdAt));
      if (archive.profileId) usedProfileIds.add(archive.profileId);
      for (const analysis of linked) usedAnalysisIds.add(analysis.id);
      result.push({
        id: `archive:${archive.id}`, name: archive.candidateName || "Unknown candidate",
        designation: archive.designation || "Candidate resume", fileName: archive.fileName,
        updatedAt: archive.updatedAt, profileId: archive.profileId, archiveId: archive.id,
        evaluations: linked, source: "archived"
      });
    }

    // Legacy records are grouped only for the visual index. They are not merged in backend storage.
    // Identity is not inferred from names alone; matching also requires the exact uploaded filename.
    const legacy = new Map<string, SavedItem>();
    for (const profile of profiles) {
      if (usedProfileIds.has(profile.id)) continue;
      const key = pair(profile.candidateName, profile.sourceFileName);
      const previous = legacy.get(key);
      if (!previous || profile.updatedAt > previous.updatedAt) {
        legacy.set(key, { id: `legacy:${key}`, name: profile.candidateName || "Unknown candidate",
          designation: profile.designation || "Candidate resume", fileName: profile.sourceFileName,
          updatedAt: profile.updatedAt, profileId: profile.id, archiveId: null,
          evaluations: previous?.evaluations ?? [], source: "legacy" });
      }
    }
    for (const analysis of analyses) {
      if (usedAnalysisIds.has(analysis.id)) continue;
      const key = pair(analysis.candidateName, analysis.fileName);
      const previous = legacy.get(key);
      if (previous) {
        previous.evaluations.push(analysis);
        if (analysis.createdAt > previous.updatedAt) previous.updatedAt = analysis.createdAt;
      } else {
        legacy.set(key, { id: `legacy:${key}`, name: analysis.candidateName || "Unknown candidate",
          designation: "Analysis record", fileName: analysis.fileName, updatedAt: analysis.createdAt,
          profileId: null, archiveId: null, evaluations: [analysis], source: "legacy" });
      }
    }
    return [...result, ...legacy.values()].map(row => ({
      ...row, evaluations: [...row.evaluations].sort((a,b) => b.createdAt.localeCompare(a.createdAt))
    })).sort((a,b) => b.updatedAt.localeCompare(a.updatedAt));
  }, [archives, profiles, analyses]);

  const active = records.find(row => row.id === openedId) ?? null;
  const evaluation = active?.evaluations.find(a => a.id === selectedAnalysis) ?? active?.evaluations[0] ?? null;
  const filtered = records.filter(row => `${row.name} ${row.designation} ${row.fileName} ${row.evaluations.map(a => a.jobTitle).join(" ")}`.toLowerCase().includes(search.toLowerCase()));

  useEffect(() => {
    if (!highlightedAnalysisId) return;
    const row = records.find(x => x.evaluations.some(a => a.id === highlightedAnalysisId));
    if (row) { setOpenedId(row.id); setSelectedAnalysis(highlightedAnalysisId); setShowAll(false); }
  }, [highlightedAnalysisId, records]);

  useEffect(() => {
    if (!active) return;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === "Escape") setOpenedId(null); };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [active]);

  function open(row: SavedItem) {
    setOpenedId(row.id); setSelectedAnalysis(row.evaluations[0]?.id ?? null); setShowAll(false);
  }

  async function removeSelected() {
    if (!active?.archiveId || deleting) return;
    const choice = window.confirm(`Delete ${active.name}'s saved resume, linked profile and linked ATS results? This also deletes its encrypted original from local storage. This cannot be undone. Make a private backup first.`);
    if (!choice) return;
    setDeleteError("");
    setDeleting(true);
    try {
      await onDeleteArchive(active.archiveId);
      setOpenedId(null);
      setSelectedAnalysis(null);
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : "Unable to delete this record.");
    } finally { setDeleting(false); }
  }

  return <div className="waah-library">
    <div className="waah-section-title">
      <div><span className="eyebrow">ONE PLACE FOR EVERY RESUME</span><h1>Your history, beautifully organized<span className="waah-period">.</span></h1>
        <p>Each saved resume has one record. Open it to see the original file, editable profile and previous evaluations.</p></div>
      <div className="waah-counter"><strong>{records.length}</strong><span>resume records</span></div>
    </div>
    <section className="waah-library-panel">
      <div className="waah-library-head">
        <div><h2>Saved resumes</h2><p>Search once. Everything related stays together.</p></div>
        <label className="waah-search"><Search size={18}/><input aria-label="Search saved resumes" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search name, role or job" /></label>
      </div>
      {filtered.length ? <div className="waah-history-list">{filtered.map(row => {
        const recent = row.evaluations[0];
        return <button className="waah-history-row" key={row.id} type="button" onClick={() => open(row)}>
          <span className="waah-file-icon"><FileText size={23}/></span>
          <span className="waah-history-name"><strong>{row.name}</strong><small>{row.designation}</small><em>{row.fileName}</em></span>
          <span className="waah-history-meta"><span>{row.evaluations.length} {row.evaluations.length === 1 ? "evaluation" : "evaluations"}</span><small>{date(row.updatedAt)}</small></span>
          {recent ? <span className="waah-score">{recent.score}<small>/100</small></span> : <span className="waah-new-tag">Profile</span>}
          <ChevronRight size={20} className="waah-chevron" />
        </button>;
      })}</div> : <div className="waah-library-empty"><FolderOpen size={38}/><h3>No resumes found</h3><p>{search ? "Try another name or job title." : "Your first uploaded resume will appear here."}</p></div>}
    </section>
    <p className="waah-privacy"><ShieldCheck size={16}/> New uploads are archived in encrypted local development storage. Earlier records may have only their structured profile. Production file access will require user authentication.</p>

    {active && <div className="waah-dialog-backdrop" onClick={() => setOpenedId(null)} role="presentation">
      <section className="waah-detail-drawer" role="dialog" aria-modal="true" aria-label={`${active.name} saved resume`} onClick={e => e.stopPropagation()}>
        <div className="waah-detail-top"><span>RESUME RECORD</span><button type="button" onClick={() => setOpenedId(null)} aria-label="Close record"><X size={21}/></button></div>
        <div className="waah-detail-identity"><span className="waah-detail-monogram">{active.name.split(" ").map(x=>x.charAt(0)).join("").slice(0,2)}</span><h2>{active.name}</h2><p>{active.designation}</p><small>{active.fileName}</small></div>
        <div className="waah-detail-actions">
          {active.profileId && <button type="button" className="button primary" onClick={() => onOpenProfile(active.profileId!)}>Open editable profile <ArrowRight size={17}/></button>}
          {active.archiveId && <a href={api.originalDownloadUrl(active.archiveId)} className="button secondary" download><FileDown size={16}/> Download original</a>}
          {!active.archiveId && <span className="waah-legacy-message">Original file was not archived with this earlier record.</span>}
          {active.archiveId && <button type="button" className="button secondary" style={{color:"#b42318"}} disabled={deleting} onClick={() => void removeSelected()}><Trash2 size={16}/>{deleting ? "Deleting..." : "Delete record"}</button>}
        </div>
        {deleteError && <p role="alert" style={{color:"#b42318", padding:"8px 0"}}>{deleteError}</p>}
        <div className="waah-detail-evaluations"><h3>JD evaluations <span>{active.evaluations.length}</span></h3>
          {active.evaluations.length ? (showAll ? active.evaluations : active.evaluations.slice(0,4)).map(a =>
            <button type="button" className={`waah-evaluation ${evaluation?.id === a.id ? "active" : ""}`} key={a.id} onClick={() => setSelectedAnalysis(a.id)}>
              <div><strong>{a.jobTitle}</strong><small>{date(a.createdAt)}</small></div><span>{a.score}/100</span>
            </button>) : <p className="waah-empty-eval">No job alignment has been calculated for this resume yet.</p>}
          {!showAll && active.evaluations.length>4 && <button type="button" className="waah-show-all" onClick={() => setShowAll(true)}>Show all {active.evaluations.length} evaluations <ArrowUpRight size={15}/></button>}
        </div>
        {evaluation && <div className="waah-evaluation-detail"><div className="waah-evaluation-title"><span>Estimated ATS alignment</span><strong>{evaluation.score}<small>/100</small></strong></div>
          {evaluation.breakdown.map(part => <div className="waah-eval-part" key={part.category}><div><span>{part.category}</span><span>{part.applicable ? `${part.awarded}/${part.weight}` : "N/A"}</span></div><i><b style={{width:`${part.applicable && part.weight ? Math.min(100,100*part.awarded/part.weight) : 0}%`}}/></i></div>)}
          <p><CheckCircle2 size={15}/> <strong>Matched:</strong> {evaluation.matchedSkills.join(" · ") || "None confirmed"}</p>
          <p><strong>Not evidenced:</strong> {evaluation.missingSkills.join(" · ") || "None in current comparison"}</p>
          <ScoreRecommendations result={evaluation} />
        </div>}
      </section>
    </div>}
  </div>;
}
