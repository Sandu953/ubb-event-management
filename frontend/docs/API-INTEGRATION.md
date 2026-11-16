# Frontend API integration and MVP traceability

Contract checked against `backend/UbbEvents.Api/Controllers`, `DTOs/Contracts.cs`, services, and the running Development Swagger at `http://localhost:5088/swagger/v1/swagger.json`. JSON properties are camelCase; enums are uppercase strings. All routes below are relative to `VITE_API_URL`. No backend code or migrations were modified.

| Frontend workflow              | Actual endpoint                                         | Contract / access                                                                 |
| ------------------------------ | ------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Student account creation       | `POST /api/auth/register`                               | `{email,password,fullName}` → `{accessToken,expiresAt,user}`; STUDENT only        |
| Sign in                        | `POST /api/auth/login`                                  | `{email,password}` → auth response                                                |
| Account and organization roles | `GET /api/auth/me`                                      | `{user,memberships:[{organizationId,organizationName,orgRole}]}`                  |
| Public discovery               | `GET /api/events`                                       | `Page<Event>`; excludes drafts by default                                         |
| Event details                  | `GET /api/events/{id}`                                  | `EventResponse`; staff-only draft visibility                                      |
| Draft creation                 | `POST /api/events`                                      | Event details plus `organizationId`; staff in that organization or global ADMIN   |
| Event editing                  | `PUT /api/events/{id}`                                  | Event details plus original `version`; 409 for stale changes or capacity conflict |
| Lifecycle                      | `PATCH /api/events/{id}/status`                         | `{status,version}`; staff or global ADMIN                                         |
| Register / re-register         | `POST /api/events/{id}/registrations`                   | No user ID; response status is CONFIRMED or WAITLISTED                            |
| My history                     | `GET /api/registrations/me`                             | Paginated `MyRegistrationResponse`; includes cancellations                        |
| Cancel own registration        | `DELETE /api/events/{id}/registrations/me`              | 204; server atomically promotes a waiting participant                             |
| Event participant list         | `GET /api/events/{id}/registrations`                    | Paginated participants; staff or global ADMIN                                     |
| Check-in                       | `POST /api/events/{id}/check-in`                        | `{registrationId}`; confirmed, not yet checked in, registration open              |
| Attendance totals              | `GET /api/events/{id}/attendance`                       | `{eventId,confirmed,waitlisted,canceled,checkedIn}`; staff or global ADMIN        |
| Organization list / details    | `GET /api/organizations`, `GET /api/organizations/{id}` | Authenticated staff see authorized organizations; global ADMIN sees all           |
| Organization creation          | `POST /api/organizations`                               | `{name,description,initialAdminUserId}`; global ADMIN only                        |
| Members                        | `GET /api/organizations/{id}/members`                   | Paginated staff list; organization staff or global ADMIN                          |
| Add member                     | `POST /api/organizations/{id}/members`                  | `{userId,orgRole}`; ORG_ADMIN can add ORGANIZER; global ADMIN can add either role |
| Change member role             | `PUT /api/organizations/{id}/members/{userId}`          | `{orgRole}`; admin appointment/demotion requires global ADMIN                     |
| Remove member                  | `DELETE /api/organizations/{id}/members/{userId}`       | 204; global ADMIN needed to remove ORG_ADMIN; last admin protected                |

## Request details

- Lists return `{items,page,pageSize,totalCount}`. Page sizes are at most 100. The UI uses 12 event cards or 20 records, with pagination. Administrator organization selectors incrementally load additional pages.
- Event filters: `search` (title, description, venue), exact `type`, `organizationId`, `status`, UTC `from` and `to`, and `includeDrafts`. Filters persist in discovery and management URLs. Public discovery never requests drafts. Management scopes requests to an organization authorized by `/auth/me`, or a global administrator's selected organization.
- Registration lists support `status`. Participant name/email search is explicitly limited to the current loaded page; no unsupported server-side search is sent.
- Event details contain `title,type,description,venue,startTime,endTime,maxCapacity`, plus `id,organizationId,status,version,createdBy`. Create sends only details and organization ID; update sends details and original version. Extra request properties are rejected by the backend.
- Create forms enforce actual maximum lengths, password length (12–128 for account creation), positive integer capacity, and end-after-start. Local date input becomes an ISO UTC timestamp. Display identifies the browser's timezone.
- The central HTTP client handles validation ProblemDetails, 401, 403, 404, 409, rate limiting, connection failures, AbortSignal cancellation, and 204 responses. It masks 5xx response internals. Access tokens and credentials are not logged.
- TanStack Query caches server state. Successful mutations invalidate server-data families; errors preserve forms and permit explicit retry. Own registration lookup traverses history pages because there is no event-specific own-status endpoint. Refetch and explicit refresh retrieve waitlist promotion changes; there is no invented live notification service.

## Session and authorization decisions

Access tokens stay in memory. No localStorage, sessionStorage, cookie, refresh token, or persistent token is added. Login and public registration return a bearer token; `/auth/me` then loads user and membership information. Expiry, authenticated 401 responses, and sign-out clear the token and cached server data. Page reload requires signing in again. This trades persistence for reducing stored-token exposure; durable sessions need a backend-supported refresh/cookie design.

Global ADMIN and organization roles are separate. A selected organization ID alone never grants staff controls. Protected pages require account loading first; event/organization pages then verify the specific membership. Backend authorization remains authoritative, including revoked permissions and races. Only global ADMIN sees organization-administrator appointment controls. Organization administrators can add/remove organizers.

Lifecycle controls mirror `EventLifecycle.cs`: Draft → Published; Published → Registration open or Cancelled; Registration open → Completed or Cancelled. Terminal events cannot be edited. Participant cancellation, member role changes/removals, lifecycle changes, and check-in have keyboard-accessible confirmation dialogs. Check-in is offered only for confirmed participants while registration is open. Cancellation is not offered for checked-in participants, completed events, or already canceled records.

## Integration gaps and graceful UI behavior

| Backend gap                                                                                       | Frontend behavior / next backend capability                                                                                                                                                          |
| ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Public events have organization ID but no organization name; organization directory is staff-only | Use known membership names where available; show the ID in public details. Organization filter accepts a UUID and explains the limitation. Add public organization summaries or names to event DTOs. |
| My-registration response has no organization fields                                               | History shows event, venue, time and registration/check-in status, linking to event details. No fabricated organization data.                                                                        |
| No public remaining-capacity count                                                                | Cards show total capacity, never “seats left.” Register response determines confirmed versus waitlisted.                                                                                             |
| No current-registration endpoint per event                                                        | Traverse paginated own history. An event-specific endpoint would reduce requests for large histories.                                                                                                |
| No user search/directory                                                                          | Admin forms require an existing user's UUID, available from their profile. No invented email lookup.                                                                                                 |
| No profile/password editing or refresh/logout endpoint                                            | Profile is read-only; logout clears the client session. In-memory sessions end on reload.                                                                                                            |
| No organization update/delete endpoint                                                            | Organization overview is read-only; creation and member operations use actual endpoints.                                                                                                             |
| No dashboard aggregate, featured events, media, or real-time notifications                        | Event counts come from filtered list totals and attendance from its endpoint. Home shows upcoming events. Card art is decorative CSS, not invented event imagery.                                    |
| No undo-check-in endpoint                                                                         | Dialog explains the API cannot undo check-in. No local-only reversal.                                                                                                                                |

## Requirement → implementation → verification

| Requirements                      | Implementation                                                         | Automated evidence                                                                                          |
| --------------------------------- | ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| R1, R4 event management/lifecycle | EventForm, EventEditorPage, ManageEventPage, eventService              | Creation validation; supported/terminal action availability; stale-edit preservation; exact version payload |
| R2 roles and organizations        | AuthProvider, ProtectedRoute, Layout, organization pages, MembersPanel | Account lifecycle; role-aware navigation; organization isolation; org-admin role restrictions               |
| R3 registrations and waitlisting  | RegistrationPanel, RegistrationsPage, registrationService              | CONFIRMED/WAITLISTED success; cancellation/refetch; closed registration; history paging                     |
| R5 attendance                     | ParticipantsPage, check-in/attendance wrappers                         | Confirmed-only check-in; refetch; no duplicate UI check-in after success                                    |
| R6 discovery                      | EventsPage, HomePage, EventDetailsPage                                 | Cards, server filters, details/login redirect, loading/empty/error states                                   |
| R8, R9 integration                | Typed services and DTOs, shared API client                             | HTTP errors, ProblemDetails, cancellation, 204, bearer headers; live OpenAPI smoke checks                   |
| R10 responsive/accessibility      | Shared CSS, semantic forms, focusable navigation, native dialogs       | Accessible labels and dialog tests; manual responsive/visual QA outstanding                                 |
| R11 testing                       | `src/test`, `scripts/api-smoke.mjs`                                    | Vitest/Testing Library and read-only Node API smoke suites                                                  |
| R12 deployment                    | Vite build, environment setup, README                                  | Strict TypeScript + production build; SPA hosting instructions                                              |
| R13–R15 oversight/traceability    | This matrix and GENAI-USAGE.md                                         | Review log and reproducible commands; human evaluation still required                                       |

Backend locking, waitlist promotion, tenant isolation and transaction guarantees remain backend responsibilities covered by the repository's PostgreSQL-backed backend tests. Frontend isolated tests do not establish database guarantees.
