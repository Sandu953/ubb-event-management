import { api } from "./api";
import type { AuthResponse, CurrentUser } from "../types/api";
export const authService = {
  login: (body: { email: string; password: string }) =>
    api<AuthResponse>("/auth/login", { method: "POST", body, auth: false }),
  register: (body: { email: string; password: string; fullName: string }) =>
    api<AuthResponse>("/auth/register", { method: "POST", body, auth: false }),
  me: (signal?: AbortSignal) => api<CurrentUser>("/auth/me", { signal }),
};
