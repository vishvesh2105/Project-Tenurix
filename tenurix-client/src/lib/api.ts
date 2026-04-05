const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

export async function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const token = typeof window !== "undefined"
    ? localStorage.getItem("tenurix_token") || ""
    : "";

  const headers: Record<string, string> = {};

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  // Auto-set Content-Type for JSON (skip for FormData)
  if (options.body && !(options.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      ...headers,
      ...(options.headers as Record<string, string>),
    },
  });

  if (res.status === 401) {
    if (typeof window !== "undefined") {
      const portal = localStorage.getItem("tenurix_portal") || "client";
      const validPortal = portal === "landlord" ? "landlord" : "client";
      localStorage.removeItem("tenurix_token");
      localStorage.removeItem("tenurix_portal");
      window.location.href = `/auth?role=${validPortal}`;
    }
    throw new Error("Session expired. Please sign in again.");
  }

  return res;
}

/**
 * Safely read JSON from a Response — handles empty bodies and non-JSON gracefully.
 * Returns null if the body is empty or not valid JSON.
 */
export async function safeJson<T = unknown>(res: Response): Promise<T | null> {
  try {
    const text = await res.text();
    if (!text || !text.trim()) return null;
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}
