import type { CurrentUser, EventStatus, MyRegistration } from "../types/api";
export const eventStatuses: EventStatus[] = [
  "DRAFT",
  "PUBLISHED",
  "REGISTRATION_OPEN",
  "COMPLETED",
  "CANCELLED",
];
export const transitions: Record<EventStatus, EventStatus[]> = {
  DRAFT: ["PUBLISHED"],
  PUBLISHED: ["REGISTRATION_OPEN", "CANCELLED"],
  REGISTRATION_OPEN: ["COMPLETED", "CANCELLED"],
  COMPLETED: [],
  CANCELLED: [],
};
export const label = (value: string) =>
  value
    .toLowerCase()
    .replaceAll("_", " ")
    .replace(/^./, (character) => character.toUpperCase());
export const isAdmin = (current?: CurrentUser) =>
  current?.user.globalRole === "ADMIN";
export const isStaff = (current?: CurrentUser) =>
  isAdmin(current) || !!current?.memberships.length;
export const canManage = (current: CurrentUser | undefined, id: string) =>
  isAdmin(current) ||
  !!current?.memberships.some((m) => m.organizationId === id);
export const canManageMembers = (
  current: CurrentUser | undefined,
  id: string,
) =>
  isAdmin(current) ||
  !!current?.memberships.some(
    (m) => m.organizationId === id && m.orgRole === "ORG_ADMIN",
  );
export const canCancel = (registration: MyRegistration) =>
  registration.status !== "CANCELED" &&
  !registration.checkedIn &&
  registration.eventStatus !== "COMPLETED";
export const uuidPattern =
  "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
