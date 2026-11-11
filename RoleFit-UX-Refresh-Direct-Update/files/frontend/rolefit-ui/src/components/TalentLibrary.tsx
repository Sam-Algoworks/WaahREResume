import { useEffect, useMemo, useState } from "react";
import { ArrowRight, BriefcaseBusiness, CheckCircle2, ChevronRight, FileText, FolderOpen, Search, ShieldCheck, UserRound, X } from "lucide-react";
import type { Analysis, ResumeProfileSummary } from "../api";

type Tab = "people" | "evaluations";
type Props = {
  profiles: ResumeProfileSummary[];
  analyses: Analysis[];
  onOpenProfile: (id: string) => void;
  highlightedAnalysisId?: string | null;
};
const dateLabel = (iso: string) => new Date(iso).toLocaleString(undefined, { month: "short", day: "numeric", year: "numeric" });
const pairKey = (name: string, file: string) => `${name.trim().toLowerCase()}|${file.trim().toLowerCase()}`;

export default function TalentLibrary({ profiles, analyses, onOpenProfile, highlightedAnalysisId }: Props) {
  const [tab, setTab] = useState<Tab>("people");
  const [query, setQuery] = useState("");
  const [active, setActive] = useState<Analysis | null>(null);
  const [activeProfile, setActiveProfile] = useState<ResumeProfileSummary | null>(null);

  useEffect(() => {
    if (!highlightedAnalysisId) return;
    const record = analyses.find(a => a.id === highlightedAnalysisId);
    if (record) { setTab("evaluations"); setActive(record); setActiveProfile(null); }
  }, [highlightedAnalysisId, analyses]);

  useEffect(() => {
    if (!active && !activeProfile) return;
    const onEscape = (e: KeyboardEvent) => { if (e.key === "Escape") { setActive(null); setActiveProfile(null); } };
    document.addEventListener("keydown", onEscape);
    return () => document.removeEventListener("keydown", onEscape);
  }, [active, activeProfile]);

  // An analysis has no immutable candidate ID yet. Keep individual saved profiles
  // separate and show analyses without a matching saved profile as provisional rows.
  const people = useMemo(() => {
    const stored = profiles.map(p => ({
      id: `profile:${p.id}`, name: p.candidateName || "Unknown candidate", subtitle: p.designation || "Profile draft",
      file: p.sourceFileName, date: p.updatedAt, profile: p,
      evaluations: analyses.filter(a => pairKey(a.candidateName, a.fileName) === pairKey(p.candidateName, p.sourceFileName))
    }));
    const profilePairs = new Set(stored.map(p => pairKey(p.name, p.file)));
    // Analysis-only records use their analysis ID. Do not merge unrelated people by name.
    const groupedAnalyses = new Map<string, Analysis[]>();
    for (const analysis of analyses) {
      const key = pairKey(analysis.candidateName, analysis.fileName);
      if (profilePairs.has(key)) continue;
      groupedAnalyses.set(key, [...(groupedAnalyses.get(key) ?? []), analysis]);
    }
    const analysisOnly = [...groupedAnalyses.values()].map(group => {
      const newest = [...group].sort((a,b) => b.createdAt.localeCompare(a.createdAt))[0];
      return {
        id: `analysis:${newest.id}`, name: newest.candidateName || "Unknown candidate",
        subtitle: "Analysis record · profile not yet saved", file: newest.fileName,
        date: newest.createdAt, profile: null as ResumeProfileSummary | null, evaluations: group
      };
    });
    return [...stored, ...analysisOnly].sort((a,b) => b.date.localeCompare(a.date));
  }, [profiles, analyses]);

  const matchingPeople = people.filter(p => `${p.name} ${p.subtitle} ${p.file}`.toLowerCase().includes(query.toLowerCase()));
  const matchingAnalyses = analyses.filter(a => `${a.candidateName} ${a.jobTitle} ${a.fileName}`.toLowerCase().includes(query.toLowerCase()));
  const relatedProfile = active ? profiles.find(p => pairKey(p.candidateName, p.sourceFileName) === pairKey(active.candidateName, active.fileName)) : null;

  const openPerson = (person: (typeof people)[number]) => {
    if (person.profile) { setActiveProfile(person.profile); setActive(null); }
    else if (person.evaluations[0]) { setActive(person.evaluations[0]); setActiveProfile(null); }
  };

  return <div className="rf-library">
    <div className="rf-editorial-head">
      <div><span className="eyebrow">YOUR RECRUITMENT RECORDS</span>
        <h1>Talent Library<span className="rf-accent-dot">.</span></h1>
        <p>One home for candidate profiles and their JD evaluations. Every record opens for review.</p></div>
      <div className="rf-library-count"><UserRound size={22}/><strong>{people.length}</strong><span>records</span></div>
    </div>
    <section className="rf-library-surface">
      <div className="rf-library-toolbar">
        <div className="rf-pill-tabs" role="tablist" aria-label="Talent Library view">
          <button type="button" role="tab" aria-selected={tab === "people"} className={tab === "people" ? "active" : ""} onClick={() => { setTab("people"); setQuery(""); }}>Candidates <span>{people.length}</span></button>
          <button type="button" role="tab" aria-selected={tab === "evaluations"} className={tab === "evaluations" ? "active" : ""} onClick={() => { setTab("evaluations"); setQuery(""); }}>Evaluations <span>{analyses.length}</span></button>
        </div>
        <label className="rf-library-search"><Search size={17}/><input value={query} onChange={e => setQuery(e.target.value)} aria-label="Search saved records" placeholder="Search name, job or file"/></label>
      </div>
      {tab === "people" ?
        <div className="rf-record-grid">
          {matchingPeople.length ? matchingPeople.map(p => <button type="button" className="rf-person" key={p.id} onClick={() => openPerson(p)}>
            <span className="rf-person-monogram">{p.name.split(" ").map(v => v[0]).join("").slice(0,2).toUpperCase() || "?"}</span>
            <span className="rf-person-main"><strong>{p.name}</strong><small>{p.subtitle}</small><em>{p.file}</em></span>
            <span className="rf-person-meta"><small>{dateLabel(p.date)}</small><span>{p.profile ? "Saved profile" : `${p.evaluations.length} evaluation`}</span></span>
            <ChevronRight size={19} className="rf-open-chevron"/>
          </button>) : <div className="rf-library-empty"><FolderOpen size={28}/><strong>No matching candidates</strong><p>Parse a resume or analyze an applicant to create a record.</p></div>}
        </div> :
        <div className="rf-record-grid">
          {matchingAnalyses.length ? matchingAnalyses.map(a => <button type="button" className="rf-person" key={a.id} onClick={() => {setActive(a); setActiveProfile(null);}}>
            <span className="rf-person-monogram rf-eval-icon"><FileText size={20}/></span>
            <span className="rf-person-main"><strong>{a.candidateName}</strong><small>{a.jobTitle}</small><em>{a.fileName}</em></span>
            <span className="rf-person-meta"><strong className="rf-library-score">{a.score}<small>/100</small></strong><small>{dateLabel(a.createdAt)}</small></span>
            <ChevronRight size={19} className="rf-open-chevron"/>
          </button>) : <div className="rf-library-empty"><FileText size={28}/><strong>No evaluations found</strong><p>Run an analysis to save a reproducible score and breakdown.</p></div>}
        </div>}
    </section>
    <div className="rf-library-explainer"><ShieldCheck size={16}/><span>Local development storage: parsed profiles and analysis results are saved on the backend. The original uploaded file is not archived, and candidate identities are not automatically merged by name alone.</span></div>

    {(active || activeProfile) && <div className="rf-detail-backdrop" role="presentation" onClick={() => {setActive(null);setActiveProfile(null);}}>
      <div role="dialog" aria-modal="true" aria-label={active ? "Analysis details" : "Candidate details"} className="rf-detail-panel" onClick={e=>e.stopPropagation()}>
        <div className="rf-detail-bar"><span>RECORD DETAILS</span><button onClick={() => {setActive(null);setActiveProfile(null);}} aria-label="Close details"><X size={20}/></button></div>
        {activeProfile ? <>
          <div className="rf-detail-hero"><div className="rf-detail-avatar"><UserRound size={30}/></div><h2>{activeProfile.candidateName}</h2><p>{activeProfile.designation}</p></div>
          <div className="rf-detail-facts"><div><span>Projects</span><strong>{activeProfile.projectCount}</strong></div><div><span>Experience</span><strong>{activeProfile.experienceYears ?? "Not stated"}</strong></div><div><span>Updated</span><strong>{dateLabel(activeProfile.updatedAt)}</strong></div></div>
          <div className="rf-detail-text"><strong>Source document</strong><p>{activeProfile.sourceFileName}</p><small>{activeProfile.status}</small></div>
          <button className="button primary full-width" onClick={() => onOpenProfile(activeProfile.id)}>Open editable profile <ArrowRight size={17}/></button>
          <p className="small-note">The saved structured profile can be reopened and edited. Original binary resume retrieval requires protected document storage, which is not implemented yet.</p>
        </> : active ? <>
          <div className="rf-detail-hero"><span className="eyebrow">ESTIMATED JD ALIGNMENT</span><h2>{active.candidateName}</h2><p>{active.jobTitle}</p><div className="rf-large-score">{active.score}<span>/100</span></div></div>
          <div className="rf-detail-text"><strong>Score breakdown</strong>{active.breakdown.map(part => <div className="rf-score-line" key={part.category}><div><span>{part.category}</span><span>{part.applicable ? `${part.awarded}/${part.weight}` : "N/A"}</span></div><i><b style={{width: `${part.applicable && part.weight ? Math.min(100,100*part.awarded/part.weight) : 0}%`}}/></i></div>)}</div>
          <div className="rf-detail-text"><strong><CheckCircle2 size={16}/> Matched requirements</strong><p>{active.matchedSkills.join(" · ") || "No exact match evidenced"}</p></div>
          <div className="rf-detail-text"><strong>Missing / not evidenced</strong><p>{active.missingSkills.join(" · ") || "None from recognized requirements"}</p></div>
          <div className="rf-detail-text"><strong><BriefcaseBusiness size={16}/> Source</strong><p>{active.fileName} · {dateLabel(active.createdAt)}</p><small>{active.notes}</small></div>
          {relatedProfile && <button className="button primary full-width" onClick={() => onOpenProfile(relatedProfile.id)}>Open saved profile <ArrowRight size={17}/></button>}
          <p className="small-note">Scores reflect the stored evaluation, not a hiring recommendation. This record remains unchanged when a resume is edited later.</p>
        </> : null}
      </div>
    </div>}
  </div>;
}
