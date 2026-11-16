import { describe, expect, it, vi } from "vitest";
import {
  api,
  ApiError,
  onUnauthorized,
  queryString,
  setAccessToken,
} from "../services/api";
import { eventService } from "../services/eventService";
import { organizationService } from "../services/organizationService";
import { registrationService } from "../services/registrationService";
import { eventId, json, orgId, paged, registration } from "./fixtures";
describe("HTTP API contracts", () => {
  it("attaches bearer tokens and preserves exact lifecycle payloads", async () => {
    const fetcher = vi.fn().mockResolvedValue(json({}));
    vi.stubGlobal("fetch", fetcher);
    setAccessToken("test-token");
    await eventService.transition(eventId, "PUBLISHED", 3);
    expect(fetcher).toHaveBeenCalledWith(
      expect.stringContaining(`/api/events/${eventId}/status`),
      expect.objectContaining({
        method: "PATCH",
        body: JSON.stringify({ status: "PUBLISHED", version: 3 }),
        headers: expect.objectContaining({
          Authorization: "Bearer test-token",
        }),
      }),
    );
  });
  it("invalidates authenticated sessions on 401, while public credential failures do not log out a session", async () => {
    const expired = vi.fn();
    const dispose = onUnauthorized(expired);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() => Promise.resolve(json({}, 401))),
    );
    setAccessToken("expired");
    await expect(api("/auth/me")).rejects.toBeInstanceOf(ApiError);
    expect(expired).toHaveBeenCalledOnce();
    await expect(api("/auth/login", { auth: false })).rejects.toBeInstanceOf(
      ApiError,
    );
    expect(expired).toHaveBeenCalledOnce();
    dispose();
  });
  it.each([400, 403, 404, 409, 429])(
    "preserves distinct %i errors and backend validation messages",
    async (status) => {
      vi.stubGlobal(
        "fetch",
        vi
          .fn()
          .mockResolvedValue(
            json({ errors: { Title: ["A title is required."] } }, status),
          ),
      );
      await expect(api("/events")).rejects.toMatchObject({
        status,
        message: "A title is required.",
      });
    },
  );
  it("does not reveal internal details on a server failure", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(json({ detail: "secret stack trace" }, 500)),
    );
    await expect(api("/events")).rejects.toMatchObject({
      status: 500,
      message: "The server could not complete your request. Try again later.",
    });
  });
  it("handles network failure, cancellation, and empty success responses", async () => {
    const fetcher = vi
      .fn()
      .mockRejectedValueOnce(new TypeError("Failed to fetch"))
      .mockRejectedValueOnce(new DOMException("Aborted", "AbortError"))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetcher);
    await expect(api("/events")).rejects.toMatchObject({ status: 0 });
    await expect(api("/events")).rejects.toMatchObject({ name: "AbortError" });
    await expect(registrationService.cancel(eventId)).resolves.toBeUndefined();
  });
  it("looks through every history page when finding an event registration", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(
        json({
          ...paged([]),
          pageSize: 1,
          totalCount: 2,
          items: [{ ...registration, eventId: "another-event" }],
        }),
      )
      .mockResolvedValueOnce(
        json({ ...paged([registration]), page: 2, pageSize: 1, totalCount: 2 }),
      );
    vi.stubGlobal("fetch", fetcher);
    await expect(registrationService.forEvent(eventId)).resolves.toMatchObject({
      eventId,
    });
    expect(fetcher.mock.calls[1][0]).toContain("page=2");
  });
  it("uses organization-scoped membership routes and exact roles", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetcher);
    await organizationService.updateMember(orgId, "user-id", "ORGANIZER");
    expect(fetcher).toHaveBeenCalledWith(
      expect.stringContaining(`/organizations/${orgId}/members/user-id`),
      expect.objectContaining({
        method: "PUT",
        body: '{"orgRole":"ORGANIZER"}',
      }),
    );
  });
  it("encodes query parameters and retains false values", () => {
    expect(
      queryString({
        search: "A & B",
        includeDrafts: false,
        status: "",
        page: undefined,
      }),
    ).toBe("?search=A+%26+B&includeDrafts=false");
  });
});
