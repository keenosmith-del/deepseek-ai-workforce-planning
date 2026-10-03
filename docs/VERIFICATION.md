# Verification record — 3 October 2026

Executed locally against the real Docker Compose services and PostgreSQL database:

- ASP.NET Core 10 release builds completed with warnings treated as errors. EF Core generated and applied the initial schema; the custom pgvector migration installed vector columns and HNSW cosine indexing.
- React/TypeScript production build passed. TypeScript lint passed. npm audit reported zero known vulnerabilities for the installed lockfile after the Vite patch update.
- Ten focused Python checks passed: source-preserving PDF/DOCX parsing, private-service access, malformed/macro archive rejection, alias word boundaries, unsupported/omitted evidence rejection, unverified/missing states and forbidden extra hiring attributes.
- `python3 scripts/smoke.py` passed with genuinely unavailable local models, verifying persisted failure/retry states without fabricated analysis success.
- `python3 scripts/smoke.py --require-ai` passed after installing actual `deepseek-r1:1.5b` and `nomic-embed-text` weights. It verified the full persisted recruitment journey, evidence correction/concurrency and stale versions, fresh grounded inference with model digest, pgvector retrieval, permission and candidate isolation checks, interview scheduling/notes, manually recorded decisions and vacancy closure, employee skill verification, dashboards, audit and erasure of documents/facts/analyses/reviews/application records.
- Browser checks verified HR and candidate sign-in, role-specific navigation, the persisted workforce dashboard and creation of a department and vacancy through frontend forms. The complete automated journey exercised the actual same-origin HTTP API; it is not a full automated browser regression suite.
- OpenTofu formatting, provider initialization and configuration validation passed using OpenTofu 1.9.0 and the locked Docker provider. Optional infrastructure was not applied.
- `scripts/populate-demo.py` created a retained, real persisted fictional application at Interview. It includes an original resume, extracted source sections, a documented human correction, stale and fresh real analysis versions, model metadata, human review, interview notes and manual state decisions.
- Python compilation and repository whitespace checks passed.

The initial small model failed strict evidence validation when references from different skills were allowed in the same field. Those failures remained Failed, with no analysis saved. Final inference uses non-thinking mode, four CPU threads, compact grouped references and per-skill constrained JSON fields; independent membership/coverage validation remains enabled. Real inference completed successfully after these changes. Explanatory prose uses checked evidence templates, rather than unvalidated free-form model claims.

External requirements for another machine: Docker/Compose, initial dependency/model downloads and enough memory for the selected models. Native Ollama is optional for accelerated host inference. OCR for scanned PDFs is unsupported; supply selectable text or DOCX. Broader deployment requires organization-specific authentication, TLS, backup/retention policies and parser/malware hardening as documented in SECURITY.md. GitHub Actions is committed configuration; it has not been executed on GitHub from this session.
