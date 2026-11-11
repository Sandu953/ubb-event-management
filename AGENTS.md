# AGENTS.md — UBB Event Management System

These instructions apply to the entire repository. They guide Codex and other coding agents working on the project. When a user request conflicts with these instructions, follow the user's explicit request while preserving security and data safety. Inspect the actual repository before changing code; this file describes intended conventions, not proof that every component has been implemented.

## 1. Project mission and source of truth

Build an enterprise-style event management system for Babeș-Bolyai University (UBB) student organizations. The system supports multiple organizations, members and roles, event creation and lifecycle management, participant registration, waitlisting, attendance, and event discovery.

Read the project documents before changing domain behavior, if they are available in the workspace:

1. `Project Description.docx` — overall requirements (R1–R16), architecture, testing, deployment, traceability, human oversight.
2. `Enhanced_MVP_Architecture_DB.docx` — expanded MVP, five-entity relational schema, invariants, concurrency and waitlisting rules.
3. `Technical Documentation Midetrm.docx` — actors, use cases and workflows.
4. `Repo_Infrastructure_Init.docx` — repository structure, Docker, branching and initial stack.
5. `MidTerm Delivery.docx` — repository and documentation deliverables.

These may be stored under `docs/` or supplied separately. Do not claim to have read a document unless it is accessible. If a detail is absent, distinguish a proposed implementation decision from a documented requirement. Preserve traceability between requirements, endpoints, implementation, and tests.

## 2. Existing stack and boundaries

- **Backend:** C# / ASP.NET Core **.NET 8** Web API, Entity Framework Core 8, Npgsql/PostgreSQL provider.
- **Database:** PostgreSQL **16** through Docker Compose. pgAdmin is optional.
- **Frontend:** React + TypeScript + Vite, React Router.
- **API:** REST/JSON, Swagger/OpenAPI in development.
- **Authentication:** JWT bearer tokens; authorization through global and organization-specific roles.
- **Tests:** xUnit for .NET and PostgreSQL-backed integration tests for transactional behavior; frontend tests using the repository's existing tooling (Vitest/React Testing Library if introduced).
- **Git:** GitHub Flow (`main` plus `feature/...` branches); pull requests and CI checks.

The repository already has working initial infrastructure. **Do not scaffold a replacement solution, overwrite working configuration, or reinitialize the database.** Inspect actual names/paths and adapt to them; expected paths include:

```text
backend/UbbEvents.Api/
  Controllers/
  Services/
  Repositories/
  Models/
  DTOs/
  Data/AppDbContext.cs
  Data/Migrations/
  Program.cs
frontend/src/
docker-compose.yml
```

Do not modify frontend application behavior unless the task explicitly calls for frontend changes. When implementing backend functionality, supply usable API contracts for frontend integration.

## 3. First actions for every coding task

1. Read relevant source files, existing tests, migrations, `Program.cs`, project files, and configuration. Use the actual code, not assumptions based on this file.
2. Check `git status`; preserve uncommitted user changes. Do not revert or overwrite unrelated edits.
3. Identify which documented requirement/use case the change implements.
4. For nontrivial changes, state a short plan, then implement and validate it. Do not stop at pseudocode or scaffolding when code was requested.
5. Prefer the smallest coherent change that completes an end-to-end workflow.
6. Summarize changed files, new migrations, commands/tests executed, results, assumptions, and known limitations.

## 4. Backend architecture and coding standards

- **Controllers:** routing, authentication/authorization attributes, request/response handling; keep thin.
- **Services:** business rules, workflows, transactions, validation, state transitions.
- **Repositories / persistence:** EF Core queries and writes; add repository abstractions when they help testing and modularity, not by default for every entity.
- **Models:** persistence entities and domain enums.
- **DTOs:** API inputs/outputs; do not expose EF entities directly to clients.
- **Data:** `AppDbContext`, entity configuration, migrations.

Use dependency injection, async EF Core APIs, and `CancellationToken` for request-driven operations. Use explicit request validation and a consistent error response (prefer `ProblemDetails`). Prefer readable, straightforward code to speculative abstractions, generic frameworks, or microservices. Keep dates/times in UTC (`DateTime` UTC or a consistent equivalent) and avoid mixing local timestamps with UTC persistence. Use pagination for unbounded list endpoints.

## 4A. Frontend architecture, quality, and integration

These rules apply whenever a task includes frontend implementation. Do not modify the frontend during backend-only tasks except when explicitly requested. The existing frontend stack is **React + TypeScript + Vite with React Router**; extend it instead of replacing it. Read existing `frontend/package.json`, routes, components, styling conventions, and tests before choosing new packages or patterns.

**Structure and responsibility** (adapt to actual existing folders):

- `src/pages/`: route-level screens and workflow composition, such as event browsing/details, login, registrations, organization administration, and organizer dashboards.
- `src/components/`: reusable, focused, presentational components and shared form/layout primitives.
- `src/services/` or `src/api/`: typed HTTP clients and endpoint wrappers; keep `fetch`/Axios details out of page components.
- `src/types/`: DTO/types that reflect the backend's documented request and response contracts.
- `src/hooks/`: reusable UI/data-fetching hooks; avoid duplicating state and networking logic across pages.
- Follow the repository's existing styling system. Do not introduce a second UI framework, global state manager, or design system without clear need and approval.

**TypeScript and React:**

- Prefer typed props, API responses, form values, and route parameters. Avoid `any`, unsafe casts, duplicated DTO definitions, and large monolithic page components.
- Use functional components and idiomatic hooks. Keep business-critical rules on the backend; frontend validation improves UX but never substitutes for server validation.
- Build reusable loading, empty, success, error, and permission-denied states. Handle network failures and validation errors without blank screens or silent failures.
- Avoid unnecessary dependencies. Prefer existing packages and conventions; check installed package versions before proposing new APIs.

**Backend contract and authentication:**

- Use the API base URL from `import.meta.env.VITE_API_URL` (or the repository's existing configuration). Never hardcode production URLs or put secrets in `VITE_` variables, which become browser-visible.
- Call the actual implemented backend routes and use the OpenAPI/Swagger contract. Do not invent fields, endpoints, status transitions, or role names. If an endpoint is missing, clearly report the backend dependency instead of silently faking server behavior.
- Centralize authentication headers, HTTP status handling, and API error translation. Do not log access tokens or credentials.
- Prefer safe session handling appropriate to the backend's real auth design. Do not add persistent browser token storage (especially `localStorage`) or token refresh behavior without an explicit security decision and documented tradeoffs.
- Role-based route guards and conditional UI help usability, **but backend authorization remains authoritative**. Never treat hidden buttons as access control. Respect global `ADMIN`/`STUDENT` and organization-specific `ORG_ADMIN`/`ORGANIZER` scopes; one user can have different roles in multiple organizations.
- Handle `401` (authentication required), `403` (insufficient permissions), `404`, `409` (business/concurrency conflict), and validation errors as distinct UX states. Do not assume registration succeeds as `CONFIRMED`: display the actual `CONFIRMED` or `WAITLISTED` server response.

**UX requirements:**

- Support principal workflows established by the documents: discover/filter events; inspect event details; register/cancel and see status; create/manage events; manage organizations/members within permissions; view registrations and perform check-in.
- Only present applicable event actions for valid lifecycle states, while allowing the API to enforce transitions. Show meaningful, actionable messages for closed registration, full capacity/waitlisting, cancellation, and stale updates.
- Use semantic HTML, keyboard-accessible controls, associated form labels, visible focus states, and clear feedback messages. Provide accessible validation errors and sufficient contrast.
- Treat dates received from the backend as UTC; format in the user's local timezone with an explicit timezone where ambiguity matters. Avoid accidental UTC/local conversions when sending event times.
- Keep layouts responsive and functional on narrow screens. Do not rewrite unrelated screens for style-only improvements when the task is functional.

**Frontend validation and verification:**

- Run `npm ci`, `npm run lint`, and `npm run build` from `frontend/` when those scripts are available, and report actual results.
- Add or update component, hook, and workflow tests using the test setup already present. If none exists and tests are part of the task, propose a lightweight React Testing Library + Vitest setup before adding dependencies.
- Cover key states: successful event discovery, auth/permission rejection, registration confirmed vs waitlisted, canceled registration, API failure, and organizer-only controls. For multi-layer changes, verify UI behavior against the real documented API contract.
- Do not modify generated `dist/`, `node_modules/`, lockfiles without relevant dependency changes, or other generated artifacts by hand.

## 5. Database schema and invariants

Use the existing model and migrations as the starting point; the documented core schema is:

- **Users:** `Id` (UUID), unique `Email`, `PasswordHash`, `FullName`, `GlobalRole` (`ADMIN` / `STUDENT`), `CreatedAt`, `UpdatedAt`.
- **Organizations:** `Id` (UUID), unique `Name`, `Description`, `CreatedAt`.
- **OrganizationMembers:** `UserId`, `OrganizationId`, `OrgRole` (`ORG_ADMIN` / `ORGANIZER`); composite primary key `(UserId, OrganizationId)`.
- **Events:** `Id`, `OrganizationId`, `Title`, `Type`, `Description`, `Venue`, `StartTime`, `EndTime`, `MaxCapacity`, `Status`, `Version`, `CreatedBy`. Enforce `EndTime > StartTime` and `MaxCapacity > 0`.
- **Registrations:** `Id`, `EventId`, `UserId`, `Status` (`CONFIRMED`, `WAITLISTED`, `CANCELED`), `CheckedIn` (default `false`), `RegisteredAt`. Unique `(EventId, UserId)`.

Enforce referential integrity, uniqueness and other appropriate constraints at the database layer, in addition to service validation. A user can belong to multiple organizations with different organization roles. Do not model organization roles as global permissions.

**Migration safety:** Inspect `Data/Migrations` and the model snapshot before editing schema. Do not delete, replace, squash, or reset an already-applied/shared migration; add a new migration for schema changes. Never execute `docker compose down -v`, `dotnet ef database drop`, destructive SQL, or data-reset scripts without explicit authorization. Review generated migration `Up()`/`Down()` operations; an updated snapshot alone does not create tables. Avoid automatic production database migrations on app startup.

## 6. Authentication, authorization, and tenant isolation

- Use a vetted secure password hasher (e.g., ASP.NET Core `PasswordHasher<TUser>`). Never store or log plaintext passwords.
- Validate JWT signature, issuer, audience, expiration, and authorization. Read signing keys and credentials from configuration or user secrets; no hardcoded secrets.
- Public registration creates **STUDENT** accounts only; clients cannot self-assign `ADMIN`, `ORG_ADMIN`, or `ORGANIZER`.
- **Global ADMIN:** administration across organizations.
- **ORG_ADMIN:** manages members and authorized operations within *their* organization.
- **ORGANIZER:** creates/manages authorized events and attendance within *their* organization.
- **STUDENT/participant:** browses eligible events, manages own registrations, and views own information.
- Resolve the acting user from validated authentication claims, not from a user ID submitted by the client.
- Every organization-scoped action must check membership/role against the requested organization's ID. Prevent cross-organization reads and writes, including list endpoints and event-specific operations.
- Seed a global admin only through a documented, safe, idempotent mechanism (prefer development-only), never an unauthenticated admin-creation endpoint.

## 7. Event lifecycle and permissions

The documented lifecycle is:

```text
DRAFT -> PUBLISHED -> REGISTRATION_OPEN -> COMPLETED
                    \-> CANCELLED
PUBLISHED ----------> CANCELLED
```

Implement allowed transitions explicitly in a dedicated service/domain component. Reject invalid transitions, including transitions out of `COMPLETED` or `CANCELLED` unless the specification is formally changed. New events begin in `DRAFT`. Only authorized organization staff may create, edit, publish, open, complete or cancel their organization's events.

**Decision for consistent implementation:** accept new registrations **only** when the event is `REGISTRATION_OPEN`. Although one source says registrations are prohibited in Draft/Completed/Canceled, the documented workflow says opening registration activates it; use the stricter interpretation and document it. Do not publicly expose draft events. Capacity may never be reduced below the number of `CONFIRMED` registrations.

## 8. Registration, concurrency and waitlisting — critical invariants

Never implement registration as an unprotected `COUNT` followed by `INSERT`. In PostgreSQL, use an explicit transaction and a consistent row-level lock on the parent event (e.g., `SELECT ... FOR UPDATE`) **before** examining or changing capacity-related registration data. Apply the same locking order to registration, cancellation, waitlist promotion, and capacity changes to avoid races/deadlocks. EF's `IsConcurrencyToken()` on an integer does not automatically increment it; do not rely on that alone.

Required registration behavior:

1. Confirm authenticated participant and event eligibility/status.
2. Begin transaction and lock the event row.
3. Check unique `(EventId, UserId)` registration; do not insert duplicates.
4. Count `CONFIRMED` registrations while protected by the lock.
5. If below `MaxCapacity`, assign `CONFIRMED`; otherwise assign `WAITLISTED`.
6. Commit atomically and return registration ID/status.

Cancellation and promotion:

- Preserve the registration record by changing status to `CANCELED`, not deleting it.
- If a `CONFIRMED` participant cancels, promote the oldest `WAITLISTED` participant in the **same transaction** (sort by `RegisteredAt`, then `Id` for deterministic ties).
- Canceling a waitlisted registration does not promote another participant.
- Prevent duplicate active registrations and overbooking under simultaneous requests.
- For a previously `CANCELED` user registering again, **proposed policy:** reuse the existing row, reset `CheckedIn`, update `RegisteredAt`, and re-evaluate confirmed vs waitlisted status under the same lock. Record this as an assumption unless the team specifies otherwise.

Handle concurrency and unique-constraint failures with well-defined, non-leaking API errors (use `409 Conflict` where appropriate). Prefer PostgreSQL-backed integration tests to establish actual concurrency guarantees.

## 9. Attendance, discovery, audit

- Only `CONFIRMED` registrations are eligible for check-in; prevent duplicate check-ins and unauthorized organizer access.
- Support relevant event discovery/search/filtering and the participant's own registration history.
- Preserve registration history. The documents call for soft deletion or status-based cancellation and auditing; if introducing an `AuditLogs` table, explain the schema decision and create a new migration. Record important actions and actors without storing tokens or secrets.
- Do not add payments, ticketing, email notifications, or unrelated integrations without a documented requirement or explicit request.

## 10. API and error conventions

Expose consistent REST endpoints under `/api` with request/response DTOs and Swagger annotations where useful. Suggested resources: `/api/auth`, `/api/organizations`, `/api/events`, `/api/registrations`, and event attendance/check-in routes. Preserve existing routes if clients already use them; coordinate breaking changes.

- Use appropriate HTTP status codes (`200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`).
- Do not disclose internal stack traces, passwords, tokens, or database connection details.
- Apply consistent validation and `ProblemDetails` error contracts.
- Document permissions, payload shapes, pagination/filtering, and common errors in OpenAPI/README.

## 11. Local infrastructure and configuration

Docker Compose manages PostgreSQL 16; pgAdmin is optional. The team observed a conflict with Windows-native PostgreSQL on port 5432 and moved the **Docker host mapping** to `127.0.0.1:5433:5432`. Consequently:

- Backend running **on the host:** `Host=127.0.0.1;Port=5433`.
- Backend running **in Docker Compose:** `Host=postgres;Port=5432`.
- Inspect `docker-compose.yml` and effective configuration before assuming ports; these values may change.

The browser frontend generally uses `http://localhost:5173`; the development API is generally `http://localhost:5000`, subject to the actual launch settings. Keep development CORS restricted to configured frontend origins. Do not commit `.env`, User Secrets, JWT keys, passwords, database dumps, build outputs, or `node_modules`. Maintain sanitized `.env.example` and setup instructions.

Useful commands (run from the appropriate directory; adjust actual project paths):

```powershell
docker compose up -d
docker compose ps
dotnet restore
dotnet build
dotnet test
dotnet ef migrations list --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api
dotnet ef database update --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api
# frontend:
cd frontend
npm ci
npm run lint
npm run build
```

Migrations must be reviewed before `database update`, and database commands must point to the intended development database. Do not run migrations against unknown databases automatically.

## 12. Tests and definition of done

For each workflow, add tests at the appropriate layer. At minimum cover:

- Valid and invalid event transitions; registrations allowed only when open.
- Event field validation and capacity reduction constraints.
- Registration when space is available / full; unique user-event registration.
- Two concurrent users competing for one remaining spot (no overbooking).
- Confirmed cancellation and FIFO waitlist promotion, atomically.
- Waitlisted cancellation and canceled-user re-registration.
- Organization membership enforcement and cross-tenant denial.
- Role escalation prevention, unauthenticated access, and check-in restrictions.

Use xUnit. Mock domain collaborators for isolated unit tests, but use a **real PostgreSQL test database** (e.g., Testcontainers or isolated Compose instance) for transaction/locking behavior. Do not use EF Core InMemory as evidence of PostgreSQL race safety. Never run destructive integration tests against the developer's normal database.

Before claiming completion:

1. Restore/build successfully.
2. Run relevant unit/integration tests and state what actually ran (and what could not run).
3. Review generated migrations and ensure they contain actual operations.
4. Check authorization and tenant isolation of new endpoints.
5. For frontend changes, run lint/build and relevant UI tests, and check responsive/accessibility states.
6. Update relevant README/API docs and, when applicable, requirement-to-test traceability.
6. Report known gaps honestly; never claim tests passed if they were not executed.

## 13. Git, collaboration, and AI oversight

Work on a feature branch where possible; keep `main` stable. Do not force-push, merge, rebase shared branches, or push automatically without explicit instruction. Never commit real secrets. Use focused commits and include relevant tests with code changes. Respect local user changes.

The mid-term deliverables also require a public repository, documented workflows and architecture, UX research notes, GenAI usage reporting, and individual contribution statements. When AI changes code or documentation, help the team record: tool/model used, purpose, artifacts changed, how output was reviewed/tested, and the human decisions that affected the result. The team remains accountable for every generated artifact.

## 14. Agent reporting format

At the end of each substantive task, report concisely:

- **Implemented:** user-visible behavior and technical changes.
- **Files/migrations:** paths added or modified.
- **Validation:** exact commands and pass/fail results.
- **Design decisions:** assumptions or divergences from supplied docs.
- **Remaining work:** unresolved issues and suggested next step.

When details are ambiguous, prefer documenting a bounded assumption over silently rewriting documented domain rules. Ask for confirmation before destructive, sensitive, or production-impacting actions.
