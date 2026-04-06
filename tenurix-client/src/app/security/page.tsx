"use client";

import { useState } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/useAuth";
import { apiFetch } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { useToast } from "@/components/ui/Toast";
import { Shield, Eye, EyeOff, ArrowLeft } from "lucide-react";
import Link from "next/link";

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 pr-10 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400";

function getPasswordStrength(pw: string): { score: number; label: string; color: string } {
  let score = 0;
  if (pw.length >= 8) score++;
  if (pw.length >= 12) score++;
  if (/[A-Z]/.test(pw)) score++;
  if (/[a-z]/.test(pw)) score++;
  if (/[0-9]/.test(pw)) score++;
  if (/[^A-Za-z0-9]/.test(pw)) score++;

  if (score <= 2) return { score, label: "Weak", color: "bg-red-500" };
  if (score <= 3) return { score, label: "Fair", color: "bg-amber-500" };
  if (score <= 4) return { score, label: "Good", color: "bg-blue-500" };
  return { score, label: "Strong", color: "bg-green-500" };
}

export default function SecurityPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const { toast } = useToast();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const passwordsMatch = newPassword === confirmPassword;
  const minLength = newPassword.length >= 8;
  const canSubmit = currentPassword && newPassword && confirmPassword && passwordsMatch && minLength && !saving;

  async function handleChangePassword() {
    if (!canSubmit) return;

    try {
      setSaving(true);
      setError("");
      setSuccess("");

      const res = await apiFetch("/account/change-password", {
        method: "POST",
        body: JSON.stringify({ currentPassword, newPassword }),
      });

      if (!res.ok) {
        const raw = await res.text();
        const data = raw ? JSON.parse(raw) : null;
        throw new Error(data?.message || data?.title || "Failed to change password");
      }

      toast("Password changed successfully", "success");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (e: any) {
      toast(e?.message ?? "Failed to change password", "error");
    } finally {
      setSaving(false);
    }
  }

  if (!isReady) {
    return (
      <AppShell>
        <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="h-6 w-32 rounded-lg bg-slate-200" />
          <div className="mt-4 space-y-3">
            <div className="h-10 w-full rounded-xl bg-slate-100" />
            <div className="h-10 w-full rounded-xl bg-slate-100" />
            <div className="h-10 w-full rounded-xl bg-slate-100" />
          </div>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("security")}</h1>
            <p className="mt-1 text-sm text-slate-500">Manage your password and account security.</p>
          </div>
          <Link href="/dashboard">
            <Button variant="outline" className="gap-2 border-slate-200 text-slate-600">
              <ArrowLeft className="h-4 w-4" /> {t("dashboard")}
            </Button>
          </Link>
        </div>

        {/* Messages */}
        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
        )}
        {success && (
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">{success}</div>
        )}

        {/* Change Password Card */}
        <div className="max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-3 mb-6">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100">
              <Shield className="h-5 w-5 text-indigo-600" />
            </div>
            <div>
              <h2 className="text-sm font-semibold text-slate-800">{t("changePassword")}</h2>
              <p className="text-xs text-slate-400">Update your password to keep your account secure.</p>
            </div>
          </div>

          <div className="space-y-4">
            {/* Current Password */}
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("currentPassword")}</label>
              <div className="relative">
                <input
                  type={showCurrent ? "text" : "password"}
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  className={inputClass}
                  placeholder="Enter current password"
                />
                <button
                  type="button"
                  onClick={() => setShowCurrent(!showCurrent)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition-colors"
                >
                  {showCurrent ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            {/* New Password */}
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("newPassword")}</label>
              <div className="relative">
                <input
                  type={showNew ? "text" : "password"}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className={inputClass}
                  placeholder="Enter new password"
                />
                <button
                  type="button"
                  onClick={() => setShowNew(!showNew)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 transition-colors"
                >
                  {showNew ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              {newPassword && !minLength && (
                <p className="mt-1.5 text-xs text-amber-600">Password must be at least 8 characters.</p>
              )}
              {newPassword && (() => {
                const strength = getPasswordStrength(newPassword);
                return (
                  <div className="mt-2 space-y-1">
                    <div className="flex gap-1">
                      {[...Array(4)].map((_, i) => (
                        <div key={i} className={`h-1.5 flex-1 rounded-full transition-colors ${i < Math.ceil(strength.score / 1.5) ? strength.color : "bg-slate-200"}`} />
                      ))}
                    </div>
                    <p className={`text-xs ${strength.color.replace("bg-", "text-")}`}>{strength.label}</p>
                  </div>
                );
              })()}
            </div>

            {/* Confirm Password */}
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("confirmPassword")}</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400"
                placeholder="Confirm new password"
              />
              {confirmPassword && !passwordsMatch && (
                <p className="mt-1.5 text-xs text-red-600">Passwords do not match.</p>
              )}
            </div>

            <Button
              className="mt-2 w-full bg-indigo-600 hover:bg-indigo-700 text-white"
              onClick={handleChangePassword}
              disabled={!canSubmit}
            >
              {saving ? "Changing..." : t("changePassword")}
            </Button>
          </div>
        </div>

        {/* Info Note */}
        <div className="max-w-lg rounded-2xl border border-amber-200 bg-amber-50 p-4">
          <div className="text-sm font-semibold text-indigo-600">Note</div>
          <div className="mt-1 text-xs text-slate-600 leading-relaxed">
            If you signed in with Google, you may not have a current password set.
            In that case, use the temporary password provided during account setup.
          </div>
        </div>
      </div>
    </AppShell>
  );
}
