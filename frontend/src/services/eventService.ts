import { api, queryString } from "./api";
import type {
  Event,
  EventDetails,
  EventFilters,
  EventStatus,
  Page,
} from "../types/api";
export const eventService = {
  list: (filters: EventFilters, signal?: AbortSignal) =>
    api<Page<Event>>(`/events${queryString(filters)}`, { signal }),
  get: (id: string, signal?: AbortSignal) =>
    api<Event>(`/events/${id}`, { signal }),
  create: (body: EventDetails & { organizationId: string }) =>
    api<Event>("/events", { method: "POST", body }),
  update: (id: string, body: EventDetails & { version: number }) =>
    api<Event>(`/events/${id}`, { method: "PUT", body }),
  transition: (id: string, status: EventStatus, version: number) =>
    api<Event>(`/events/${id}/status`, {
      method: "PATCH",
      body: { status, version },
    }),
};
