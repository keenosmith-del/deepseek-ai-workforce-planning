import React, {
  useState,
  useEffect,
  type FormEvent,
  type ReactNode,
} from "react";
import { createRoot } from "react-dom/client";
import {
  api,
  setToken,
  download,
  type User,
  type Skill,
  type Department,
  type Job,
  type Application,
  type Detail,
  type Fact,
  type Dashboard,
  type EmployeeSkill,
  type Interview,
  type Requirement,
} from "./api";
import "./style.css";
const levels = [
  "",
  "1 · Awareness",
  "2 · Working",
  "3 · Advanced",
  "4 · Expert",
];
function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  );
}
function Table({ headers, rows }: { headers: string[]; rows: ReactNode[][] }) {
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {headers.map((h) => (
              <th key={h}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i}>
              {r.map((c, j) => (
                <td key={j}>{c}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {!rows.length && <p>No records yet.</p>}
    </div>
  );
}
function App() {
  const [user, setUser] = useState<User | null>(null),
    [page, setPage] = useState("Jobs"),
    [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [busy, setBusy] = useState(false),
    [tick, setTick] = useState(0);
  const [skills, setSkills] = useState<Skill[]>([]),
    [departments, setDepartments] = useState<Department[]>([]),
    [jobs, setJobs] = useState<Job[]>([]),
    [apps, setApps] = useState<Application[]>([]),
    [selected, setSelected] = useState<string | null>(null),
    [detail, setDetail] = useState<Detail | null>(null);
  const staff = user?.role === "HRAdmin" || user?.role === "Recruiter",
    hr = user?.role === "HRAdmin";
  async function run(fn: () => Promise<unknown>, message = "Saved") {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await fn();
      setNotice(message);
      setTick((t) => t + 1);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    if (!user) return;
    let active = true;
    Promise.all([
      api<Skill[]>("/skills"),
      api<Department[]>("/departments"),
      api<Job[]>("/jobs"),
      api<Application[]>("/applications"),
    ])
      .then(([s, d, j, a]) => {
        if (active) {
          setSkills(s);
          setDepartments(d);
          setJobs(j);
          setApps(a);
        }
      })
      .catch((e) => setError(e.message));
    return () => {
      active = false;
    };
  }, [user, tick]);
  useEffect(() => {
    if (!selected || !user) {
      setDetail(null);
      return;
    }
    let active = true;
    const load = () =>
      api<Detail>("/applications/" + selected)
        .then((d) => {
          if (active) setDetail(d);
        })
        .catch((e) => {
          if (active) setError(e.message);
        });
    void load();
    const timer = setInterval(load, 4000);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [selected, user, tick]);
  const skillName = (id: string) => skills.find((s) => s.id === id)?.name || id;
  if (!user)
    return (
      <div className="login">
        <p className="eyebrow">LOCAL • EVIDENCE • HUMAN OVERSIGHT</p>
        <h1>Workforce Intelligence</h1>
        <p>Planning, recruitment and skills development</p>
        <Auth
          onLogin={(u, t) => {
            setToken(t);
            setUser(u);
            setPage(
              u.role === "Employee"
                ? "My skills"
                : u.role === "HRAdmin"
                  ? "Dashboard"
                  : "Jobs",
            );
          }}
        />
      </div>
    );
  return (
    <>
      <header>
        <div>
          <strong>Workforce Intelligence</strong>
          <small>
            {user.name} · {user.role}
          </small>
        </div>
        <button
          onClick={() => {
            setToken("");
            setUser(null);
            setNotice("");
            setError("");
            setApps([]);
            setJobs([]);
            setDetail(null);
            setSelected(null);
          }}
        >
          Sign out
        </button>
      </header>
      <nav>
        {[
          "Jobs",
          "Applications",
          ...(user.role === "Employee" ? ["My skills"] : []),
          ...(hr
            ? ["Dashboard", "Departments", "Employees", "Taxonomy", "Audit"]
            : []),
          ...(staff ? ["Models"] : []),
        ].map((p) => (
          <button
            className={p === page ? "active" : ""}
            key={p}
            onClick={() => {
              setPage(p);
              setSelected(null);
            }}
          >
            {p}
          </button>
        ))}
      </nav>
      <main>
        {error && (
          <div role="alert" className="error">
            {error}
          </div>
        )}
        {notice && (
          <div role="status" className="notice">
            {notice}
          </div>
        )}
        {busy && <p role="status">Working…</p>}
        {page === "Jobs" && (
          <>
            <h1>Vacancies</h1>
            {staff && (
              <JobForm
                departments={departments}
                skills={skills}
                save={(dto) =>
                  run(() => api("/jobs", "POST", dto), "Vacancy created")
                }
              />
            )}
            <div className="cards">
              {jobs.map((j) => (
                <section key={j.id}>
                  <h2>
                    {j.title}{" "}
                    <span className="badge">{j.open ? "Open" : "Closed"}</span>
                  </h2>
                  <p>
                    {departments.find((d) => d.id === j.departmentId)?.name}
                  </p>
                  <p>{j.description}</p>
                  <ul>
                    {j.requirements.map((r) => (
                      <li key={r.skillId}>
                        {skillName(r.skillId)} · {levels[r.proficiency]}
                      </li>
                    ))}
                  </ul>
                  {user.role === "Candidate" && (
                    <button
                      disabled={busy || apps.some((a) => a.jobId === j.id)}
                      onClick={() =>
                        run(async () => {
                          const a = await api<Application>(
                            "/jobs/" + j.id + "/apply",
                            "POST",
                          );
                          setSelected(a.id);
                          setPage("Applications");
                        }, "Application submitted. Upload your resume below.")
                      }
                    >
                      {apps.some((a) => a.jobId === j.id) ? "Applied" : "Apply"}
                    </button>
                  )}
                  {staff && (
                    <details>
                      <summary>Edit vacancy</summary>
                      <JobForm
                        initial={j}
                        departments={departments}
                        skills={skills}
                        save={(dto) =>
                          run(
                            () => api("/jobs/" + j.id, "PUT", dto),
                            "Vacancy updated; earlier analyses are stale",
                          )
                        }
                      />
                    </details>
                  )}
                </section>
              ))}
            </div>
          </>
        )}
        {page === "Applications" && (
          <>
            <h1>{staff ? "Recruitment workspace" : "My applications"}</h1>
            <Table
              headers={["Vacancy", "Candidate", "Status", ""]}
              rows={apps.map((a) => [
                a.jobTitle,
                a.candidateName,
                a.status,
                <button onClick={() => setSelected(a.id)}>Open</button>,
              ])}
            />
            {detail && selected && (
              <section>
                <h2>
                  {detail.job.title} · {detail.application.status}
                </h2>
                <p>
                  Evidence version {detail.application.evidenceVersion}.
                  Extracted facts require human verification. Missing
                  information does not establish inability.
                </p>
                <h3>Documents & processing</h3>
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    const file = (
                      e.currentTarget.elements.namedItem(
                        "resume",
                      ) as HTMLInputElement
                    ).files?.[0];
                    if (file) {
                      const f = new FormData();
                      f.append("file", file);
                      void run(
                        () =>
                          api(
                            "/applications/" + selected + "/documents",
                            "POST",
                            f,
                          ),
                        "Resume queued for extraction",
                      );
                    }
                  }}
                >
                  <Field label="Resume · selectable-text PDF or DOCX · maximum 5 MiB">
                    <input
                      name="resume"
                      type="file"
                      accept=".pdf,.docx"
                      required
                    />
                  </Field>
                  <button disabled={busy}>Upload resume</button>
                </form>
                {detail.documents.map((d) => (
                  <details key={d.id}>
                    <summary>
                      {d.originalName} · expires{" "}
                      {new Date(d.expiresAt).toLocaleDateString()}
                    </summary>
                    <button
                      onClick={() =>
                        run(
                          () =>
                            download(
                              `/applications/${selected}/documents/${d.id}/download`,
                              d.originalName,
                            ),
                          "Downloaded",
                        )
                      }
                    >
                      Download original
                    </button>
                    <button
                      className="danger"
                      onClick={() => {
                        if (
                          confirm(
                            "Delete this document, its facts and analyses?",
                          )
                        )
                          void run(
                            () =>
                              api(
                                `/applications/${selected}/documents/${d.id}`,
                                "DELETE",
                              ),
                            "Document deleted",
                          );
                      }}
                    >
                      Delete document
                    </button>
                    {(
                      JSON.parse(d.sections) as { id: string; text: string }[]
                    ).map((s) => (
                      <div key={s.id}>
                        <strong>{s.id}</strong>
                        <pre>{s.text}</pre>
                      </div>
                    ))}
                  </details>
                ))}
                <Table
                  headers={["Operation", "State", "Attempts", "Error", ""]}
                  rows={detail.processing.map((p) => [
                    p.kind,
                    p.status,
                    p.attempts,
                    p.error,
                    staff && p.status === "Failed" ? (
                      <button
                        onClick={() =>
                          run(
                            () =>
                              api(
                                `/applications/${selected}/processing/${p.id}/retry`,
                                "POST",
                              ),
                            "Retry queued",
                          )
                        }
                      >
                        Retry
                      </button>
                    ) : null,
                  ])}
                />
                <h3>Extracted evidence</h3>
                {detail.facts.map((f) => (
                  <FactCard
                    key={f.id + ":" + detail.application.evidenceVersion}
                    fact={f}
                    skills={skills}
                    staff={staff}
                    save={(dto) =>
                      run(
                        () =>
                          api(
                            `/applications/${selected}/facts/${f.id}`,
                            "PUT",
                            {
                              ...(dto as Record<string, unknown>),
                              evidenceVersion:
                                detail.application.evidenceVersion,
                            },
                          ),
                        "Correction saved; prior analyses invalidated",
                      )
                    }
                  />
                ))}
                {!detail.facts.length && (
                  <p>
                    No extracted facts yet. Check document processing above.
                  </p>
                )}
                {staff && detail.documents.length > 0 && (
                  <details>
                    <summary>Add a missed fact from source evidence</summary>
                    <form
                      onSubmit={(e) => {
                        e.preventDefault();
                        const d = new FormData(e.currentTarget);
                        void run(
                          () =>
                            api(`/applications/${selected}/facts`, "POST", {
                              evidenceVersion:
                                detail.application.evidenceVersion,
                              documentId: d.get("documentId"),
                              sectionId: d.get("sectionId"),
                              quote: d.get("quote"),
                              kind: d.get("kind"),
                              value: d.get("value"),
                              skillId: d.get("skillId") || null,
                            }),
                          "Source-backed fact added",
                        );
                      }}
                    >
                      <Field label="Source document">
                        <select name="documentId">
                          {detail.documents.map((d) => (
                            <option key={d.id} value={d.id}>
                              {d.originalName}
                            </option>
                          ))}
                        </select>
                      </Field>
                      <Field label="Section ID (from source preview)">
                        <input name="sectionId" required />
                      </Field>
                      <Field label="Exact source quotation">
                        <textarea name="quote" required maxLength={2000} />
                      </Field>
                      <Field label="Fact kind">
                        <select name="kind">
                          {[
                            "skill",
                            "education",
                            "experience",
                            "technology",
                          ].map((k) => (
                            <option key={k}>{k}</option>
                          ))}
                        </select>
                      </Field>
                      <Field label="Structured value">
                        <input name="value" required maxLength={2000} />
                      </Field>
                      <Field label="Canonical skill (if applicable)">
                        <select name="skillId">
                          <option value="">Unmapped</option>
                          {skills.map((s) => (
                            <option key={s.id} value={s.id}>
                              {s.name}
                            </option>
                          ))}
                        </select>
                      </Field>
                      <button>Add unverified fact</button>
                    </form>
                  </details>
                )}
                {staff && (
                  <>
                    <h3>Evidence gap analyses</h3>
                    <button
                      disabled={
                        busy ||
                        detail.processing.some(
                          (p) =>
                            p.status === "Running" || p.status === "Queued",
                        )
                      }
                      onClick={() =>
                        run(
                          () =>
                            api(`/applications/${selected}/analysis`, "POST"),
                          "Analysis queued",
                        )
                      }
                    >
                      Run local DeepSeek analysis
                    </button>
                    <p>
                      AI never ranks applicants or makes hiring recommendations.
                      Analyses remain separate from verified facts and manual
                      decisions.
                    </p>
                    {detail.analyses.map((a) => (
                      <details key={a.id} open={!a.stale}>
                        <summary>
                          {a.stale ? "STALE · " : ""}Evidence version{" "}
                          {a.evidenceVersion} ·{" "}
                          {new Date(a.createdAt).toLocaleString()}
                        </summary>
                        <Table
                          headers={[
                            "Skill",
                            "Evidence state",
                            "Explanation",
                            "Uncertainty",
                            "Source sections",
                            "Proficiency met",
                          ]}
                          rows={(
                            JSON.parse(a.result).gaps as {
                              skill_id: string;
                              status: string;
                              explanation: string;
                              uncertainty: string;
                              section_ids: string[];
                              proficiency_met: boolean | null;
                            }[]
                          ).map((g) => [
                            skillName(g.skill_id),
                            g.status,
                            g.explanation,
                            g.uncertainty,
                            g.section_ids.join(", "),
                            g.proficiency_met === null
                              ? "Unknown"
                              : g.proficiency_met
                                ? "Documented"
                                : "Documented gap",
                          ])}
                        />
                        <details>
                          <summary>Model and prompt metadata</summary>
                          <pre>
                            {JSON.stringify(JSON.parse(a.metadata), null, 2)}
                          </pre>
                        </details>
                      </details>
                    ))}
                    <h3>Human review</h3>
                    <NoteForm
                      label="Review note"
                      save={(note) =>
                        run(
                          () =>
                            api(`/applications/${selected}/reviews`, "POST", {
                              note,
                            }),
                          "Review recorded",
                        )
                      }
                    />
                    {detail.reviews.map((r) => (
                      <p key={r.id}>
                        <strong>{r.action}</strong> · {r.note} ·{" "}
                        {new Date(r.createdAt).toLocaleString()}
                      </p>
                    ))}
                    <h3>Interview scheduling</h3>
                    <InterviewForm
                      save={(dto) =>
                        run(
                          () =>
                            api(
                              `/applications/${selected}/interviews`,
                              "POST",
                              dto,
                            ),
                          "Interview scheduled",
                        )
                      }
                    />
                    {detail.interviews.map((i) => (
                      <InterviewForm
                        key={i.id}
                        initial={i}
                        save={(dto) =>
                          run(
                            () =>
                              api(
                                `/applications/${selected}/interviews/${i.id}`,
                                "PUT",
                                dto,
                              ),
                            "Interview notes saved",
                          )
                        }
                      />
                    ))}
                    <h3>Manual status & decision</h3>
                    <StatusForm
                      status={detail.application.status}
                      save={(dto) =>
                        run(
                          () =>
                            api(`/applications/${selected}/status`, "PUT", dto),
                          "Manual decision recorded",
                        )
                      }
                    />
                    {detail.decisions.map((d) => (
                      <p key={d.id}>
                        <strong>{d.outcome}</strong> · {d.rationale}
                      </p>
                    ))}
                  </>
                )}
                {!staff &&
                  detail.interviews.map((i) => (
                    <p key={i.id}>
                      Interview: {new Date(i.scheduledAt).toLocaleString()} ·{" "}
                      {i.location}
                    </p>
                  ))}
                <button
                  className="danger"
                  onClick={() => {
                    if (
                      confirm(
                        "Permanently delete this application and all documents and recruitment records?",
                      )
                    )
                      void run(async () => {
                        await api("/applications/" + selected, "DELETE");
                        setSelected(null);
                      }, "Application deleted");
                  }}
                >
                  Delete application and personal records
                </button>
              </section>
            )}
          </>
        )}
        {page === "My skills" && (
          <Profile skills={skills} tick={tick} run={run} />
        )}
        {page === "Dashboard" && (
          <DashboardView tick={tick} skillName={skillName} />
        )}
        {page === "Departments" && (
          <>
            <h1>Departments</h1>
            <NameForm
              label="Department name"
              save={(name) => run(() => api("/departments", "POST", { name }))}
            />
            {departments.map((d) => (
              <section key={d.id}>
                <NameForm
                  initial={d.name}
                  label="Rename department"
                  save={(name) =>
                    run(() => api("/departments/" + d.id, "PUT", { name }))
                  }
                />
              </section>
            ))}
          </>
        )}
        {page === "Employees" && (
          <Employees
            departments={departments}
            skills={skills}
            tick={tick}
            run={run}
          />
        )}
        {page === "Taxonomy" && <Taxonomy skills={skills} run={run} />}
        {page === "Models" && (
          <RemoteData
            path="/models"
            title="Local model availability"
            tick={tick}
          />
        )}
        {page === "Audit" && <Audit tick={tick} />}
      </main>
      <footer>
        Fictional demonstration data · Human decisions only · Proficiency:
        awareness / working / advanced / expert
      </footer>
    </>
  );
}
function Auth({ onLogin }: { onLogin: (u: User, t: string) => void }) {
  const [register, setRegister] = useState(false),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError("");
    const d = new FormData(e.currentTarget);
    try {
      const r = await api<{ token: string; user: User }>(
        "/auth/" + (register ? "register" : "login"),
        "POST",
        Object.fromEntries(d),
      );
      onLogin(r.user, r.token);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <h2>{register ? "Create candidate account" : "Sign in"}</h2>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <form onSubmit={submit}>
        {register && (
          <Field label="Name">
            <input name="name" required minLength={2} maxLength={100} />
          </Field>
        )}
        <Field label="Email">
          <input name="email" type="email" autoComplete="username" required />
        </Field>
        <Field label="Password">
          <input
            name="password"
            type="password"
            autoComplete={register ? "new-password" : "current-password"}
            required
            minLength={register ? 12 : 1}
          />
        </Field>
        <button disabled={busy}>
          {busy ? "Signing in…" : register ? "Register" : "Sign in"}
        </button>
      </form>
      <button className="link" onClick={() => setRegister(!register)}>
        {register ? "Back to sign in" : "Register as a candidate"}
      </button>
      <p>
        Demo accounts: hr@demo.test, recruiter@demo.test, employee@demo.test,
        candidate@demo.test. Use DEMO_PASSWORD from your local .env.
      </p>
    </section>
  );
}
type Run = (fn: () => Promise<unknown>, message?: string) => Promise<void>;
function NameForm({
  label,
  initial = "",
  save,
}: {
  label: string;
  initial?: string;
  save: (name: string) => void;
}) {
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save(String(new FormData(e.currentTarget).get("name")));
      }}
    >
      <Field label={label}>
        <input
          name="name"
          defaultValue={initial}
          required
          minLength={2}
          maxLength={100}
        />
      </Field>
      <button>Save</button>
    </form>
  );
}
function NoteForm({
  label,
  save,
}: {
  label: string;
  save: (note: string) => void;
}) {
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save(String(new FormData(e.currentTarget).get("note")));
      }}
    >
      <Field label={label}>
        <textarea name="note" required maxLength={2000} />
      </Field>
      <button>Record note</button>
    </form>
  );
}
function JobForm({
  departments,
  skills,
  initial,
  save,
}: {
  departments: Department[];
  skills: Skill[];
  initial?: Job;
  save: (dto: unknown) => void;
}) {
  const [reqs, setReqs] = useState<Requirement[]>(initial?.requirements || []);
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const d = new FormData(e.currentTarget);
        save({
          title: d.get("title"),
          description: d.get("description"),
          departmentId: d.get("department"),
          open: d.get("open") === "on",
          requirements: reqs,
        });
      }}
    >
      <h3>{initial ? "Edit vacancy" : "Create vacancy"}</h3>
      <Field label="Title">
        <input
          name="title"
          required
          defaultValue={initial?.title}
          maxLength={150}
        />
      </Field>
      <Field label="Department">
        <select name="department" defaultValue={initial?.departmentId} required>
          <option value="">Select department</option>
          {departments.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
      </Field>
      <Field label="Description">
        <textarea
          name="description"
          defaultValue={initial?.description}
          required
          maxLength={6000}
        />
      </Field>
      <label>
        <input
          name="open"
          type="checkbox"
          defaultChecked={initial?.open ?? true}
        />{" "}
        Open for applications
      </label>
      <fieldset>
        <legend>Required skills & proficiency</legend>
        {skills.map((s) => {
          const r = reqs.find((r) => r.skillId === s.id);
          return (
            <div className="requirement" key={s.id}>
              <label>
                <input
                  type="checkbox"
                  checked={!!r}
                  onChange={(e) =>
                    setReqs(
                      e.target.checked
                        ? [...reqs, { skillId: s.id, proficiency: 2 }]
                        : reqs.filter((r) => r.skillId !== s.id),
                    )
                  }
                />
                {s.name}
              </label>
              {r && (
                <select
                  aria-label={s.name + " required proficiency"}
                  value={r.proficiency}
                  onChange={(e) =>
                    setReqs(
                      reqs.map((r) =>
                        r.skillId === s.id
                          ? { ...r, proficiency: Number(e.target.value) }
                          : r,
                      ),
                    )
                  }
                >
                  {levels.slice(1).map((l, i) => (
                    <option key={l} value={i + 1}>
                      {l}
                    </option>
                  ))}
                </select>
              )}
            </div>
          );
        })}
      </fieldset>
      <button disabled={!reqs.length}>Save vacancy</button>
    </form>
  );
}
function FactCard({
  fact: f,
  skills,
  staff,
  save,
}: {
  fact: Fact;
  skills: Skill[];
  staff: boolean;
  save: (dto: unknown) => void;
}) {
  const [suggestions, setSuggestions] = useState<
      { skillId: string; name: string; similarity: number }[]
    >([]),
    [error, setError] = useState("");
  return (
    <section className="fact">
      <h4>
        {f.kind}: {f.value} <span className="badge">{f.verification}</span>
      </h4>
      <blockquote>{f.quote}</blockquote>
      <p>
        Document {f.documentId} · section {f.sectionId} · mapping {f.mapping}
      </p>
      {staff && (
        <details>
          <summary>Correct / verify extracted fact</summary>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              save({
                value: d.get("value"),
                skillId: d.get("skillId") || null,
                verification: d.get("verification"),
                proficiency: d.get("proficiency")
                  ? Number(d.get("proficiency"))
                  : null,
                note: d.get("note"),
              });
            }}
          >
            <Field label="Corrected value">
              <input
                name="value"
                defaultValue={f.value}
                required
                maxLength={2000}
              />
            </Field>
            <Field label="Canonical skill">
              <select name="skillId" defaultValue={f.skillId || ""}>
                <option value="">Unmapped</option>
                {skills.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Evidence state">
              <select name="verification" defaultValue={f.verification}>
                {["Documented", "Unverified", "Ambiguous", "Rejected"].map(
                  (s) => (
                    <option key={s}>{s}</option>
                  ),
                )}
              </select>
            </Field>
            <Field label="Human-assessed proficiency">
              <select name="proficiency" defaultValue={f.proficiency || ""}>
                <option value="">Not established</option>
                {levels.slice(1).map((l, i) => (
                  <option key={l} value={i + 1}>
                    {l}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Correction / verification reason">
              <textarea name="note" required maxLength={2000} />
            </Field>
            <button>Save correction</button>
          </form>
          <button
            onClick={async () => {
              try {
                setSuggestions(
                  await api("/skills/search?q=" + encodeURIComponent(f.value)),
                );
                setError("");
              } catch (e) {
                setError((e as Error).message);
              }
            }}
          >
            Find semantic skill matches
          </button>
          {error && <p className="error">{error}</p>}
          {suggestions.map((s) => (
            <p key={s.skillId}>
              {s.name} · semantic similarity {s.similarity.toFixed(3)} ·
              requires human confirmation
            </p>
          ))}
        </details>
      )}
    </section>
  );
}
function InterviewForm({
  initial,
  save,
}: {
  initial?: Interview;
  save: (dto: unknown) => void;
}) {
  const local = initial
    ? new Date(
        new Date(initial.scheduledAt).getTime() -
          new Date().getTimezoneOffset() * 60000,
      )
        .toISOString()
        .slice(0, 16)
    : "";
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        const d = new FormData(e.currentTarget);
        save({
          scheduledAt: new Date(String(d.get("time"))).toISOString(),
          location: d.get("location"),
          notes: d.get("notes") || "",
        });
      }}
    >
      <Field label="Interview time (your local timezone)">
        <input
          type="datetime-local"
          name="time"
          defaultValue={local}
          required
        />
      </Field>
      <Field label="Location / meeting link">
        <input
          name="location"
          defaultValue={initial?.location}
          required
          maxLength={500}
        />
      </Field>
      <Field label="Recruiter-only notes">
        <textarea
          name="notes"
          defaultValue={initial?.notes || ""}
          maxLength={4000}
        />
      </Field>
      <button>
        {initial ? "Update interview / notes" : "Schedule interview"}
      </button>
    </form>
  );
}
function StatusForm({
  status,
  save,
}: {
  status: string;
  save: (dto: unknown) => void;
}) {
  const transitions: Record<string, string[]> = {
    Submitted: ["UnderReview", "Withdrawn"],
    UnderReview: ["Interview", "Rejected", "Withdrawn"],
    Interview: ["Offered", "Rejected", "UnderReview", "Withdrawn"],
    Offered: ["Hired", "Rejected", "Withdrawn"],
  };
  return transitions[status] ? (
    <form
      key={status}
      onSubmit={(e) => {
        e.preventDefault();
        save(Object.fromEntries(new FormData(e.currentTarget)));
      }}
    >
      <Field label="New status">
        <select name="status">
          {transitions[status].map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
      </Field>
      <Field label="Human decision rationale">
        <textarea name="rationale" required maxLength={2000} />
      </Field>
      <button>Record manual decision</button>
    </form>
  ) : (
    <p>This application is in a terminal state.</p>
  );
}
function Profile({
  skills,
  tick,
  run,
}: {
  skills: Skill[];
  tick: number;
  run: Run;
}) {
  const [records, setRecords] = useState<EmployeeSkill[]>([]);
  useEffect(() => {
    api<EmployeeSkill[]>("/profile").then(setRecords).catch(e => run(async () => { throw e; }));
  }, [tick]);
  return (
    <>
      <h1>My skills & development</h1>
      <p>Profile updates are self-reported and require HR verification.</p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          void run(() =>
            api("/profile/skills", "PUT", {
              skillId: d.get("skillId"),
              proficiency: Number(d.get("proficiency")),
              evidence: d.get("evidence"),
              developmentNeed: d.get("developmentNeed") || "",
            }),
          );
        }}
      >
        <Field label="Skill">
          <select name="skillId">
            {skills.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Self-reported proficiency">
          <select name="proficiency">
            {levels.slice(1).map((l, i) => (
              <option key={l} value={i + 1}>
                {l}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Evidence">
          <textarea name="evidence" required maxLength={2000} />
        </Field>
        <Field label="Development need">
          <textarea name="developmentNeed" maxLength={1000} />
        </Field>
        <button>Save skill / update existing skill</button>
      </form>
      <Table
        headers={[
          "Skill",
          "Level",
          "Evidence",
          "State",
          "Development need",
          "",
        ]}
        rows={records.map((r) => [
          skills.find((s) => s.id === r.skillId)?.name,
          levels[r.proficiency],
          r.evidence,
          r.verification,
          r.developmentNeed,
          <button
            onClick={() => run(() => api("/profile/skills/" + r.id, "DELETE"))}
          >
            Remove
          </button>,
        ])}
      />
    </>
  );
}
function DashboardView({
  tick,
  skillName,
}: {
  tick: number;
  skillName: (id: string) => string;
}) {
  const [data, setData] = useState<Dashboard | null>(null),
    [error, setError] = useState("");
  useEffect(() => {
    api<Dashboard>("/dashboard")
      .then(setData)
      .catch((e) => setError(e.message));
  }, [tick]);
  return (
    <>
      <h1>Workforce planning</h1>
      <p>
        Aggregate skill coverage and development needs. No individual
        performance scores.
      </p>
      {error && <p className="error">{error}</p>}
      {data && (
        <>
          <h2>Departments</h2>
          <Table
            headers={["Department", "Employees", "Open vacancies"]}
            rows={data.departments.map((d) => [
              d.name,
              d.employees,
              d.vacancies,
            ])}
          />
          <h2>Skill coverage</h2>
          <Table
            headers={[
              "Skill",
              "Documented employees",
              "Unverified employees",
              "Vacancies requiring skill",
              "Departments without documented coverage",
            ]}
            rows={data.coverage.map((s) => [
              s.name,
              s.documentedEmployees,
              s.unverifiedEmployees,
              s.vacanciesRequiring,
              s.departmentsWithoutDocumentedCoverage,
            ])}
          />
          <h2>Development needs</h2>
          <Table
            headers={["Skill", "Employees reporting need"]}
            rows={data.development.map((d) => [skillName(d.skillId), d.count])}
          />
          <h2>Recruitment states</h2>
          <Table
            headers={["State", "Applications"]}
            rows={data.applicationStates.map((s) => [s.status, s.count])}
          />
        </>
      )}
    </>
  );
}
function Employees({
  departments,
  skills,
  tick,
  run,
}: {
  departments: Department[];
  skills: Skill[];
  tick: number;
  run: Run;
}) {
  const [employees, setEmployees] = useState<(User & { active: boolean })[]>(
      [],
    ),
    [inventory, setInventory] = useState<EmployeeSkill[]>([]),
    [selected, setSelected] = useState("");
  useEffect(() => {
    api<(User & { active: boolean })[]>("/employees").then(setEmployees).catch(e => run(async () => { throw e; }));
  }, [tick]);
  useEffect(() => {
    if (selected)
      api<EmployeeSkill[]>("/profile/employees/" + selected).then(setInventory).catch(e => run(async () => { throw e; }));
  }, [selected, tick]);
  return (
    <>
      <h1>Employee administration</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void run(() =>
            api(
              "/employees",
              "POST",
              Object.fromEntries(new FormData(e.currentTarget)),
            ),
          );
        }}
      >
        <Field label="Fictional name">
          <input name="name" required maxLength={100} />
        </Field>
        <Field label="Email">
          <input name="email" type="email" required />
        </Field>
        <Field label="Initial password">
          <input name="password" type="password" required minLength={12} />
        </Field>
        <Field label="Department">
          <select name="departmentId">
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
        </Field>
        <button>Create employee</button>
      </form>
      {employees.map((u) => (
        <section key={u.id}>
          <h3>
            {u.name} · {u.email}
          </h3>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(() =>
                api("/employees/" + u.id, "PUT", {
                  departmentId: d.get("departmentId"),
                  active: d.get("active") === "on",
                }),
              );
            }}
          >
            <Field label="Department">
              <select name="departmentId" defaultValue={u.departmentId}>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </Field>
            <label>
              <input type="checkbox" name="active" defaultChecked={u.active} />{" "}
              Active account
            </label>
            <button>Save employee</button>
          </form>
          <button onClick={() => setSelected(u.id)}>
            Review skill inventory
          </button>
        </section>
      ))}
      {selected && (
        <Table
          headers={[
            "Skill",
            "Evidence",
            "Proficiency",
            "Development need",
            "Verification",
          ]}
          rows={inventory.map((i) => [
            skills.find((s) => s.id === i.skillId)?.name,
            i.evidence,
            levels[i.proficiency],
            i.developmentNeed,
            <select
              aria-label="Verification"
              value={i.verification}
              onChange={(e) =>
                run(() =>
                  api(
                    "/profile/employees/skills/" + i.id + "/verification",
                    "PUT",
                    { verification: e.target.value },
                  ),
                )
              }
            >
              {["Documented", "Unverified", "Ambiguous"].map((v) => (
                <option key={v}>{v}</option>
              ))}
            </select>,
          ])}
        />
      )}
    </>
  );
}
function Taxonomy({ skills, run }: { skills: Skill[]; run: Run }) {
  const [result, setResult] = useState<{ name: string; similarity: number }[]>(
    [],
  );
  return (
    <>
      <h1>Canonical skills taxonomy</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          void run(() =>
            api("/skills", "POST", {
              name: d.get("name"),
              description: d.get("description"),
              aliases: String(d.get("aliases"))
                .split(",")
                .map((s) => s.trim())
                .filter(Boolean),
            }),
          );
        }}
      >
        <Field label="Canonical name">
          <input name="name" required maxLength={100} />
        </Field>
        <Field label="Meaning">
          <textarea name="description" required maxLength={1000} />
        </Field>
        <Field label="Aliases (comma separated)">
          <input name="aliases" />
        </Field>
        <button>Add skill</button>
      </form>
      <button
        onClick={() =>
          run(
            () => api("/skills/embeddings", "POST"),
            "Canonical embeddings refreshed",
          )
        }
      >
        Refresh local taxonomy embeddings
      </button>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const q = new FormData(e.currentTarget).get("q");
          void run(
            async () =>
              setResult(
                await api("/skills/search?q=" + encodeURIComponent(String(q))),
              ),
            "Semantic search complete",
          );
        }}
      >
        <Field label="Semantic skill query">
          <input name="q" required maxLength={1000} />
        </Field>
        <button>Search pgvector</button>
      </form>
      {result.map((s, i) => (
        <p key={i}>
          {s.name} · similarity {s.similarity.toFixed(3)} · human confirmation
          required
        </p>
      ))}
      <Table
        headers={["Name", "Meaning", "Aliases"]}
        rows={skills.map((s) => [
          s.name,
          s.description,
          JSON.parse(s.aliases).join(", "),
        ])}
      />
    </>
  );
}
function RemoteData({
  path,
  title,
  tick,
}: {
  path: string;
  title: string;
  tick: number;
}) {
  const [data, setData] = useState<unknown>(),
    [error, setError] = useState(""), [refresh, setRefresh] = useState(0);
  useEffect(() => {
    api(path)
      .then(setData)
      .catch((e) => setError(e.message));
  }, [path, tick, refresh]);
  return (
    <>
      <h1>{title}</h1>
      <button onClick={() => {setError(""); setRefresh(n => n + 1)}}>Refresh availability</button>
      {error && <p className="error">{error}</p>}
      <pre>{JSON.stringify(data, null, 2)}</pre>
    </>
  );
}
function Audit({ tick }: { tick: number }) {
  const [error, setError] = useState("");
  const [events, setEvents] = useState<
    {
      id: string;
      action: string;
      entity: string;
      actorId: string | null;
      createdAt: string;
    }[]
  >([]);
  useEffect(() => {
    api<typeof events>("/audit").then(setEvents).catch(e => setError(e.message));
  }, [tick]);
  return (
    <>
      <h1>Audit trail</h1>
      {error && <p className="error">{error}</p>}
      <p>
        Most recent 200 events. Personal document text is excluded from audit
        logs.
      </p>
      <Table
        headers={["Time", "Action", "Entity", "Actor"]}
        rows={events.map((e) => [
          new Date(e.createdAt).toLocaleString(),
          e.action,
          e.entity,
          e.actorId || "Background worker",
        ])}
      />
    </>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
