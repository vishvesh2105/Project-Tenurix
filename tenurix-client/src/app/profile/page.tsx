"use client";

import { useEffect, useState, useRef } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/useAuth";
import { apiFetch } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { useToast } from "@/components/ui/Toast";
import { User, Camera, Save, ArrowLeft, Lock } from "lucide-react";
import Link from "next/link";

type Profile = {
  userId: number;
  email: string;
  fullName: string;
  roleName: string;
  phone: string | null;
  jobTitle: string | null;
  department: string | null;
  photoBase64: string | null;
  photoContentType: string | null;
};

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400";

export default function ProfilePage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const { toast } = useToast();
  const fileRef = useRef<HTMLInputElement>(null);

  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [department, setDepartment] = useState("");

  useEffect(() => {
    if (!isReady) return;

    async function load() {
      try {
        setLoading(true);
        const res = await apiFetch("/account/me");
        if (!res.ok) throw new Error("Failed to load profile");
        const raw = await res.text();
        const data: Profile = raw ? JSON.parse(raw) : null;
        if (!data) throw new Error("Empty response");
        setProfile(data);
        setFullName(data.fullName || "");
        setPhone(data.phone || "");
        setJobTitle(data.jobTitle || "");
        setDepartment(data.department || "");
      } catch (e: any) {
        setError(e?.message ?? "Failed to load profile");
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [isReady]);

  async function handleSave() {
    try {
      setSaving(true);
      setError("");
      setSuccess("");

      const res = await apiFetch("/account/me", {
        method: "PUT",
        body: JSON.stringify({
          fullName: fullName.trim(),
          phone: phone.trim() || null,
          jobTitle: jobTitle.trim() || null,
          department: department.trim() || null,
        }),
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data?.message || data?.title || "Failed to save");
      }

      toast("Profile updated successfully", "success");
      setProfile((prev) =>
        prev ? { ...prev, fullName: fullName.trim(), phone: phone.trim(), jobTitle: jobTitle.trim(), department: department.trim() } : prev
      );
    } catch (e: any) {
      toast(e?.message ?? "Failed to update profile", "error");
    } finally {
      setSaving(false);
    }
  }

  async function handlePhotoUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 2_000_000) {
      setError("Photo must be under 2MB.");
      return;
    }

    try {
      setUploading(true);
      setError("");
      setSuccess("");

      const form = new FormData();
      form.append("photo", file);

      const res = await apiFetch("/account/me/photo", { method: "POST", body: form });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data?.message || data?.title || "Upload failed");
      }

      const profileRes = await apiFetch("/account/me");
      if (profileRes.ok) {
        const data: Profile = await profileRes.json();
        setProfile(data);
      }

      toast("Photo updated successfully", "success");
    } catch (e: any) {
      toast(e?.message ?? "Failed to upload photo", "error");
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  if (!isReady || loading) {
    return (
      <AppShell>
        <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="h-6 w-40 rounded-lg bg-slate-200" />
          <div className="mt-6 flex gap-6">
            <div className="h-32 w-32 rounded-full bg-slate-200" />
            <div className="flex-1 space-y-3">
              <div className="h-4 w-48 rounded-lg bg-slate-200" />
              <div className="h-10 w-full rounded-xl bg-slate-100" />
              <div className="h-10 w-full rounded-xl bg-slate-100" />
            </div>
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
            <h1 className="text-2xl font-bold text-slate-900">{t("myProfile")}</h1>
            <p className="mt-1 text-sm text-slate-500">Manage your account information.</p>
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

        <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
          {/* Photo Card */}
          <div className="rounded-2xl border border-slate-200 bg-white p-6 text-center shadow-sm">
            <div className="mx-auto relative inline-block">
              {profile?.photoBase64 ? (
                <img
                  src={`data:${profile.photoContentType};base64,${profile.photoBase64}`}
                  alt="Profile"
                  className="h-32 w-32 rounded-full object-cover border-4 border-amber-200 shadow-md"
                />
              ) : (
                <div className="flex h-32 w-32 items-center justify-center rounded-full border-4 border-amber-200 bg-indigo-50 shadow-md">
                  <User className="h-16 w-16 text-indigo-200" />
                </div>
              )}
            </div>

            <input
              ref={fileRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={handlePhotoUpload}
            />

            <Button
              variant="outline"
              className="mt-4 gap-2 border-slate-200 text-slate-600"
              onClick={() => fileRef.current?.click()}
              disabled={uploading}
            >
              <Camera className="h-4 w-4" />
              {uploading ? "Uploading..." : t("changePhoto")}
            </Button>

            <div className="mt-4">
              <span className="inline-block rounded-full bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-600 border border-indigo-100">
                {profile?.roleName || "Client"}
              </span>
            </div>
          </div>

          {/* Profile Form */}
          <div className="md:col-span-2 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="space-y-4">
              {/* Email (read-only) */}
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("email")}</label>
                <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-400">
                  <Lock className="h-3.5 w-3.5 shrink-0" />
                  {profile?.email}
                </div>
              </div>

              {/* Full Name */}
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("fullName")}</label>
                <input
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className={inputClass}
                  placeholder="Your full name"
                />
              </div>

              {/* Phone */}
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("phone")}</label>
                <input
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  className={inputClass}
                  placeholder="+1 (555) 123-4567"
                />
              </div>

              {/* Job Title */}
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Job Title</label>
                <input
                  value={jobTitle}
                  onChange={(e) => setJobTitle(e.target.value)}
                  className={inputClass}
                  placeholder="e.g. Property Manager"
                />
              </div>

              {/* Department */}
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Department</label>
                <input
                  value={department}
                  onChange={(e) => setDepartment(e.target.value)}
                  className={inputClass}
                  placeholder="e.g. Operations"
                />
              </div>

              <Button
                className="mt-2 gap-2 bg-indigo-600 hover:bg-indigo-700 text-white"
                onClick={handleSave}
                disabled={saving || !fullName.trim()}
              >
                <Save className="h-4 w-4" />
                {saving ? "Saving..." : t("saveChanges")}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
