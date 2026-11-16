import { api, queryString } from "./api";
import type { Member, Organization, OrgRole, Page } from "../types/api";
export const organizationService = {
  list: (page = 1, signal?: AbortSignal) =>
    api<Page<Organization>>(
      `/organizations${queryString({ page, pageSize: 20 })}`,
      { signal },
    ),
  get: (id: string, signal?: AbortSignal) =>
    api<Organization>(`/organizations/${id}`, { signal }),
  create: (body: {
    name: string;
    description: string;
    initialAdminUserId: string;
  }) => api<Organization>("/organizations", { method: "POST", body }),
  members: (id: string, page = 1, signal?: AbortSignal) =>
    api<Page<Member>>(
      `/organizations/${id}/members${queryString({ page, pageSize: 20 })}`,
      { signal },
    ),
  addMember: (id: string, userId: string, orgRole: OrgRole) =>
    api<Member>(`/organizations/${id}/members`, {
      method: "POST",
      body: { userId, orgRole },
    }),
  updateMember: (id: string, userId: string, orgRole: OrgRole) =>
    api<void>(`/organizations/${id}/members/${userId}`, {
      method: "PUT",
      body: { orgRole },
    }),
  removeMember: (id: string, userId: string) =>
    api<void>(`/organizations/${id}/members/${userId}`, { method: "DELETE" }),
};
