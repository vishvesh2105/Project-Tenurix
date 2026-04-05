"use client";

import { useState, useEffect } from "react";
import AuthShell from "@/components/auth/AuthShell";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

export default function LandlordVerifyPage() {
  const [verificationId, setVerificationId] = useState("");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const id = sessionStorage.getItem("ll_verify_id") || "";
    const ph = sessionStorage.getItem("ll_verify_phone") || "";
    setVerificationId(id);
    setPhone(ph);
    if (!id) setError("Verification session expired. Please register again.");
  }, []);

  async function verify() {
    try {
      setBusy(true);
      setError("");

      const res = await fetch(`${API_BASE}/auth/landlord/verify-otp`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ verificationId, code }),
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data?.message || data?.title || "Verification failed. Please try again.");

      // after verify, API returns token
      localStorage.setItem("tenurix_token", data.token);
      localStorage.setItem("tenurix_portal", "landlord");
      window.location.href = "/landlord/dashboard";
    } catch (e: any) {
      setError(e?.message ?? "Verify failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell title="Verify Mobile Number" subtitle={`Enter the 4-digit code sent to ${phone || "your phone"}`}>
      {error ? (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      <div className="space-y-3">
        <div className="rounded-lg border bg-slate-50 px-3 py-2">
          <div className="text-xs text-slate-500">4-digit code</div>
          <input
            className="w-full bg-transparent text-sm outline-none tracking-[0.4em]"
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 4))}
            placeholder="1234"
            inputMode="numeric"
          />
        </div>

        <button
          onClick={verify}
          disabled={busy || code.length !== 4}
          className="mt-2 w-full rounded-lg bg-indigo-600 px-4 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors"
        >
          {busy ? "Verifying…" : "Verify & Continue"}
        </button>
      </div>
    </AuthShell>
  );
}
