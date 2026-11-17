export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}
const baseUrl = (
  import.meta.env.VITE_API_URL || "http://localhost:5088"
).replace(/\/$/, "");
let token: string | null = null;
let unauthorized: (() => void) | undefined;
export function setAccessToken(value: string | null) {
  token = value;
}
export function onUnauthorized(handler: () => void) {
  unauthorized = handler;
  return () => {
    unauthorized = undefined;
  };
}
export function queryString(values: object) {
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== "") params.set(key, String(value));
  });
  return params.toString() ? `?${params}` : "";
}
const fallback: Record<number, string> = {
  400: "Check the form values and try again.",
  401: "Please sign in again. Your session may have expired.",
  403: "You do not have permission to perform this action.",
  404: "This resource was not found or is no longer available.",
  409: "This information changed or conflicts with an existing record. Refresh and try again.",
  429: "Too many attempts. Wait a minute before trying again.",
};
export async function api<T>(
  path: string,
  options: {
    method?: string;
    body?: unknown;
    signal?: AbortSignal;
    auth?: boolean;
  } = {},
): Promise<T> {
  const requestToken = options.auth === false ? null : token;
  let response: Response;
  try {
    response = await fetch(`${baseUrl}/api${path}`, {
      method: options.method || "GET",
      signal: options.signal,
      headers: {
        Accept: "application/json",
        ...(options.body === undefined
          ? {}
          : { "Content-Type": "application/json" }),
        ...(requestToken ? { Authorization: `Bearer ${requestToken}` } : {}),
      },
      body:
        options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch (error) {
    if (
      error &&
      typeof error === "object" &&
      "name" in error &&
      error.name === "AbortError"
    )
      throw error;
    throw new ApiError(
      0,
      "Unable to reach UBB Events. Check your connection and that the API is running.",
    );
  }
  if (!response.ok) {
    if (response.status === 401 && requestToken && requestToken === token)
      unauthorized?.();
    let message =
      fallback[response.status] ||
      "The server could not complete your request. Try again later.";
    if (response.status < 500) {
      const problem: unknown = await response.json().catch(() => null);
      if (problem && typeof problem === "object") {
        if ("detail" in problem && typeof problem.detail === "string")
          message = problem.detail;
        if (
          "errors" in problem &&
          problem.errors &&
          typeof problem.errors === "object"
        ) {
          const errors = Object.values(problem.errors)
            .flat()
            .filter((value): value is string => typeof value === "string");
          if (errors.length) message = errors.join(" ");
        }
      }
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
