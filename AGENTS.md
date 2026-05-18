# Agent Instructions

> **Universal directives for all AI agents working in this repository.**
> Stack-specific rules live in [src/AGENTS.md](src/AGENTS.md) — closest file wins per the [AGENTS.md spec](https://agents.md/).

## Variables

| Variable | Description | Example (Windows) | Example (macOS/Linux) |
|----------|-------------|--------------------|-----------------------|
| `${REPOS_ROOT}` | Root directory where tools/repos are cloned | `E:\tools` | `~/tools` |

> **Setup**: Set the `REPOS_ROOT` environment variable on your machine, or mentally substitute the correct path when reading these instructions.

---

## Vertical Slice Implementation

**Implement features as vertical slices — UI to datastore — not horizontal layers.**

When building a feature, complete the full stack for that feature before starting the next:

```
UI Component → API Client / Hook → API Endpoint → Service Layer → Data Access → Database Schema
```

### Rules

1. **One feature at a time** — Finish the entire vertical before moving on
2. **Start from the outside in** — Define the user-facing contract (UI/API shape) first, then work inward
3. **Tests at every layer** — Each slice includes tests for UI, API, service, and data access
4. **Commit per slice** — Each vertical slice should be a single, deployable commit
5. **No partial layers** — Never build "all the API endpoints" then "all the UI" — that's horizontal

### Workflow

```
1. Define the user story / acceptance criteria
2. Write BDD feature file (.feature) for the slice
3. Build UI component (with mock data / stub API)
4. Build API endpoint + service layer
5. Build data access + schema migration
6. Wire everything together
7. Run full vertical test suite (unit + integration + E2E)
8. Commit
```

---

## Workflow: Plan Before Coding

**STOP and PLAN before writing any code.**

1. **Understand the task** — Read the task/issue thoroughly
2. **Check ADO work items** — If using Azure DevOps, read the work item description and acceptance criteria
3. **Review existing code** — Understand the current implementation and patterns
4. **Plan the approach** — Outline what files need changes and why
5. **Only then implement** — One vertical slice at a time

---

## Session Management

### Maintain a `session.md` File

Every project should have a `session.md` (or `docs/session.md`) tracking:
- **Last completed task** — Work item ID, title, commit hash
- **Current task** — What's in progress
- **Next tasks** — What's queued up
- **Blockers** — Any issues preventing progress
- **Notes** — Important decisions or context

### When Starting Work

1. **Read `session.md`** to understand where we left off
2. **Read the ADO work item** (if applicable) for full context
3. **Update status** as you progress

### When Finishing a Task

1. **Update `session.md`** — Mark task complete with commit hash
2. **Prompt for next work** — Always end with a clear prompt:
   > *Task 474 is complete. The next task is **Task 475 - [title]**. Ready to proceed?*
3. **Never silently finish** — The user should always know what comes next

---

## Development Methodology: BDD/TDD First

**NO CODE WITHOUT TESTS FIRST.** This is non-negotiable.

### Implementation Order

```
Write failing test → Write minimum code to pass → Refactor
```

1. **BDD**: Write Gherkin `.feature` files defining expected behavior BEFORE implementation
2. **TDD**: Write unit tests that fail BEFORE writing production code
3. **Red-Green-Refactor**: Fail first, pass minimally, then clean up
4. **No exceptions**: Even "simple" changes get tests first

### Test Coverage

- **90% minimum** code coverage for all new code
- Unit tests for ALL business logic
- Integration tests for ALL API endpoints
- Security tests for EVERY endpoint
- Accessibility tests for EVERY UI component
- E2E tests for critical user flows (at least the happy path per vertical slice)

---

## Security Requirements (OWASP)

All code must follow **OWASP WSTG v4.2** and address **OWASP Top 10:2025**.

**References:**
- OWASP WSTG v4.2: https://owasp.org/www-project-web-security-testing-guide/v42/
- OWASP Top 10:2025: https://owasp.org/Top10/2025/
- **OWASP Cheat Sheet Series** (local): `${REPOS_ROOT}/owasp-cheatsheets/cheatsheets/`
- **OWASP Cheat Sheet Series** (web): https://cheatsheetseries.owasp.org/

### OWASP Cheat Sheets (Consult Before Implementation)

**Local copies are available at `${REPOS_ROOT}/owasp-cheatsheets/cheatsheets/`** — use these for faster, offline access.

| Feature Area | Local Cheat Sheet File |
|--------------|------------------------|
| **Authentication** | `Authentication_Cheat_Sheet.md`, `Password_Storage_Cheat_Sheet.md`, `Session_Management_Cheat_Sheet.md`, `Multifactor_Authentication_Cheat_Sheet.md` |
| **Authorization** | `Authorization_Cheat_Sheet.md`, `Access_Control_Cheat_Sheet.md`, `Insecure_Direct_Object_Reference_Prevention_Cheat_Sheet.md` |
| **Input Validation** | `Input_Validation_Cheat_Sheet.md`, `Injection_Prevention_Cheat_Sheet.md` |
| **SQL/Database** | `SQL_Injection_Prevention_Cheat_Sheet.md`, `Query_Parameterization_Cheat_Sheet.md`, `Database_Security_Cheat_Sheet.md` |
| **XSS Prevention** | `Cross_Site_Scripting_Prevention_Cheat_Sheet.md`, `DOM_based_XSS_Prevention_Cheat_Sheet.md`, `DOM_Clobbering_Prevention_Cheat_Sheet.md` |
| **CSRF Protection** | `Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.md` |
| **API Security** | `REST_Security_Cheat_Sheet.md`, `GraphQL_Cheat_Sheet.md`, `Web_Service_Security_Cheat_Sheet.md` |
| **Cryptography** | `Cryptographic_Storage_Cheat_Sheet.md`, `Key_Management_Cheat_Sheet.md`, `Transport_Layer_Security_Cheat_Sheet.md` |
| **File Handling** | `File_Upload_Cheat_Sheet.md` |
| **Error Handling** | `Error_Handling_Cheat_Sheet.md` |
| **Logging** | `Logging_Cheat_Sheet.md`, `Logging_Vocabulary_Cheat_Sheet.md` |
| **HTTP Security** | `HTTP_Headers_Cheat_Sheet.md`, `HTTP_Strict_Transport_Security_Cheat_Sheet.md`, `Content_Security_Policy_Cheat_Sheet.md` |
| **Multi-Tenancy** | `Multi_Tenant_Security_Cheat_Sheet.md` |
| **Secrets** | `Secrets_Management_Cheat_Sheet.md` |
| **Microservices** | `Microservices_Security_Cheat_Sheet.md`, `Docker_Security_Cheat_Sheet.md`, `Kubernetes_Security_Cheat_Sheet.md` |
| **AI/LLM** | `AI_Agent_Security_Cheat_Sheet.md`, `LLM_Prompt_Injection_Prevention_Cheat_Sheet.md`, `Secure_AI_Model_Ops_Cheat_Sheet.md` |
| **CI/CD** | `CI_CD_Security_Cheat_Sheet.md`, `Software_Supply_Chain_Security_Cheat_Sheet.md` |
| **Cloud/IaC** | `Secure_Cloud_Architecture_Cheat_Sheet.md`, `Infrastructure_as_Code_Security_Cheat_Sheet.md`, `Serverless_FaaS_Security_Cheat_Sheet.md` |
| **Threat Modeling** | `Threat_Modeling_Cheat_Sheet.md`, `Attack_Surface_Analysis_Cheat_Sheet.md`, `Abuse_Case_Cheat_Sheet.md` |

### OWASP Top 10:2025 Compliance

| Rank | Vulnerability | Key Mitigations |
|------|---------------|-----------------|
| **A01** | Broken Access Control | Deny by default, verify ownership, log failures |
| **A02** | Security Misconfiguration | Security headers, remove unused features |
| **A03** | Software Supply Chain Failures | Verify packages, use lockfiles, audit deps |
| **A04** | Cryptographic Failures | AES-256, Argon2id, TLS 1.2+, no hardcoded secrets |
| **A05** | Injection | Parameterized queries, input validation |
| **A06** | Insecure Design | Threat modeling, secure design patterns |
| **A07** | Authentication Failures | MFA, rate limiting, secure sessions |
| **A08** | Software/Data Integrity | Verify signatures, validate serialized data |
| **A09** | Logging & Alerting Failures | Log security events, protect logs |
| **A10** | Mishandling Exceptions | No stack traces leaked, fail securely |

### Security Checklist (Pre-Merge)

- [ ] All endpoints verify resource ownership (no IDOR)
- [ ] Security headers configured, no debug info in prod
- [ ] Dependencies audited, lockfiles used
- [ ] Strong encryption, no hardcoded secrets
- [ ] Parameterized queries, no XSS
- [ ] Threat model reviewed for new features
- [ ] Auth has rate limiting, secure session config
- [ ] Serialized data validated, signatures verified
- [ ] Security events logged (auth failures, access denied)
- [ ] Exceptions handled securely, no stack traces leaked

---

## Observability: Telemetry, Metrics & Logging

**Every application must be observable.** If you can't measure it, you can't fix it.

### Guiding Principles

1. **Instrument from day one** — Don't bolt on observability after launch
2. **Use OpenTelemetry (OTel)** — Prefer the vendor-neutral standard for traces, metrics, and logs
3. **Correlate everything** — Every log, metric, and trace must share a correlation/trace ID
4. **Structured over unstructured** — Always emit structured (key-value) logs, never free-text strings
5. **Low-cardinality metrics** — Labels must be bounded. Never use user IDs, request IDs, or URLs as metric labels

### Log Levels

| Level | When to use |
|-------|-------------|
| `Trace` | Ultra-verbose diagnostics — off in production |
| `Debug` | Developer-useful detail (cache hit/miss) — off in production by default |
| `Information` | Normal operations worth recording (request completed, job ran) |
| `Warning` | Unexpected but recoverable (retry, fallback, deprecated usage) |
| `Error` | Failure affecting the current operation but not the process |
| `Critical/Fatal` | Process-level failure, crash, unrecoverable state |

### What to Log

- Request start/end with duration
- Authentication and authorization outcomes (success and failure)
- External dependency calls (database, HTTP, queue) with duration and status
- Background job start/complete/fail
- Configuration changes at startup
- Feature flag evaluations

### What NEVER to Log

- Passwords, tokens, API keys, secrets
- Full credit card or SSN numbers
- PII unless explicitly consented and pseudonymized
- Request/response bodies in production (unless redacted)
- Health-check noise at `Information` level

### Distributed Tracing Rules

- Propagate trace context (`traceparent` / W3C Trace Context) across all service boundaries
- Create spans for every meaningful unit of work
- Add span attributes for domain-relevant data
- Record errors on spans — set status to `Error` and attach exception details
- Name spans clearly — `POST /api/orders`, `sql SELECT orders`

### Metrics Rules

- Use OTel instruments: Counter, Histogram, UpDownCounter, Gauge
- Name with dots following OTel semantic conventions
- Keep labels low-cardinality
- Capture **RED** metrics for services: Rate, Errors, Duration
- Capture **USE** metrics for resources: Utilization, Saturation, Errors

### Standard Metrics (Every Service)

| Metric | Type | Labels | Purpose |
|--------|------|--------|---------|
| `http.server.request.duration` | Histogram | `method`, `route`, `status_code` | Request latency |
| `http.server.active_requests` | UpDownCounter | `method`, `route` | Concurrency |
| `app.errors.total` | Counter | `type`, `operation` | Error rate |
| `db.client.operation.duration` | Histogram | `operation`, `collection` | DB latency |
| `app.queue.depth` | Gauge | `queue_name` | Queue backlog |
| `app.cache.hit_ratio` | Gauge | `cache_name` | Cache effectiveness |

### Health Checks

- Expose `/health` and `/ready` endpoints
- `/health` — Is the process alive?
- `/ready` — Can it serve traffic?
- Do not log health-check requests at `Information` level

### Alerting

| Severity | Meaning | Response |
|----------|---------|----------|
| **P1 / Critical** | Service down, data loss risk | Immediate page |
| **P2 / High** | Degraded for many users | Respond within 30 min |
| **P3 / Medium** | Degraded for some users | Respond within business hours |
| **P4 / Low** | Cosmetic or informational | Next sprint |

### Observability Checklist (Pre-Merge)

- [ ] Structured logging with correlation IDs on all new code paths
- [ ] No sensitive data in logs, traces, or metric labels
- [ ] Distributed tracing spans for external calls
- [ ] RED metrics for new endpoints/operations
- [ ] Health-check endpoints implemented and tested
- [ ] Log levels used correctly
- [ ] Alerts defined for critical failure paths with runbook links

---

## Code Documentation

- All public methods/classes: purpose, parameters, return values, exceptions
- Complex logic: inline comments for non-obvious algorithms
- Public APIs: request/response examples
- Configuration: all environment variables documented

---

## GitFlow Branching

**Always create feature branches from `develop`, never from `master`.**

| Branch Type | Create From | Merge To | Pattern |
|-------------|-------------|----------|---------|
| `feature/*` | `develop` | `develop` | `feature/descriptive-name` |
| `bugfix/*` | `develop` | `develop` | `bugfix/descriptive-name` |
| `release/*` | `develop` | `master` **+** `develop` | `release/x.y.z` |
| `hotfix/*` | `master` | `master` **+** `develop` | `hotfix/x.y.z` |

The stable branch here is **`master`** (not `main`). Tags ship from `master` only.

References: [Atlassian GitFlow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow) · [git-flow-next CLI](https://git-flow.sh/docs/commands/)

### Tooling

We standardize on `git-flow-next`. One-time setup in this repo:

```bash
git flow init --preset=classic --main=master --develop=develop --tag=v --defaults
git flow config list   # verify
```

### Versioning Strategy (NBGV)

Versions are driven by **`version.json`** at the repo root, computed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (pinned in `src/Directory.Build.props`). Do **not** pass `-p:Version=...` on the build CLI — NBGV's pack target overrides it.

| Branch | `version.json` `"version"` | NBGV output | NuGet package |
|---|---|---|---|
| `master` | `"X.Y.Z"` (no prerelease, no `{height}`) | `X.Y.Z` | `JamesConsulting.X.Y.Z.nupkg` |
| `release/X.Y.x` | `"X.Y.Z-rc.{height}"` | `X.Y.Z-rc.N` | `JamesConsulting.X.Y.Z-rc.N.nupkg` |
| `develop` | `"X.(Y+1).0-alpha.{height}"` | `…-alpha.N-g<sha>` (non-public-release) | CI-only artifact |
| feature/bugfix | inherits from `develop` | non-public-release | CI-only artifact |

`publicReleaseRefSpec` in `version.json` includes `master`, `release/*`, and `hotfix/*`. The `nugetPackageVersion.semVer: 2` setting is required — without it the dot in `-rc.N` becomes a dash and the digit gets zero-padded.

### Release Workflow

```bash
# 1. cut the release from develop
git switch develop && git pull
git flow release start 2.0.0
git push -u origin release/2.0.0
# -> publish-rc fires: 2.0.0-rc.1, 2.0.0-rc.2, ...

# 2. QA the RCs on nuget.org / consuming apps. Push fixes to release/2.0.0
#    to produce 2.0.0-rc.N+1 until QA is clean.

# 3. BUMP version.json BEFORE finishing the release.
#    On release/2.0.0 ONLY:
#      "version": "2.0.0-rc.{height}"  ->  "version": "2.0.0"
#    Commit locally, but DO NOT push to release/2.0.0 -- a push would
#    trigger publish-rc, whose version validator requires -rc(\.\d+)?$ and
#    would fail on a clean "2.0.0". Let `git flow release finish` carry
#    the bump straight into master.
git add version.json
git commit -m "chore(release): bump version.json to 2.0.0 for final tag"
dotnet nbgv get-version -p src/JamesConsulting -v NuGetPackageVersion
# expected output: 2.0.0   (NOT 2.0.0-rc.N)

# 4. finish the release: merges into master, tags v2.0.0, merges into develop
git flow release finish 2.0.0
git push origin master develop --follow-tags
# -> publish-stable fires on the v2.0.0 tag: ships 2.0.0 + GitHub Release

# 5. start the next dev cycle on develop (bump to next minor -alpha)
git switch develop
# edit version.json: "version": "2.1.0-alpha.{height}"
git commit -am "chore(develop): start 2.1.0-alpha"
git push
```

### Hotfix Workflow

```bash
git flow hotfix start 2.0.1
# edit version.json on hotfix/2.0.1: "version": "2.0.1"
# fix + commit + push (publish-rc does not run on hotfix/*; if you want
# RC artifacts from hotfix branches, keep version.json as "2.0.1-rc.{height}"
# until ready to finish, same pattern as release/*)
git flow hotfix finish 2.0.1
git push origin master develop --follow-tags
```

### CI Triggers (`.github/workflows/ci.yml`)

| Trigger | Job | Output |
|---|---|---|
| Any push / PR / dispatch | `build-test` | Build, test, Sonar, coverage, preview pack |
| Push to `release/**` | `publish-rc` | Signs + publishes `X.Y.Z-rc.N` (gated on `release` environment approval) |
| Push tag `v*` | `publish-stable` | Validates tag is reachable from `master` and matches NBGV-computed version, then signs + publishes stable + creates GitHub Release |

### Rules

1. **Never tag from `develop`.** Tags live on `master` only.
2. **Never skip the back-merge to `develop`** — always use `git flow release finish` / `git flow hotfix finish`.
3. **Always bump `version.json` to the clean stable version on the release branch before `git flow release finish`** — but commit only, don't push the bump to `release/*`.
4. **No squash merges on `release/*`/`hotfix/*` → `master` or `develop`.** GitFlow needs the real merge commits.
5. **One release branch at a time.** Finish or abandon before starting another.

---

## Azure DevOps Integration

### Before Starting a Task

1. **Assign the work item** to the user
2. **Move to In Progress**
3. **Read the work item description** and acceptance criteria

### Task Workflow

1. Read the work item before starting implementation
2. Reference work item IDs in commits and PRs
3. Update work item status as you progress
4. Link commits/PRs to work items

---

## Core Principles (Priority Order)

1. **Vertical Slices** — Implement features UI-to-datastore, not layer-by-layer
2. **BDD/TDD** — Tests first, always
3. **Security First** — Designed into every feature from the start
4. **Accessibility** — WCAG 2.1 AA minimum, semantic HTML
5. **Localization** — All user-facing text localizable
6. **Mobile Responsiveness** — Mobile-first CSS approach
7. **Documentation** — Document all public APIs
8. **Observability** — Structured logging, metrics, telemetry
9. **SOLID Principles** — Clean architecture, dependency inversion
10. **DRY** — Extract reusable components, services, and utilities
