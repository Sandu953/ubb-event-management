# UBB Events — User Flows for Every Role

This guide explains how to use the implemented application, from discovering an event to administering organizations. It covers the steps a user takes, the result they should see, and the important alternative outcomes.

**Application:** `http://localhost:5173`  
**Related technical reference:** [API integration, permissions and backend gaps](API-INTEGRATION.md)

## Contents

1. [User types and permissions](#1-user-types-and-permissions)
2. [Guest flows](#2-guest-flows)
3. [Student and participant flows](#3-student-and-participant-flows)
4. [Organizer flows](#4-organizer-flows)
5. [Organization administrator flows](#5-organization-administrator-flows)
6. [Global administrator flows](#6-global-administrator-flows)
7. [Multiple organizations and combined roles](#7-multiple-organizations-and-combined-roles)
8. [Sessions, navigation and error recovery](#8-sessions-navigation-and-error-recovery)
9. [Event and registration state reference](#9-event-and-registration-state-reference)
10. [Complete demonstration journeys](#10-complete-demonstration-journeys)
11. [Current product limits](#11-current-product-limits)

## 1. User types and permissions

The application distinguishes a user's **global role** from their **role in a particular organization**.

| User type | How the user obtains access | Main purpose |
| --- | --- | --- |
| Guest | Visits without signing in | Discover public events, inspect details, create an account, or sign in |
| Student / participant | Creates an ordinary `STUDENT` account | Register for events, track places and waiting lists, cancel eligible registrations, and view their profile |
| Organizer | An administrator assigns `ORGANIZER` membership in an organization | Create and manage that organization's events, view participants, and perform check-in |
| Organization administrator | A global administrator assigns `ORG_ADMIN` membership | All organizer activities in that organization, plus adding and removing organizers |
| Global administrator | Receives the global `ADMIN` role through the backend's administration/bootstrap process | Create organizations, appoint organization administrators, and manage organizations and events across the system |

Public signup never offers a privileged-role selector. There is no frontend action for making someone a global administrator.

### Permission overview

“Own organization” means an organization in which the signed-in user has the relevant membership. Being an organizer elsewhere does not qualify.

| Action | Guest | Student | Organizer | Organization admin | Global admin |
| --- | --- | --- | --- | --- | --- |
| Browse public events and details | Yes | Yes | Yes | Yes | Yes |
| Register and manage own eligible registrations | Sign in first | Yes | Yes | Yes | Yes |
| View own profile and user ID | Sign in first | Yes | Yes | Yes | Yes |
| View drafts and manage events | No | No | Own organization | Own organization | Any organization |
| View participants and take attendance | No | No | Own organization | Own organization | Any organization |
| View organization details and staff members | No | No | Own organization | Own organization | Any organization |
| Add or remove organizers | No | No | No | Own organization | Any organization |
| Appoint, demote or remove organization admins | No | No | No | No | Yes, with last-admin protection |
| Create an organization and assign its initial admin | No | No | No | No | Yes |

Organization membership is a staff assignment. It is **not required to register for a public event**. A staff member's personal event registration does not give other participants staff access.

## 2. Guest flows

### G1. Discover events

**Start:** Home (`/`) or **Explore events** (`/events`). No account is required.

1. Open Home to see the introduction and upcoming events returned by the API.
2. Select **Explore events** or **See all events**.
3. Enter a search term to search titles, descriptions and venues.
4. Optionally enter an exact event type and choose an event status.
5. Open **More filters** to set a starting-from time, starting-before time, or organization UUID.
6. Select **Find events** to apply the filters.
7. Use **Previous** and **Next** to move through results, or **Clear filters** to start again.

**Result:** Matching public event cards appear, sorted by start time. Search/filter choices are kept in the page URL.

**Alternatives:** No matches produce an empty state. Invalid filters or a connection problem produce an error. A public organization-name directory is unavailable, so organization filtering currently uses an ID rather than a public name picker. Drafts never appear in public discovery.

### G2. Read event details

**Start:** An event card's **View event** link, or `/events/:id`.

1. Open an event.
2. Read its description, type, venue, start/end times, capacity and lifecycle status.
3. Review the organization information available on the page.
4. If interested in attending, select **Sign in to register**.

**Result:** The sign-in page opens with the event as the intended return destination. After successful authentication, the user returns to the event and must submit the registration action; signing in does not register them automatically.

**Alternatives:** A missing event or an unauthorized draft is unavailable. A completed or cancelled public event can still be inspected. Total capacity does not mean that all those places remain available.

### G3. Create a student account

**Start:** **Join the community**, **Create an account**, or `/register`.

1. Enter full name, email address, password and confirmation password.
2. Use **Show password** / **Hide password** if needed.
3. Choose a password of 12–128 characters and make the two password fields match.
4. Select **Create student account**.

**Result:** The backend creates an ordinary student account. The application signs the user in, loads their account and memberships, and opens the saved destination or Explore events.

**Alternatives:** Required fields, invalid email, short password or mismatched passwords prevent submission or show validation feedback. A registered email produces a conflict message. Too many authentication attempts can require waiting before retrying.

### G4. Sign in and resume a protected flow

**Start:** **Sign in**, `/login`, or a redirect from a protected page.

1. Enter email and password.
2. Select **Sign in**.
3. Wait for the account and organization roles to load.

**Result:** The saved local destination opens, or Explore events opens by default. Navigation includes the workspaces allowed by the user's roles. Signing in directly does not automatically send every staff member to a dashboard; they can select **Dashboard** from navigation.

**Alternatives:** Invalid credentials show an error. Failure to load the account shows retry feedback. A saved staff destination still requires staff access after sign-in.

## 3. Student and participant flows

These flows are also available to signed-in organizers and administrators acting as participants. Each registration belongs to the signed-in account; the interface does not register another user on their behalf.

### S1. Register for an event

**Preconditions:** Signed in; event is `REGISTRATION_OPEN`; no active registration for this account/event.

1. Open Explore events and select an event.
2. Review the details and **Your place at this event** panel.
3. Select **Register for this event** once.
4. Wait for the server response while the action is disabled.

**Result:** The panel and registration data refresh using the actual returned status:

| Server result | Meaning | What the participant does next |
| --- | --- | --- |
| `CONFIRMED` | A place was allocated | Review the event time and venue; see the record in My registrations |
| `WAITLISTED` | Capacity was reached and the participant joined the waiting list | Keep the registration and refresh later to check for promotion |

**Alternatives:** The event may close or change between viewing and submitting. The server can reject the request with a meaningful conflict message. Duplicate active registrations are rejected. Published, draft, completed and cancelled events do not offer a new-registration action.

### S2. Follow a waiting-list registration

**Start:** An existing `WAITLISTED` registration.

1. Open **My registrations** (`/registrations`) or the event details.
2. Read the waiting-list explanation.
3. Select **Refresh status** in history or **Refresh my status** on the event page.
4. Check whether the status has become `CONFIRMED`.

**Result:** The latest server status is shown. While registration remains open, the backend promotes the oldest waiting registration when a confirmed cancellation releases a place. Increasing capacity can also allow server-side promotion.

**Limits:** There is no manual “promote me” action, waiting-list position display, email alert, or real-time notification. A place is secured only when the displayed server status becomes confirmed.

### S3. View registration history

**Start:** **My registrations** (`/registrations`).

1. Review event titles, dates, venues, registration statuses and check-in indicators.
2. Filter by confirmed, waitlisted or canceled status, or keep all registrations visible.
3. Use pagination for additional records.
4. Select **View event** to return to details.

**Result:** The participant sees their own registration history, including cancellations. Organization names are not included in the current history response.

**Alternatives:** An account without registrations sees an invitation to explore events. A filter with no matches produces an empty state. API failures offer retry feedback.

### S4. Cancel a registration

**Preconditions:** An active confirmed or waitlisted registration; participant has not checked in; event is not completed.

1. Open My registrations or the event's registration panel.
2. Select **Cancel registration**.
3. Read the confirmation dialog.
4. Confirm cancellation, or choose **Keep current state** to leave the registration unchanged.

**Result:** After a successful server response, the record becomes `CANCELED` and remains in history. When an eligible confirmed cancellation releases a place while registration is open, the server handles waiting-list promotion atomically.

**Alternatives:** Cancelling a waiting-list entry does not release a confirmed place. Completed-event and checked-in registrations cannot be canceled by the participant. A changed server state may cause rejection even if the button was visible earlier.

### S5. Register again after cancellation

**Preconditions:** The previous registration is canceled and the event still has open registration.

1. Open the event details.
2. Select **Register for this event** again.
3. Read the new confirmed or waitlisted result.

**Result:** The backend reuses the registration record, resets check-in, updates its registration time, and re-evaluates capacity. The participant does not retain their former waiting-list position or a previously confirmed place.

### S6. View profile and organization roles

**Start:** **My profile** (`/profile`).

1. Review name, email and global role.
2. Find **Your user ID**, the account's UUID.
3. Review the organizations and organization roles listed below.
4. Open an organization card if the account has a staff membership.

**Result:** The user can distinguish their account-wide role from each organization assignment. Profile and password editing are unavailable in the current interface/API.

### S7. Become an organizer through administrator onboarding

1. Create an ordinary student account if necessary.
2. Open My profile and share the displayed user UUID with the relevant organization administrator through the team's usual communication channel.
3. Ask that administrator to add the account as an organizer in the appropriate organization.
4. After the assignment, sign out and sign in again if the new workspace is not yet visible.
5. Open Dashboard or My organizations and select the assigned organization.

**Result:** The same account gains staff access only in the organization where membership was assigned.

**Limits:** There is no membership-request form, invitation acceptance screen, or self-service role upgrade. Browsing or registering for an event does not create staff membership.

## 4. Organizer flows

Every flow in this section is also available to the relevant organization administrator and to a global administrator. An organizer can manage their organization's events, including events created by another staff member of that organization.

### O1. Select an organization workspace and read the dashboard

**Start:** **Dashboard** (`/dashboard`).

1. Choose an authorized organization in **Organization workspace**.
2. Review counts for all managed events, drafts, published events and events with registration open.
3. Review the upcoming-event list.
4. Open an event's **Manage** link, select **Manage all events**, or choose **Create event**.

**Result:** Dashboard data is scoped to the selected organization. Registration and attendance totals are available inside each event workspace rather than as invented organization-wide analytics.

**Alternatives:** No organization selection produces a selection prompt. An ordinary student sees an organization-access message instead of the staff dashboard. A failed count appears as unavailable rather than a fabricated zero.

### O2. Find and inspect managed events

**Start:** **Manage events** (`/manage/events`).

1. Select the organization workspace.
2. Optionally search the organization's events and choose a lifecycle status.
3. Select **Apply filters**.
4. Use pagination and select **Manage** for an event.

**Result:** The list includes authorized drafts and public lifecycle states. The event workspace (`/manage/events/:id`) shows status, dates, capacity, confirmed/waitlisted/check-in counts, current version and applicable actions.

### O3. Create a draft event

**Start:** **Create event** (`/manage/events/new`).

1. Select the organization that will own the event.
2. Enter title, type, description and venue.
3. Enter start/end date and time in the displayed local timezone.
4. Set a positive whole-number maximum capacity.
5. Select **Create draft event**.

**Result:** After server validation, a new `DRAFT` event is created and its management workspace opens. It is not publicly discoverable and does not accept registrations.

**Alternatives:** Missing or invalid fields require correction. End time must be later than start time. An organization selection without the corresponding staff permission cannot authorize creation. The form does not let the organizer choose an arbitrary initial lifecycle status.

### O4. Edit an active event and resolve stale updates

**Preconditions:** Authorized organization staff; event is draft, published or registration open.

1. Open the event workspace and choose **Edit event details** (`/manage/events/:id/edit`).
2. Change permitted details, including capacity if needed. The owning organization is not changed in this form.
3. Keep capacity at or above the confirmed-registration count.
4. Select **Save changes**.

**Result:** A successful update returns to the management workspace. The backend rechecks capacity and the original event version. If extra places become available while registration is open, it can promote waiting participants.

**Conflict recovery:** If another organizer changed the event, the server can reject the outdated version. Unsaved text and the loaded version are preserved. Review/copy any edits you need to keep, then select **Reload latest event details** to explicitly replace the form with current server values. Reapply the intended changes and save again.

**Limits:** Completed and cancelled events cannot be edited. There is no automatic merge of concurrent edits.

### O5. Publish a draft

**Precondition:** Event is `DRAFT`.

1. Review the event's details in its management workspace.
2. Select **Publish event**.
3. Read the dialog and confirm **Update event status**.

**Result:** Status becomes `PUBLISHED`. The event is publicly visible, but participant registration remains closed.

### O6. Open registration

**Precondition:** Event is `PUBLISHED`.

1. Open its management workspace.
2. Select **Open registration**.
3. Confirm **Update event status**.

**Result:** Status becomes `REGISTRATION_OPEN`. Signed-in participants can request a place, with the backend assigning confirmed or waitlisted status. Check-in is also available for eligible confirmed participants.

**Limit:** There is no separate “close registration but keep event active” or return-to-published action in the implemented lifecycle.

### O7. Inspect participants and the waiting list

**Start:** **Manage registrations** (`/manage/events/:id/registrations`).

1. Review confirmed, waitlisted, canceled and checked-in totals.
2. Review participant names, emails, registration times, status and attendance.
3. Filter by registration status.
4. Optionally search by name/email using **Search this page**.
5. Use pagination, clearing the page search if it hides records on another page.
6. Select **Refresh participants** for current information.

**Result:** Authorized staff see the selected event's participants. Text search is limited to the currently loaded page; totals are event-wide server values.

**Limits:** Staff cannot use this page to manually confirm a waiting participant, register another person, cancel another participant's registration, or export a participant file.

### O8. Check in a participant

**Start:** **Take attendance** (`/manage/events/:id/attendance`), or the event registrations page. Both pages expose the same participant/check-in workflow.

**Preconditions:** Event registration is open; participant is confirmed and not already checked in.

1. Find the participant and verify their identity.
2. Select that participant's **Check in** action.
3. Read the confirmation dialog and confirm **Check in**.
4. Wait for the successful server response.

**Result:** The participant shows **Checked in**, the action is no longer offered for that record, and counts refresh. The participant can see their check-in indicator in their own registration views.

**Alternatives:** Waiting-list and canceled entries have no check-in action. Closed or completed events do not permit check-in. A concurrent check-in may cause a conflict; refresh the list. There is no undo-check-in operation.

### O9. Complete an event

**Precondition:** Event is `REGISTRATION_OPEN`.

1. Finish attendance recording before closing the event.
2. Return to the event workspace.
3. Select **Complete event**.
4. Read and confirm the status-change dialog.

**Result:** Status becomes `COMPLETED`. Registration history and recorded attendance remain available to authorized users. New registrations, event editing, check-in and participant cancellation are closed.

**Limit:** Completion is final in the supported lifecycle. It does not happen automatically merely because the event end time has passed.

### O10. Cancel an event

**Precondition:** Event is `PUBLISHED` or `REGISTRATION_OPEN`.

1. Open the event workspace and select **Cancel event**.
2. Read the consequences in the confirmation dialog.
3. Confirm **Update event status**, or keep the current state.

**Result:** Event status becomes `CANCELLED`. The backend marks active confirmed/waitlisted registrations as `CANCELED` and clears their check-in flags. The event and registration history remain available; the event cannot be reopened or edited.

**Limits:** Draft and completed events do not offer cancellation in this lifecycle. There is no event deletion action or notification/email delivery flow.

### O11. View the organization and its staff

**Start:** **My organizations** (`/organizations`) or **Open organization** from the dashboard.

1. Open an authorized organization card.
2. Review its name, description and ID.
3. Review the paginated member list and organization roles.
4. Select **Organization events** to return to its managed-event list.

**Result:** An organizer can inspect their organization's staff list but cannot add, remove or promote members.

## 5. Organization administrator flows

An `ORG_ADMIN` can perform all organizer flows in section 4 for their own organization, plus the following member-management flows. Their powers do not extend to other organizations or to assigning administrator roles.

### OA1. Open the organization administration context

1. Sign in with an organization-admin membership.
2. Open Dashboard and select the correct organization, or open My organizations.
3. Open the organization details (`/organizations/:id`).
4. Review members and use the available member actions.

**Result:** Member-management controls appear only for an organization the account administers. Organization name and description remain read-only.

### OA2. Add an organizer

**Precondition:** The person already has a registered account and has provided their user UUID.

1. Open the correct organization's member list.
2. Select **Add member**.
3. Enter the existing user's ID.
4. Keep **Organization role** as **Organizer**.
5. Select **Add member** and wait for confirmation.

**Result:** The account becomes an `ORGANIZER` in this organization. It can manage this organization's events after its account/membership data is refreshed, for example by signing in again.

**Alternatives:** A malformed UUID needs correction; an unknown account produces a not-found response; an existing member produces a duplicate-membership conflict. Adding a member does not create their account or an event registration.

### OA3. Remove an organizer

1. Find the organizer in the organization's member list.
2. Select **Remove**.
3. Review the confirmation dialog and confirm **Remove member**.

**Result:** The organization membership is removed after server confirmation. The person's account, personal registrations and memberships in other organizations are not removed.

**Limits:** An organization administrator cannot remove or demote an organization administrator, including themselves as an `ORG_ADMIN`. There are only two staff roles; appointing someone as an organization admin requires a global administrator. There is no additional ordinary-member role to assign.

### OA4. Coordinate administrator changes

1. Identify the existing account that should become an organization administrator.
2. Provide its user UUID and the organization context to a global administrator through the team's usual communication channel.
3. The global administrator follows GA3 or GA4 below.

**Result:** Administrator assignment is performed by a user with the required global authority. There is no in-app administrator-change request or approval inbox.

## 6. Global administrator flows

A global `ADMIN` can perform the participant flows and the event-management/attendance flows in any organization. The global role does not have to be duplicated as an organization membership to provide this administrative access.

### GA1. Browse organizations and choose an administration context

**Start:** **Administration** (`/organizations`) or Dashboard (`/dashboard`).

1. Open Administration to list organizations across the system.
2. Use pagination and open the desired organization.
3. Review organization details, members and organization events.
4. For event work, open Dashboard or Manage events and select that organization.
5. Use **Load more organizations** when additional organization options are available.

**Result:** The global administrator can manage a specific organization's work without creating a fictitious all-organizations analytics dashboard. The event dashboard still requires a selected organization.

### GA2. Create an organization and appoint its first administrator

**Preconditions:** Signed in as global ADMIN; the initial organization administrator already has a registered account.

1. Open Administration and select **New organization**.
2. Enter the organization's name and description.
3. Enter the **Initial administrator user ID** from that person's profile.
4. Select **Create organization**.
5. On success, select **Open organization**.

**Result:** The backend creates the organization and its initial `ORG_ADMIN` membership together. The new organization can then receive organizers and events.

**Alternatives:** A duplicate organization name produces a conflict; an unknown initial-admin account produces a not-found response. The interface does not create an empty organization without an initial administrator.

### GA3. Add an organizer or an additional organization administrator

1. Open the target organization and select **Add member**.
2. Enter the existing account's user UUID.
3. Choose **Organizer** or **Organization admin**.
4. Select **Add member**.

**Result:** The selected organization-specific role is assigned. Assigning `ORG_ADMIN` does not make the person a global ADMIN.

**Alternative:** If the account is already a member, change its role using GA4 instead of adding a duplicate membership.

### GA4. Promote or demote an existing member

1. Find the member in the correct organization's list.
2. Select **Make org admin** for an organizer, or **Make organizer** for an organization administrator.
3. Review and confirm **Update role**.

**Result:** The membership's role changes only in this organization after backend approval.

**Protection:** Demoting the last organization administrator is rejected. Appoint another organization administrator first, then retry the demotion.

### GA5. Remove an organization member or replace an administrator

1. Open the correct organization's member list.
2. If replacing the only administrator, add or promote another administrator first.
3. Select **Remove** on the target member.
4. Confirm **Remove member**.

**Result:** The target organization membership is removed. The account itself remains, as do memberships in other organizations and personal registration history.

**Protection:** Removing the last organization administrator is rejected. This interface cannot delete user accounts or assign/change global roles.

### GA6. Oversee events across organizations

1. Open Dashboard or Manage events.
2. Choose the organization to administer.
3. Follow the relevant organizer flow: create a draft, edit, publish, open registration, inspect participants, take attendance, complete, or cancel.
4. Switch organizations before starting work in another tenant.

**Result:** A global administrator can assist any organization while following the same event lifecycle, capacity, version and attendance rules as organization staff. Global authority does not bypass those business constraints.

## 7. Multiple organizations and combined roles

A single account may have different responsibilities in different organizations.

| Example assignment | Available work |
| --- | --- |
| `ORG_ADMIN` in Organization A | Manage A's events and attendance; add/remove A's organizers |
| `ORGANIZER` in Organization B | Manage B's events and attendance; inspect B's staff; no member administration |
| No membership in Organization C | Browse C's public events and register personally; no staff access to C |

### M1. Switch organization safely

1. Sign in once with the account that holds the memberships.
2. Open Dashboard or Manage events.
3. Select Organization A to work in A's context.
4. Select Organization B to switch the scoped list/counts and available organization operations.
5. Check the organization shown in a create-event form before submitting it.

**Result:** Selecting another organization changes the workspace; it does not grant a new role. Opening an event or organization URL from an unauthorized tenant shows an access/unavailable state and remains subject to server authorization.

**Membership changes during a session:** Controls may reflect previously loaded account data until a refetch or new sign-in. If the server rejects an operation after access was revoked, sign in again and choose an organization still assigned to the account. Hiding a button is not the application's security boundary; the server checks each protected operation.

## 8. Sessions, navigation and error recovery

### C1. Sign out

1. Select **Sign out** in navigation or on My profile.
2. The application clears its in-memory token and cached account/server data.
3. Public discovery remains available. A protected page redirects to sign-in.

There is no server logout endpoint in the current API. Sign-out ends the application's local session.

### C2. Recover after expiry or browser reload

1. If a page reload, token expiry or rejected token ends the session, sign in again.
2. When redirected from a protected page, use the preserved local return destination.
3. Review current data before repeating an action.

The application does not keep access tokens in persistent browser storage and cannot silently refresh them. Unsaved forms can be lost on a full page reload or session-ending navigation; copy important work before reloading.

### C3. Use navigation on a narrow screen or with a keyboard

1. On narrow screens, select **Menu** to open navigation.
2. Choose the desired page; navigation closes after a menu link is selected.
3. Use Tab/Shift+Tab to move between controls and Enter/Space as appropriate to activate them.
4. Escape closes the mobile menu and restores focus to its trigger.
5. Use **Skip to content** to bypass navigation.

Confirmation dialogs keep consequential actions separate from ordinary browsing. Choose **Keep current state** to dismiss a dialog without applying its action. Escape can dismiss it while no submission is in progress.

### C4. Respond to errors

| What the user encounters | Meaning | Recovery |
| --- | --- | --- |
| Required-field or validation feedback | The submitted values do not meet the form/API rules | Correct the highlighted/communicated values and submit again |
| Authentication required / expired session | The request has no valid session | Sign in again and return to the intended page |
| Access denied | The account lacks permission in this organization | Check the selected organization and account; contact the relevant administrator if needed |
| Not found / unavailable | The record does not exist or cannot be exposed to this account | Return to the list and select an available record; drafts are restricted |
| Conflict | A duplicate membership/registration, stale version, capacity rule or concurrent change prevents the action | Read the specific message, refresh relevant data, and retry only if still applicable |
| Too many authentication attempts | The authentication rate limit was reached | Wait before retrying |
| Connection or server failure | The API is unavailable or the request could not finish | Check connectivity/API availability and use the offered retry/refresh action |

For an uncertain mutation outcome after a connection failure, refresh first to check whether the change was saved before submitting it again. A loading indicator or an empty list is not itself a successful mutation.

## 9. Event and registration state reference

### Event lifecycle

| Current state | Publicly visible | Accepts new registrations | Staff may edit details | Check-in allowed | Available next states |
| --- | --- | --- | --- | --- | --- |
| `DRAFT` | No; authorized staff can view it | No | Yes | No | `PUBLISHED` |
| `PUBLISHED` | Yes | No | Yes | No | `REGISTRATION_OPEN`, `CANCELLED` |
| `REGISTRATION_OPEN` | Yes | Yes | Yes, subject to capacity/version rules | Confirmed, not-yet-checked-in participants | `COMPLETED`, `CANCELLED` |
| `COMPLETED` | Yes | No | No | No | None |
| `CANCELLED` | Yes | No | No | No | None |

Lifecycle actions are performed explicitly by authorized staff. An event date passing does not automatically complete it. Terminal states do not support reopening, and drafts cannot jump directly to registration open.

### Participant registration states

| State | Meaning | Participant options |
| --- | --- | --- |
| `CONFIRMED` | A place is allocated | View details; cancel if not checked in and event is not completed; organizer may check the participant in while registration is open |
| `WAITLISTED` | Waiting for a place | Refresh to see promotion; cancel if eligible; no check-in while waitlisted |
| `CANCELED` | Registration is inactive but retained | View history; request registration again if the event is open |

Check-in is a separate flag, not a fourth registration status. Event cancellation is spelled `CANCELLED` in the API, while registration cancellation is spelled `CANCELED`.

## 10. Complete demonstration journeys

Use a dedicated demonstration/test environment with prepared accounts. These sequences describe user actions; they do not imply that sample records or accounts already exist.

### Journey A. Organization onboarding to completed event

1. **Initial organization admin and organizer:** Create ordinary student accounts and share their profile UUIDs with the appropriate administrator.
2. **Global admin:** Create an organization and assign the first organization admin (GA2).
3. **Organization admin:** Add the organizer to that organization (OA2).
4. **Organizer:** Sign in again, choose the organization, and create a draft event (O1, O3).
5. **Organizer:** Review/edit the draft, publish it, then open registration (O4–O6).
6. **Student:** Discover the event, sign in if necessary, and register (G1–G4, S1).
7. **Organizer:** Inspect registrations and check the confirmed participant in (O7, O8).
8. **Student:** See the confirmed and checked-in record in My registrations (S3).
9. **Organizer:** Complete the event after attendance is recorded (O9).
10. **Organization admin / global admin:** Inspect the retained event, participants and attendance in the authorized workspace.

### Journey B. Capacity, waiting list and automatic promotion

1. **Organizer:** Create, publish and open an event with capacity one.
2. **Student A:** Register and receive `CONFIRMED`.
3. **Student B:** Register and receive `WAITLISTED`.
4. **Student A:** Cancel their own registration before check-in/completion.
5. **Backend:** Cancel A and promote the oldest waiting participant within the protected registration operation.
6. **Student B:** Refresh status and see `CONFIRMED`.
7. **Organizer:** Refresh participants and verify the updated statuses/counts.
8. **Student A, optionally:** Register again while registration is open; the current capacity determines the new outcome, rather than the former place.

### Journey C. Multi-organization permission boundaries

1. **Global admin:** Assign one account as an organization admin in A and an organizer in B.
2. **That account:** Sign in, select A, and confirm that adding/removing organizers is available.
3. **That account:** Switch to B and confirm that event management is available while member administration is not.
4. **That account:** Browse public events in C without a staff membership.
5. **That account:** Open a C staff-management URL and observe the access/unavailable state.

### Journey D. Concurrent event editing

1. **Organizer A and Organizer B:** Open the same active event's editor at the same version.
2. **Organizer A:** Save a valid change.
3. **Organizer B:** Submit their older loaded version and receive a stale-update conflict.
4. **Organizer B:** Retain/copy their unsaved work, explicitly reload current details, reapply the intended change and save.

## 11. Current product limits

The following actions are not currently supported as user flows:

- Editing profile details/passwords, password-reset recovery, persistent sign-in, or silent session refresh.
- Self-service organization joining, membership requests, invitations, or global-admin role assignment.
- Public organization-name browsing; public remaining-seat counts; organization information in registration-history rows.
- Searching a user directory by email/name when assigning staff; an existing account UUID is required.
- Editing/deleting organization records or deleting events/accounts through the frontend.
- Closing registration independently, reopening terminal events, manually promoting waiting participants, or reversing check-in.
- Registering/canceling another person's registration from the staff participant page.
- Email/push notifications, live updates, participant exports, payments, or ticketing.

Where these features are absent, the user should use the available read-only information or coordinate with the responsible administrator through their usual communication channel. The interface does not simulate a successful unsupported action.
