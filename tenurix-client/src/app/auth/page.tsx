"use client";

import { Suspense, useEffect, useState, useCallback } from "react";
import { useSearchParams } from "next/navigation";
import Image from "next/image";
import { useI18n } from "@/components/providers/I18nProvider";
import { HeaderControls } from "@/components/ui/HeaderControls";
import { Eye, EyeOff, Check, X, ArrowLeft, Mail, Lock, User, Phone } from "lucide-react";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");
const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID!;

function safeParse(raw: string): any {
  if (!raw) return null;
  try { return JSON.parse(raw); } catch { return null; }
}

declare global {
  interface Window {
    google?: any;
  }
}

type Role = "client" | "landlord";
type View = "login" | "register" | "verify" | "forgot" | "forgot-verify" | "reset-password";

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

  const rawNext = sp.get("next") || "";
  const defaultNext = role === "client" ? "/dashboard" : "/landlord/dashboard";
  const next = rawNext.startsWith("/") && !rawNext.startsWith("//") ? rawNext : defaultNext;

  const [view, setView] = useState<View>("login");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  // Login fields
  const [loginEmail, setLoginEmail] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [showLoginPw, setShowLoginPw] = useState(false);

  // Register fields
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [regEmail, setRegEmail] = useState("");
  const [regPhone, setRegPhone] = useState("");
  const [regPassword, setRegPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showRegPw, setShowRegPw] = useState(false);
  const [showConfirmPw, setShowConfirmPw] = useState(false);

  // 2FA fields
  const [verifyEmail, setVerifyEmail] = useState("");
  const [maskedEmail, setMaskedEmail] = useState("");
  const [verifyCode, setVerifyCode] = useState("");
  const [verifyPassword, setVerifyPassword] = useState(""); // stored for login verify
  const [verifyMode, setVerifyMode] = useState<"login" | "register">("login");
  const [resending, setResending] = useState(false);
  const [resendCooldown, setResendCooldown] = useState(0);

  // Forgot password fields
  const [forgotEmail, setForgotEmail] = useState("");
  const [resetPassword, setResetPassword] = useState("");
  const [resetConfirm, setResetConfirm] = useState("");
  const [showResetPw, setShowResetPw] = useState(false);
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const [resetSuccess, setResetSuccess] = useState(false);

  // Password validation
  const pwChecks = {
    length: regPassword.length >= 8,
    upper: /[A-Z]/.test(regPassword),
    lower: /[a-z]/.test(regPassword),
    number: /[0-9]/.test(regPassword),
    special: /[^A-Za-z0-9]/.test(regPassword),
  };
  const pwValid = pwChecks.length && pwChecks.upper && pwChecks.lower && pwChecks.number;
  const pwMatch = regPassword.length > 0 && confirmPassword.length > 0 && regPassword === confirmPassword;
  const pwMismatch = confirmPassword.length > 0 && regPassword !== confirmPassword;

  // Reset password validation
  const resetPwChecks = {
    length: resetPassword.length >= 8,
    upper: /[A-Z]/.test(resetPassword),
    lower: /[a-z]/.test(resetPassword),
    number: /[0-9]/.test(resetPassword),
    special: /[^A-Za-z0-9]/.test(resetPassword),
  };
  const resetPwValid = resetPwChecks.length && resetPwChecks.upper && resetPwChecks.lower && resetPwChecks.number;
  const resetPwMatch = resetPassword.length > 0 && resetConfirm.length > 0 && resetPassword === resetConfirm;
  const resetPwMismatch = resetConfirm.length > 0 && resetPassword !== resetConfirm;

  // Resend cooldown timer
  useEffect(() => {
    if (resendCooldown <= 0) return;
    const timer = setTimeout(() => setResendCooldown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendCooldown]);

  // Success handler — verify role matches portal, store token, redirect
  function handleSuccess(data: any) {
    const expectedRole = role === "landlord" ? "Landlord" : "Client";
    if (data.roleName && data.roleName.toLowerCase() !== expectedRole.toLowerCase()) {
      const otherPortal = role === "landlord" ? "Tenant" : "Landlord";
      setError(`This account is registered as a ${otherPortal}. Please use the ${otherPortal} portal to sign in.`);
      setBusy(false);
      return;
    }
    localStorage.setItem("tenurix_token", data.token);
    localStorage.setItem("tenurix_portal", role);
    window.location.href = next;
  }

  // ─── Google OAuth ─────────────────────────────────────────────
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
      try { data = raw ? JSON.parse(raw) : null; } catch { data = null; }
      if (!res.ok) throw new Error(data?.message || data?.title || "Login failed. Please try again.");
      if (!data?.token) throw new Error("Login failed. Please try again.");
      handleSuccess(data);
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
          theme: "outline", size: "large", width: 330, shape: "pill", text: "continue_with",
        });
      }
    }, 150);
    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role, view]);

  // ─── Email/Password Login ────────────────────────────────────
  async function handleLogin() {
    if (!loginEmail || !loginPassword) return setError("Please enter your email and password.");
    setBusy(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/auth/web/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: loginEmail, password: loginPassword, role }),
      });
      const raw = await res.text();
      const data = safeParse(raw);
      if (!res.ok) throw new Error(data?.message || data?.title || "Login failed. Please try again.");
      if (data?.requiresTwoFactor) {
        setVerifyEmail(data.email);
        setMaskedEmail(data.maskedEmail);
        setVerifyPassword(loginPassword);
        setVerifyMode("login");
        setVerifyCode("");
        setResendCooldown(30);
        setView("verify");
      } else if (data.token) {
        handleSuccess(data);
      }
    } catch (e: any) {
      setError(e?.message ?? "Login failed");
    } finally {
      setBusy(false);
    }
  }

  // ─── Register ────────────────────────────────────────────────
  async function handleRegister() {
    if (!firstName || !lastName || !regEmail || !regPassword || !confirmPassword) {
      return setError("Please fill in all required fields.");
    }
    if (!pwValid) return setError("Password does not meet the requirements.");
    if (!pwMatch) return setError("Passwords do not match.");

    setBusy(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/auth/web/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName, lastName, email: regEmail,
          phone: regPhone, password: regPassword, role,
        }),
      });
      const rawReg = await res.text();
      const data = safeParse(rawReg);
      if (!res.ok) throw new Error(data?.message || data?.title || "Registration failed. Please try again.");
      if (data?.requiresTwoFactor) {
        setVerifyEmail(data.email);
        setMaskedEmail(data.maskedEmail);
        setVerifyMode("register");
        setVerifyCode("");
        setResendCooldown(30);
        setView("verify");
      }
    } catch (e: any) {
      setError(e?.message ?? "Registration failed");
    } finally {
      setBusy(false);
    }
  }

  // ─── Verify 2FA ──────────────────────────────────────────────
  async function handleVerify() {
    if (!verifyCode || verifyCode.length !== 6) return setError("Please enter the 6-digit code.");
    setBusy(true);
    setError("");
    try {
      const endpoint = verifyMode === "login" ? "/auth/web/login/verify" : "/auth/web/register/verify";
      const body = verifyMode === "login"
        ? { email: verifyEmail, password: verifyPassword, code: verifyCode }
        : { email: verifyEmail, code: verifyCode };

      const res = await fetch(`${API_BASE}${endpoint}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      const rawVerify = await res.text();
      const data = safeParse(rawVerify);
      if (!res.ok) throw new Error(data?.message || data?.title || "Verification failed. Please try again.");
      if (data?.token) {
        handleSuccess(data);
      }
    } catch (e: any) {
      setError(e?.message ?? "Verification failed");
    } finally {
      setBusy(false);
    }
  }

  // ─── Resend 2FA ──────────────────────────────────────────────
  async function handleResend() {
    if (resendCooldown > 0) return;
    setResending(true);
    try {
      await fetch(`${API_BASE}/auth/web/resend-2fa`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: verifyEmail }),
      });
      setResendCooldown(30);
    } catch { }
    finally { setResending(false); }
  }

  // ─── Forgot Password ──────────────────────────────────────────
  async function handleForgotPassword() {
    if (!forgotEmail) return setError("Please enter your email address.");
    setBusy(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/auth/web/forgot-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: forgotEmail }),
      });
      const raw = await res.text();
      const data = safeParse(raw);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed. Please try again.");
      if (data?.requiresTwoFactor) {
        setVerifyEmail(data.email);
        setMaskedEmail(data.maskedEmail);
        setVerifyCode("");
        setResendCooldown(30);
        setView("forgot-verify");
      }
    } catch (e: any) {
      setError(e?.message ?? "Failed to send verification code");
    } finally {
      setBusy(false);
    }
  }

  async function handleForgotVerify() {
    if (!verifyCode || verifyCode.length !== 6) return setError("Please enter the 6-digit code.");
    setBusy(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/auth/web/forgot-password/verify`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: verifyEmail, code: verifyCode }),
      });
      const raw = await res.text();
      const data = safeParse(raw);
      if (!res.ok) throw new Error(data?.message || data?.title || "Verification failed.");
      if (data?.verified) {
        setResetPassword("");
        setResetConfirm("");
        setError("");
        setView("reset-password");
      }
    } catch (e: any) {
      setError(e?.message ?? "Verification failed");
    } finally {
      setBusy(false);
    }
  }

  async function handleResetPassword() {
    if (!resetPwValid) return setError("Password does not meet the requirements.");
    if (!resetPwMatch) return setError("Passwords do not match.");
    setBusy(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/auth/web/reset-password`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: verifyEmail, newPassword: resetPassword }),
      });
      const raw = await res.text();
      const data = safeParse(raw);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to reset password.");
      setResetSuccess(true);
      setTimeout(() => {
        setResetSuccess(false);
        setView("login");
        setError("");
      }, 2000);
    } catch (e: any) {
      setError(e?.message ?? "Failed to reset password");
    } finally {
      setBusy(false);
    }
  }

  function switchView(newView: View) {
    setView(newView);
    setError("");
    setResetSuccess(false);
  }

  return (
    <main className="min-h-screen bg-gradient-to-br from-slate-100 to-slate-50 px-4 py-8">
      <div className="mx-auto flex min-h-[calc(100vh-4rem)] max-w-6xl items-center justify-center">
        <div className="grid w-full max-w-5xl overflow-hidden rounded-[32px] bg-white shadow-[0_30px_80px_rgba(15,23,42,0.18)] lg:grid-cols-2">
          {/* Left side */}
          <div className="hidden lg:flex flex-col justify-between bg-gradient-to-br from-indigo-950 to-slate-900 px-10 py-10 text-white relative overflow-hidden">
            <div className="absolute -top-20 -right-20 h-64 w-64 rounded-full bg-indigo-500/10 blur-3xl" />
            <div className="absolute -bottom-16 -left-16 h-48 w-48 rounded-full bg-indigo-500/10 blur-3xl" />

            <div className="relative">
              <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-xs font-medium text-amber-500 backdrop-blur-sm">
                <Image src="/dark-logo.svg" alt="" width={16} height={12} />
                Tenurix
              </div>

              <h2 className="mt-6 text-4xl font-bold leading-tight">
                {view === "register" ? (
                  <>Create your<br /><span className="text-amber-500">Account.</span></>
                ) : view === "verify" || view === "forgot-verify" ? (
                  <>Verify your<br /><span className="text-amber-500">Identity.</span></>
                ) : view === "forgot" || view === "reset-password" ? (
                  <>Reset your<br /><span className="text-amber-500">Password.</span></>
                ) : (
                  <>Login to your<br /><span className="text-amber-500">Portal.</span></>
                )}
              </h2>

              <p className="mt-4 text-sm text-white/60 leading-relaxed max-w-sm">
                {view === "register"
                  ? "Create an account to get started with Tenurix property management."
                  : view === "verify" || view === "forgot-verify"
                  ? "We sent a verification code to your email for security."
                  : view === "forgot"
                  ? "Enter your email to receive a code and reset your password."
                  : view === "reset-password"
                  ? "Create a new strong password for your account."
                  : "Access your personalized dashboard to manage listings, applications, leases, and more."}
              </p>
            </div>

            <div className="relative space-y-4">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-5 backdrop-blur-sm hover:bg-white/10 transition-colors">
                <p className="text-sm font-semibold">Tenant Portal</p>
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
            <div className="mb-6 flex items-start justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white shadow-lg shadow-indigo-600/25 overflow-hidden">
                  <Image src="/home-logo.svg" alt="Tenurix" width={36} height={28} />
                </div>
                <div>
                  <p className="text-sm font-medium text-slate-500">Welcome to</p>
                  <h1 className="text-2xl font-bold tracking-tight text-slate-900">
                    {role === "client" ? t("clientPortal") : t("landlordPortal")}
                  </h1>
                </div>
              </div>
              <div className="shrink-0"><HeaderControls /></div>
            </div>

            {/* Role Tabs */}
            {(view === "login" || view === "register") && (
              <div className="mb-6">
                <div className="grid grid-cols-2 rounded-2xl bg-slate-100 p-1">
                  <button type="button" onClick={() => setRole("client")}
                    className={`rounded-xl px-4 py-3 text-sm font-medium transition-all duration-200 ${role === "client" ? "bg-white text-indigo-600 shadow-sm" : "text-slate-500 hover:text-slate-900"}`}>
                    {t("client")}
                  </button>
                  <button type="button" onClick={() => setRole("landlord")}
                    className={`rounded-xl px-4 py-3 text-sm font-medium transition-all duration-200 ${role === "landlord" ? "bg-white text-indigo-600 shadow-sm" : "text-slate-500 hover:text-slate-900"}`}>
                    {t("landlord")}
                  </button>
                </div>
              </div>
            )}

            {/* Error */}
            {error && (
              <div className="mb-4 rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            )}

            {/* ─── LOGIN VIEW ─── */}
            {view === "login" && (
              <div className="space-y-4">
                {/* Email/Password form */}
                <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                  <p className="text-sm font-semibold text-slate-900 mb-1">Sign in with Email</p>
                  <p className="text-xs text-slate-500 mb-4">Enter your credentials to continue</p>

                  <div className="space-y-3">
                    <div className="relative">
                      <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type="email" placeholder="Email address" value={loginEmail}
                        aria-label="Email address"
                        onChange={(e) => setLoginEmail(e.target.value)}
                        onKeyDown={(e) => e.key === "Enter" && handleLogin()}
                        className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-4 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                    </div>

                    <div className="relative">
                      <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type={showLoginPw ? "text" : "password"} placeholder="Password" value={loginPassword}
                        aria-label="Password"
                        onChange={(e) => setLoginPassword(e.target.value)}
                        onKeyDown={(e) => e.key === "Enter" && handleLogin()}
                        className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-10 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                      <button type="button" onClick={() => setShowLoginPw(!showLoginPw)}
                        aria-label={showLoginPw ? "Hide password" : "Show password"}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                        {showLoginPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                    </div>

                    <button onClick={handleLogin} disabled={busy}
                      className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                      {busy ? "Signing in..." : "Sign In"}
                    </button>
                  </div>

                  <div className="mt-4 flex flex-col items-center gap-1.5">
                    <button type="button" onClick={() => { setForgotEmail(loginEmail); switchView("forgot"); }}
                      className="text-xs font-medium text-indigo-600 hover:text-indigo-700">
                      Forgot Password?
                    </button>
                    <p className="text-xs text-slate-500">
                      Don&apos;t have an account?{" "}
                      <button type="button" onClick={() => switchView("register")}
                        className="font-semibold text-indigo-600 hover:text-indigo-700">
                        Register
                      </button>
                    </p>
                  </div>
                </div>

                {/* Divider */}
                <div className="flex items-center gap-3">
                  <div className="h-px flex-1 bg-slate-200" />
                  <span className="text-xs text-slate-400 font-medium">or</span>
                  <div className="h-px flex-1 bg-slate-200" />
                </div>

                {/* Google sign-in */}
                <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                  <p className="text-sm font-semibold text-slate-900 mb-1">Continue with Google</p>
                  <p className="text-xs text-slate-500 mb-3">Quick sign-in using your Google account</p>
                  <div className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
                    <div className="flex items-center justify-center">
                      <div id="googleBtn" />
                    </div>
                  </div>
                  {busy && <div className="mt-3 text-center text-xs text-slate-500">{t("signingIn")}</div>}
                </div>
              </div>
            )}

            {/* ─── REGISTER VIEW ─── */}
            {view === "register" && (
              <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <div className="flex items-center gap-2 mb-4">
                  <button type="button" onClick={() => switchView("login")}
                    className="rounded-lg p-1.5 hover:bg-slate-200 transition-colors">
                    <ArrowLeft className="h-4 w-4 text-slate-600" />
                  </button>
                  <div>
                    <p className="text-sm font-semibold text-slate-900">Create Account</p>
                    <p className="text-xs text-slate-500">
                      Register as {role === "client" ? "Tenant" : "Landlord"}
                    </p>
                  </div>
                </div>

                <div className="space-y-3">
                  {/* Name row */}
                  <div className="grid grid-cols-2 gap-3">
                    <div className="relative">
                      <User className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type="text" placeholder="First name" value={firstName}
                        aria-label="First name"
                        onChange={(e) => setFirstName(e.target.value)}
                        className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-3 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                    </div>
                    <div className="relative">
                      <User className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type="text" placeholder="Last name" value={lastName}
                        aria-label="Last name"
                        onChange={(e) => setLastName(e.target.value)}
                        className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-3 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                    </div>
                  </div>

                  {/* Email */}
                  <div className="relative">
                    <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input type="email" placeholder="Email address" value={regEmail}
                      aria-label="Email address"
                      onChange={(e) => setRegEmail(e.target.value)}
                      className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-4 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                  </div>

                  {/* Phone (optional) */}
                  <div className="relative">
                    <Phone className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input type="tel" placeholder="Phone number (optional)" value={regPhone}
                      aria-label="Phone number"
                      onChange={(e) => setRegPhone(e.target.value)}
                      className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-4 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                  </div>

                  {/* Password */}
                  <div className="relative">
                    <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input type={showRegPw ? "text" : "password"} placeholder="Password" value={regPassword}
                      aria-label="Password"
                      onChange={(e) => setRegPassword(e.target.value)}
                      className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-10 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                    <button type="button" onClick={() => setShowRegPw(!showRegPw)}
                      aria-label={showRegPw ? "Hide password" : "Show password"}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                      {showRegPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>

                  {/* Password strength indicators */}
                  {regPassword.length > 0 && (
                    <div className="rounded-xl border border-slate-200 bg-white p-3 space-y-1.5">
                      <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Password Requirements</p>
                      <PwCheck ok={pwChecks.length} label="At least 8 characters" />
                      <PwCheck ok={pwChecks.upper} label="One uppercase letter" />
                      <PwCheck ok={pwChecks.lower} label="One lowercase letter" />
                      <PwCheck ok={pwChecks.number} label="One number" />
                      <PwCheck ok={pwChecks.special} label="One special character (recommended)" optional />
                    </div>
                  )}

                  {/* Confirm Password */}
                  <div className="relative">
                    <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input type={showConfirmPw ? "text" : "password"} placeholder="Confirm password" value={confirmPassword}
                      aria-label="Confirm password"
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      onKeyDown={(e) => e.key === "Enter" && handleRegister()}
                      className={`w-full rounded-xl border bg-white py-3 pl-10 pr-10 text-sm text-slate-900 placeholder:text-slate-400 outline-none transition-all ${
                        pwMismatch ? "border-red-300 focus:border-red-400 focus:ring-red-100" :
                        pwMatch ? "border-emerald-300 focus:border-emerald-400 focus:ring-emerald-100" :
                        "border-slate-200 focus:border-indigo-400 focus:ring-indigo-100"
                      } focus:ring-2`} />
                    <button type="button" onClick={() => setShowConfirmPw(!showConfirmPw)}
                      aria-label={showConfirmPw ? "Hide confirm password" : "Show confirm password"}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                      {showConfirmPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>

                  {/* Real-time match indicator */}
                  {confirmPassword.length > 0 && (
                    <div className={`flex items-center gap-1.5 text-xs ${pwMatch ? "text-emerald-600" : "text-red-500"}`}>
                      {pwMatch ? <Check className="h-3.5 w-3.5" /> : <X className="h-3.5 w-3.5" />}
                      {pwMatch ? "Passwords match" : "Passwords do not match"}
                    </div>
                  )}

                  <button onClick={handleRegister} disabled={busy || !pwValid || !pwMatch}
                    className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                    {busy ? "Creating account..." : "Create Account & Verify"}
                  </button>

                  <p className="text-center text-xs text-slate-500">
                    Already have an account?{" "}
                    <button type="button" onClick={() => switchView("login")}
                      className="font-semibold text-indigo-600 hover:text-indigo-700">
                      Sign In
                    </button>
                  </p>
                </div>
              </div>
            )}

            {/* ─── 2FA VERIFY VIEW ─── */}
            {view === "verify" && (
              <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <div className="flex items-center gap-2 mb-4">
                  <button type="button" onClick={() => switchView("login")}
                    className="rounded-lg p-1.5 hover:bg-slate-200 transition-colors">
                    <ArrowLeft className="h-4 w-4 text-slate-600" />
                  </button>
                  <div>
                    <p className="text-sm font-semibold text-slate-900">Verify Your Email</p>
                    <p className="text-xs text-slate-500">
                      Enter the 6-digit code sent to <span className="font-medium text-slate-700">{maskedEmail}</span>
                    </p>
                  </div>
                </div>

                <div className="space-y-4">
                  {/* Code input */}
                  <div className="flex justify-center">
                    <input type="text" maxLength={6} value={verifyCode}
                      aria-label="Verification code"
                      onChange={(e) => setVerifyCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                      onKeyDown={(e) => e.key === "Enter" && handleVerify()}
                      placeholder="000000"
                      className="w-48 rounded-xl border-2 border-slate-200 bg-white py-4 text-center text-2xl font-bold tracking-[0.4em] text-slate-900 outline-none focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100 transition-all"
                      autoFocus />
                  </div>

                  <button onClick={handleVerify} disabled={busy || verifyCode.length !== 6}
                    className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                    {busy ? "Verifying..." : "Verify & Continue"}
                  </button>

                  <div className="text-center">
                    <button type="button" onClick={handleResend}
                      disabled={resending || resendCooldown > 0}
                      className="text-xs text-indigo-600 hover:text-indigo-700 font-medium disabled:text-slate-400 disabled:cursor-not-allowed">
                      {resending ? "Sending..." : resendCooldown > 0 ? `Resend code in ${resendCooldown}s` : "Didn't receive a code? Resend"}
                    </button>
                  </div>

                  <p className="text-center text-[11px] text-slate-400">
                    Code expires in 5 minutes. Check your spam folder if you don&apos;t see the email.
                  </p>
                </div>
              </div>
            )}

            {/* ─── FORGOT PASSWORD VIEW ─── */}
            {view === "forgot" && (
              <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <div className="flex items-center gap-2 mb-4">
                  <button type="button" onClick={() => switchView("login")}
                    className="rounded-lg p-1.5 hover:bg-slate-200 transition-colors">
                    <ArrowLeft className="h-4 w-4 text-slate-600" />
                  </button>
                  <div>
                    <p className="text-sm font-semibold text-slate-900">Forgot Password</p>
                    <p className="text-xs text-slate-500">Enter your email to receive a verification code</p>
                  </div>
                </div>

                <div className="space-y-3">
                  <div className="relative">
                    <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input type="email" placeholder="Email address" value={forgotEmail}
                      aria-label="Email address"
                      onChange={(e) => setForgotEmail(e.target.value)}
                      onKeyDown={(e) => e.key === "Enter" && handleForgotPassword()}
                      className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-4 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                  </div>

                  <button onClick={handleForgotPassword} disabled={busy || !forgotEmail}
                    className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                    {busy ? "Sending code..." : "Send Verification Code"}
                  </button>

                  <p className="text-center text-xs text-slate-500">
                    Remember your password?{" "}
                    <button type="button" onClick={() => switchView("login")}
                      className="font-semibold text-indigo-600 hover:text-indigo-700">
                      Sign In
                    </button>
                  </p>
                </div>
              </div>
            )}

            {/* ─── FORGOT VERIFY VIEW ─── */}
            {view === "forgot-verify" && (
              <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <div className="flex items-center gap-2 mb-4">
                  <button type="button" onClick={() => switchView("forgot")}
                    className="rounded-lg p-1.5 hover:bg-slate-200 transition-colors">
                    <ArrowLeft className="h-4 w-4 text-slate-600" />
                  </button>
                  <div>
                    <p className="text-sm font-semibold text-slate-900">Verify Your Email</p>
                    <p className="text-xs text-slate-500">
                      Enter the 6-digit code sent to <span className="font-medium text-slate-700">{maskedEmail}</span>
                    </p>
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="flex justify-center">
                    <input type="text" maxLength={6} value={verifyCode}
                      aria-label="Verification code"
                      onChange={(e) => setVerifyCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                      onKeyDown={(e) => e.key === "Enter" && handleForgotVerify()}
                      placeholder="000000"
                      className="w-48 rounded-xl border-2 border-slate-200 bg-white py-4 text-center text-2xl font-bold tracking-[0.4em] text-slate-900 outline-none focus:border-indigo-500 focus:ring-4 focus:ring-indigo-100 transition-all"
                      autoFocus />
                  </div>

                  <button onClick={handleForgotVerify} disabled={busy || verifyCode.length !== 6}
                    className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                    {busy ? "Verifying..." : "Verify Code"}
                  </button>

                  <div className="text-center">
                    <button type="button" onClick={handleResend}
                      disabled={resending || resendCooldown > 0}
                      className="text-xs text-indigo-600 hover:text-indigo-700 font-medium disabled:text-slate-400 disabled:cursor-not-allowed">
                      {resending ? "Sending..." : resendCooldown > 0 ? `Resend code in ${resendCooldown}s` : "Didn't receive a code? Resend"}
                    </button>
                  </div>

                  <p className="text-center text-[11px] text-slate-400">
                    Code expires in 5 minutes. Check your spam folder if you don&apos;t see the email.
                  </p>
                </div>
              </div>
            )}

            {/* ─── RESET PASSWORD VIEW ─── */}
            {view === "reset-password" && (
              <div className="rounded-3xl border border-slate-200 bg-slate-50 p-5">
                <div className="mb-4">
                  <p className="text-sm font-semibold text-slate-900">Set New Password</p>
                  <p className="text-xs text-slate-500">Create a strong password for your account</p>
                </div>

                {resetSuccess && (
                  <div className="mb-4 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 flex items-center gap-2">
                    <Check className="h-4 w-4" />
                    Password reset successfully! Redirecting to login...
                  </div>
                )}

                {!resetSuccess && (
                  <div className="space-y-3">
                    <div className="relative">
                      <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type={showResetPw ? "text" : "password"} placeholder="New password" value={resetPassword}
                        aria-label="New password"
                        onChange={(e) => setResetPassword(e.target.value)}
                        className="w-full rounded-xl border border-slate-200 bg-white py-3 pl-10 pr-10 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition-all" />
                      <button type="button" onClick={() => setShowResetPw(!showResetPw)}
                        aria-label={showResetPw ? "Hide password" : "Show password"}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                        {showResetPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                    </div>

                    {resetPassword.length > 0 && (
                      <div className="rounded-xl border border-slate-200 bg-white p-3 space-y-1.5">
                        <p className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider">Password Requirements</p>
                        <PwCheck ok={resetPwChecks.length} label="At least 8 characters" />
                        <PwCheck ok={resetPwChecks.upper} label="One uppercase letter" />
                        <PwCheck ok={resetPwChecks.lower} label="One lowercase letter" />
                        <PwCheck ok={resetPwChecks.number} label="One number" />
                        <PwCheck ok={resetPwChecks.special} label="One special character (recommended)" optional />
                      </div>
                    )}

                    <div className="relative">
                      <Lock className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                      <input type={showResetConfirm ? "text" : "password"} placeholder="Confirm new password" value={resetConfirm}
                        aria-label="Confirm new password"
                        onChange={(e) => setResetConfirm(e.target.value)}
                        onKeyDown={(e) => e.key === "Enter" && handleResetPassword()}
                        className={`w-full rounded-xl border bg-white py-3 pl-10 pr-10 text-sm text-slate-900 placeholder:text-slate-400 outline-none transition-all ${
                          resetPwMismatch ? "border-red-300 focus:border-red-400 focus:ring-red-100" :
                          resetPwMatch ? "border-emerald-300 focus:border-emerald-400 focus:ring-emerald-100" :
                          "border-slate-200 focus:border-indigo-400 focus:ring-indigo-100"
                        } focus:ring-2`} />
                      <button type="button" onClick={() => setShowResetConfirm(!showResetConfirm)}
                        aria-label={showResetConfirm ? "Hide confirm password" : "Show confirm password"}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                        {showResetConfirm ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                    </div>

                    {resetConfirm.length > 0 && (
                      <div className={`flex items-center gap-1.5 text-xs ${resetPwMatch ? "text-emerald-600" : "text-red-500"}`}>
                        {resetPwMatch ? <Check className="h-3.5 w-3.5" /> : <X className="h-3.5 w-3.5" />}
                        {resetPwMatch ? "Passwords match" : "Passwords do not match"}
                      </div>
                    )}

                    <button onClick={handleResetPassword} disabled={busy || !resetPwValid || !resetPwMatch}
                      className="w-full rounded-xl bg-indigo-600 py-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors">
                      {busy ? "Resetting..." : "Reset Password"}
                    </button>
                  </div>
                )}
              </div>
            )}

            <div className="mt-5 text-center text-xs leading-5 text-slate-500">
              {t("terms")}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}

function PwCheck({ ok, label, optional }: { ok: boolean; label: string; optional?: boolean }) {
  return (
    <div className={`flex items-center gap-1.5 text-xs ${ok ? "text-emerald-600" : optional ? "text-slate-400" : "text-slate-400"}`}>
      {ok ? <Check className="h-3 w-3" /> : <div className="h-3 w-3 rounded-full border border-slate-300" />}
      <span>{label}</span>
    </div>
  );
}
