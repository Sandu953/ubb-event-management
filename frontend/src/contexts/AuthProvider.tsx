import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { authService } from "../services/authService";
import { onUnauthorized, setAccessToken } from "../services/api";
import { AuthContext } from "./auth";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const client = useQueryClient();
  const logout = useCallback(() => {
    setAccessToken(null);
    setExpiresAt(null);
    client.clear();
  }, [client]);
  useEffect(() => onUnauthorized(logout), [logout]);
  useEffect(() => {
    if (!expiresAt) return;
    const timer = window.setTimeout(
      logout,
      Math.max(0, new Date(expiresAt).getTime() - Date.now()),
    );
    return () => window.clearTimeout(timer);
  }, [expiresAt, logout]);
  const me = useQuery({
    queryKey: ["auth", expiresAt],
    queryFn: ({ signal }) => authService.me(signal),
    enabled: !!expiresAt,
    retry: false,
    staleTime: 60_000,
  });
  async function login(email: string, password: string, fullName?: string) {
    const result =
      fullName === undefined
        ? await authService.login({ email, password })
        : await authService.register({ email, password, fullName });
    client.clear();
    setAccessToken(result.accessToken);
    setExpiresAt(result.expiresAt);
  }
  return (
    <AuthContext.Provider
      value={{
        current: expiresAt ? me.data : undefined,
        loading: !!expiresAt && me.isPending,
        signedIn: !!expiresAt,
        error: me.error,
        login,
        logout,
        refresh: () => {
          void me.refetch();
        },
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
