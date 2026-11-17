# Project Structure & Development Environment Configuration

This document describes the repository layout, technology stack, network ports, configuration variables, and commands required to build, run, and test the **UBB Event Management System** locally and with Docker.

---

## 1. Repository Structure

The application is organized as a multi-layer enterprise monorepo separating the ASP.NET Core 8 backend, the React + TypeScript frontend, automated test suites, and container infrastructure.

```text
/
├── UbbEvents.sln                          # .NET solution file (API + automated test projects)
├── docker-compose.yml                     # Local infrastructure (PostgreSQL 16 service)
├── README.md                              # Quickstart, API overview, and setup instructions
├── AGENTS.md                              # Repository conventions and engineering guidelines
├── .github/                               # CI workflow definitions
│
├── backend/
│   ├── Dockerfile                         # Multi-stage Linux build & non-root runtime image (UID 1654)
│   ├── .dockerignore
│   ├── UbbEvents.Api/                     # ASP.NET Core 8 Web API project
│   │   ├── Program.cs                     # DI registration, middleware pipeline, CORS, JWT, rate limiting, Swagger
│   │   ├── Controllers/                   # Thin HTTP layer & route bindings (/api/*)
│   │   │   ├── AuthController.cs          # Registration, login, and current-user endpoints
│   │   │   ├── OrganizationsController.cs # Tenant creation and membership management
│   │   │   ├── EventsController.cs        # Event discovery, CRUD, and lifecycle transitions
│   │   │   ├── RegistrationsController.cs # Participant registration, cancellation, history, and staff lists
│   │   │   ├── AttendanceController.cs    # Participant check-in and attendance summary totals
│   │   │   └── WeatherForecastController.cs # Initial template scaffold sample (/WeatherForecast)
│   │   ├── Services/                      # Business logic, validation, and transaction orchestration
│   │   │   ├── AuthService.cs             # Account creation, PBKDF2 password hashing/verification
│   │   │   ├── JwtTokenService.cs         # Signed JWT issuance and claim configuration
│   │   │   ├── CurrentUser.cs             # Scoped JWT subject resolution and database user lookup
│   │   │   ├── OrganizationPermissions.cs # Global ADMIN and tenant ORG_ADMIN / ORGANIZER checks
│   │   │   ├── OrganizationService.cs     # Tenant onboarding and locked membership mutations
│   │   │   ├── EventService.cs            # Event queries, filtering, creation, versioned updates, status changes
│   │   │   ├── EventLifecycle.cs          # Dedicated state machine enforcing valid transitions
│   │   │   ├── RegistrationService.cs     # Locked transactional registration, reactivation, and cancellation
│   │   │   ├── WaitlistService.cs         # FIFO promotion within the caller's locked transaction
│   │   │   ├── AttendanceService.cs       # Locked check-in validation and aggregate attendance counts
│   │   │   ├── AuditService.cs            # Stages persistent AuditLog records in the active DbContext
│   │   │   └── DevelopmentAdminSeeder.cs  # Opt-in, advisory-locked Development global ADMIN bootstrap
│   │   ├── Repositories/
│   │   │   └── EventLockRepository.cs     # Parameterized PostgreSQL row-level locking (SELECT ... FOR UPDATE)
│   │   ├── Models/
│   │   │   └── Entities.cs                # Domain entities (User, Organization, OrganizationMember, Event, Registration, AuditLog) & enums
│   │   ├── DTOs/
│   │   │   └── Contracts.cs               # Strongly typed request/response contracts & validation rules
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs            # EF Core 8 context, fluent mappings, constraints, and indexes
│   │   │   └── Migrations/                # Versioned EF Core migrations & ModelSnapshot
│   │   │       ├── 20261107162303_InitialCreate.cs
│   │   │       ├── 20261109201157_BackendCompletion.cs
│   │   │       └── AppDbContextModelSnapshot.cs
│   │   └── Infrastructure/
│   │       └── ApiException.cs            # Global exception-to-ProblemDetails mapping (400/401/403/404/409/500)
│   │
│   └── UbbEvents.Tests/                   # xUnit unit & PostgreSQL 16 Testcontainers integration suite (64 tests)
│       ├── EventLifecycleTests.cs         # All 25 state transition pairs
│       ├── EventApiTests.cs               # CRUD, UTC dates, capacity, version conflicts, draft visibility, Swagger
│       ├── AuthAndOrganizationTests.cs    # Auth, multi-org roles, onboarding, last-admin protection
│       ├── RegistrationTests.cs           # Capacity, waitlisting, FIFO ordering, reactivation
│       ├── ConcurrencyTests.cs            # Parallel row-lock contention, final-place races, duplicate races
│       ├── AttendanceTests.cs             # Check-in rules, tenant isolation, duplicate check-in rejection
│       ├── PersistenceTests.cs            # Legacy migration upgrade, normalized email index, transaction rollback
│       └── SecurityTests.cs               # JWT validation, claim-derived identity, bootstrap, rate limiting
│
└── frontend/                              # React + TypeScript + Vite SPA
    ├── package.json                       # Dependencies (React, React Router, TanStack Query, Vitest, Testing Library)
    ├── vite.config.ts                     # Vite build and Vitest configuration
    ├── .env.example                       # Template environment variables (VITE_API_URL)
    ├── scripts/
    │   └── api-smoke.mjs                  # Read-only OpenAPI & endpoint smoke test script
    └── src/
        ├── App.tsx                        # Root application shell
        ├── main.tsx                       # Application entry point & providers
        ├── app/
        │   ├── router/                    # Route definitions & ProtectedRoute guards
        │   └── providers/                 # AuthProvider (in-memory bearer token & /auth/me state)
        ├── components/
        │   ├── common/                    # Reusable UI primitives, status badges, confirmation dialogs
        │   ├── layout/                    # Role-aware navigation bar, mobile drawer, skip-to-content layout
        │   ├── events/                    # Event cards, EventForm, RegistrationPanel
        │   ├── organizations/             # MembersPanel, organization workspace selector
        │   └── registrations/             # Registration history & participant tables
        ├── pages/
        │   ├── public/                    # HomePage, EventsPage, EventDetailsPage
        │   ├── auth/                      # LoginPage, RegisterPage
        │   ├── student/                   # RegistrationsPage, ProfilePage
        │   ├── organizer/                 # Dashboard, ManageEventPage, EventEditorPage, ParticipantsPage
        │   └── admin/                     # Organizations list, creation, and membership management
        ├── services/
        │   ├── api.ts                     # Centralized fetch client, ProblemDetails parsing, 401/403/404/409 handling
        │   ├── authService.ts             # /api/auth/* wrappers
        │   ├── eventService.ts            # /api/events/* wrappers
        │   ├── organizationService.ts     # /api/organizations/* wrappers
        │   └── registrationService.ts     # /api/registrations/* and attendance wrappers
        ├── hooks/                         # TanStack Query hooks & cache invalidation
        ├── contexts/                      # Authentication context definitions
        ├── types/                         # Strict TypeScript interfaces matching backend DTOs
        ├── utils/                         # UTC-to-local date formatting and validation helpers
        ├── styles/                        # Shared responsive CSS design system
        └── test/                          # Vitest & React Testing Library component/workflow tests
```

---

## 2. Technology Stack & Service Topology

| Layer | Technology | Local URL / Port | Docker Internal Address |
| :--- | :--- | :--- | :--- |
| **Frontend SPA** | React 18, TypeScript, Vite, React Router, TanStack Query | `http://localhost:5173` | — |
| **Backend API** | .NET 8, ASP.NET Core Web API, EF Core 8, Npgsql | `http://localhost:5088` | — |
| **OpenAPI / Swagger** | Swashbuckle (enabled in `Development`) | `http://localhost:5088/swagger` | — |
| **Database** | PostgreSQL 16 (`docker-compose.yml`) | `127.0.0.1:5433` | `postgres:5432` |

---

## 3. Prerequisites

Ensure the following tools are installed on your development machine:

1. **.NET 8 SDK** (includes runtime and CLI; `dotnet tool restore` installs `dotnet-ef` version `8.0.33`).
2. **Node.js (v20+) & npm** (for the Vite frontend and Vitest test runner).
3. **Docker Desktop / Docker Engine with Compose** (required to run PostgreSQL 16 locally and to run the Testcontainers-backed backend integration tests).

---

## 4. Environment Configuration

Secrets, signing keys, and environment-specific credentials are excluded from source control and supplied via environment variables, `.NET User Secrets`, or local `.env` files.

### 4.1 Backend Configuration (`backend/UbbEvents.Api`)

Configure these keys via `appsettings.Development.json`, environment variables (`__` separator), or `dotnet user-secrets`:

| Configuration Key | Description | Example / Local Value |
| :--- | :--- | :--- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string targeting the Docker Compose host port (`5433`) | `Host=127.0.0.1;Port=5433;Database=ubbevents;Username=postgres;Password=<dev-password>` |
| `Jwt__Issuer` | Expected JWT `iss` claim | `UbbEvents.Api` |
| `Jwt__Audience` | Expected JWT `aud` claim | `UbbEvents.Client` |
| `Jwt__SigningKey` | Symmetric HMAC signing secret (minimum 32 characters; never commit) | `<strong-random-32+-char-secret>` |
| `Jwt__ExpirationMinutes` | Access token lifetime in minutes | `60` |
| `Cors__AllowedOrigins__0` | Frontend origin permitted by CORS middleware | `http://localhost:5173` |
| `DevelopmentAdmin__Enabled` | Enables opt-in startup seeding of a global `ADMIN` in `Development` only | `true` (or `false`) |
| `DevelopmentAdmin__Email` | Email for the bootstrapped global administrator | `admin@ubb.local` |
| `DevelopmentAdmin__Password` | Password for the bootstrapped global administrator (12–128 chars) | `<strong-dev-admin-password>` |
| `DevelopmentAdmin__FullName` | Display name for the bootstrapped global administrator | `UBB System Admin` |

### 4.2 Frontend Configuration (`frontend/.env`)

Copy `frontend/.env.example` to `frontend/.env`:

```env
VITE_API_URL=http://localhost:5088
```

- Access tokens are kept **strictly in memory** (no `localStorage`, `sessionStorage`, or persistent cookies) to prevent stored-token exposure.
- Never place secrets or signing keys in `VITE_*` variables.

---

## 5. Local Development Setup & Execution Commands

### Step 1: Start PostgreSQL 16 via Docker Compose

From the repository root:

```bash
docker compose up -d
```

Verify PostgreSQL is listening on `127.0.0.1:5433`.

### Step 2: Restore Tools, Apply Migrations & Start the Backend

From the repository root:

```bash
# Restore local EF Core CLI tools (dotnet-ef 8.0.33)
dotnet tool restore

# Apply existing EF Core migrations to PostgreSQL (startup does not auto-migrate)
dotnet ef database update --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api

# Run the ASP.NET Core Web API (listens on http://localhost:5088)
dotnet run --project backend/UbbEvents.Api
```

- Liveness check: `GET http://localhost:5088/api/health` returns `{"status":"ok"}`.
- Interactive API docs: `http://localhost:5088/swagger`.

### Step 3: Start the Frontend Development Server

In a separate terminal:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173` in your browser.

---

## 6. Verification & Testing Commands

### Backend Build, Migration Verification & Automated Tests

```bash
# Build solution in Release mode
dotnet build UbbEvents.sln --configuration Release

# Verify EF Core model matches migrations (no pending drift)
dotnet ef migrations has-pending-model-changes --project backend/UbbEvents.Api --startup-project backend/UbbEvents.Api --configuration Release --no-build

# Run all 64 xUnit unit and PostgreSQL 16 Testcontainers integration tests
dotnet test UbbEvents.sln --configuration Release --logger "console;verbosity=minimal"
```

*(Note: Backend integration tests spin up disposable PostgreSQL 16 containers via Testcontainers and never touch your local development database.)*

### Frontend Typecheck, Tests, Smoke Check & Production Build

```bash
cd frontend

# Run Vitest + React Testing Library suite
npm test

# Run read-only API smoke checks against a running backend
node scripts/api-smoke.mjs

# Type-check and create production bundle
npm run build
```

### Container Build Validation

```bash
docker compose config --quiet
docker build -f backend/Dockerfile -t ubb-events-api:local .
```