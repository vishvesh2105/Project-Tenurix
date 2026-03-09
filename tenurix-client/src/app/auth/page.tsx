"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Image from "next/image";
import { useI18n } from "@/components/providers/I18nProvider";
import { HeaderControls } from "@/components/ui/HeaderControls";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");
const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID!;

declare global {
  interface Window {
    google?: any;
  }
}

type Role = "client" | "landlord";

export default function AuthPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-slate-100" />}>
      <AuthPageInner />
    </Suspense>
  );
}

function AuthPageInner() {
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
          "Login failed. Please check your credentials and try again.";
        throw new Error(msg);
      }

      if (!data?.token) {
        throw new Error("Login failed. Please try again.");
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
    <main className="min-h-screen bg-gradient-to-br from-slate-100 to-slate-50 px-4 py-8">
      <div className="mx-auto flex min-h-[calc(100vh-4rem)] max-w-6xl items-center justify-center">
        <div className="grid w-full max-w-5xl overflow-hidden rounded-[32px] bg-white shadow-[0_30px_80px_rgba(15,23,42,0.18)] lg:grid-cols-2">
          {/* Left side */}
          <div className="hidden lg:flex flex-col justify-between bg-gradient-to-br from-indigo-950 to-slate-900 px-10 py-10 text-white relative overflow-hidden">
            {/* Decorative elements */}
            <div className="absolute -top-20 -right-20 h-64 w-64 rounded-full bg-indigo-500/10 blur-3xl" />
            <div className="absolute -bottom-16 -left-16 h-48 w-48 rounded-full bg-indigo-500/10 blur-3xl" />

            <div className="relative">
              <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-xs font-medium text-amber-500 backdrop-blur-sm">
                <Image src="/dark-logo.svg" alt="" width={16} height={16} />
                Tenurix
              </div>

              <h2 className="mt-6 text-4xl font-bold leading-tight">
                Login to your<br />
                <span className="text-amber-500">Portal.</span>
              </h2>

              <p className="mt-4 text-sm text-white/60 leading-relaxed max-w-sm">
                Access your personalized dashboard to manage listings, applications, leases, and more.
              </p>
            </div>

            <div className="relative space-y-4">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-5 backdrop-blur-sm hover:bg-white/10 transition-colors">
                <p className="text-sm font-semibold">Client Portal</p>
                <p className="text-xs text-white/50 mt-1">Browse listings, apply for leases, manage payments</p>
              </div>

              <div className="rounded-2xl border border-white/10 bg-white/5 p-5 backdrop-blur-sm hover:bg-white/10 transition-colors">
                <p className="text-sm font-semibold">Landlord Portal</p>
                <p className="text-xs text-white/50 mt-1">Submit properties, manage tenants, track income</p>
              </div>
            </div>
          </div>

          {/* Right side */}
          <div className="relative px-6 py-8 sm:px-10 sm:py-10">
            <div className="mb-8 flex items-start justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[#FFFFFF] shadow-lg shadow-indigo-600/25 overflow-hidden">
                  <Image src="/home-logo.svg" alt="Tenurix" width={36} height={36} />
                </div>

                <div>
                  <p className="text-sm font-medium text-slate-500">Welcome to</p>
                  <h1 className="text-2xl font-bold tracking-tight text-slate-900">
                    {role === "client" ? t("clientPortal") : t("landlordPortal")}
                  </h1>
                </div>
              </div>

              <div className="shrink-0">
                <HeaderControls />
              </div>
            </div>

            <p className="max-w-md text-sm leading-6 text-slate-500">
              {role === "client"
                ? "Sign in using Google to apply for properties."
                : "Sign in using Google to submit listings for management approval."}
            </p>

            {/* Tabs */}
            <div className="mt-8">
              <div className="grid grid-cols-2 rounded-2xl bg-slate-100 p-1">
                <button
                  type="button"
                  onClick={() => setRole("client")}
                  className={`rounded-xl px-4 py-3 text-sm font-medium transition-all duration-200 ${
                    role === "client"
                      ? "bg-white text-indigo-600 shadow-sm"
                      : "text-slate-500 hover:text-slate-900"
                  }`}
                >
                  {t("client")}
                </button>

                <button
                  type="button"
                  onClick={() => setRole("landlord")}
                  className={`rounded-xl px-4 py-3 text-sm font-medium transition-all duration-200 ${
                    role === "landlord"
                      ? "bg-white text-indigo-600 shadow-sm"
                      : "text-slate-500 hover:text-slate-900"
                  }`}
                >
                  {t("landlord")}
                </button>
              </div>
            </div>

            {/* Auth box */}
            <div className="mt-6 rounded-3xl border border-slate-200 bg-slate-50 p-5">
              <div className="mb-4">
                <p className="text-sm font-semibold text-slate-900">
                  Continue with Google
                </p>
                <p className="mt-1 text-xs leading-5 text-slate-500">
                  Use your Google account to continue to the{" "}
                  {role === "client" ? t("client") : t("landlord")} portal.
                </p>
              </div>

              {error && (
                <div className="mb-4 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  {error}
                </div>
              )}

              <div className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
                <div className="flex items-center justify-center">
                  <div id="googleBtn" />
                </div>
              </div>

              {busy && (
                <div className="mt-3 text-center text-xs text-slate-500">
                  {t("signingIn")}
                </div>
              )}
            </div>

            <div className="mt-6 text-center text-xs leading-5 text-slate-500">
              {t("terms")}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}
