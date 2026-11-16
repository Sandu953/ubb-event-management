// Read-only checks. Never creates accounts, events, memberships, or registrations.
import { strict as assert } from "node:assert";
import { test } from "node:test";
const base = (process.env.VITE_API_URL || "http://localhost:5088").replace(
  /\/$/,
  "",
);
const get = (path) =>
  fetch(`${base}${path}`, { signal: AbortSignal.timeout(10_000) });
test("real API health", async () => {
  const response = await get("/api/health");
  assert.equal(response.status, 200);
  assert.equal((await response.json()).status, "ok");
});
test("API allows the local frontend origin", async () => {
  const response = await fetch(`${base}/api/health`, {
    headers: { Origin: "http://localhost:5173" },
    signal: AbortSignal.timeout(10_000),
  });
  assert.equal(response.status, 200);
  assert.equal(
    response.headers.get("access-control-allow-origin"),
    "http://localhost:5173",
  );
});
test("live OpenAPI contains every integrated workflow and required response fields", async () => {
  const response = await get("/swagger/v1/swagger.json");
  assert.equal(
    response.status,
    200,
    "Run the API in Development to expose Swagger.",
  );
  const spec = await response.json();
  const methods = {
    "/api/auth/login": ["post"],
    "/api/auth/register": ["post"],
    "/api/auth/me": ["get"],
    "/api/events": ["get", "post"],
    "/api/events/{id}": ["get", "put"],
    "/api/events/{id}/status": ["patch"],
    "/api/events/{id}/registrations": ["get", "post"],
    "/api/events/{id}/registrations/me": ["delete"],
    "/api/registrations/me": ["get"],
    "/api/events/{id}/check-in": ["post"],
    "/api/events/{id}/attendance": ["get"],
    "/api/organizations": ["get", "post"],
    "/api/organizations/{id}": ["get"],
    "/api/organizations/{id}/members": ["get", "post"],
    "/api/organizations/{id}/members/{userId}": ["put", "delete"],
  };
  for (const [path, verbs] of Object.entries(methods))
    for (const method of verbs)
      assert.ok(spec.paths[path]?.[method], `${method} ${path}`);
  const fields = {
    AuthResponse: ["accessToken", "expiresAt", "user"],
    CurrentUserResponse: ["user", "memberships"],
    EventResponse: [
      "id",
      "organizationId",
      "title",
      "type",
      "description",
      "venue",
      "startTime",
      "endTime",
      "maxCapacity",
      "status",
      "version",
      "createdBy",
    ],
    MyRegistrationResponse: [
      "id",
      "eventId",
      "eventTitle",
      "eventStatus",
      "startTime",
      "venue",
      "status",
      "checkedIn",
      "registeredAt",
    ],
    ParticipantResponse: [
      "registrationId",
      "userId",
      "fullName",
      "email",
      "status",
      "checkedIn",
      "registeredAt",
    ],
    MemberResponse: ["userId", "email", "fullName", "orgRole"],
    AttendanceResponse: [
      "eventId",
      "confirmed",
      "waitlisted",
      "canceled",
      "checkedIn",
    ],
  };
  for (const [schema, names] of Object.entries(fields))
    for (const name of names)
      assert.ok(
        spec.components.schemas[schema]?.properties[name],
        `${schema}.${name}`,
      );
});
test("real public discovery returns a paginated response without drafts", async () => {
  const response = await get("/api/events?page=1&pageSize=1");
  assert.equal(response.status, 200);
  const page = await response.json();
  assert.ok(Array.isArray(page.items));
  assert.equal(page.page, 1);
  assert.equal(page.pageSize, 1);
  assert.equal(typeof page.totalCount, "number");
  for (const event of page.items) {
    assert.notEqual(event.status, "DRAFT");
    const detail = await get(`/api/events/${event.id}`);
    assert.equal(detail.status, 200);
    assert.equal((await detail.json()).id, event.id);
  }
});
test("protected real endpoints reject anonymous requests with 401", async () => {
  for (const path of [
    "/api/auth/me",
    "/api/organizations",
    "/api/registrations/me",
  ]) {
    const response = await get(path);
    assert.equal(response.status, 401, path);
  }
});
