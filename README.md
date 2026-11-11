# UBB Event Management

ASP.NET Core 8 / EF Core 8 / PostgreSQL 16 backend for UBB student organizations, with the existing React/Vite frontend. The backend implements student authentication, organization onboarding, tenant permissions, event discovery and lifecycle, registration/waitlisting, attendance, and persistent auditing.

## Local setup

Requirements: .NET SDK 8 or a newer SDK that targets .NET 8, .NET 8 runtime, and Docker Desktop with Linux containers. The test suite uses Docker independently of Compose.

1. Copy `.env.example` to `.env` and set private PostgreSQL credentials. For an existing volume, keep the database/user/password that initialized it. Changing `.env` does not reset credentials inside a volume.
2. Start PostgreSQL: `docker compose up -d postgres`, then `docker compose ps`. Existing volumes are preserved. Optional pgAdmin: `docker compose --profile tools up -d pgadmin` after configuring its credentials.
3. Configure the backend using .NET User Secrets (PowerShell, repository root):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=<your-db>;Username=<your-user>;Password=<your-password>" --project backend/UbbEvents.Api
$jwtKeyBytes = New-Object byte[] 48
$jwtKeyRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtKeyRng.GetBytes($jwtKeyBytes)
$jwtKeyRng.Dispose()
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($jwtKeyBytes)) --project backend/UbbEvents.Api
Remove-Variable jwtKeyBytes, jwtKeyRng
dotnet tool restore
dotnet restore UbbEvents.sln
dotnet build UbbEvents.sln
```

The connection string must point to the intended development database. Do not paste actual secrets into source files or commit `.env`. User Secrets are a development facility; deployments should use their secret manager or injected environment variables.

4. Review the migrations before applying them. `InitialCreate` is preserved. `20261109201157_BackendCompletion` adds audit persistence, enum/check-in constraints, query indexes, a checked-in default, and a functional unique index on `lower(btrim("Email"))`; existing zero event versions become 1. It does not delete domain records. Legacy duplicate emails or invalid enum/check-in data cause the migration to fail for manual resolution, rather than silently rewriting/deleting records. Back up shared databases before schema deployment.

```powershell
dotnet ef migrations list --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api
dotnet ef migrations script --idempotent --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api --output "$env:TEMP/ubb-events-migrations.sql"
# Only after reviewing the SQL and verifying the target database:
dotnet ef database update --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api
dotnet run --project backend/UbbEvents.Api --launch-profile http
```

The checked-in HTTP profile uses **http://localhost:5088**; Swagger is at **http://localhost:5088/swagger** in Development. `/api/health` retains the existing liveness response `{ "status": "ok" }`; it is not a database readiness check. The frontend's existing fallback is port 5000: set `VITE_API_URL=http://localhost:5088` in its untracked local environment, or run the API with `--no-launch-profile --urls http://localhost:5000` and `ASPNETCORE_ENVIRONMENT=Development`. Frontend source and Compose services are unchanged.

## Configuration

Environment variables use double underscores in place of colons. No signing keys or connection credentials are included in source control.

| Configuration | Environment variable | Requirement/default |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Required PostgreSQL connection string |
| `Jwt:Issuer` | `Jwt__Issuer` | `UbbEvents.Api` |
| `Jwt:Audience` | `Jwt__Audience` | `UbbEvents.Frontend` |
| `Jwt:SigningKey` | `Jwt__SigningKey` | Required, random key of at least 32 UTF-8 bytes |
| `Jwt:ExpirationMinutes` | `Jwt__ExpirationMinutes` | 60; accepted range 5–1440 |
| `Cors:Origins:0` | `Cors__Origins__0` | `http://localhost:5173`; add indexed entries as needed |
| `DevelopmentAdmin:Enabled` | `DevelopmentAdmin__Enabled` | `false` |
| `DevelopmentAdmin:Email` | `DevelopmentAdmin__Email` | Required if bootstrap enabled |
| `DevelopmentAdmin:Password` | `DevelopmentAdmin__Password` | Required if bootstrap enabled; 12–128 characters |
| `DevelopmentAdmin:FullName` | `DevelopmentAdmin__FullName` | `Development Administrator` |

Startup validates JWT configuration and fails if it is missing or unsafe. Startup **never applies database migrations**. Apply migrations before enabling the bootstrap.

For a local global administrator, set the three `DevelopmentAdmin` secrets (`Enabled=true`, a private email, and a private password), then launch in Development. Seeding is transactional and idempotent, never changes an existing account's password or elevates a student account, and refuses to run outside Development. Disable it and remove the password secret after onboarding. There is no administrator-creation API. Production administrator provisioning is an operator responsibility through a controlled deployment process.

## API and authentication

Swagger describes DTOs, enum values, security, and responses. Register/login return `{ accessToken, expiresAt, user }`. Supply `Authorization: Bearer <accessToken>` or use Swagger's **Authorize** button. Registration creates only `STUDENT` accounts. Passwords use ASP.NET Core `PasswordHasher<User>`; email matching and uniqueness ignore case and surrounding whitespace. Passwords require 12–128 characters. Unknown JSON properties and integer enum values are rejected.

Global `ADMIN` can administer all organizations. Organization roles are always checked against the requested organization's membership in PostgreSQL, including when a token was issued before a membership change. `ORG_ADMIN` can manage organizers; only global `ADMIN` can appoint, demote, or remove `ORG_ADMIN` members. At least one organization administrator must remain. `ORG_ADMIN` and `ORGANIZER` can manage all events in their organization. Authenticated accounts can participate in public events regardless of staff roles.

| Method | Route | Access / result |
| --- | --- | --- |
| POST | `/api/auth/register` | Public → 201 with student token/user |
| POST | `/api/auth/login` | Public → 200 with token/user |
| GET | `/api/auth/me` | Authenticated user and organization roles |
| GET | `/api/organizations` | Own organizations; all for global ADMIN |
| GET | `/api/organizations/{id}` | Tenant staff or global ADMIN |
| POST | `/api/organizations` | Global ADMIN; creates org and initial ORG_ADMIN atomically |
| GET | `/api/organizations/{id}/members` | Tenant staff or global ADMIN |
| POST | `/api/organizations/{id}/members` | ORG_ADMIN adds ORGANIZER; global ADMIN can assign either role |
| PUT | `/api/organizations/{id}/members/{userId}` | Role management within the same permission limits |
| DELETE | `/api/organizations/{id}/members/{userId}` | Remove membership, preserving an administrator |
| GET | `/api/events` | Public discovery, excluding drafts by default |
| GET | `/api/events/{id}` | Public; drafts visible only to own tenant staff or global ADMIN |
| POST | `/api/events` | Tenant staff or global ADMIN; creates DRAFT |
| PUT | `/api/events/{id}` | Tenant staff or global ADMIN; requires current version |
| PATCH | `/api/events/{id}/status` | Tenant staff or global ADMIN; requires current version |
| POST | `/api/events/{id}/registrations` | Authenticated; returns registration ID and CONFIRMED/WAITLISTED, 200 |
| GET | `/api/events/{id}/registrations` | Tenant staff or global ADMIN; optional status filter |
| DELETE | `/api/events/{id}/registrations/me` | Cancel own registration, 204; repeated cancellation is idempotent |
| GET | `/api/registrations/me` | Own history, including canceled rows; optional status filter |
| POST | `/api/events/{id}/check-in` | Tenant staff or global ADMIN; confirmed participants only |
| GET | `/api/events/{id}/attendance` | Tenant staff or global ADMIN; registration and check-in totals |
| GET | `/api/health` | Public liveness probe |

All list endpoints return `{ items, page, pageSize, totalCount }`. `page` defaults to 1 (maximum 1,000,000), `pageSize` to 20 (maximum 100). Event discovery additionally accepts `organizationId`, case-insensitive `search` (title/description/venue), exact `type`, `status`, `from`/`to` (inclusive start-time range), and `includeDrafts`. `includeDrafts=true` requires authentication and still excludes other tenants' drafts. Registration lists accept `status=CONFIRMED|WAITLISTED|CANCELED`. Ordering is deterministic.

Key request bodies (UUID examples represent IDs returned by earlier requests):

```jsonc
// POST /api/auth/register
{ "email": "student@example.test", "password": "<private password of 12-128 characters>", "fullName": "Student Name" }
// POST /api/organizations (ADMIN)
{ "name": "Student Society", "description": "UBB organization", "initialAdminUserId": "<registered-user-id>" }
// POST /api/organizations/{id}/members
{ "userId": "<registered-user-id>", "orgRole": "ORGANIZER" }
// POST /api/events
{ "organizationId": "<organization-id>", "title": "Workshop", "type": "Workshop", "description": "Event details", "venue": "Campus", "startTime": "2027-01-10T10:00:00Z", "endTime": "2027-01-10T12:00:00Z", "maxCapacity": 25 }
// PATCH /api/events/{id}/status (use the returned version each time)
{ "status": "PUBLISHED", "version": 1 }
// POST /api/events/{id}/check-in
{ "registrationId": "<registration-id>" }
```

`PUT /api/events/{id}` takes the event-detail fields plus `version`, excluding `organizationId`. Registration and cancellation take no body; the participant ID is always obtained from validated claims. A submitted user ID cannot act on behalf of another user. Responses use uppercase string enums and UTC timestamps. Send timestamps with an explicit `Z` or UTC offset and URL-encode offsets in query strings.

Failures use ProblemDetails (`application/problem+json`): 400 invalid fields/query, 401 missing/invalid credentials, 403 permission denial, 404 missing resource (also inaccessible drafts), 409 business/unique/concurrency conflict, and 429 excessive auth requests (20 per minute per client IP). Unhandled failures return a generic 500 without database details. Reload an event on a version conflict; retry a temporary lock/deadlock conflict. Do not blindly retry a duplicate active registration.

## Lifecycle and transactional decisions

Allowed transitions are exactly `DRAFT → PUBLISHED → REGISTRATION_OPEN → COMPLETED`, plus `PUBLISHED → CANCELLED` and `REGISTRATION_OPEN → CANCELLED`. Only `REGISTRATION_OPEN` accepts registrations. Terminal events cannot be edited or transitioned. Status is changed only through the dedicated transition operation. Event updates/status changes require the last returned `version`; the service increments it explicitly.

All capacity/status/registration/check-in writes acquire PostgreSQL `SELECT ... FOR UPDATE` on the parent event inside an explicit READ COMMITTED transaction **before** reading/modifying registrations. This isolation level ensures a request waiting for a lock sees the previous request's committed registrations. Locks last until commit; a five-second lock timeout returns 409. Registration, cancellation, FIFO promotion, capacity changes, event cancellation, and check-in share this order. The user-event unique index remains the final duplicate safeguard. This guarantees capacity within these application workflows; SQL writers must observe the same protocol.

Confirmed cancellation promotes the oldest waitlisted row by `RegisteredAt`, then UUID, in the same transaction. Canceled rows are retained. Re-registration reuses the row, resets its timestamp/check-in flag, and evaluates capacity again. Increasing capacity fills new places from the queue before subsequent registrants can use them. Event cancellation marks active registrations CANCELED, clears check-in flags, and records their previous status/check-in state in audit details, without promotion. Completed events preserve attendance; participants cannot cancel completed or already checked-in registrations. A repeated cancellation of an already canceled registration returns 204.

Check-in is allowed while registration is open, without a wall-clock restriction relative to the scheduled start. A second check-in returns 409; waitlisted/canceled/nonexistent or cross-event registrations cannot be checked in. These timing, terminal-cancellation, capacity-increase, last-administrator, and event-cancellation policies are bounded decisions where the source documents were silent. See [implementation notes and traceability](docs/Backend-Implementation.md).

Audit rows record actor, organization where relevant, entity type/ID, action, details (status/role/version only), and UTC timestamp. Audits and domain changes save atomically. Audit records are not exposed through a public API and contain no credentials or tokens.

## Tests and CI

```powershell
dotnet restore UbbEvents.sln
dotnet build UbbEvents.sln --configuration Release --no-restore
dotnet test UbbEvents.sln --configuration Release --no-build
# Unit tests only (Docker not required):
dotnet test UbbEvents.sln --filter "Category!=Integration"
dotnet ef migrations has-pending-model-changes --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api
```

Integration tests use Testcontainers to create a fresh PostgreSQL 16 server with random credentials and a random port. They never accept a developer connection string and never connect to the Compose database. Docker must be available; missing Docker fails the integration suite rather than silently skipping it. Containers are disposed afterward. Tests cover actual HTTP authentication/authorization, tenant isolation, lifecycle and validation, duplicates, capacity races, lock blocking, concurrent cancellations, FIFO, capacity increases/reductions, re-registration, attendance, audit atomicity, migration upgrades, and database uniqueness. `.github/workflows/backend.yml` runs the Release build, model-snapshot check, and all tests on a Docker-enabled Linux runner.

## Container deployment

Compose remains the existing database/tools infrastructure. Build the API separately:

```powershell
docker build -f backend/Dockerfile -t ubb-events-api .
```

Use an orchestrator or `docker run` to attach the image to the existing Compose network and inject `ConnectionStrings__DefaultConnection` with **Host=postgres;Port=5432**, JWT settings, and CORS origins. The image runs as the non-root `app` user on port 8080. Publish behind a TLS-terminating reverse proxy, configure the public HTTPS port/forwarded-header handling for your trusted proxy, and use a secret manager. The host application's connection uses **127.0.0.1:5433**, not `postgres:5432`. Swagger and admin bootstrap are disabled outside Development. Run reviewed migrations as a separate deployment step. No production deployment or database update is performed by the application.

## Project evidence and scope

The functional sources are the five supplied DOCX files in `docs/`. [Backend implementation notes](docs/Backend-Implementation.md) map requirements to services and tests and record decisions and GenAI usage. [Backend flows from HTTP to PostgreSQL](docs/Backend-Flows.md) trace each implemented operation through its controller, service, authorization checks, transaction, and database tables. This task does not implement frontend workflows, payments, notifications, a token-refresh/revocation system, a production administrator-provisioning interface, or a production hosting configuration. The team still owns human review, UX/user research, individual contribution declarations, and production operations.
