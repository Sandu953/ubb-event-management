import { render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import { vi } from "vitest";
import { AuthContext, type AuthContextValue } from "../contexts/auth";
import type { CurrentUser } from "../types/api";
export function renderApp(
  element: ReactNode,
  {
    current,
    path = "/",
    auth: overrides = {},
  }: {
    current?: CurrentUser;
    path?: string;
    auth?: Partial<AuthContextValue>;
  } = {},
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const auth: AuthContextValue = {
    current,
    signedIn: !!current,
    loading: false,
    error: null,
    login: vi.fn(),
    logout: vi.fn(),
    refresh: vi.fn(),
    ...overrides,
  };
  return {
    ...render(
      <QueryClientProvider client={client}>
        <AuthContext.Provider value={auth}>
          <MemoryRouter initialEntries={[path]}>{element}</MemoryRouter>
        </AuthContext.Provider>
      </QueryClientProvider>,
    ),
    client,
    auth,
  };
}
