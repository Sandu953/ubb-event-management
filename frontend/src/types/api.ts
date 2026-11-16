export type GlobalRole = "ADMIN" | "STUDENT";
export type OrgRole = "ORG_ADMIN" | "ORGANIZER";
export type EventStatus =
  "DRAFT" | "PUBLISHED" | "REGISTRATION_OPEN" | "COMPLETED" | "CANCELLED";
export type RegistrationStatus = "CONFIRMED" | "WAITLISTED" | "CANCELED";
export interface User {
  id: string;
  email: string;
  fullName: string;
  globalRole: GlobalRole;
}
export interface Membership {
  organizationId: string;
  organizationName: string;
  orgRole: OrgRole;
}
export interface CurrentUser {
  user: User;
  memberships: Membership[];
}
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface Organization {
  id: string;
  name: string;
  description: string;
  createdAt: string;
}
export interface Member {
  userId: string;
  email: string;
  fullName: string;
  orgRole: OrgRole;
}
export interface EventDetails {
  title: string;
  type: string;
  description: string;
  venue: string;
  startTime: string;
  endTime: string;
  maxCapacity: number;
}
export interface Event extends EventDetails {
  id: string;
  organizationId: string;
  status: EventStatus;
  version: number;
  createdBy: string;
}
export interface Registration {
  id: string;
  eventId: string;
  userId: string;
  status: RegistrationStatus;
  checkedIn: boolean;
  registeredAt: string;
}
export interface MyRegistration {
  id: string;
  eventId: string;
  eventTitle: string;
  eventStatus: EventStatus;
  startTime: string;
  venue: string;
  status: RegistrationStatus;
  checkedIn: boolean;
  registeredAt: string;
}
export interface Participant {
  registrationId: string;
  userId: string;
  fullName: string;
  email: string;
  status: RegistrationStatus;
  checkedIn: boolean;
  registeredAt: string;
}
export interface Attendance {
  eventId: string;
  confirmed: number;
  waitlisted: number;
  canceled: number;
  checkedIn: number;
}
export interface EventFilters {
  page?: number;
  pageSize?: number;
  organizationId?: string;
  search?: string;
  type?: string;
  status?: string;
  from?: string;
  to?: string;
  includeDrafts?: boolean;
}
