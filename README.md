# AI Workforce Planning, Recruitment & Talent Intelligence

A locally demonstrable platform with React/TypeScript, an authoritative ASP.NET Core 10 API, EF Core/PostgreSQL persistence, pgvector semantic taxonomy retrieval, a private FastAPI document service and Ollama-hosted DeepSeek-R1 Distill inference. All demonstration identities and resumes are fictional.

## Local setup

Prerequisites: Docker Desktop/Engine with Compose v2, Python 3.10+ for the convenience scripts, approximately 6 GB of available Docker memory and 8 GB free disk for the small-model demo. Native .NET/Node installations are optional; Docker builds everything. Initial image/dependency/model downloads need internet access. No paid AI service or cloud provisioning is required.

```sh
cd '/Users/keenosmith/Expansion Projects/Talent & Rec Intelligence Platform/deepseek-ai-workforce-planning'
python3 scripts/setup-env.py
python3 scripts/make-resume.py
docker compose --profile models up -d --build
docker compose exec ollama ollama pull deepseek-r1:1.5b
docker compose exec ollama ollama pull nomic-embed-text
```

Open [http://localhost:5180](http://localhost:5180). Migrations and fictional seeding run automatically. `setup-env.py` preserves an existing `.env`; on a new checkout it generates random DB/JWT/internal keys and sets `DEMO_PASSWORD=FictionalDemo!2026`. Never commit `.env`. The API, PostgreSQL, private document service and Ollama have no published host ports. Change `WEB_PORT` to avoid a local port conflict.

| Account | Role | Password on new setup |
| --- | --- | --- |
| `hr@demo.test` | HR administrator | `FictionalDemo!2026` |
| `recruiter@demo.test` | Recruiter | `FictionalDemo!2026` |
| `candidate@demo.test` | Candidate | `FictionalDemo!2026` |
| `employee@demo.test` | Employee | `FictionalDemo!2026` |

Set `DEMO_PASSWORD` **before the first startup** to choose another password. Seeding only runs when the database has no users, so changing it later does not reset existing credentials. Candidate registration is available on the sign-in screen and can never create privileged users. JWTs are kept in browser memory; refresh or sign-out requires another sign-in.

### Host Ollama instead of container inference

For Apple Silicon, native Ollama can use Metal and is generally faster than CPU-only Docker inference. Install Ollama, run `ollama pull deepseek-r1:1.5b` and `ollama pull nomic-embed-text`, then set `OLLAMA_URL=http://host.docker.internal:11434` in `.env` and run `docker compose up -d --build` without the models profile. The host daemon must accept connections from Docker; configure `OLLAMA_HOST` appropriately and keep access restricted to trusted local clients. Linux Compose supplies the host-gateway mapping. Recreate `ai` after changing model configuration: `docker compose up -d --force-recreate ai`.

The default `deepseek-r1:1.5b` is the Qwen 1.5B distilled variant, practical for CPU demonstration but less capable than larger variants. Use `OLLAMA_MODEL=deepseek-r1:7b` with more available memory (roughly 8–12 GB or more for inference plus the services). Context length and quantization affect actual memory. The default requests non-thinking structured output (`OLLAMA_THINK=false`) with four CPU threads (`OLLAMA_THREADS=4`) to keep evidence matching practical on a local CPU. Both settings are configurable and recorded in metadata; thinking controls vary by model. See [Ollama thinking controls](https://github.com/ollama/ollama/blob/main/docs/capabilities/thinking.mdx). The 768-dimension `nomic-embed-text` embedding model is separate from DeepSeek. Switching to another embedding family requires compatible dimensions and re-embedding the entire taxonomy; the API isolates vectors by embedding model name.

DeepSeek-R1 weights use the MIT license; Qwen distill variants derive from Qwen models with Apache 2.0 licensing. Llama distill variants have different upstream terms. Check the selected weights and upstream license before redistribution. See the [official Ollama model card](https://ollama.com/library/deepseek-r1), [DeepSeek repository](https://github.com/deepseek-ai/DeepSeek-R1), and [embedding model card](https://ollama.com/library/nomic-embed-text). Record model digests from saved analysis metadata for reproducibility.

To populate a ready-to-inspect recruitment workspace using real APIs and inference, run `python3 scripts/populate-demo.py` after model setup. This idempotent script uploads the fictional resume, records explicit fictional human corrections/review actions, generates real analysis versions and leaves the application at Interview. It never fabricates an AI result; unavailable models remain visibly failed. The original Platform engineer vacancy stays available for the walkthrough below.

## Complete walkthrough

1. Sign in as HR. **Departments** → create a department. **Jobs** → create a vacancy, choose department and skill requirements with proficiency. **Employees** provides employee creation, department assignment, activation and skill verification. **Taxonomy** provides canonical skill creation and aliases. Click **Refresh local taxonomy embeddings** after model setup; use semantic search to retrieve real pgvector matches.
2. Sign out and sign in as the candidate. Browse **Jobs**, apply, then open the application. Upload `demo/fictional-resume.docx` or `.pdf`. Uploaded files are privately stored and background processing is visible in **Documents & processing**. Source preview retains numbered page/paragraph sections; the original is downloadable only with authorization.
3. Sign in as recruiter. Open **Applications** and inspect the extracted facts and original source. Each fact starts Unverified. Expand **Correct / verify extracted fact** to fix value/mapping, assess proficiency, choose Documented/Ambiguous/Rejected and record a reason. Original quotations remain intact. Use semantic search for uncertain mappings; its similarity is taxonomy similarity, never applicant scoring. Add a missed fact from an exact stored quote if the conservative extractor missed it.
4. The upload queues genuine local DeepSeek inference automatically. **Models** reports model availability. If inference is unavailable or its structured output fails evidence validation, the job retries three times and visibly fails. No substitute success is manufactured. Install/fix the local model and use **Retry**, or **Run local DeepSeek analysis**.
5. Inspect analysis evidence states, referenced document sections, uncertainty, human-assessed proficiency and model/prompt metadata. Correcting facts or changing vacancy requirements makes older analyses **STALE**. Rerun to save a fresh evidence version. Record a human review note.
6. Manually move Submitted → UnderReview. Schedule a future interview; edit it to record recruiter-only notes. Move to Interview, then manually record Offered/Hired or Rejected with a rationale. Every state change is a separate human decision. Manually recording Hired closes the vacancy so the HR vacancy dashboard reflects it. The candidate sees application status and interview time/location, but never internal interview notes, analyses or decision rationales.
7. Sign in as HR to view **Dashboard** for departmental vacancies, documented coverage, departments lacking coverage, aggregate development needs and recruitment states. Review **Audit**. No individual performance scores exist.
8. Sign in as employee → **My skills**. Add/update a self-reported skill, evidence and development need. HR reviews it in **Employees**. Employee updates reset verification to Unverified; HR verification affects the aggregate dashboard.
9. Candidates or staff can delete a document or application. Document deletion removes its facts and saved analyses; application deletion erases all attached recruitment records and files. Documents also expire automatically after 180 days.

## Architecture and persistence

```text
React → nginx (same-origin /api proxy) → ASP.NET Core API → PostgreSQL/pgvector
                                          ↓
                                 durable processing jobs
                                          ↓
                              private FastAPI → Ollama
```

- `backend/Domain`: relational entities. `Application`: validated DTOs, password/token services, access boundary, intelligence client, semantic retrieval, durable worker. `Infrastructure`: EF context, migrations, design-time factory and fictional seed. `Api`: authorized REST controllers.
- `frontend/src`: functional role-specific screens and TypeScript API contracts. `ai`: stateless bounded PDF/DOCX parsing, source-preserving extraction, local embeddings, inference, schema/evidence validation and targeted tests.
- Persistent records include users/roles, departments, vacancies/requirements, applications, private documents, source sections, extracted facts/mappings, employee inventory/development needs, versioned analyses, review corrections, interviews, manual decisions and audit events. Embeddings are stored in a real `vector(768)` column with cosine HNSW index and model identifier.
- The worker serializes processing with a PostgreSQL session advisory lock, recovers interrupted jobs after restart, performs three attempts with increasing delay and preserves failure states. Evidence has an optimistic concurrency token; edits submitted against an old version return 409.
- AI receives canonical requirements and factual IDs/statuses only. DeepSeek emits schema-constrained JSON evidence selections. The service rejects unsupported/omitted references and constructs bounded explanations from checked facts. This deliberately constrains generative language: there is no unchecked model prose, hiring ranking, suitability score, recommendation or autonomous application advancement. Output and model metadata remain separate from verified facts and human decisions.

See [API contracts](docs/API.md), [security and data handling](docs/SECURITY.md), and [optional OpenTofu deployment](infra/README.md).

See [the executed verification record](docs/VERIFICATION.md) for actual checks and their limits.

## Focused verification

```sh
# Real API journey; accepts an explicitly failed local model state
python3 scripts/smoke.py
# Additionally requires actual DeepSeek inference and pgvector retrieval
python3 scripts/smoke.py --require-ai
# Document and evidence safety checks in the built runtime
docker run --rm -v "$PWD/ai/tests:/tests:ro" -v "$PWD/demo:/fixtures:ro" workforce-ai \
  sh -c 'PYTHONPATH=/app pytest -q -p no:cacheprovider /tests'
# Optional native development checks
cd frontend
npm ci
npm run lint
npm run build
```

The smoke script creates fictional departments/vacancies/accounts, uploads a resume, waits for persisted jobs, verifies access isolation, corrects/version-checks evidence, schedules/notes interviews, records manual decisions, verifies employee skills and dashboard coverage, then tests document/application erasure. It removes its application; the fictional departments/vacancies and isolation account remain as verification history. CI runs .NET compilation, TypeScript lint/build, npm audit, Python tests and the Docker-backed smoke journey. CI does not assert model inference because it does not download inference weights.

For native backend development install .NET SDK 10, then `dotnet build backend/Workforce.csproj`. Migrations are committed; use `dotnet ef migrations add <Name> --project backend` with the EF 10 tool and design-time factory when changing entities. Run the UI dev server with `npm run dev` inside `frontend`; it proxies API calls to the running Compose stack at port 5180. For a different port adjust `frontend/vite.config.ts`.

## Operations and limitations

```sh
docker compose ps
docker compose logs --tail=100 api ai
docker compose restart api
# Stop while preserving all persisted data
docker compose --profile models down
```

Back up the PostgreSQL database and private document volume together. `docker compose down -v` erases volumes, including model downloads; only use for an intentional full reset. The health endpoint checks the database, and AI health checks the parser service; model availability is independently reported. Slow CPU inference may take minutes; the client shows Running and polls every four seconds.

The extractor is conservative and source-based; it does not reconstruct arbitrary resume layouts or infer proficiency/history. It supports text PDF/DOCX, not scanned-image OCR, and provides human correction/manual fact entry for misses. Model outputs can fail strict validation and need a human retry. The demo is designed for local use; external deployment additionally needs TLS, organization authentication and retention policy, encrypted backups, malware scanning/parser isolation and operational monitoring. The application audit table is not an external tamper-proof ledger. Visual redesign and user acceptance testing remain separate work.

For an unseeded optional deployment, bootstrap the first administrator once with:

```sh
# Use environment values chosen locally; keep them out of shell history in shared environments.
Bootstrap__Enabled=true Bootstrap__Email=admin@example.test Bootstrap__Password='choose-a-new-long-password' \
  dotnet backend/bin/Release/net10.0/Workforce.dll
# Container equivalent; environment/configuration names are the same:
docker compose run --rm -e Bootstrap__Enabled=true -e Bootstrap__Email -e Bootstrap__Password api
```

The first command also requires the normal API configuration variables. Bootstrap refuses if an HR administrator exists and exits after creating the administrator; it never starts a web service. Set `SEED_ENABLED=false` for an unseeded database. No real identities are included in this repository.
