# Frontend verification — 11 November 2026

| Check | Result |
| --- | --- |
| `npm ci` | Passed against the original checkout before implementation |
| `npm.cmd ci --offline --no-audit` | Passed against the final dependency lockfile; 240 packages installed |
| `npm run lint` | Passed |
| `npm test` | Passed: 42 tests across 4 files |
| `npm run test:api` | Passed: 5 read-only checks against the running API on port 5088 |
| `npm run build` | Passed: strict TypeScript and Vite production output |
| `npm run dev` | Started successfully at `http://localhost:5173`; HTTP 200 verified |
| `git diff --check` | Passed |
| `npm audit --omit=dev --json` | Passed: zero runtime vulnerabilities reported |
| Full `npm audit --json` | Reported 10 moderate findings in the existing ESLint/typescript-eslint/minimatch development dependency chain; no forced downgrade applied |

Isolated tests cover account validation, authentication/membership loading, logout and 401 handling, protected routes, role-aware navigation, navigation focus, event filters/details, loading/empty/error states, confirmed/waitlisted registration, cancellation/refetch, event validation, lifecycle controls, stale-edit preservation/version submission, cross-organization denial, confirmed-only attendance check-in, membership role controls, and HTTP error/cancellation contracts.

Live API checks verify health, localhost frontend CORS, integrated Swagger paths/methods/response fields, public event pagination/draft exclusion, and anonymous rejection on protected routes. Public discovery returned zero events; the smoke script's event-detail branch was therefore not exercised against live data. No accounts, events, memberships, or registrations were created or changed.

Browser automation stopped because the computer-use tool could not reliably determine the current Chrome URL. Desktop/mobile visual QA, real modal/keyboard behavior in a browser, and complete authenticated end-to-end workflows still require human verification with authorized test accounts and a dedicated test environment. Automated UI tests use isolated HTTP fixtures; they do not establish backend transaction/concurrency guarantees. Backend test suites were not rerun because no backend files changed.

Review [API integration and gaps](API-INTEGRATION.md), [startup/routes/dependencies](../README.md), and [GenAI oversight record](GENAI-USAGE.md) before the project demonstration. Backend infrastructure, migrations and developer data remain unchanged. No commit, push, merge or deployment was performed.
