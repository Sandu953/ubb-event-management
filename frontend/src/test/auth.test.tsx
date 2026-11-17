import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../contexts/AuthProvider";
import { useAuth } from "../contexts/auth";
import { api } from "../services/api";
import { json, organizer } from "./fixtures";
function AuthHarness() {
  const auth = useAuth();
  return (
    <>
      <p>
        {auth.current?.user.fullName ||
          (auth.loading ? "Loading account" : "Guest")}
      </p>
      <p>{auth.current?.memberships[0]?.organizationName}</p>
      <button
        onClick={() => {
          void auth.login("student@example.test", "safe-long-password");
        }}
      >
        Log in
      </button>
      <button onClick={auth.logout}>Log out</button>
    </>
  );
}
function renderAuth() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <AuthHarness />
      </AuthProvider>
    </QueryClientProvider>,
  );
  return client;
}
describe("in-memory authentication lifecycle", () => {
  it("loads the current user and memberships after login, then clears account and cached data on logout", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(
        json({
          accessToken: "test-token",
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
          user: organizer.user,
        }),
      )
      .mockResolvedValueOnce(json(organizer));
    vi.stubGlobal("fetch", fetcher);
    const client = renderAuth();
    expect(screen.getByText("Guest")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));
    expect(
      await screen.findByText(organizer.user.fullName),
    ).toBeInTheDocument();
    expect(
      screen.getByText(organizer.memberships[0].organizationName),
    ).toBeInTheDocument();
    expect(fetcher.mock.calls[1][1].headers.Authorization).toBe(
      "Bearer test-token",
    );
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    await userEvent.click(screen.getByRole("button", { name: "Log out" }));
    expect(screen.getByText("Guest")).toBeInTheDocument();
    expect(
      client
        .getQueryCache()
        .getAll()
        .filter((q) => q.state.data),
    ).toHaveLength(0);
  });
  it("clears authentication when an authenticated endpoint rejects the token", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(
        json({
          accessToken: "test-token",
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
          user: organizer.user,
        }),
      )
      .mockResolvedValueOnce(json(organizer))
      .mockResolvedValueOnce(json({}, 401));
    vi.stubGlobal("fetch", fetcher);
    renderAuth();
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));
    await screen.findByText(organizer.user.fullName);
    await expect(api("/registrations/me")).rejects.toMatchObject({
      status: 401,
    });
    await waitFor(() => expect(screen.getByText("Guest")).toBeInTheDocument());
  });
});
