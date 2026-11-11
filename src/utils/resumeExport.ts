import type { ResumeProfile } from "../api";

/**
 * Presentation-only normalization. Never invent a skill, upgrade a proficiency level,
 * or alter the stored resume or the ATS evidence used to rank candidates.
 *
 * Rules are intentionally conservative: obvious duplicates and declared aliases are
 * merged, while differently qualified or merely related competencies are retained.
 * Unrecognized skills are always shown under "Other Skills".
 */
export type ExportSkillGroup = { label: string; skills: string[] };

type SkillEntry = { text: string; base: string; key: string; qualifier: string | null };

const trimSkill = (s: string): string => s.replace(/\s+/g, " ").replace(/^[•·\-]\s*/, "").trim();

const qualificationPattern = /\s*(?:\((awareness|fundamentals|beginner|basic|intermediate|advanced|expert)\)|\b(awareness|fundamentals))\s*$/i;

function splitSkillQualification(text: string) {
  const match = qualificationPattern.exec(text);
  if (!match) return { base: text, qualifier: null as string | null };
  return {
    base: text.slice(0, match.index).trim(),
    qualifier: (match[1] || match[2]).toLowerCase()
  };
}

/** Known spelling aliases, not guesses about transferable skills. */
function conceptKey(base: string): string {
  const key = base.toLowerCase().replace(/[\u2010-\u2015]/g, "-").replace(/\s+/g, " ").trim();
  if (/^html\s*(?:\/|&|and|\+)\s*css$/.test(key)) return "html+css";
  if (/^(?:ux\s*\/\s*ui|ui\s*\/\s*ux|ui\s*&\s*ux)$/.test(key)) return "ui+ux";
  if (/^(?:js|javascript)$/.test(key)) return "javascript";
  if (/^typescript$/.test(key)) return "typescript";
  if (/^(?:react\.js|reactjs|react)$/.test(key)) return "react";
  if (/^(?:postgres|postgresql)$/.test(key)) return "postgresql";
  if (/^(?:ms excel|microsoft excel|excel)$/.test(key)) return "excel";
  if (/^(?:agile|agile collaboration)$/.test(key)) return "agile";
  if (/^rest(?:\s+apis?)?$/.test(key)) return "rest-api";
  if (/^(?:reusable components?|components?)$/.test(key)) return "components";
  return key;
}

function makeEntry(raw: string): SkillEntry | null {
  const text = trimSkill(raw ?? "");
  if (!text) return null;
  const { base, qualifier } = splitSkillQualification(text);
  return { text, base, qualifier, key: conceptKey(base) };
}

function preferDuplicate(candidate: SkillEntry, existing: SkillEntry): boolean {
  // Never silently replace a stated skill level with a conflicting level.
  if (candidate.qualifier && !existing.qualifier) return true;
  if (existing.qualifier && !candidate.qualifier) return false;
  // Prefer source-provided specifics to broad bare labels, e.g. Agile collaboration.
  return candidate.text.length > existing.text.length;
}

/** Conservative deduplication for ALL professions, with stable source ordering. */
export function normalizeSkillsForExport(skills: readonly string[]): string[] {
  const unique: SkillEntry[] = [];
  for (const raw of skills) {
    const candidate = makeEntry(raw);
    if (!candidate) continue;
    const matching = unique.findIndex(item =>
      item.key === candidate.key &&
      (item.qualifier === candidate.qualifier || !item.qualifier || !candidate.qualifier)
    );
    if (matching < 0) {
      unique.push(candidate);
    } else if (preferDuplicate(candidate, unique[matching])) {
      unique[matching] = candidate;
    }
  }

  // A source-listed combined skill is more faithful than two unqualified bare labels.
  // Preserve separately stated *qualified* expertise (e.g. HTML (advanced)).
  const combinedWeb = unique.some(item => item.key === "html+css");
  return unique
    .filter(item => !(combinedWeb && (item.key === "html" || item.key === "css") && !item.qualifier))
    .map(item => item.text);
}

type CategoryRule = { label: string; matches: RegExp };

/** High-precision categories; all other competencies remain visible. */
const skillCategories: CategoryRule[] = [
  { label: "Design Tools", matches: /^(?:figma|figjam|miro|maze|sketch|canva|invision|framer|balsamiq|axure(?: rp)?|adobe (?:xd|photoshop|illustrator|creative cloud)|penpot)$/i },
  { label: "UX Research & Methods", matches: /\b(?:user research|ux research|discovery interviews?|user interviews?|stakeholder interviews?|user journeys?|journey (?:mapping|maps)|information architecture|personas?|user flows?|task flows?|wirefram\w*|usability test\w*|a\/?b test\w*|heuristic review|prototyp\w*|interaction design|design thinking|card sorting|contextual inquiry)\b/i },
  { label: "Design Systems & Accessibility", matches: /\b(?:design tokens?|variants?|auto layout|reusable components?|component libraries|design systems?|wcag|accessibility|visual design|typography|responsive (?:web\/?mobile )?design|ui\s*\/\s*ux|ux\s*\/\s*ui|color (?:systems?|contrast))\b/i },
  { label: "Collaboration & Delivery", matches: /\b(?:jira|agile|scrum|kanban|developer handoff|design (?:qa|specs?|specifications?)|product analytics|stakeholder facilitation|handoff|sprint planning|project management|stakeholder management|cross-functional collaboration)\b/i },
  { label: "Languages / Web", matches: /^(?:c|c\+\+|c#|java|javascript|js|typescript|ts|python|sql|r|html(?:5)?|css(?:3)?|html\s*(?:\/|&|and|\+)\s*css|php|go|golang|rust|kotlin|swift|dart|ruby|matlab|scala|bash|powershell)$/i },
  { label: "Architecture & Integration", matches: /^(?:kafka|graphql|rest(?:\s+apis?)?|microservices?|distributed systems?|event-driven architecture|api (?:design|integration|gateway)|system design|domain-driven design|ddd|service architecture|message queues?|rabbitmq|event sourcing|soa|service-oriented architecture)$/i },
  { label: "Security & Identity", matches: /^(?:oauth\s*2(?:\.0)?|oidc|open\s*id connect|spring security|jwt|rbac|access control|identity management|authentication|authorization|secrets management|security testing|threat model(?:ing|ling)|sso|single sign-on)$/i },
  { label: "Quality & Observability", matches: /^(?:junit|testcontainers|pact|prometheus|grafana|opentelemetry|elk|elasticsearch logstash kibana|splunk|unit testing|integration testing|contract testing|automated testing|observability|distributed tracing|slo|slis?|incident management|datadog|new relic)$/i },
  { label: "Frameworks", matches: /^(?:django|flask|fastapi|react(?:\.js)?|reactjs|angular|vue(?:\.js)?|spring(?: boot)?|asp\.net(?: core)?|\.net(?: core)?|bootstrap|next\.js|node\.js|express(?:\.js)?|laravel|svelte|streamlit|flutter|pytorch lightning)$/i },
  { label: "Data / Analytics & AI", matches: /\b(?:numpy|pandas|scikit-learn|scikit learn|plotly|power bi|tableau|tensorflow|pytorch|machine learning|deep learning|snowflake|redshift|spark|hadoop|matplotlib|data clean\w*|data analys\w*|etl|eda|exploratory data analysis|feature extraction|kpi|data reporting|kpi reporting|reporting\s*&\s*kpi|mapreduce|hdfs|statistics|data visualization|business intelligence|predictive model\w*)\b/i },
  { label: "Databases", matches: /^(?:postgresql|postgres|redis|sqlite|sql server|mysql|mongodb|oracle(?: database)?|mariadb|firebase|cassandra|dynamodb|bigquery)$/i },
  { label: "Cloud / DevOps", matches: /\b(?:aws|azure|gcp|google cloud|docker|kubernetes|terraform|git|github|gitlab|heroku|nginx|ci\/?cd|jenkins|linux|ansible|devops|cloudformation|azure devops|helm|argo\s?cd|amazon\s?s3|s3)\b/i },
  { label: "Automation & Workflows", matches: /\b(?:n8n|zapier|power automate|make\.com|rpa|robotic process automation|workflow automation|airflow|ssis|process automation)\b/i },
  { label: "Finance & Accounting", matches: /\b(?:payroll|reconcil\w*|bookkeeping|accounts payable|accounts receivable|tally|gst|tax\w*|financial reporting|budgeting|balance sheet|general ledger|auditing|financial modelling|forecasting|sap fico)\b/i },
  { label: "Sales & Marketing", matches: /\b(?:crm|salesforce|hubspot|lead generation|digital marketing|seo|sem|google analytics|google ads|campaign management|content marketing|market research|customer acquisition|email marketing)\b/i },
  { label: "Healthcare Practices", matches: /\b(?:patient care|clinical documentation|electronic health records?|ehr|emr|care coordination|triage|medical coding|cpr|first aid|clinical assessment|patient safety|pharmacovigilance|medication administration)\b/i },
  { label: "Education & Training", matches: /\b(?:lesson planning|curriculum design|classroom management|student assessment|instructional design|training delivery|learning management|lms|teaching|pedagogy|educational assessment)\b/i },
  { label: "Office & Productivity Tools", matches: /^(?:microsoft (?:excel|word|powerpoint|outlook|teams)|ms (?:excel|word|powerpoint)|excel|word|powerpoint|outlook|google sheets|google docs|google slides|sharepoint|citrix|notion|vlookup|xlookup)$/i },
  { label: "Human Resources", matches: /\b(?:recruit\w*|talent acquisition|onboarding|employee relations|performance appraisal|hris|workforce planning|compensation|benefits administration|candidate screening)\b/i },
  { label: "Operations & Supply Chain", matches: /\b(?:inventory management|supply chain|procurement|logistics|warehouse|vendor management|demand planning|quality assurance|lean six sigma|six sigma|process improvement|manufacturing operations)\b/i },
  { label: "Legal & Compliance", matches: /\b(?:contract drafting|contract review|regulatory compliance|due diligence|litigation support|legal research|data protection|gdpr|iso 27001|risk management|policy drafting)\b/i },
];

function isDesignContext(names: readonly string[]): boolean {
  const anchor = /^(?:ui\s*\/\s*ux|ux\s*\/\s*ui|figma|figjam|miro|wirefram\w*|usability test\w*|user interviews?|user research|user journeys?|journey mapping|design systems?|auto layout|interactive prototypes?|responsive design)$/i;
  return names.filter(name => anchor.test(splitSkillQualification(name).base)).length >= 2;
}

/** Shared by both PDF and DOCX; never changes underlying resume data or ATS scoring. */
export function groupedSkills(skills: readonly string[]): ExportSkillGroup[] {
  const names = normalizeSkillsForExport(skills);
  const designContext = isDesignContext(names);
  // Excel and spreadsheet functions are cross-profession tools. Label them
  // as financial only when the source also contains finance-specific skills.
  const financeContext = names.some(skill =>
    /\b(?:payroll|reconcil\w*|bookkeeping|accounts payable|accounts receivable|tally|gst|taxation|budgeting|balance sheet|general ledger|financial reporting|auditing|financial modelling)\b/i.test(skill)
  );
  const groups = new Map<string, string[]>();
  const add = (label: string, skill: string) => {
    if (!groups.has(label)) groups.set(label, []);
    groups.get(label)!.push(skill);
  };

  for (const name of names) {
    const { base } = splitSkillQualification(name);
    let category: string | undefined;
    // "Components" without context is ambiguous. A React developer's components
    // should not be silently classified as design-system expertise.
    if (designContext && /^(?:components?|reusable components?)$/i.test(base)) {
      category = "Design Systems & Accessibility";
    } else {
      category = skillCategories.find(rule => rule.matches.test(base))?.label;
    }
    if (category === "Languages / Web" && (
      designContext || (/^(?:html|css|html\s*(?:\/|&|and|\+)\s*css)$/i.test(base) &&
                        /\b(?:awareness|fundamentals)\b/i.test(name))
    )) category = "Web Knowledge";
    if (category === "Office & Productivity Tools" && financeContext &&
        /^(?:microsoft excel|ms excel|excel|vlookup|xlookup)$/i.test(base)) {
      category = "Finance & Accounting";
    }
    add(category || "Other Skills", name);
  }
  return [...groups].map(([label, values]) => ({ label, skills: values }));
}

/**
 * A project may list UX methods, professional practices, software technology,
 * or both. The output label must describe the supplied evidence accurately.
 * This is presentation only: no skills are created or changed in storage.
 */
export function projectEvidenceLabel(skills: readonly string[]): string {
  const groups = groupedSkills(skills);
  if (groups.length === 0) return "Technology Stack Used";
  const methods = new Set([
    "Design Tools", "UX Research & Methods", "Design Systems & Accessibility",
    "Collaboration & Delivery", "Healthcare Practices", "Education & Training",
    "Finance & Accounting", "Operations & Supply Chain", "Legal & Compliance",
    "Human Resources", "Sales & Marketing", "Office & Productivity Tools"
  ]);
  const hasMethod = groups.some(group => methods.has(group.label));
  const hasTechnology = groups.some(group => !methods.has(group.label) && group.label !== "Other Skills");
  if (hasMethod && hasTechnology) return "Technologies / Methods Used";
  if (hasMethod) return "Tools / Methods Used";
  if (!hasTechnology) return "Skills / Tools Used";
  return "Technology Stack Used";
}

const htmlEscape = (s: string) => (s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;")
  .replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#39;");
const xmlEscape = (s: string) => (s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;")
  .replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&apos;");
const safePublicText = (value: string) => (value || "").replace(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi, "").replace(/(?:\+?\d{1,3}[\s.\-]?)?(?:\d[\s.\-]?){10,12}/g, "").replace(/\s{2,}/g, " ").trim();
const safeDesignation = (value: string) => /@|https?:|(?:\+\d|\b\d[\d .()\-]{8,}\d\b)/i.test(value) ? "" : safePublicText(value);
const displayName = (p: ResumeProfile) => p.candidateName?.trim() || "Professional_Profile";
const exportName = (p: ResumeProfile) => (displayName(p).replace(/[^\p{L}\p{N}_-]+/gu, "_").slice(0, 80) || "Resume") + "_Algoworks";
const experienceText = (p: ResumeProfile) => p.experienceYears == null ? "" :
  `${p.experienceYears} ${p.experienceYears === 1 ? "year" : "years"} of experience`;

/** Opens a self-contained A4 print document. Browser: choose Save as PDF / Microsoft Print to PDF. */
export function printResumePdf(profile: ResumeProfile): void {
  // Must open synchronously during the button click to avoid popup blocking.
  const popup = window.open("", "_blank");
  if (!popup) {
    window.alert("Your browser blocked the print window. Allow pop-ups for localhost and click Save PDF again.");
    return;
  }
  const summary = htmlEscape(safePublicText(profile.professionalSummary || "Professional summary not provided."));
  const skills = groupedSkills(profile.technicalSkills || [])
    .map(g => `<div class="techrow"><b>${htmlEscape(g.label)}</b><span>${htmlEscape(g.skills.join(", "))}</span></div>`).join("");
  const projects = (profile.projects || []).slice(0, 10).map((p, i) => `
    <div class="project">
      <div class="projectTitle"><strong>${i + 1}. ${htmlEscape(p.domain || p.title || "Project")}</strong><span>${htmlEscape(p.year || "")}</span></div>
      ${p.description ? `<p>${htmlEscape(safePublicText(p.description))}</p>` : ""}
      ${p.technologyStack?.length ? `<p class="technology"><b>${htmlEscape(projectEvidenceLabel(p.technologyStack))}:</b> ${htmlEscape(p.technologyStack.join(", "))}</p>` : ""}
    </div>`).join("");
  const documentHtml = `<!doctype html><html lang="en"><head><meta charset="utf-8"/>
    <title>${htmlEscape(displayName(profile))} - Algoworks Profile</title>
    <meta name="viewport" content="width=device-width,initial-scale=1"/>
    <style>
      @page {size:A4 portrait;margin:15mm 16mm 16mm;}
      *{box-sizing:border-box}
      html,body{background:#fff!important;color:#1d2833!important;margin:0;padding:0;font-family:Arial,Helvetica,sans-serif;font-size:9.1pt;line-height:1.49}
      article{width:100%;background:white;color:#1d2833}
      .top{padding:0 0 11mm}.logo{width:36mm;height:auto;display:block;max-height:12mm;object-fit:contain;object-position:left}
      .greenrule{border-top:1.6pt solid #15c65d;margin:0 0 7.5mm}
      h1{font-size:18pt;line-height:1.16;margin:0 0 2mm;font-weight:750}
      .subtitle{font-size:9.6pt;color:#556372;margin:0 0 8mm;white-space:pre-wrap}
      section{margin:0 0 7mm}h2{font-size:10.1pt;color:#202c37;letter-spacing:.15pt;margin:0 0 3mm;padding:0 0 1.8mm;border-bottom:1px solid #cbd4d9;break-after:avoid}
      .summary{margin:0;white-space:pre-wrap;overflow-wrap:break-word}
      .techbox{width:55%;min-width:80mm;border:1px solid #d4dbde;background:#f8faf9;font-size:8.3pt}
      .techrow{display:grid;grid-template-columns:32% 68%;border-bottom:1px solid #e0e6e5;break-inside:avoid}
      .techrow:last-child{border-bottom:none}.techrow b,.techrow span{padding:1.3mm 1.8mm;overflow-wrap:anywhere}
      .techrow b{color:#445967;background:#f1f6f3}
      .project{margin:0 0 4.5mm;padding:0 0 3.5mm;border-bottom:1px solid #e8edee;break-inside:avoid;page-break-inside:avoid}
      .projectTitle{display:flex;gap:5mm;justify-content:space-between;align-items:baseline;margin:0 0 1.8mm;break-after:avoid}
      .projectTitle strong{font-size:9.8pt}.projectTitle span{color:#536578;white-space:nowrap}
      .project p{margin:0 0 2mm;white-space:pre-wrap;overflow-wrap:anywhere}
      .technology{background:#f5f8f6;border-left:2pt solid #15c65d;padding:1.3mm 2mm;font-size:8.4pt}
      .printbar{position:sticky;top:0;display:flex;justify-content:space-between;align-items:center;background:#eef4f1;padding:10px 16px;margin:0 0 24px;border-radius:10px;font:14px Arial}
      .printbar button{border:0;background:#137f63;color:white;padding:11px 18px;border-radius:8px;cursor:pointer}
      @media print{.printbar{display:none!important}article{margin:0;padding:0}html,body{background:white!important}header,main,section,article{transform:none!important;filter:none!important}}
      @media screen{body{background:#e9eceb!important;padding:18px}article{margin:auto;max-width:210mm;min-height:297mm;padding:15mm 16mm;box-shadow:0 8px 30px #1e293b1c}.techbox{max-width:100%}}
    </style></head><body>
      <div class="printbar"><span>Algoworks profile — Print / Save as PDF</span><button type="button" onclick="window.print()">Print / Save PDF</button></div>
      <article><div class="top"><img class="logo" src="${htmlEscape(window.location.origin)}/algoworks-logo.png" alt="Algoworks logo"/></div>
      <div class="greenrule"></div>
      <h1>${htmlEscape(displayName(profile))}</h1>
      <p class="subtitle">${htmlEscape([safeDesignation(profile.designation), experienceText(profile)].filter(Boolean).join("  ·  "))}</p>
      <section><h2>PROFESSIONAL SUMMARY</h2><p class="summary">${summary}</p></section>
      <section><h2>TECHNOLOGY STACK</h2><div class="techbox">${skills || '<div class="techrow"><span>Not provided</span></div>'}</div></section>
      <section><h2>PROJECT EXPERIENCE</h2>${projects || '<p>No verified projects provided.</p>'}</section>
      </article>
    </body></html>`;
  popup.document.open();
  popup.document.write(documentHtml);
  popup.document.close();
  // The separate print tab is intentionally left open for manual retry.
  popup.addEventListener("load", () => {
    window.setTimeout(() => { try { popup.focus(); popup.print(); } catch { /* Use visible print button */ } }, 400);
  }, { once: true });
}

// Minimal standards-compliant ZIP writer (STORE compression) for OOXML Word .docx.
// No external runtime package or dependency changes are needed.
const encoder = new TextEncoder();
const u16 = (n: number) => [n & 255, (n >>> 8) & 255];
const u32 = (n: number) => [n & 255, (n >>> 8) & 255, (n >>> 16) & 255, (n >>> 24) & 255];
const toBytes = (s: string) => encoder.encode(s);
const crcTable = Array.from({ length: 256 }, (_, index) => {
  let v = index;
  for (let i = 0; i < 8; i++) v = v & 1 ? 0xedb88320 ^ (v >>> 1) : v >>> 1;
  return v >>> 0;
});
function crc32(bytes: Uint8Array) {
  let value = 0xffffffff;
  for (const byte of bytes) value = crcTable[(value ^ byte) & 255] ^ (value >>> 8);
  return (value ^ 0xffffffff) >>> 0;
}
function zipBlob(files: { name: string; data: Uint8Array }[]): Blob {
  const records: number[] = [];
  const central: number[] = [];
  let cursor = 0;
  for (const f of files) {
    const name = toBytes(f.name);
    const checksum = crc32(f.data);
    const local = [80, 75, 3, 4, ...u16(20), ...u16(0), ...u16(0), ...u16(0), ...u16(0),
      ...u32(checksum), ...u32(f.data.length), ...u32(f.data.length), ...u16(name.length), ...u16(0), ...name];
    records.push(...local);
    for (const byte of f.data) records.push(byte);
    central.push(80, 75, 1, 2, ...u16(20), ...u16(20), ...u16(0), ...u16(0),
      ...u16(0), ...u16(0), ...u32(checksum), ...u32(f.data.length), ...u32(f.data.length),
      ...u16(name.length), ...u16(0), ...u16(0), ...u16(0), ...u16(0), ...u32(0), ...u32(cursor), ...name);
    cursor += local.length + f.data.length;
  }
  const end = [80, 75, 5, 6, ...u16(0), ...u16(0), ...u16(files.length), ...u16(files.length),
    ...u32(central.length), ...u32(cursor), ...u16(0)];
  const bytes = new Uint8Array([...records, ...central, ...end]);
  return new Blob([Uint8Array.from(bytes)], { type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
}

const run = (text: string, bold = false) => `<w:r>${bold ? '<w:rPr><w:b/></w:rPr>' : ''}<w:t xml:space="preserve">${xmlEscape(text)}</w:t></w:r>`;
function paragraph(content: string, opts: { style?: string; after?: number; before?: number; keepNext?: boolean; bottomBorder?: boolean } = {}) {
  const pPr = `<w:pPr>${opts.style ? `<w:pStyle w:val="${opts.style}"/>` : ""}` +
    `<w:spacing w:before="${opts.before ?? 0}" w:after="${opts.after ?? 95}"/>` +
    (opts.keepNext ? "<w:keepNext/>" : "") +
    (opts.bottomBorder ? '<w:pBdr><w:bottom w:val="single" w:sz="5" w:space="4" w:color="16BC5A"/></w:pBdr>' : "") +
    `</w:pPr>`;
  return `<w:p>${pPr}${content}</w:p>`;
}
function wordLogo() {
  const cx = 1333500, cy = 404495; // 37mm wide, preserving 1200x364 aspect ratio
  return `<w:p><w:pPr><w:spacing w:after="260"/></w:pPr><w:r><w:drawing>` +
    `<wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="${cx}" cy="${cy}"/>` +
    '<wp:docPr id="1" name="Algoworks Logo" descr="Algoworks corporate logo"/>' +
    '<a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">' +
    '<pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:nvPicPr><pic:cNvPr id="0" name="algoworks.png"/><pic:cNvPicPr/></pic:nvPicPr>' +
    '<pic:blipFill><a:blip r:embed="rIdLogo"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>' +
    `<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm>` +
    '<a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>' +
    '</wp:inline></w:drawing></w:r></w:p>';
}
function tableCell(text: string, width: number, bold = false) {
  return `<w:tc><w:tcPr><w:tcW w:w="${width}" w:type="dxa"/><w:shd w:fill="F7FAF8"/>` +
    '<w:tcMar><w:top w:w="75" w:type="dxa"/><w:left w:w="90" w:type="dxa"/><w:bottom w:w="75" w:type="dxa"/><w:right w:w="90" w:type="dxa"/></w:tcMar></w:tcPr>' +
    paragraph(run(text, bold), { after: 0 }) + '</w:tc>';
}
function techTable(groups: ReturnType<typeof groupedSkills>) {
  if (!groups.length) return paragraph(run("Not provided"));
  return `<w:tbl><w:tblPr><w:tblW w:w="5650" w:type="dxa"/><w:tblInd w:w="0" w:type="dxa"/>` +
    '<w:tblBorders><w:bottom w:val="single" w:sz="4" w:color="DEE7E2"/><w:insideH w:val="single" w:sz="3" w:color="E3EAE6"/></w:tblBorders></w:tblPr>' +
    '<w:tblGrid><w:gridCol w:w="1670"/><w:gridCol w:w="3980"/></w:tblGrid>' +
    groups.map(g => '<w:tr>' + tableCell(g.label, 1670, true) + tableCell(g.skills.join(', '), 3980) + '</w:tr>').join('') + '</w:tbl>';
}

/** Export a real .docx that opens and can be edited in Microsoft Word. */
export async function downloadResumeDocx(profile: ResumeProfile): Promise<void> {
  const imageResponse = await fetch("/algoworks-logo.png");
  if (!imageResponse.ok) throw new Error("Algoworks logo could not be loaded for the Word file.");
  const logo = new Uint8Array(await imageResponse.arrayBuffer());
  const paragraphs: string[] = [];
  paragraphs.push(wordLogo());
  paragraphs.push(paragraph("", { bottomBorder: true, after: 220 }));
  paragraphs.push(paragraph(run(displayName(profile), true), { style: "Title", after: 80, keepNext: true }));
  paragraphs.push(paragraph(run([safeDesignation(profile.designation), experienceText(profile)].filter(Boolean).join("  ·  ")), { style: "Subtitle", after: 310 }));
  paragraphs.push(paragraph(run("PROFESSIONAL SUMMARY", true), { style: "Heading1", bottomBorder: true, after: 140, keepNext: true }));
  paragraphs.push(paragraph(run(safePublicText(profile.professionalSummary || "Professional summary not provided.")), { after: 240 }));
  paragraphs.push(paragraph(run("TECHNOLOGY STACK", true), { style: "Heading1", bottomBorder: true, after: 150, keepNext: true }));
  paragraphs.push(techTable(groupedSkills(profile.technicalSkills || [])));
  paragraphs.push(paragraph(run("PROJECT EXPERIENCE", true), { style: "Heading1", bottomBorder: true, before: 280, after: 180, keepNext: true }));
  for (const [i, project] of (profile.projects || []).slice(0, 10).entries()) {
    const title = `${i + 1}. ${project.domain || project.title || "Project"}`;
    paragraphs.push(paragraph(run(title, true) + (project.year ? run(`     ${project.year}`) : ""), { after: 90, keepNext: true }));
    if (project.description) paragraphs.push(paragraph(run(safePublicText(project.description)), { after: 100, keepNext: Boolean(project.technologyStack?.length) }));
    if (project.technologyStack?.length)
      paragraphs.push(paragraph(run(projectEvidenceLabel(project.technologyStack) + ": ", true) + run(project.technologyStack.join(", ")), { after: 210 }));
  }
  const doc = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
    `<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">` +
    `<w:body>${paragraphs.join("")}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/>` +
    '<w:pgMar w:top="850" w:right="900" w:bottom="850" w:left="900" w:header="360" w:footer="360" w:gutter="0"/>' +
    '</w:sectPr></w:body></w:document>';
  const styles = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
    '<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">' +
    '<w:style w:type="paragraph" w:styleId="Normal" w:default="1"><w:name w:val="Normal"/><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="19"/><w:color w:val="25313C"/></w:rPr></w:style>' +
    '<w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:rPr><w:b/><w:sz w:val="36"/><w:color w:val="1D2935"/></w:rPr></w:style>' +
    '<w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:rPr><w:sz w:val="19"/><w:color w:val="536374"/></w:rPr></w:style>' +
    '<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:rPr><w:b/><w:sz w:val="21"/><w:color w:val="202C37"/></w:rPr></w:style>' +
    '</w:styles>';
  const contentTypes = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
    '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
    '<Default Extension="xml" ContentType="application/xml"/>' +
    '<Default Extension="png" ContentType="image/png"/>' +
    '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>' +
    '<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>' +
    '</Types>';
  const rootRels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
    '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>' +
    '</Relationships>';
  const docRels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
    '<Relationship Id="rIdStyles" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
    '<Relationship Id="rIdLogo" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/algoworks.png"/>' +
    '</Relationships>';
  const blob = zipBlob([
    { name: "[Content_Types].xml", data: toBytes(contentTypes) },
    { name: "_rels/.rels", data: toBytes(rootRels) },
    { name: "word/document.xml", data: toBytes(doc) },
    { name: "word/styles.xml", data: toBytes(styles) },
    { name: "word/_rels/document.xml.rels", data: toBytes(docRels) },
    { name: "word/media/algoworks.png", data: logo },
  ]);
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = exportName(profile) + ".docx";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 45_000);
}
