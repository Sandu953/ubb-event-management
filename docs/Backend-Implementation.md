# Backend implementation and traceability

The backend extends the original ASP.NET Core 8 / EF Core 8 / Npgsql infrastructure on the existing `feature/backend-mvp` branch. No frontend source or Docker Compose service was replaced, no development database was reset, and `20261107162303_InitialCreate` remains intact.

## Specification sources

All five supplied documents were read before implementation: `Project Description.docx` (R1–R16), `Enhanced_MVP_Architecture_DB.docx` (expanded MVP and BR1–BR4), `Technical Documentation Midetrm.docx` (actors, use cases, three principal workflows), `Repo_Infrastructure_Init.docx` (stack and layering), and `MidTerm Delivery.docx` (code, UX, GenAI, and contribution evidence).

The explicit task resolves the source ambiguity about PUBLISHED registrations: registrations require REGISTRATION_OPEN. Row-level locking is used rather than assuming an integer EF concurrency token increments itself. Dates are persisted as UTC; explicit input offsets are converted to UTC. The HTTP launch profile actually uses port 5088 and Compose publishes PostgreSQL on host port 5433.

## Architecture and data

Controllers handle HTTP/DTO contracts. Scoped services implement workflows. `EventLifecycle` contains the state machine, `OrganizationPermissions` and `CurrentUser` enforce identity/tenant boundaries, and `EventLockRepository` centralizes PostgreSQL event locking. EF Core is the unit of work; a generic repository for every entity would add no useful boundary here. `WaitlistService` operates only within the caller's locked transaction. Audits are staged in the same DbContext and committed atomically with domain changes.

`BackendCompletion` is additive: it creates `AuditLogs`, indexed by tenant/time and entity/time, referencing actors/organizations with restricted deletion. Entity IDs are polymorphic values so history remains after membership removal. It adds enum validity constraints, `CheckedIn => CONFIRMED`, checked-in default false, status/date and FIFO query indexes, and a normalized-email unique expression index. The expression index is explicitly managed by migration SQL because EF Core 8 has no ordinary `HasIndex` mapping for that expression. A data operation advances existing zero event versions to 1. Existing malformed data causes migration failure for human resolution; no deduplication/deletion is automated. Down operations remove the new structures; rolling back audit storage would discard its audit rows and is an operator-controlled action. No rollback was run against a developer database.

## Decisions where requirements were incomplete

| Topic | Implemented decision |
| --- | --- |
| Registration eligibility | Authenticated accounts may participate, including staff; registration requires REGISTRATION_OPEN |
| Reactivation | Reuse the canceled row, reset RegisteredAt and CheckedIn, recompute status under event lock |
| Queue fairness | FIFO by RegisteredAt then Id; capacity increases fill new slots from existing waiters atomically |
| Event version | Start at 1; update/status DTOs require current version; explicitly increment on each event edit/transition |
| Terminal editing | Completed/cancelled event details are immutable |
| Event cancellation | Atomically cancel all active registrations; preserve rows and audit previous status/check-in values; no promotion |
| Participant cancellation | Idempotent for canceled rows; completed or checked-in registrations cannot be canceled by participants |
| Check-in timing | Only during REGISTRATION_OPEN, without a scheduled-time window; duplicate check-in returns 409 |
| Organization administration | Only global ADMIN assigns/demotes/removes ORG_ADMIN; ORG_ADMIN manages ORGANIZER members |
| Last organization administrator | Reject removing/demoting the final ORG_ADMIN; organization row lock serializes membership writes |
| Organization visibility | Own organizations only; global ADMIN sees all; staff may read their organization's members |
| Draft visibility | Public discovery omits drafts; requested drafts are visible only to staff of that tenant/global ADMIN |
| Email | Normalize new emails with trim/lowercase; match legacy case-insensitively; enforce uniqueness with PostgreSQL expression index |
| Authentication | PBKDF2 PasswordHasher; 12–128 character passwords; short-lived signed JWTs; no refresh endpoint or frontend token-storage change |
| Input | Unknown JSON properties and integer enums rejected; role/status fields must be explicit |
| Lock contention | Five-second event lock timeout returns 409; no automatic retries of non-idempotent operations |
| Bootstrap | Opt-in, development-only, configured credentials; idempotent; never elevate an existing student |

## Requirements to implementation to tests

| Requirement / use case | Implementation | Automated evidence |
| --- | --- | --- |
| R1 / event creation and editing | `EventService`, `EventsController`, validated DTOs | `EventApiTests` creation/UTC/date/capacity/version/terminal rules; `EventLifecycleTests.New_event_starts_in_draft` |
| R2 / organization onboarding and roles | `AuthService`, `OrganizationService`, `OrganizationPermissions` | `AuthAndOrganizationTests` registration/login, multi-org roles, onboarding, membership revocation, last administrator |
| R3 / BR2 / transactional registration | `RegistrationService`, `EventLockRepository`, unique index | `RegistrationTests`, `ConcurrencyTests` actual row-lock blocking, final-place race, duplicate race |
| R4 / BR1 / lifecycle | `EventLifecycle`, locked `EventService.ChangeStatusAsync` | All 25 state pairs in `EventLifecycleTests`; HTTP invalid/terminal transitions in `EventApiTests` |
| R5 / attendance and organizer controls | `AttendanceService`, `RegistrationService.ParticipantsAsync` | `AttendanceTests` tenant denial, confirmed-only, canceled/missing registrations, duplicate race, totals |
| R6 / event discovery | `EventService.ListAsync/GetAsync`, filter DTOs | `EventApiTests.Draft_visibility_tenant_isolation_and_paging_are_enforced` |
| R7 / persistence and schema invariants | `AppDbContext`, additive migration | `PersistenceTests` upgrade preserves records, normalized-email constraint, audit transaction rollback |
| BR3 / cancellation and FIFO | `RegistrationService.CancelAsync`, `WaitlistService` | `RegistrationTests` normal cancellation, no queue, canceled waiters, reactivation, FIFO UUID ties; `ConcurrencyTests` simultaneous/duplicate cancellations |
| BR4 / preserved history and auditing | Status cancellation, `AuditLog`, `AuditService` | Reactivation retains ID, canceled history query, cancellation audit tests; `PersistenceTests.Audit_failure_rolls_back_registration_in_the_same_transaction` |
| Cross-tenant isolation | Database membership checks per organization; user from claims | `AuthAndOrganizationTests`, `EventApiTests`, `AttendanceTests`, `SecurityTests.Participant_identity_is_derived_from_claims_and_history_is_private` |
| JWT security / public role escalation | `JwtTokenService`, bearer validation, strict public DTO | `SecurityTests` wrong issuer/audience/signature/expiry/subject, bootstrap safeguards, auth rate limit; `AuthAndOrganizationTests` role overposting denial |
| R8–R9 / layered API | Controllers/services/persistence/DTOs, Swagger and ProblemDetails | HTTP integration tests, `EventApiTests.Swagger_exposes_all_workflows_with_dto_schemas_and_bearer_security` |
| R11 / repeatable tests | xUnit + isolated Testcontainers PostgreSQL 16 | Unit + integration suite; no EF InMemory provider and no developer DB connection |
| R12 / runnable deployment | Existing Compose, API Dockerfile, external configuration, README | Local build/tests and CI definition; production hosting remains team work |
| R13–R15 / AI oversight and traceability | This record, README, source/test mapping | Human review still required; no human approval or deployment is implied |

R10 frontend workflows, real-user research, R16 individual/team ownership, and final hosted deployment require team work outside this backend task. No interview findings, human reviews, or individual contribution claims are invented here.

## GenAI usage record

Tool: Codex coding agent (exact serving model identifier is not exposed in this session). Purpose: inspect the repository/specification, implement backend layers and API contracts, design database locking, add migration/auditing/security, write tests, diagnose failures, and document setup/decisions.

Artifacts: backend controllers, services, repository, DTOs, infrastructure, audit model, context configuration, additive migration/snapshot, test project/solution entry, README, these notes, CI, tool manifest, environment example, Dockerfile/dockerignore, and test-artifact ignore rules. Supplied DOCX files and AGENTS instructions remain unchanged. User-directed decisions include preserving initial infrastructure/history, restricting registration to open events, reusing canceled registrations, JWT/role scope, PostgreSQL locking, FIFO promotion, and excluding unrelated integrations.

Verification: compile with .NET, exercise HTTP through WebApplicationFactory with real JWTs and PostgreSQL 16 Testcontainers, validate actual concurrent writes/locks, verify migration application and legacy upgrade inside disposable databases, inspect migration operations and model drift, and inspect Git diff. The first suite found untranslatable ordered DTO projections and an unresolved OpenAPI security reference; both were corrected and covered by tests. A later upgrade-test fixture was corrected to insert legacy data using the original schema contract. The final schema-contract check added an explicit health response schema; its JSON response remains unchanged. Team members must review the changes and record their own ownership and approval; automated checks do not establish human review.

## Executed validation

Results from 9 November 2026:

| Command/check | Result |
| --- | --- |
| `dotnet tool restore` | EF CLI 8.0.33 restored successfully |
| `dotnet restore UbbEvents.sln` | Passed (the initial sandbox network restriction required the approved NuGet execution path) |
| `dotnet build UbbEvents.sln --configuration Release --no-restore` | Passed, zero warnings and zero errors |
| `dotnet test UbbEvents.sln --configuration Release --no-build --logger "console;verbosity=minimal" --logger trx --results-directory TestResults` | 64 passed, zero failed/skipped; 26 unit cases and 38 PostgreSQL/HTTP integration cases |
| `dotnet ef migrations has-pending-model-changes --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api --configuration Release --no-build` | No pending model changes |
| `dotnet ef migrations script --idempotent --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api --configuration Release --no-build --output "$env:TEMP/ubb-events-reviewed-migrations.sql"` | SQL generated successfully; migration Up/Down and generated SQL inspected |
| `docker compose config --quiet` | Existing configuration valid; no services changed |
| `docker build -f backend/Dockerfile -t ubb-events-api:backend-validation .` | Image built and published successfully using the .NET 8 Linux SDK/runtime images |
| Temporary API-image container on random loopback port | `/api/health` returned `status=ok`; runtime UID 1654 (non-root); temporary container removed |
| `git diff --check` | Passed; initial migration, frontend, and Compose file have no diff |

The container smoke test validates startup/liveness and non-root execution, with a temporary random signing key and an unused dummy database address; database workflows are established by the separate real PostgreSQL integration tests. No migration was applied to the developer's normal database, no production service was deployed, and no GitHub push was made. The TRX results are ignored local artifacts. Frontend lint/build were not run because frontend code/dependencies did not change. Hosted CI is configured but was not run on GitHub during this task.
