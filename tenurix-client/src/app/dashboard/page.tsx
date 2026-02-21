"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import {
  FileText, Clock, CheckCircle, XCircle, ArrowRight, Search, User,
} from "lucide-react";

type Profile = {
  userId: number;
  email: string;
  fullName: string;
  roleName: string;
  phone: string;
  photoBase64?: string;
  photoContentType?: string;
};

type AppRow = {
  applicationId: number;
  listingId: number;
  status: string;
  submittedAt: string;
  requestedStartDate: string;
  requestedEndDate: string;
};

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString(); } catch { return d; }
}

export default function DashboardPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [apps, setApps] = useState<AppRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!isReady) return;
    async function load() {
      try {
        setLoading(true); setError("");
        const [profileRes, appsRes] = await Promise.all([apiFetch("/account/me"), apiFetch("/client/applications")]);
        if (profileRes.ok) setProfile(await safeJson<Profile>(profileRes));
        if (appsRes.ok) { const d = await safeJson<any>(appsRes); setApps(Array.isArray(d) ? d : []); }
      } catch (e: any) {
        setError(e?.message ?? "Failed to load dashboard");
      } finally {
        setLoading(false);
      }
    }
    load();
  }, [isReady]);

  if (!isReady || loading) {
    return (
      <AppShell>
        <div className="flex flex-col gap-6">
          <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="h-8 w-64 rounded-xl bg-slate-200" />
            <div className="mt-3 h-4 w-96 rounded-xl bg-slate-100" />
          </div>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="animate-pulse rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                <div className="h-4 w-20 rounded bg-slate-200" />
                <div className="mt-2 h-8 w-12 rounded bg-slate-100" />
              </div>
            ))}
          </div>
        </div>
      </AppShell>
    );
  }

  const total = apps.length;
  const pending = apps.filter((a) => a.status?.toLowerCase().includes("pending")).length;
  const approved = apps.filter((a) => a.status?.toLowerCase().includes("approved")).length;
  const rejected = apps.filter((a) => a.status?.toLowerCase().includes("rejected")).length;
  const recent = apps.slice(0, 5);

  const stats = [
    { label: t("totalApplications"), value: total, Icon: FileText, bg: "bg-indigo-50", color: "text-indigo-600" },
    { label: t("pending"), value: pending, Icon: Clock, bg: "bg-amber-50", color: "text-amber-600" },
    { label: t("approved"), value: approved, Icon: CheckCircle, bg: "bg-emerald-50", color: "text-emerald-600" },
    { label: t("rejected"), value: rejected, Icon: XCircle, bg: "bg-red-50", color: "text-red-500" },
  ];

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Welcome banner */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-4">
            {profile?.photoBase64 ? (
              <img
                src={`data:${profile.photoContentType};base64,${profile.photoBase64}`}
                alt=""
                className="h-14 w-14 rounded-full object-cover ring-2 ring-amber-300"
              />
            ) : (
              <div className="flex h-14 w-14 items-center justify-center rounded-full bg-indigo-100">
                <User className="h-7 w-7 text-indigo-600" />
              </div>
            )}
            <div>
              <h1 className="text-2xl font-bold text-slate-900">
                {t("welcomeBack")}, {profile?.fullName || "User"} 👋
              </h1>
              <p className="mt-1 text-sm text-slate-500">{profile?.email}</p>
            </div>
          </div>
        </div>

        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
        )}

        {/* Stat cards */}
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
          {stats.map((s) => (
            <div key={s.label} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-shadow">
              <div className={`inline-flex h-9 w-9 items-center justify-center rounded-xl ${s.bg} mb-3`}>
                <s.Icon className={`h-5 w-5 ${s.color}`} />
              </div>
              <div className="text-3xl font-extrabold text-slate-900">{s.value}</div>
              <div className="mt-0.5 text-xs text-slate-500">{s.label}</div>
            </div>
          ))}
        </div>

        {/* Recent Applications */}
        <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4">
            <h2 className="text-sm font-bold text-slate-800">{t("recentApplications")}</h2>
            <Link href="/applications" className="text-xs text-amber-500 hover:text-indigo-600 font-medium transition-colors">
              {t("viewApplications")} →
            </Link>
          </div>

          {recent.length === 0 ? (
            <div className="p-10 text-center">
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-indigo-50 mb-4">
                <FileText className="h-6 w-6 text-indigo-300" />
              </div>
              <p className="text-sm text-slate-500">{t("noApplications")}</p>
              <Link href="/listings" className="mt-3 inline-block text-sm text-amber-500 hover:underline font-medium">
                {t("browseListings")} →
              </Link>
            </div>
          ) : (
            <div className="divide-y divide-slate-100">
              {recent.map((app) => (
                <div key={app.applicationId} className="flex items-center justify-between px-5 py-3.5 hover:bg-slate-50 transition-colors">
                  <div>
                    <div className="flex items-center gap-2.5">
                      <span className="text-sm font-semibold text-slate-800">Application #{app.applicationId}</span>
                      <StatusBadge status={app.status} />
                    </div>
                    <div className="mt-0.5 text-xs text-slate-400">
                      Listing #{app.listingId} · {fmtDate(app.submittedAt)}
                    </div>
                  </div>
                  <Link href={`/listings/${app.listingId}`}>
                    <ArrowRight className="h-4 w-4 text-slate-400 hover:text-indigo-600 transition-colors" />
                  </Link>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Quick actions */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {[
            { href: "/listings", Icon: Search, bg: "bg-indigo-100", color: "text-indigo-600", title: t("browseListings"), sub: "Find available properties" },
            { href: "/applications", Icon: FileText, bg: "bg-emerald-100", color: "text-emerald-600", title: t("viewApplications"), sub: "Track your lease applications" },
          ].map((card) => (
            <Link key={card.href} href={card.href} className="group">
              <div className="flex items-center gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-all hover:-translate-y-0.5">
                <div className={`flex h-10 w-10 items-center justify-center rounded-xl ${card.bg}`}>
                  <card.Icon className={`h-5 w-5 ${card.color}`} />
                </div>
                <div>
                  <div className="text-sm font-semibold text-slate-800">{card.title}</div>
                  <div className="text-xs text-slate-500">{card.sub}</div>
                </div>
                <ArrowRight className="ml-auto h-4 w-4 text-slate-300 group-hover:text-indigo-600 transition-colors" />
              </div>
            </Link>
          ))}
        </div>
      </div>
    </AppShell>
  );
}
