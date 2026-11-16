import {
  act,
  fireEvent,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Route, Routes } from "react-router-dom";
import AuthPage from "../pages/auth/AuthPage";
import EventsPage from "../pages/public/EventsPage";
import EventDetailsPage from "../pages/public/EventDetailsPage";
import RegistrationsPage from "../pages/student/RegistrationsPage";
import ManageEventPage from "../pages/organizer/ManageEventPage";
import ParticipantsPage from "../pages/organizer/ParticipantsPage";
import ProtectedRoute from "../components/layout/ProtectedRoute";
import Layout from "../components/layout/Layout";
import RegistrationPanel from "../components/registrations/RegistrationPanel";
import EventForm from "../components/events/EventForm";
import MembersPanel from "../components/organizations/MembersPanel";
import EventEditorPage from "../pages/organizer/EventEditorPage";
import {
  admin,
  event,
  eventId,
  json,
  organizer,
  orgId,
  paged,
  registration,
  student,
} from "./fixtures";
import { renderApp } from "./render";
import type { MyRegistration, RegistrationStatus } from "../types/api";

function registrationNetwork(status: RegistrationStatus = "CONFIRMED") {
  let records: MyRegistration[] = [];
  const fetcher = vi
    .fn()
    .mockImplementation((_url: string, options: RequestInit) => {
      if (options.method === "POST") {
        records = [{ ...registration, status }];
        return Promise.resolve(json({ ...registration, status }));
      }
      if (options.method === "DELETE") {
        records = [{ ...registration, status: "CANCELED" }];
        return Promise.resolve(new Response(null, { status: 204 }));
      }
      return Promise.resolve(json(paged(records)));
    });
  vi.stubGlobal("fetch", fetcher);
  return fetcher;
}
describe("authentication forms and routes", () => {
  it("moves keyboard focus into mobile navigation and restores it on Escape", async () => {
    renderApp(
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<p>Page</p>} />
        </Route>
      </Routes>,
    );
    const menu = screen.getByRole("button", { name: /Menu/ });
    await userEvent.click(menu);
    expect(menu).toHaveAttribute("aria-expanded", "true");
    expect(menu).toHaveAttribute("aria-controls", "main-navigation");
    const brand = screen.getByRole("link", { name: /UBB Events/ });
    expect(brand).toHaveFocus();
    await userEvent.keyboard("{Escape}");
    expect(menu).toHaveFocus();
    expect(menu).toHaveAttribute("aria-expanded", "false");
  });
  it("requires credentials and never submits an empty login", async () => {
    const { auth } = renderApp(<AuthPage />);
    await userEvent.click(screen.getByRole("button", { name: "Sign in ↗" }));
    expect(auth.login).not.toHaveBeenCalled();
    expect(screen.getByLabelText("Email address *")).toBeRequired();
    expect(screen.getByLabelText("Password *")).toBeRequired();
  });
  it("submits credentials, toggles password visibility, and displays credential errors", async () => {
    const login = vi.fn().mockRejectedValue(new Error("Invalid credentials."));
    renderApp(<AuthPage />, { auth: { login } });
    await userEvent.type(
      screen.getByLabelText("Email address *"),
      "student@example.test",
    );
    await userEvent.type(
      screen.getByLabelText("Password *"),
      "incorrect-password",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Show password" }),
    );
    expect(screen.getByLabelText("Password *")).toHaveAttribute("type", "text");
    await userEvent.click(screen.getByRole("button", { name: "Sign in ↗" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Invalid credentials.",
    );
    expect(login).toHaveBeenCalledWith(
      "student@example.test",
      "incorrect-password",
      undefined,
    );
  });
  it("validates matching passwords and exposes no role assignment during public registration", async () => {
    const { auth } = renderApp(<AuthPage register />);
    await userEvent.type(screen.getByLabelText("Full name *"), "Alex Student");
    await userEvent.type(
      screen.getByLabelText("Email address *"),
      "student@example.test",
    );
    await userEvent.type(
      screen.getByLabelText("Password *"),
      "safe-long-password",
    );
    await userEvent.type(
      screen.getByLabelText("Confirm password *"),
      "different-password",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Create student account ↗" }),
    );
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Passwords do not match.",
    );
    expect(auth.login).not.toHaveBeenCalled();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });
  it("redirects guests to login and preserves the intended route", async () => {
    renderApp(
      <Routes>
        <Route element={<ProtectedRoute />}>
          <Route path="/registrations" element={<p>Private history</p>} />
        </Route>
        <Route path="/login" element={<AuthPage />} />
      </Routes>,
      { path: "/registrations" },
    );
    expect(
      await screen.findByRole("heading", { name: "Sign in to UBB Events" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Create an account" }),
    ).toHaveAttribute("href", "/register?redirect=%2Fregistrations");
    expect(screen.queryByText("Private history")).not.toBeInTheDocument();
  });
  it("rejects staff routes for ordinary students", () => {
    renderApp(
      <Routes>
        <Route element={<ProtectedRoute staff />}>
          <Route path="/" element={<p>Staff data</p>} />
        </Route>
      </Routes>,
      { current: student },
    );
    expect(
      screen.getByRole("heading", { name: "Organization access required" }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Staff data")).not.toBeInTheDocument();
  });
  it.each([
    { current: student, staff: false },
    { current: organizer, staff: true },
    { current: admin, staff: true },
  ])(
    "shows role-aware navigation for $current.user.globalRole / staff=$staff",
    ({ current, staff }) => {
      renderApp(
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<p>Page</p>} />
          </Route>
        </Routes>,
        { current },
      );
      expect(!!screen.queryByRole("link", { name: /Manage events/ })).toBe(
        staff,
      );
      expect(
        screen.getByRole("link", { name: /My registrations/ }),
      ).toBeInTheDocument();
    },
  );
});
describe("discovery and registration workflows", () => {
  it("shows account loading errors without an indefinite registration spinner", () => {
    renderApp(<RegistrationPanel event={event} />, {
      auth: { signedIn: true, error: new Error("Account unavailable.") },
    });
    expect(screen.getByRole("alert")).toHaveTextContent("Account unavailable.");
    expect(screen.queryByText("Loading…")).not.toBeInTheDocument();
  });
  it("renders public events and sends search and status filters to the backend", async () => {
    const fetcher = vi
      .fn()
      .mockImplementation(() => Promise.resolve(json(paged([event]))));
    vi.stubGlobal("fetch", fetcher);
    renderApp(<EventsPage />, { path: "/events" });
    expect(
      await screen.findByRole("heading", { name: event.title }),
    ).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("Search events"), "ideas");
    await userEvent.selectOptions(
      screen.getByLabelText("Event status"),
      "REGISTRATION_OPEN",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Find events ↗" }),
    );
    await waitFor(() =>
      expect(
        fetcher.mock.calls.some(
          ([url]) =>
            String(url).includes("search=ideas") &&
            String(url).includes("status=REGISTRATION_OPEN"),
        ),
      ).toBe(true),
    );
  });
  it("shows loading, empty, and API error states", async () => {
    let resolve: (response: Response) => void = () => {};
    const fetcher = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise<Response>((r) => {
            resolve = r;
          }),
      )
      .mockResolvedValueOnce(json({}, 403));
    vi.stubGlobal("fetch", fetcher);
    renderApp(<EventsPage />);
    expect(screen.getByRole("status")).toHaveTextContent("Loading");
    resolve(json(paged([])));
    expect(
      await screen.findByRole("heading", { name: "No events found" }),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("button", { name: "Find events ↗" }),
    );
    // A changed query makes the second request without relying on a timer.
    await userEvent.type(screen.getByLabelText("Search events"), "missing");
    await userEvent.click(
      screen.getByRole("button", { name: "Find events ↗" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent("Access denied");
  });
  it("renders event details and redirects guests to sign in to register", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(event)));
    renderApp(
      <Routes>
        <Route path="/events/:id" element={<EventDetailsPage />} />
      </Routes>,
      { path: `/events/${eventId}` },
    );
    expect(
      await screen.findByRole("heading", { name: event.title }),
    ).toBeInTheDocument();
    expect(screen.getByText(event.description)).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Sign in to register ↗" }),
    ).toHaveAttribute(
      "href",
      `/login?redirect=${encodeURIComponent(`/events/${eventId}`)}`,
    );
    expect(
      screen.queryByRole("link", { name: "Manage event ↗" }),
    ).not.toBeInTheDocument();
  });
  it.each(["CONFIRMED", "WAITLISTED"] as const)(
    "displays a successful %s registration using the returned status",
    async (status) => {
      const fetcher = registrationNetwork(status);
      renderApp(<RegistrationPanel event={event} />, { current: student });
      await userEvent.click(
        await screen.findByRole("button", {
          name: "Register for this event ↗",
        }),
      );
      expect(
        await screen.findByText(
          status === "WAITLISTED" ? "Waitlisted" : "Confirmed",
          { selector: ".badge" },
        ),
      ).toBeInTheDocument();
      expect(screen.getByRole("status")).toHaveTextContent(
        status === "WAITLISTED" ? "waiting list" : "confirmed",
      );
      expect(
        fetcher.mock.calls.filter(([, options]) => options.method === "POST"),
      ).toHaveLength(1);
    },
  );
  it("cancels registration after confirmation and refreshes history", async () => {
    let records = [registration];
    const fetcher = vi
      .fn()
      .mockImplementation((_url: string, options: RequestInit) => {
        if (options.method === "DELETE") {
          records = [{ ...registration, status: "CANCELED" }];
          return Promise.resolve(new Response(null, { status: 204 }));
        }
        return Promise.resolve(json(paged(records)));
      });
    vi.stubGlobal("fetch", fetcher);
    renderApp(<RegistrationsPage />, { current: student });
    await userEvent.click(
      await screen.findByRole("button", { name: "Cancel registration" }),
    );
    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAccessibleName("Cancel your registration?");
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Cancel registration" }),
    );
    expect(
      await screen.findByText("Canceled", { selector: ".badge" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Cancel registration" }),
    ).not.toBeInTheDocument();
  });
  it("does not offer registration when the lifecycle is closed", async () => {
    registrationNetwork();
    renderApp(<RegistrationPanel event={{ ...event, status: "PUBLISHED" }} />, {
      current: student,
    });
    expect(
      await screen.findByText("Registration has not opened yet."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Register for this event ↗" }),
    ).not.toBeInTheDocument();
  });
});
describe("organizer and administration workflows", () => {
  it("preserves unsaved edits and their original version when server data changes", async () => {
    let serverEvent = event;
    const fetcher = vi
      .fn()
      .mockImplementation((url: string, options: RequestInit) =>
        Promise.resolve(
          options.method === "PUT"
            ? json(
                {
                  detail:
                    "The event changed. Reload it and submit the current version.",
                },
                409,
              )
            : url.endsWith("/attendance")
              ? json({ confirmed: 0, waitlisted: 0, canceled: 0, checkedIn: 0 })
              : json(serverEvent),
        ),
      );
    vi.stubGlobal("fetch", fetcher);
    const { client } = renderApp(
      <Routes>
        <Route path="/manage/events/:id/edit" element={<EventEditorPage />} />
      </Routes>,
      { current: organizer, path: `/manage/events/${eventId}/edit` },
    );
    const title = await screen.findByLabelText("Event title *");
    await userEvent.clear(title);
    await userEvent.type(title, "My unsaved title");
    serverEvent = {
      ...event,
      title: "Another organizer updated this",
      version: 4,
    };
    act(() => {
      client.setQueryData(["event", organizer.user.id, eventId], serverEvent);
    });
    expect(title).toHaveValue("My unsaved title");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The event changed.",
    );
    expect(
      JSON.parse(
        fetcher.mock.calls.find(([, options]) => options.method === "PUT")![1]
          .body,
      ).version,
    ).toBe(3);
    expect(title).toHaveValue("My unsaved title");
    await userEvent.click(
      screen.getByRole("button", { name: "Reload latest event details" }),
    );
    await waitFor(() =>
      expect(screen.getByLabelText("Event title *")).toHaveValue(
        serverEvent.title,
      ),
    );
    await userEvent.clear(screen.getByLabelText("Event title *"));
    await userEvent.type(
      screen.getByLabelText("Event title *"),
      "Discard this edit too",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Reload latest event details" }),
    );
    await waitFor(() =>
      expect(screen.getByLabelText("Event title *")).toHaveValue(
        serverEvent.title,
      ),
    );
  });
  it("validates event times before sending a create request", async () => {
    const submit = vi.fn();
    renderApp(
      <EventForm initialOrganization={orgId} busy={false} onSubmit={submit} />,
      { current: organizer },
    );
    fireEvent.change(screen.getByLabelText("Event title *"), {
      target: { value: event.title },
    });
    fireEvent.change(screen.getByLabelText("Event type *"), {
      target: { value: event.type },
    });
    fireEvent.change(screen.getByLabelText("Venue *"), {
      target: { value: event.venue },
    });
    fireEvent.change(screen.getByLabelText("Description *"), {
      target: { value: event.description },
    });
    fireEvent.change(screen.getByLabelText("Start date and time *"), {
      target: { value: "2026-12-20T16:00" },
    });
    fireEvent.change(screen.getByLabelText("End date and time *"), {
      target: { value: "2026-12-20T15:00" },
    });
    fireEvent.submit(
      screen
        .getByRole("button", { name: "Create draft event ↗" })
        .closest("form")!,
    );
    expect(screen.getByRole("alert")).toHaveTextContent(
      "End time must be after start time.",
    );
    expect(submit).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText("End date and time *"), {
      target: { value: "2026-12-20T17:00" },
    });
    fireEvent.submit(
      screen
        .getByRole("button", { name: "Create draft event ↗" })
        .closest("form")!,
    );
    expect(submit).toHaveBeenCalledWith(
      expect.objectContaining({
        title: event.title,
        startTime: expect.stringMatching(/Z$/),
      }),
      orgId,
    );
  });
  it.each([
    { status: "DRAFT" as const, action: "Publish event" },
    { status: "COMPLETED" as const, action: null },
  ])(
    "offers only valid lifecycle controls for $status",
    async ({ status, action }) => {
      vi.stubGlobal(
        "fetch",
        vi
          .fn()
          .mockImplementation((url: string) =>
            Promise.resolve(
              json(
                url.endsWith("/attendance")
                  ? { confirmed: 2, waitlisted: 1, canceled: 0, checkedIn: 0 }
                  : { ...event, status },
              ),
            ),
          ),
      );
      renderApp(
        <Routes>
          <Route path="/manage/events/:id" element={<ManageEventPage />} />
        </Routes>,
        { current: organizer, path: `/manage/events/${eventId}` },
      );
      await screen.findByRole("heading", { name: event.title });
      if (action)
        expect(
          screen.getByRole("button", { name: action }),
        ).toBeInTheDocument();
      else {
        expect(
          screen.queryByRole("button", { name: "Publish event" }),
        ).not.toBeInTheDocument();
        expect(
          screen.queryByRole("button", { name: "Cancel event" }),
        ).not.toBeInTheDocument();
      }
      expect(
        screen.queryByRole("button", { name: "Open registration" }),
      ).not.toBeInTheDocument();
    },
  );
  it("denies event management across organization boundaries without loading attendance", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(
        json({ ...event, organizationId: "another-organization" }),
      );
    vi.stubGlobal("fetch", fetcher);
    renderApp(
      <Routes>
        <Route path="/manage/events/:id" element={<ManageEventPage />} />
      </Routes>,
      { current: organizer, path: `/manage/events/${eventId}` },
    );
    expect(
      await screen.findByRole("heading", { name: "Access denied" }),
    ).toBeInTheDocument();
    expect(
      fetcher.mock.calls.some(([url]) => String(url).endsWith("/attendance")),
    ).toBe(false);
  });
  it("only checks in confirmed participants and refreshes attendance after success", async () => {
    let checkedIn = false;
    const fetcher = vi
      .fn()
      .mockImplementation((url: string, options: RequestInit) => {
        if (options.method === "POST") {
          checkedIn = true;
          return Promise.resolve(json({ ...registration, checkedIn }));
        }
        if (url.endsWith("/attendance"))
          return Promise.resolve(
            json({
              confirmed: 1,
              waitlisted: 1,
              canceled: 0,
              checkedIn: checkedIn ? 1 : 0,
            }),
          );
        if (url.includes("/registrations"))
          return Promise.resolve(
            json(
              paged([
                {
                  registrationId: "confirmed",
                  userId: student.user.id,
                  fullName: "Confirmed Student",
                  email: student.user.email,
                  status: "CONFIRMED",
                  checkedIn,
                  registeredAt: registration.registeredAt,
                },
                {
                  registrationId: "waiting",
                  userId: "other-user",
                  fullName: "Waiting Student",
                  email: "waiting@example.test",
                  status: "WAITLISTED",
                  checkedIn: false,
                  registeredAt: registration.registeredAt,
                },
              ]),
            ),
          );
        return Promise.resolve(json(event));
      });
    vi.stubGlobal("fetch", fetcher);
    renderApp(
      <Routes>
        <Route
          path="/manage/events/:id/attendance"
          element={<ParticipantsPage attendanceView />}
        />
      </Routes>,
      { current: organizer, path: `/manage/events/${eventId}/attendance` },
    );
    await screen.findByText("Confirmed Student");
    expect(screen.getAllByRole("button", { name: "Check in" })).toHaveLength(1);
    await userEvent.click(screen.getByRole("button", { name: "Check in" }));
    await userEvent.click(
      within(screen.getByRole("dialog")).getByRole("button", {
        name: "Check in",
      }),
    );
    expect(await screen.findByText("✓ Checked in")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Check in" }),
    ).not.toBeInTheDocument();
  });
  it("lets organization administrators add organizers but hides administrator promotion", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(json(paged([]))));
    const orgAdmin = {
      ...organizer,
      memberships: [
        { ...organizer.memberships[0], orgRole: "ORG_ADMIN" as const },
      ],
    };
    renderApp(<MembersPanel organizationId={orgId} />, { current: orgAdmin });
    await userEvent.click(
      screen.getByRole("button", { name: "Add member ＋" }),
    );
    expect(
      screen.getByRole("option", { name: "Organizer" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("option", { name: "Organization admin" }),
    ).not.toBeInTheDocument();
  });
});
