import { createContext, useContext } from "react";
import type { CurrentUser } from "../types/api";
export interface AuthContextValue {
  current: CurrentUser | undefined;
  loading: boolean;
  signedIn: boolean;
  error: Error | null;
  login: (email: string, password: string, fullName?: string) => Promise<void>;
  logout: () => void;
  refresh: () => void;
}
export const AuthContext = createContext<AuthContextValue | null>(null);
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("AuthProvider is required");
  return value;
}
