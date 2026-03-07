"use client";

import { useState } from "react";
import AuthShell from "@/components/auth/AuthShell";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

export default function LandlordRegisterPage() {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function register() {
    try {
      setBusy(true);
      setError("");

      const res = await fetch(`${API_BASE}/auth/landlord/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fullName, email, phone, password }),
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data?.message || data?.title || "Registration failed. Please try again.");

      // backend returns a verificationId (or userId) to verify OTP
      sessionStorage.setItem("ll_verify_id", data.verificationId);
      sessionStorage.setItem("ll_verify_phone", phone);

      window.location.href = "/landlord/verify";
    } catch (e: any) {
      setError(e?.message ?? "Register failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell title="Create Landlord Account" subtitle="Requires @tenurix.net + phone verification">
      {error ? (
        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      <div className="space-y-3">
        <Input label="Full name" value={fullName} onChange={setFullName} placeholder="John Smith" />
        <Input label="Email" value={email} onChange={setEmail} placeholder="name@tenurix.net" />
        <Input label="Mobile number" value={phone} onChange={setPhone} placeholder="+1 647 555 1234" />
        <Input label="Password" value={password} onChange={setPassword} placeholder="Create a password" type="password" />

        <button
          onClick={register}
          disabled={busy}
          className="mt-2 w-full rounded-lg bg-indigo-600 px-4 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors"
        >
          {busy ? "Creating…" : "Create & Send OTP"}
        </button>

        <div className="text-center text-xs text-slate-500">
          Already have an account? <a className="hover:underline" href="/landlord/login">Log in</a>
        </div>
      </div>
    </AuthShell>
  );
}

function Input({
  label,
  value,
  onChange,
  placeholder,
  type = "text",
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  type?: string;
}) {
  return (
    <div className="rounded-lg border bg-slate-50 px-3 py-2">
      <div className="text-xs text-slate-500">{label}</div>
      <input
        className="w-full bg-transparent text-sm outline-none"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        type={type}
      />
    </div>
  );
}
