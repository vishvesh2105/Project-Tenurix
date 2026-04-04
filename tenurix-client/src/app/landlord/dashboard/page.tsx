"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import {
  Building2, ScrollText, AlertCircle, Plus, ArrowRight, Clock, CheckCircle2,
} from "lucide-react";

type UserInfo = { fullName?: string; email?: string };
type PropertyRow = {
  propertyId: number;
  address: string;
  propertyType: string;
  bedrooms: number;
  rentAmount: number;
  submissionStatus: string;
  listingId: number | null;
  listingStatus: string | null;
};

export default function LandlordDashboard() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [user, setUser] = useState<UserInfo | null>(null);
  const [properties, setProperties] = useState<PropertyRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  async function load() {
    try {
      setErr(""); setLoading(true);
      const [meRes, propRes] = await Promise.all([
        apiFetch("/account/me"),
        apiFetch("/landlord/properties"),
      ]);
      if (meRes.ok) setUser(await safeJson<UserInfo>(meRes) ?? {});
      if (propRes.ok) {
        const d = await safeJson<any>(propRes);
        setProperties(Array.isArray(d) ? d : []);
      }
    } catch (e: any) {
      setErr(e?.message ?? "Failed to load dashboard");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { if (isReady) load(); }, [isReady]);

  const totalProperties    = properties.length;
  const activeListings     = properties.filter((p) => p.listingStatus === "Active").length;
  const pendingSubmissions = properties.filter((p) => p.submissionStatus === "Pending").length;
  const approvedCount      = properties.filter((p) => p.submissionStatus === "Approved").length;

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Welcome banner */}
        <div className="rounded-2xl border border-indigo-100 bg-gradient-to-r from-indigo-50 to-amber-50 p-6">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h1 className="text-2xl font-bold text-slate-900">
                {t("welcomeBack")}{user?.fullName ? `, ${user.fullName}` : ""} 👋
              </h1>
              <p className="mt-1 text-sm text-slate-500">
                Manage your properties and track submissions from your landlord portal.
              </p>
            </div>
            <Link href="/landlord/submissions/new">
              <Button className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
                <Plus className="h-4 w-4" /> {t("newSubmission")}
              </Button>
            </Link>
          </div>
        </div>

        {/* Stat cards */}
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
          {[
            { label: t("totalProperties"),    value: totalProperties,    Icon: Building2,    bg: "bg-slate-50",   color: "text-slate-600"   },
            { label: t("activeListings"),     value: activeListings,     Icon: CheckCircle2, bg: "bg-emerald-50", color: "text-emerald-600" },
            { label: t("pendingSubmissions"), value: pendingSubmissions,  Icon: Clock,        bg: "bg-amber-50",   color: "text-amber-600"   },
            { label: t("approved"),           value: approvedCount,      Icon: CheckCircle2, bg: "bg-indigo-50",  color: "text-indigo-600"  },
          ].map((s) => (
            <div key={s.label} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm hover:shadow-md transition-shadow">
              <div className={`inline-flex h-9 w-9 items-center justify-center rounded-xl ${s.bg} mb-3`}>
                <s.Icon className={`h-5 w-5 ${s.color}`} />
              </div>
              <div className={`text-3xl font-extrabold ${s.color}`}>{loading ? "—" : s.value}</div>
              <div className="mt-0.5 text-xs text-slate-500">{s.label}</div>
            </div>
          ))}
        </div>

        {/* Quick actions */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          {[
            { href: "/landlord/properties", Icon: Building2,  label: t("properties"), sub: "View all your properties",  color: "text-indigo-600",  bg: "bg-indigo-100"  },
            { href: "/landlord/leases",     Icon: ScrollText,  label: t("leases"),     sub: "View tenant leases",        color: "text-emerald-600", bg: "bg-emerald-100" },
            { href: "/landlord/issues",     Icon: AlertCircle, label: t("issues"),     sub: "Tenant-reported issues",    color: "text-amber-600",   bg: "bg-amber-100"   },
          ].map((card) => (
            <Link key={card.href} href={card.href} className="group">
              <div className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm hover:shadow-md transition-all hover:-translate-y-0.5">
                <div className={`flex h-9 w-9 items-center justify-center rounded-xl ${card.bg}`}>
                  <card.Icon className={`h-5 w-5 ${card.color}`} />
                </div>
                <div className="flex-1">
                  <div className="text-sm font-semibold text-slate-800">{card.label}</div>
                  <div className="text-xs text-slate-400">{card.sub}</div>
                </div>
                <ArrowRight className="h-4 w-4 text-slate-300 group-hover:text-indigo-600 transition-colors" />
              </div>
            </Link>
          ))}
        </div>

        {err && <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{err}</div>}

        {/* Recent properties / submissions */}
        <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4">
            <h2 className="text-sm font-bold text-slate-800">{t("submissions")}</h2>
            <Link href="/landlord/properties">
              <Button variant="outline" className="gap-2 text-xs border-slate-200 text-slate-600">
                View All <ArrowRight className="h-3 w-3" />
              </Button>
            </Link>
          </div>

          <div className="p-4">
            {loading ? (
              <div className="animate-pulse space-y-3">
                <div className="h-14 rounded-xl bg-slate-100" />
                <div className="h-14 rounded-xl bg-slate-100" />
              </div>
            ) : properties.length === 0 ? (
              <div className="text-sm text-slate-500 py-4 text-center">No properties yet.</div>
            ) : (
              <div className="space-y-3">
                {properties.slice(0, 5).map((p) => (
                  <div key={p.propertyId} className="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-4">
                    <div>
                      <div className="text-sm font-semibold text-slate-800">{p.address}</div>
                      <div className="text-xs text-slate-400">
                        #{p.propertyId}
                        {p.propertyType ? ` · ${p.propertyType}` : ""}
                        {p.bedrooms != null ? ` · ${p.bedrooms} bed` : ""}
                        {p.rentAmount ? ` · $${p.rentAmount.toLocaleString()}/mo` : ""}
                      </div>
                    </div>
                    <StatusBadge status={p.submissionStatus} />
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </AppShell>
  );
}
