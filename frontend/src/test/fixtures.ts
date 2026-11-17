import type { CurrentUser, Event, MyRegistration, Page } from "../types/api";
export const orgId = "11111111-1111-4111-8111-111111111111";
export const eventId = "22222222-2222-4222-8222-222222222222";
export const student: CurrentUser = {
  user: {
    id: "33333333-3333-4333-8333-333333333333",
    email: "student@example.test",
    fullName: "Alex Student",
    globalRole: "STUDENT",
  },
  memberships: [],
};
export const organizer: CurrentUser = {
  ...student,
  memberships: [
    {
      organizationId: orgId,
      organizationName: "Student Society",
      orgRole: "ORGANIZER",
    },
  ],
};
export const admin: CurrentUser = {
  ...student,
  user: { ...student.user, globalRole: "ADMIN" },
};
export const event: Event = {
  id: eventId,
  organizationId: orgId,
  title: "Ideas into action",
  type: "Workshop",
  description: "An afternoon of collaboration and practical ideas.",
  venue: "Campus hall",
  startTime: "2026-12-20T14:00:00Z",
  endTime: "2026-12-20T16:00:00Z",
  maxCapacity: 30,
  status: "REGISTRATION_OPEN",
  version: 3,
  createdBy: student.user.id,
};
export const registration: MyRegistration = {
  id: "registration-id",
  eventId,
  eventTitle: event.title,
  eventStatus: event.status,
  startTime: event.startTime,
  venue: event.venue,
  status: "CONFIRMED",
  checkedIn: false,
  registeredAt: "2026-11-01T10:00:00Z",
};
export const paged = <T>(items: T[]): Page<T> => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
});
export const json = (value: unknown, status = 200) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
