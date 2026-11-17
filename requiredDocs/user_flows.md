# UBB Events — Main Application & User Flows

This document presents the end-to-end user flows across all system roles, from public event discovery to multi-tenant organization administration. It is consolidated from the implemented frontend and backend behavior (`USER-FLOWS.md`).

**Frontend Application:** `http://localhost:5173`  
**Backend API:** `http://localhost:5088`

---

## 1. User Types and Permissions

The application distinguishes a user's **global role** from their **role in a particular organization**.

| User type | How the user obtains access | Main purpose |
| :--- | :--- | :--- |
| **Guest** | Visits without signing in | Discover public events, inspect details, create an account, or sign in |
| **Student / Participant** | Creates an ordinary `STUDENT` account | Register for events, track places and waiting lists, cancel eligible registrations, and view their profile |
| **Organizer** | An administrator assigns `ORGANIZER` membership in an organization | Create and manage that organization's events, view participants, and perform check-in |
| **Organization Administrator** | A global administrator assigns `ORG_ADMIN` membership | All organizer activities in that organization, plus adding and removing organizers |
| **Global Administrator** | Receives the global `ADMIN` role through the backend's administration/bootstrap process | Create organizations, appoint organization administrators, and manage organizations and events across the system |

Public signup never offers a privileged-role selector. There is no frontend action for making someone a global administrator.

### Permission Matrix

"Own organization" means an organization in which the signed-in user has the relevant membership. Being an organizer elsewhere does not qualify.

| Action | Guest | Student | Organizer | Organization admin | Global admin |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Browse public events and details | Yes | Yes | Yes | Yes | Yes |
| Register and manage own eligible registrations | Sign in first | Yes | Yes | Yes | Yes |
| View own profile and user ID | Sign in first | Yes | Yes | Yes | Yes |
| View drafts and manage events | No | No | Own organization | Own organization | Any organization |
| View participants and take attendance | No | No | Own organization | Own organization | Any organization |
| View organization details and staff members | No | No | Own organization | Own organization | Any organization |
| Add or remove organizers | No | No | No | Own organization | Any organization |
| Appoint, demote, or remove organization admins | No | No | No | No | Yes, with last-admin protection |
| Create an organization and assign its initial admin | No | No | No | No | Yes |

Organization membership is a staff assignment. It is **not required to register for a public event**.

---

## 2. Guest Flows

### G1. Discover Events
**Start:** Home (`/`) or **Explore events** (`/events`). No account is required.

1. Open Home to see the introduction and upcoming events returned by the API.
2. Select **Explore events** or **See all events**.
3. Enter a search term to search titles, descriptions, and venues.
4. Optionally enter an exact event type and choose an event status.
5. Open **More filters** to set a starting-from time, starting-before time, or organization UUID.
6. Select **Find events** to apply the filters.
7. Use **Previous** and **Next** to move through results, or **Clear filters** to start again.

**Result:** Matching public event cards appear, sorted by start time. Search/filter choices are kept in the page URL. Drafts never appear in public discovery.

### G2. Read Event Details
**Start:** An event card's **View event** link, or `/events/:id`.

1. Open an event and read its description, type, venue, start/end times, capacity, and lifecycle status.
2. Review the organization information available on the page.
3. If interested in attending, select **Sign in to register**.

**Result:** The sign-in page opens with the event as the intended return destination. After successful authentication, the user returns to the event and submits the registration action.

### G3. Create a Student Account
**Start:** **Join the community**, **Create an account**, or `/register`.

1. Enter full name, email address, password (12–128 characters), and confirmation password.
2. Use **Show password** / **Hide password** if needed.
3. Select **Create student account**.

**Result:** The backend creates an ordinary `STUDENT` account. The application signs the user in, loads their account and memberships, and opens the saved destination or Explore events.

### G4. Sign In and Resume a Protected Flow
**Start:** **Sign in**, `/login`, or a redirect from a protected page.

1. Enter email and password and select **Sign in**.
2. Wait for the account and organization roles to load.

**Result:** The saved local destination opens, or Explore events opens by default. Navigation includes the workspaces allowed by the user's roles.

---

## 3. Student and Participant Flows

These flows are also available to signed-in organizers and administrators acting as participants. Each registration belongs to the signed-in account.

### S1. Register for an Event
**Preconditions:** Signed in; event is `REGISTRATION_OPEN`; no active registration for this account/event.

1. Open Explore events and select an event.
2. Review the details and **Your place at this event** panel.
3. Select **Register for this event** once and wait for the server response while the action is disabled.

**Result:** The panel and registration data refresh using the actual returned status:

| Server result | Meaning | What the participant does next |
| :--- | :--- | :--- |
| `CONFIRMED` | A place was allocated | Review the event time and venue; see the record in My registrations |
| `WAITLISTED` | Capacity was reached and the participant joined the waiting list | Keep the registration and refresh later to check for promotion |

### S2. Follow a Waiting-List Registration
**Start:** An existing `WAITLISTED` registration.

1. Open **My registrations** (`/registrations`) or the event details.
2. Select **Refresh status** in history or **Refresh my status** on the event page to check whether the status has become `CONFIRMED`.

**Result:** While registration remains open, the backend automatically promotes the oldest waiting registration (FIFO) when a confirmed cancellation releases a place or when capacity increases.

### S3. View Registration History
**Start:** **My registrations** (`/registrations`).

1. Review event titles, dates, venues, registration statuses, and check-in indicators.
2. Filter by confirmed, waitlisted, or canceled status, or keep all registrations visible.
3. Use pagination for additional records or select **View event** to return to details.

**Result:** The participant sees their full registration history, including canceled rows.

### S4. Cancel a Registration
**Preconditions:** An active `CONFIRMED` or `WAITLISTED` registration; participant has not checked in; event is not `COMPLETED`.

1. Open My registrations or the event's registration panel and select **Cancel registration**.
2. Confirm cancellation in the confirmation dialog (or choose **Keep current state** to abort).

**Result:** After a successful server response, the record transitions to `CANCELED` and remains in history. When an eligible `CONFIRMED` cancellation releases a place while registration is open, the server atomically promotes the oldest `WAITLISTED` participant.

### S5. Register Again After Cancellation
**Preconditions:** Previous registration is `CANCELED` and the event is still `REGISTRATION_OPEN`.

1. Open the event details and select **Register for this event** again.

**Result:** The backend reactivates the existing registration record, resets `CheckedIn = false`, updates `RegisteredAt` to the current UTC timestamp, and re-evaluates capacity (placing the user at the back of the queue if full).

### S6. View Profile and Organization Roles
**Start:** **My profile** (`/profile`).

1. Review name, email, global role, and **Your user ID** (UUID).
2. Review assigned organizations and organization roles below.

### S7. Become an Organizer Through Administrator Onboarding
1. Create an ordinary student account and share the profile UUID from **My profile** with the relevant organization administrator.
2. Once assigned by the administrator, sign out and sign in again if the new workspace is not yet visible, then open **Dashboard** or **My organizations**.

---

## 4. Organizer Flows

Every flow in this section is also available to the relevant `ORG_ADMIN` and to a global `ADMIN`.

### O1. Select an Organization Workspace and Read the Dashboard
**Start:** **Dashboard** (`/dashboard`).

1. Choose an authorized organization in **Organization workspace**.
2. Review counts for all managed events, drafts, published events, and events with registration open.
3. Review upcoming events, open an event's **Manage** link, or select **Create event**.

### O2. Find and Inspect Managed Events
**Start:** **Manage events** (`/manage/events`).

1. Select the organization workspace, optionally search or filter by lifecycle status, and select **Manage** for an event.
2. The event workspace (`/manage/events/:id`) shows status, dates, capacity, confirmed/waitlisted/check-in counts, current version, and valid lifecycle actions.

### O3. Create a Draft Event
**Start:** **Create event** (`/manage/events/new`).

1. Select the owning organization and enter title, type, description, venue, start/end date and time (in the displayed local timezone), and a positive whole-number `MaxCapacity`.
2. Select **Create draft event**.

**Result:** A new `DRAFT` event is created (`Version = 1`) and its management workspace opens. It is not publicly discoverable and does not accept registrations.

### O4. Edit an Active Event and Resolve Stale Updates
**Preconditions:** Authorized organization staff; event is `DRAFT`, `PUBLISHED`, or `REGISTRATION_OPEN`.

1. Open the event workspace, choose **Edit event details** (`/manage/events/:id/edit`), modify permitted fields (keeping `MaxCapacity >= ConfirmedCount`), and select **Save changes**.
2. **Conflict recovery:** If another organizer updated the event concurrently, the server rejects the outdated `Version` (`409 Conflict`). Unsaved form text is preserved so the organizer can review/copy edits, select **Reload latest event details**, reapply changes, and save again.

### O5. Publish a Draft
**Precondition:** Event is `DRAFT`.

1. In the event management workspace, select **Publish event** and confirm **Update event status**.

**Result:** Status transitions to `PUBLISHED`. The event becomes publicly visible, while registration remains closed.

### O6. Open Registration
**Precondition:** Event is `PUBLISHED`.

1. In the event management workspace, select **Open registration** and confirm **Update event status**.

**Result:** Status transitions to `REGISTRATION_OPEN`. Signed-in participants can now register, and check-in becomes available for confirmed participants.

### O7. Inspect Participants and the Waiting List
**Start:** **Manage registrations** (`/manage/events/:id/registrations`).

1. Review server-wide confirmed, waitlisted, canceled, and checked-in totals.
2. Filter by registration status, search the current loaded page by name/email, paginate, or select **Refresh participants**.

### O8. Check In a Participant
**Start:** **Take attendance** (`/manage/events/:id/attendance`) or the event registrations page.  
**Preconditions:** Event is `REGISTRATION_OPEN`; participant is `CONFIRMED` and `CheckedIn` is `false`.

1. Locate the participant, verify their identity, select **Check in**, and confirm in the dialog.

**Result:** The participant is marked **Checked in**, the button is disabled/removed for that record, and attendance counts refresh.

### O9. Complete an Event
**Precondition:** Event is `REGISTRATION_OPEN`.

1. Return to the event workspace, select **Complete event**, and confirm the dialog.

**Result:** Status transitions to `COMPLETED`. Registration history and attendance records remain preserved; new registrations, edits, check-in, and participant cancellations are closed.

### O10. Cancel an Event
**Precondition:** Event is `PUBLISHED` or `REGISTRATION_OPEN`.

1. Open the event workspace, select **Cancel event**, and confirm **Update event status**.

**Result:** Event status transitions to `CANCELLED`. The backend atomically marks all active `CONFIRMED` and `WAITLISTED` registrations as `CANCELED` and clears their check-in flags.

### O11. View the Organization and Its Staff
**Start:** **My organizations** (`/organizations`).

1. Open an authorized organization card to view its name, description, ID, and paginated staff member list.

---

## 5. Organization Administrator Flows

An `ORG_ADMIN` can perform all organizer flows (Section 4) for their own organization, plus member management for organizers within that tenant:

1. **OA1. Open Organization Administration Context:** Sign in with an `ORG_ADMIN` membership and open `/organizations/:id`.
2. **OA2. Add an Organizer:** Select **Add member**, enter an existing user's UUID, keep **Organization role** set to **Organizer**, and submit.
3. **OA3. Remove an Organizer:** Select **Remove** next to an organizer in the member list and confirm **Remove member**. (An `ORG_ADMIN` cannot add, demote, or remove `ORG_ADMIN` accounts.)
4. **OA4. Coordinate Administrator Changes:** Provide the target user's UUID and organization context to a global `ADMIN` when appointing or changing an `ORG_ADMIN`.

---

## 6. Global Administrator Flows

A global `ADMIN` can manage organizations and oversee events across any tenant:

1. **GA1. Browse Organizations & Select Workspace:** Open **Administration** (`/organizations`) or select any organization from **Dashboard** (`/dashboard`).
2. **GA2. Create an Organization & Appoint Its Initial Admin:** Open **Administration** -> **New organization**, enter name, description, and an existing user's UUID as **Initial administrator user ID**, and submit.
3. **GA3. Add an Organizer or Additional Organization Admin:** Open any organization -> **Add member**, enter the existing user's UUID, and choose **Organizer** or **Organization admin**.
4. **GA4. Promote or Demote an Existing Member:** Use **Make org admin** or **Make organizer** in the member list (blocked if it would demote the last `ORG_ADMIN`).
5. **GA5. Remove a Member or Replace an Administrator:** Select **Remove** and confirm (blocked if removing the last `ORG_ADMIN`; assign a replacement administrator first).
6. **GA6. Oversee Events Across Organizations:** Switch organization workspaces in **Dashboard** or **Manage events** to assist any tenant under standard event lifecycle rules.

---

## 7. Multi-Organization Switching & Session Recovery

- **M1. Safe Organization Switching:** Users holding roles in multiple organizations (e.g., `ORG_ADMIN` in Org A, `ORGANIZER` in Org B, and regular participant in Org C) switch active contexts via the **Organization workspace** selector. Every protected request is validated server-side against the target tenant.
- **C1–C2. In-Memory Session & Sign-Out:** Access tokens are stored strictly in memory. Signing out, token expiry, or reloading the browser clears the local session and redirects protected routes back to `/login` while preserving the intended return URL.
- **C3–C4. Accessibility & Error Handling:** Includes keyboard-accessible dialogs (`Escape` to cancel), mobile navigation drawer, and structured error handling for validation (`400`), authentication (`401`), forbidden (`403`), not found (`404`), conflicts (`409`), and rate limits (`429`).

---

## 8. Event & Registration State Reference

### Event Lifecycle States

| Current state | Publicly visible | Accepts new registrations | Staff may edit details | Check-in allowed | Available next states |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `DRAFT` | No (authorized staff only) | No | Yes | No | `PUBLISHED` |
| `PUBLISHED` | Yes | No | Yes | No | `REGISTRATION_OPEN`, `CANCELLED` |
| `REGISTRATION_OPEN` | Yes | Yes | Yes (subject to capacity & version) | Confirmed, not-yet-checked-in participants | `COMPLETED`, `CANCELLED` |
| `COMPLETED` | Yes | No | No | No | None |
| `CANCELLED` | Yes | No | No | No | None |

### Participant Registration States

| State | Meaning | Participant options |
| :--- | :--- | :--- |
| `CONFIRMED` | A place is allocated | View details; cancel if not checked in and event is not completed; organizer may check in while `REGISTRATION_OPEN` |
| `WAITLISTED` | Waiting in FIFO queue for a place | Refresh to see promotion; cancel if eligible; no check-in while waitlisted |
| `CANCELED` | Registration is inactive but preserved | View history; register again while event is `REGISTRATION_OPEN` |

---

## 9. Complete End-to-End Demonstration Journeys

- **Journey A (Organization Onboarding to Completed Event):** Student signups -> Global `ADMIN` creates organization and assigns initial `ORG_ADMIN` -> `ORG_ADMIN` adds `ORGANIZER` -> `ORGANIZER` creates `DRAFT`, publishes (`PUBLISHED`), and opens registration (`REGISTRATION_OPEN`) -> Student registers (`CONFIRMED`) -> `ORGANIZER` checks student in -> `ORGANIZER` completes event (`COMPLETED`).
- **Journey B (Capacity, Waitlist & FIFO Auto-Promotion):** Event opened with `MaxCapacity = 1` -> Student A registers (`CONFIRMED`) -> Student B registers (`WAITLISTED`) -> Student A cancels -> Backend atomically promotes Student B to `CONFIRMED` -> Student A re-registers and receives `WAITLISTED` behind current capacity.
- **Journey C (Multi-Organization Permission Boundaries):** Single account assigned `ORG_ADMIN` in Org A and `ORGANIZER` in Org B verifies member management is enabled in Org A, restricted to event management in Org B, and denied in Org C.
- **Journey D (Concurrent Event Editing):** Two organizers open the same event at `Version = 1` -> Organizer A saves (`Version` becomes `2`) -> Organizer B submits `Version = 1`, receives a `409` stale-edit conflict while keeping unsaved text, reloads latest details, and reapplies the change.