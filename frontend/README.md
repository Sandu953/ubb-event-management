# UBB Events frontend

A React + TypeScript + Vite application for student discovery, registration, organization event management, attendance, and membership administration. It calls the existing ASP.NET Core backend; production data is never mocked.

## Start locally

Use Node.js 22.14+ (or a version supported by the installed Vite) and the existing backend setup. From `frontend/`:

```powershell
npm ci
Copy-Item .env.example .env.local
npm run dev
```

Open **http://localhost:5173**. Use `localhost` to match the backend's configured CORS origin. The example API URL is **http://localhost:5088**, matching `backend/UbbEvents.Api/Properties/launchSettings.json` and the running API used for verification. If your API runs on port 5000, set `VITE_API_URL=http://localhost:5000` in `.env.local`, then restart Vite. The value is the server origin, without `/api`. Never put secrets in `VITE_` variables.

Start the backend separately using its existing README/configuration, e.g. `dotnet run --project backend/UbbEvents.Api --launch-profile http` from the repository root. Configure its database and JWT settings first. The frontend does not initialize the database, apply migrations, seed users, or connect to PostgreSQL.

PowerShell users whose `npm.ps1` strips forwarded flags can use `npm.cmd`, e.g. `npm.cmd run dev -- --port 5173`.

## Pages and permissions

| Route                              | Page                                                                         | Access                                                         |
| ---------------------------------- | ---------------------------------------------------------------------------- | -------------------------------------------------------------- |
| `/`                                | Home and upcoming events                                                     | Public                                                         |
| `/events`                          | Search, exact type, organization ID, dates, lifecycle filters and pagination | Public                                                         |
| `/events/:id`                      | Details, own registration status, register/cancel                            | Public details; signed-in registration; authorized drafts only |
| `/login`, `/register`              | Authentication and student account creation                                  | Guests                                                         |
| `/registrations`                   | History, status filters and cancellation                                     | Signed in                                                      |
| `/profile`                         | Account details, user UUID and organization roles                            | Signed in                                                      |
| `/dashboard`                       | Organization workspace, event counts and upcoming events                     | Organization staff / global ADMIN                              |
| `/manage/events`                   | Organization event list including authorized drafts                          | Organization staff / global ADMIN                              |
| `/manage/events/new`               | Create a draft                                                               | Organization staff / global ADMIN                              |
| `/manage/events/:id`               | Lifecycle, counts and event management                                       | Specific organization staff / global ADMIN                     |
| `/manage/events/:id/edit`          | Versioned editing                                                            | Specific organization staff / global ADMIN; active events      |
| `/manage/events/:id/registrations` | Participants, filters and check-in                                           | Specific organization staff / global ADMIN                     |
| `/manage/events/:id/attendance`    | Attendance counts and check-in                                               | Specific organization staff / global ADMIN                     |
| `/organizations`                   | Own organizations; global-admin creation                                     | Organization staff / global ADMIN                              |
| `/organizations/:id`               | Organization overview and members                                            | Specific organization staff / global ADMIN                     |

Users select their organization workspace. ORG_ADMIN and ORGANIZER roles come from `/api/auth/me`; neither a global STUDENT role nor a selected UUID grants organization permissions. Only global administrators can appoint/demote/remove organization administrators. Backend rules remain authoritative.

## Sessions, registration and dates

- Bearer tokens live only in memory. Refreshing the page signs you out; the backend has no refresh-token endpoint. Current-user data loads after authentication, and expiry/401/sign-out clears session and query data. Passwords are never stored by the application.
- Login preserves the intended local destination. Public account creation creates students; there is no role selector.
- CONFIRMED and WAITLISTED are successful registrations. Cancellation and promotion happen on the backend. Refresh status or return to the tab to see changes; no WebSocket capability is assumed.
- Persisted timestamps are UTC. Date/time inputs use the browser's local timezone and convert to ISO UTC on submit. Displays identify the timezone.
- Editors keep the loaded event version and unsaved text. A 409 cannot silently overwrite a newer event. Use “Reload latest event details” to explicitly replace the form and obtain the current version.

## Validation

```powershell
npm run lint
npm test
npm run build
# Optional: API must already run in Development; read-only, no DB mutations.
npm run test:api
```

`npm run test:watch` runs Vitest interactively. Tests use React Testing Library with isolated mocked HTTP responses. The ordinary application always uses real HTTP services. The API smoke test checks health, integrated Swagger routes/fields, public pagination, and authentication rejection. It defaults to port 5088; override with `$env:VITE_API_URL='http://localhost:5000'` if needed. It creates no accounts, events, memberships, or registrations.

The isolated suite covers auth validation/state, logout/401, protected routes, role-aware navigation, discovery/filter/detail states, confirmed/waitlisted outcomes, cancellation, event validation, lifecycle controls, stale edits, tenant denial, check-in, membership controls, and API errors. Authenticated end-to-end verification requires a dedicated test database and authorized accounts; do not run mutating demonstrations against the developer's normal database.

## Implementation

The existing React Router stack and plain CSS are retained. Added runtime dependency: `@tanstack/react-query`. Added test dependencies: `vitest`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, and `jsdom`. Major pages are lazy-loaded. Shared components provide badges, forms, loading/error/empty/success states, pagination and native modal confirmations. Layouts cover mobile, tablet and desktop, with keyboard focus, a skip link and reduced-motion styling.

`src/services/` owns HTTP contracts; `src/types/` matches backend DTOs; `src/contexts/` owns in-memory auth; `src/hooks/` owns queries/actions; `src/pages/` composes workflows. There is no second UI framework or global state manager. Query invalidation follows the [TanStack Query workflow](https://github.com/TanStack/query/blob/main/docs/framework/react/quick-start.md).

See [API integration matrix, gaps and requirement traceability](docs/API-INTEGRATION.md) for endpoint/payload mappings and backend-dependent limits, and [GenAI review record](docs/GENAI-USAGE.md) for oversight notes.

For step-by-step journeys for guests, students, organizers, organization administrators and global administrators, see [User flows for every role](docs/USER-FLOWS.md).

## Deployment and remaining verification

`npm run build` emits `dist/`. Serve it as a static SPA with history fallback to `index.html` for deep links. Set `VITE_API_URL` before building; Vite embeds it at build time. Serve the API over HTTPS in production and configure its allowed frontend origin. Existing Docker/backend infrastructure was preserved. This task does not publish or deploy the application.

Manual browser, mobile-width visual, and authenticated end-to-end QA remain to be performed. Browser automation was blocked by the local browser tool's URL-confidence check. The backend returned no public events during smoke verification, so the live detail branch could not run; details are tested in isolation. No sample records were inserted.

The installation audit reported 10 moderate findings in the existing ESLint/typescript-eslint development dependency chain involving minimatch. No forced toolchain downgrade was applied. `npm audit --omit=dev` reported zero runtime vulnerabilities. Review the development tooling updates separately.
