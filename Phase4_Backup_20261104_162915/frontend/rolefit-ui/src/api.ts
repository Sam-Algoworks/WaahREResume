export type ResumeProject = { title: string; description: string };

export type ResumeProfile = {
  id: string;
  sourceFileName: string;
  candidateName: string;
  designation: string;
  email: string;
  phone: string;
  experienceYears: number | null;
  professionalSummary: string;
  technicalSkills: string[];
  employmentHistory: string[];
  projects: ResumeProject[];
  education: string[];
  certifications: string[];
  status: string;
  parserNote: string;
  updatedAt: string;
};

export type Job = {
  id: string;
  title: string;
  description: string;
  version: number;
  createdAt: string;
};

export type ScorePart = {
  category: string;
  awarded: number;
  weight: number;
  applicable: boolean;
};

export type Analysis = {
  id: string;
  candidateName: string;
  fileName: string;
  jobTitle: string;
  jobFingerprint: string;
  score: number;
  applicableWeight: number;
  jobSkills: string[];
  candidateSkills: string[];
  matchedSkills: string[];
  missingSkills: string[];
  breakdown: ScorePart[];
  notes: string;
  createdAt: string;
};

export type Stats = {
  totalJobs: number;
  totalAnalyses: number;
  uniqueCandidates: number;
  averageScore: number;
  recent: Analysis[];
};

export type BatchResponse = {
  results: Analysis[];
  failures: { fileName: string; error: string }[];
};

async function call<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, options);
  } catch {
    throw new Error("Unable to reach the RoleFit backend. Start the API terminal and try again.");
  }
  if (!response.ok) {
    let reason = `Request failed (HTTP ${response.status})`;
    try {
      const body = await response.json() as { error?: string; title?: string };
      reason = body.error || body.title || reason;
    } catch { /* API returned a non-JSON error */ }
    throw new Error(reason);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  readDocument: (file: File) => {
    const form = new FormData();
    form.set("file", file);
    return call<{ text: string; fileName: string }>("/api/documents/read", { method: "POST", body: form });
  },
  parseResume: (file: File) => {
    const form = new FormData();
    form.set("file", file);
    return call<ResumeProfile>("/api/resumes/parse", { method: "POST", body: form });
  },
  saveResumeDraft: (profile: ResumeProfile) => call<ResumeProfile>(`/api/resumes/${profile.id}/draft`, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(profile)
  }),
  health: () => call<{ service: string; status: string; version: string }>("/api/health"),
  stats: () => call<Stats>("/api/dashboard/stats"),
  jobs: () => call<Job[]>("/api/jobs"),
  analyses: () => call<Analysis[]>("/api/analysis"),
  createJob: (title: string, description: string) => call<Job>("/api/jobs", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, description })
  }),
  deleteJob: (id: string) => call<void>(`/api/jobs/${id}`, { method: "DELETE" }),
  single: (jdText: string, file: File) => {
    const form = new FormData();
    form.set("jdText", jdText);
    form.set("file", file);
    return call<Analysis>("/api/analysis/single", { method: "POST", body: form });
  },
  bulk: (jdText: string, files: File[]) => {
    const form = new FormData();
    form.set("jdText", jdText);
    files.forEach(file => form.append("files", file));
    return call<BatchResponse>("/api/analysis/bulk", { method: "POST", body: form });
  }
};
