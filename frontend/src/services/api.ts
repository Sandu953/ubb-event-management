const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export async function getHealth(): Promise<{
  status: string;
}> {
  const response = await fetch(`${API_URL}/api/health`);

  if (!response.ok) {
    throw new Error("Backend is not available");
  }

  return response.json();
}
