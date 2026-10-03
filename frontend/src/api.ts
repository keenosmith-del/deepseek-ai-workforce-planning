export type User = {
  id: string;
  name: string;
  email: string;
  role: "HRAdmin" | "Recruiter" | "Employee" | "Candidate";
  departmentId?: string;
};
export type Department = { id: string; name: string };
export type Skill = {
  id: string;
  name: string;
  description: string;
  aliases: string;
};
export type Requirement = { skillId: string; proficiency: number };
export type Job = {
  id: string;
  title: string;
  description: string;
  departmentId: string;
  open: boolean;
  requirements: Requirement[];
};
export type Application = {
  id: string;
  jobId: string;
  status: string;
  evidenceVersion: number;
  jobTitle?: string;
  candidateName?: string;
  createdAt: string;
};
export type Fact = {
  id: string;
  documentId: string;
  kind: string;
  value: string;
  skillId: string | null;
  sectionId: string;
  quote: string;
  verification: string;
  proficiency: number | null;
  mapping: string;
};
export type Processing = {
  id: string;
  kind: string;
  status: string;
  attempts: number;
  error: string | null;
};
export type Analysis = {
  id: string;
  evidenceVersion: number;
  stale: boolean;
  result: string;
  metadata: string;
  createdAt: string;
};
export type Interview = {
  id: string;
  scheduledAt: string;
  location: string;
  notes: string | null;
};
export type Detail = {
  application: Application;
  job: Job;
  documents: {
    id: string;
    originalName: string;
    sections: string;
    expiresAt: string;
  }[];
  facts: Fact[];
  analyses: Analysis[];
  processing: Processing[];
  reviews: { id: string; action: string; note: string; createdAt: string }[];
  interviews: Interview[];
  decisions: { id: string; outcome: string; rationale: string }[];
};
export type EmployeeSkill = {
  id: string;
  userId: string;
  skillId: string;
  proficiency: number;
  evidence: string;
  verification: string;
  developmentNeed: string;
};
export type Dashboard = {
  departments: {
    id: string;
    name: string;
    employees: number;
    vacancies: number;
  }[];
  coverage: {
    id: string;
    name: string;
    documentedEmployees: number;
    unverifiedEmployees: number;
    vacanciesRequiring: number;
    departmentsWithoutDocumentedCoverage: number;
  }[];
  development: { skillId: string; count: number }[];
  applicationStates: { status: string; count: number }[];
};
let token = "";
export function setToken(value: string) {
  token = value;
}
export async function api<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const form = body instanceof FormData;
  const r = await fetch("/api" + path, {
    method,
    headers: {
      ...(token ? { Authorization: "Bearer " + token } : {}),
      ...(!form && body !== undefined
        ? { "Content-Type": "application/json" }
        : {}),
    },
    body: body === undefined ? undefined : form ? body : JSON.stringify(body),
  });
  if (!r.ok) {
    const e = await r.json().catch(() => ({}));
    throw Error(
      e.message || e.detail || e.title || `Request failed (${r.status})`,
    );
  }
  const text = await r.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}
export async function download(path: string, name: string) {
  const r = await fetch("/api" + path, {
    headers: { Authorization: "Bearer " + token },
  });
  if (!r.ok) throw Error("Download failed");
  const url = URL.createObjectURL(await r.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
