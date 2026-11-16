import { describe, expect, it } from "vitest";
import {
  canCancel,
  canManage,
  canManageMembers,
  eventStatuses,
  transitions,
} from "../utils/domain";
import { toLocalInput, toUtc } from "../utils/dates";
import { admin, organizer, orgId, registration, student } from "./fixtures";
describe("role and workflow presentation rules", () => {
  it("keeps organization permissions separate from the global student role", () => {
    expect(canManage(student, orgId)).toBe(false);
    expect(canManage(organizer, orgId)).toBe(true);
    expect(canManage(organizer, "other-tenant")).toBe(false);
    expect(canManageMembers(organizer, orgId)).toBe(false);
    expect(
      canManageMembers(
        {
          ...organizer,
          memberships: [{ ...organizer.memberships[0], orgRole: "ORG_ADMIN" }],
        },
        orgId,
      ),
    ).toBe(true);
    expect(canManage(admin, "other-tenant")).toBe(true);
  });
  it("exposes exactly the five supported lifecycle transitions", () => {
    expect(
      eventStatuses.flatMap((from) =>
        transitions[from].map((to) => `${from}:${to}`),
      ),
    ).toEqual([
      "DRAFT:PUBLISHED",
      "PUBLISHED:REGISTRATION_OPEN",
      "PUBLISHED:CANCELLED",
      "REGISTRATION_OPEN:COMPLETED",
      "REGISTRATION_OPEN:CANCELLED",
    ]);
    expect(transitions.COMPLETED).toEqual([]);
    expect(transitions.CANCELLED).toEqual([]);
  });
  it("only offers participant cancellation when the backend allows it", () => {
    expect(canCancel(registration)).toBe(true);
    expect(canCancel({ ...registration, status: "CANCELED" })).toBe(false);
    expect(canCancel({ ...registration, eventStatus: "COMPLETED" })).toBe(
      false,
    );
    expect(canCancel({ ...registration, checkedIn: true })).toBe(false);
  });
  it("round-trips local date inputs to UTC without shifting the instant", () => {
    const utc = "2026-12-20T14:15:00.000Z";
    expect(toUtc(toLocalInput(utc))).toBe(utc);
  });
});
