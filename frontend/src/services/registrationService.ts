import { api, queryString } from "./api";
import type {
  Attendance,
  MyRegistration,
  Page,
  Participant,
  Registration,
} from "../types/api";
export const registrationService = {
  mine: (page = 1, status = "", signal?: AbortSignal) =>
    api<Page<MyRegistration>>(
      `/registrations/me${queryString({ page, pageSize: 20, status })}`,
      { signal },
    ),
  async forEvent(
    id: string,
    signal?: AbortSignal,
  ): Promise<MyRegistration | null> {
    // No event-specific own-registration endpoint exists. Traverse history without assuming the first page contains it.
    for (let page = 1; ; page++) {
      const result = await this.mine(page, "", signal);
      const match = result.items.find((item) => item.eventId === id);
      if (match) return match;
      if (page * result.pageSize >= result.totalCount || !result.items.length)
        return null;
    }
  },
  register: (id: string) =>
    api<Registration>(`/events/${id}/registrations`, { method: "POST" }),
  cancel: (id: string) =>
    api<void>(`/events/${id}/registrations/me`, { method: "DELETE" }),
  participants: (id: string, page = 1, status = "", signal?: AbortSignal) =>
    api<Page<Participant>>(
      `/events/${id}/registrations${queryString({ page, pageSize: 20, status })}`,
      { signal },
    ),
  attendance: (id: string, signal?: AbortSignal) =>
    api<Attendance>(`/events/${id}/attendance`, { signal }),
  checkIn: (id: string, registrationId: string) =>
    api<Registration>(`/events/${id}/check-in`, {
      method: "POST",
      body: { registrationId },
    }),
};
