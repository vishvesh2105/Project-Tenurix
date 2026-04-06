"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { ScrollText, RefreshCw, ArrowRight, Home, Key } from "lucide-react";

type LeaseRow = {
  leaseId: number;
  listingId: number;
  address: string;
  leaseStartDate: string;
  leaseEndDate: string;
  leaseStatus: string;
  rentAmount: number;
};

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString(); } catch { return d; }
}

export default function LeasesPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [rows, setRows] = useState<LeaseRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    try {
      setLoading(true); setError("");
      const res = await apiFetch("/client/leases");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load leases. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load leases");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { if (isReady) load(); }, [isReady]);

  if (!isReady) {
    return (
      <AppShell>
        <div className="animate-pulse flex flex-col gap-6">
          <div className="flex items-center justify-between">
            <div className="space-y-2">
              <div className="h-7 w-32 rounded-lg bg-slate-200" />
              <div className="h-4 w-64 rounded-lg bg-slate-100" />
            </div>
            <div className="h-9 w-24 rounded-lg bg-slate-100" />
          </div>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-4">
                <div className="flex items-start gap-3">
                  <div className="h-10 w-10 rounded-xl bg-slate-200" />
                  <div className="flex-1 space-y-2">
                    <div className="h-4 w-28 rounded bg-slate-200" />
                    <div className="h-3 w-44 rounded bg-slate-100" />
                  </div>
                  <div className="h-5 w-16 rounded-full bg-slate-100" />
                </div>
                <div className="grid grid-cols-3 gap-3">
                  {[...Array(3)].map((_, j) => (
                    <div key={j} className="rounded-xl border border-slate-100 bg-slate-50 p-3 space-y-2">
                      <div className="h-2.5 w-14 rounded bg-slate-200" />
                      <div className="h-4 w-20 rounded bg-slate-100" />
                    </div>
                  ))}
                </div>
                <div className="flex justify-end">
                  <div className="h-8 w-28 rounded-lg bg-slate-100" />
                </div>
              </div>
            ))}
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
            <h1 className="text-2xl font-bold text-slate-900">{t("leases")}</h1>
            <p className="mt-1 text-sm text-slate-500">View your active and past lease agreements.</p>
          </div>
          <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
            Refresh
          </Button>
        </div>

        {/* Error */}
        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <div className="font-semibold">Unable to load leases</div>
            <div className="mt-0.5">{error}</div>
          </div>
        )}

        {/* Loading */}
        {loading && (
          <div className="animate-pulse grid grid-cols-1 gap-4 md:grid-cols-2">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm space-y-4">
                <div className="flex items-start gap-3">
                  <div className="h-10 w-10 rounded-xl bg-slate-200" />
                  <div className="flex-1 space-y-2">
                    <div className="h-4 w-28 rounded bg-slate-200" />
                    <div className="h-3 w-44 rounded bg-slate-100" />
                  </div>
                  <div className="h-5 w-16 rounded-full bg-slate-100" />
                </div>
                <div className="grid grid-cols-3 gap-3">
                  {[...Array(3)].map((_, j) => (
                    <div key={j} className="rounded-xl border border-slate-100 bg-slate-50 p-3 space-y-2">
                      <div className="h-2.5 w-14 rounded bg-slate-200" />
                      <div className="h-4 w-20 rounded bg-slate-100" />
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Empty */}
        {!loading && !error && rows.length === 0 && (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="rounded-full bg-indigo-50 p-4 mb-4">
              <Key className="h-8 w-8 text-indigo-400" />
            </div>
            <h3 className="text-lg font-semibold text-slate-700 mb-1">{t("noLeases")}</h3>
            <p className="text-sm text-slate-500 mb-4">Lease details will appear here once your application is approved.</p>
            <Link href="/listings">
              <Button className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
                {t("browseListings")} <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        )}

        {/* Lease cards */}
        {!loading && !error && rows.length > 0 && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {rows.map((lease) => (
              <div
                key={lease.leaseId}
                className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-all"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100">
                      <Home className="h-5 w-5 text-indigo-600" />
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-slate-900">Lease #{lease.leaseId}</div>
                      <div className="text-xs text-slate-500">{lease.address}</div>
                    </div>
                  </div>
                  <StatusBadge status={lease.leaseStatus} />
                </div>

                <div className="mt-4 grid grid-cols-3 gap-3">
                  {[
                    { label: t("startDate"), value: fmtDate(lease.leaseStartDate) },
                    { label: t("endDate"), value: fmtDate(lease.leaseEndDate) },
                    { label: t("rent"), value: `$${lease.rentAmount?.toLocaleString() ?? "—"}/mo`, highlight: true },
                  ].map((item) => (
                    <div key={item.label} className={`rounded-xl border p-3 ${item.highlight ? "border-emerald-100 bg-emerald-50" : "border-slate-100 bg-slate-50"}`}>
                      <div className="text-[10px] text-slate-400 font-medium">{item.label}</div>
                      <div className={`mt-1 text-sm font-semibold ${item.highlight ? "text-emerald-700" : "text-slate-800"}`}>{item.value}</div>
                    </div>
                  ))}
                </div>

                <div className="mt-4 flex justify-end">
                  <Link href={`/listings/${lease.listingId}`}>
                    <Button variant="outline" className="gap-2 text-xs border-slate-200 text-slate-600">
                      View Property <ArrowRight className="h-3 w-3" />
                    </Button>
                  </Link>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
