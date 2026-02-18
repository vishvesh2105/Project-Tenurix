"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useI18n } from "@/components/providers/I18nProvider";
import { HeaderControls } from "@/components/ui/HeaderControls";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;
const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID!;

declare global {
  interface Window {
    google?: any;
  }
}

type Role = "client" | "landlord";

export default function AuthPage() {
  const sp = useSearchParams();
  const { t } = useI18n();

  const initialRole = (sp.get("role") as Role) || "client";
  const [role, setRole] = useState<Role>(initialRole);

  const next = sp.get("next") || (role === "client" ? "/dashboard" : "/landlord/dashboard");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

async function handleGoogleCredential(idToken: string) {
  setBusy(true);
  setError("");

  try {
    const endpoint = role === "client" ? "/auth/client/google" : "/auth/landlord/google";

    const res = await fetch(`${API_BASE}${endpoint}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ idToken }),
    });

    // SAFELY read response
    const raw = await res.text();
    let data: any = null;

    try {
      data = raw ? JSON.parse(raw) : null;
    } catch {
      data = null;
    }

    if (!res.ok) {
      const msg =
        data?.message ||
        data?.title ||
        (raw ? raw.slice(0, 200) : `Login failed (${res.status})`);
      throw new Error(msg);
    }

    if (!data?.token) {
      throw new Error("Login succeeded but API did not return { token }.");
    }

    localStorage.setItem("tenurix_token", data.token);
    localStorage.setItem("tenurix_portal", role);

    window.location.href = next;
  } catch (e: any) {
    setError(e?.message ?? "Login failed");
  } finally {
    setBusy(false);
  }
}

  useEffect(() => {
    const timer = setInterval(() => {
      if (window.google?.accounts?.id) {
        clearInterval(timer);

        window.google.accounts.id.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: async (response: any) => {
            const idToken = response?.credential;
            if (!idToken) return setError("Google did not return token");
            await handleGoogleCredential(idToken);
          },
        });

        const el = document.getElementById("googleBtn");
        if (el) el.innerHTML = "";

        window.google.accounts.id.renderButton(el, {
          theme: "outline",
          size: "large",
          width: 330,
          shape: "pill",
          text: "continue_with",
        });
      }
    }, 150);

    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role]);

  return (
    <main className="auth-bg min-h-screen px-4 py-10">
      <div className="mx-auto flex max-w-md items-center justify-center">
        <div className="relative w-full rounded-2xl bg-white shadow-2xl">
          {/* top-right controls like WHC */}
          <div className="absolute right-4 top-4">
            <HeaderControls />
          </div>

          <div className="px-10 pt-10 text-center">
            <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-full bg-red-600">
              {/* simple logo */}
              <span className="text-white text-lg font-bold">T</span>
            </div>

            <h1 className="text-xl font-semibold text-slate-900">
              {role === "client" ? t("clientPortal") : t("landlordPortal")}
            </h1>
            <p className="mt-2 text-sm text-slate-500">
              {role === "client"
                ? "Sign in using Google to apply for properties."
                : "Sign in using Google to submit listings for management approval."}
            </p>
          </div>

          {/* Tabs */}
          <div className="mt-6 px-10">
            <div className="flex rounded-xl bg-slate-100 p-1">
              <button
                type="button"
                onClick={() => setRole("client")}
                className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium ${
                  role === "client" ? "bg-white shadow text-slate-900" : "text-slate-600"
                }`}
              >
                {t("client")}
              </button>

              <button
                type="button"
                onClick={() => setRole("landlord")}
                className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium ${
                  role === "landlord" ? "bg-white shadow text-slate-900" : "text-slate-600"
                }`}
              >
                {t("landlord")}
              </button>
            </div>
          </div>

          <div className="px-10 pb-10 pt-6">
            {error ? (
              <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            ) : null}

            <button
              type="button"
              disabled
              className="mb-3 w-full rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-left text-xs text-slate-500"
            >
              {role === "client" ? "Client login (Google only)" : "Landlord login (Google only)"}
            </button>

            <div className="flex items-center justify-center">
              <div id="googleBtn" />
            </div>

            {busy ? (
              <div className="mt-3 text-center text-xs text-slate-500">{t("signingIn")}</div>
            ) : null}

            <div className="mt-6 border-t pt-4 text-center text-xs text-slate-500">
              {t("terms")}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}
