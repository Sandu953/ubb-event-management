import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../contexts/auth";
import { eventService } from "../services/eventService";
import { organizationService } from "../services/organizationService";
import type { EventFilters } from "../types/api";
export function useEvents(filters: EventFilters, enabled = true) {
  const { current } = useAuth();
  return useQuery({
    queryKey: ["events", current?.user.id, filters],
    queryFn: ({ signal }) => eventService.list(filters, signal),
    enabled,
  });
}
export function useEvent(id: string) {
  const { current } = useAuth();
  return useQuery({
    queryKey: ["event", current?.user.id, id],
    queryFn: ({ signal }) => eventService.get(id, signal),
    enabled: !!id,
  });
}
export function useOrganizations(page = 1) {
  const { current } = useAuth();
  return useQuery({
    queryKey: ["organizations", current?.user.id, page],
    queryFn: ({ signal }) => organizationService.list(page, signal),
    enabled: !!current,
  });
}
