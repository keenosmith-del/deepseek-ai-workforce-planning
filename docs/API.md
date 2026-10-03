# API contracts and access

The API uses camelCase JSON. React contracts are in `frontend/src/api.ts`; validated input contracts are in `backend/Application/Contracts.cs`. Proficiency levels are 1 awareness, 2 working, 3 advanced, 4 expert. JWT lifetime is two hours; UI tokens are held in memory. Signing out clears the token, and browser reload requires sign-in. Disabled accounts fail bearer validation immediately.

| Route | Method | Permission / purpose |
| --- | --- | --- |
| `/api/auth/login`, `/api/auth/register` | POST | Public; registration always creates Candidate |
| `/api/auth/me` | GET | Signed-in user |
| `/api/departments` | GET / POST | All / HRAdmin |
| `/api/departments/{id}` | PUT | HRAdmin |
| `/api/skills` | GET / POST | All / HRAdmin |
| `/api/skills/embeddings` | POST | HRAdmin; local model builds 768-d taxonomy embeddings |
| `/api/skills/search?q=...` | GET | Authenticated; pgvector cosine search, never person scoring |
| `/api/jobs` | GET / POST | Open vacancies for all; staff see closed / staff create |
| `/api/jobs/{id}` | PUT | Staff; invalidates analyses for affected applications |
| `/api/jobs/{id}/apply` | POST | Candidate; one application per vacancy |
| `/api/applications` | GET | Own records for candidates; all for staff |
| `/api/applications/{id}` | GET / DELETE | Owner or staff; staff-only notes, analyses and decisions withheld from candidates |
| `/api/applications/{id}/documents` | POST | Owner or staff; multipart `file`, PDF/DOCX only |
| `/api/applications/{id}/documents/{docId}/download` | GET | Owner or staff; attachment, never public static file |
| `/api/applications/{id}/documents/{docId}` | DELETE | Owner or staff; erases extracted facts and analyses |
| `/api/applications/{id}/facts` | POST | Staff; add missed fact using exact quote from a stored source section |
| `/api/applications/{id}/facts/{factId}` | PUT | Staff; evidenceVersion concurrency check; reason required |
| `/api/applications/{id}/analysis` | POST | Staff; queued real inference |
| `/api/applications/{id}/processing/{jobId}/retry` | POST | Staff; explicit retry of failed operation |
| `/api/applications/{id}/reviews` | POST | Staff; review action |
| `/api/applications/{id}/interviews` | POST | Staff; schedule |
| `/api/applications/{id}/interviews/{interviewId}` | PUT | Staff; reschedule / record notes |
| `/api/applications/{id}/status` | PUT | Staff; manually record state and rationale |
| `/api/profile`, `/api/profile/skills` | GET / PUT | Employee or HRAdmin; own inventory |
| `/api/profile/skills/{id}` | DELETE | Own inventory |
| `/api/profile/employees/{id}` | GET | HRAdmin |
| `/api/profile/employees/skills/{id}/verification` | PUT | HRAdmin; verify employee evidence |
| `/api/employees` | GET / POST | HRAdmin |
| `/api/employees/{id}` | PUT | HRAdmin; department and active flag |
| `/api/dashboard`, `/api/audit` | GET | HRAdmin |
| `/api/models` | GET | Staff; explicit local inference availability |
| `/health` | GET | PostgreSQL connectivity |

State transitions: Submitted → UnderReview → Interview → Offered → Hired. Rejected can follow UnderReview, Interview or Offered. Withdrawn can follow any nonterminal state. Interview can return to UnderReview. All transitions require a human-authored rationale and record an immutable decision row. AI worker never changes status.

Validation failures return 400 with validation details, inaccessible object IDs return 404, duplicate or concurrent edits return 409, authorization returns 401/403, unavailable dependencies return 503 with a trace ID. Document and inference errors are stored in processing jobs; three attempts with increasing delays precede Failed. Jobs interrupted by restart are recovered under a PostgreSQL advisory lock. The single worker avoids duplicate concurrent inference and processes persisted queues across restarts.
