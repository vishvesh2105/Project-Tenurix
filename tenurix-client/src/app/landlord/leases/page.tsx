"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { ScrollText, RefreshCw, Home, Mail } from "lucide-react";

type LeaseRow = {
  leaseId: number;
  listingId: number;
  address: string;
  clientUserId: number;
  tenantEmail: string;
  leaseStartDate: string;
  leaseEndDate: string;
  leaseStatus: string;
  rentAmount: number | null;
};

function fmtDate(d: string) {
  try {
    return new Date(d).toLocaleDateString();
  } catch {
    return d;
  }
}

export default function LandlordLeasesPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [rows, setRows] = useState<LeaseRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    try {
      setLoading(true);
      setError("");
      const res = await apiFetch("/landlord/leases");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load leases. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load leases");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (isReady) load();
  }, [isReady]);

  if (!isReady) {
    return (
      <AppShell>
        <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="h-6 w-32 rounded-lg bg-slate-200" />
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
            <p className="mt-1 text-sm text-slate-500">View leases on your properties.</p>
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
            <div className="mt-1 text-red-500">{error}</div>
          </div>
        )}

        {/* Loading */}
        {loading && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="animate-pulse space-y-3">
              <div className="h-5 w-44 rounded-lg bg-slate-200" />
              <div className="h-16 w-full rounded-xl bg-slate-100" />
              <div className="h-16 w-full rounded-xl bg-slate-100" />
            </div>
          </div>
        )}

        {/* Empty */}
        {!loading && !error && rows.length === 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-50 mb-5">
              <ScrollText className="h-7 w-7 text-indigo-300" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">{t("noLeases")}</h2>
            <p className="mt-2 text-sm text-slate-500">
              Leases will appear here when tenants are approved for your properties.
            </p>
          </div>
        )}

        {/* Lease Cards */}
        {!loading && !error && rows.length > 0 && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {rows.map((lease) => (
              <div
                key={lease.leaseId}
                className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-all hover:-translate-y-0.5"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100">
                      <Home className="h-5 w-5 text-indigo-600" />
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-slate-800">Lease #{lease.leaseId}</div>
                      <div className="text-xs text-slate-400">{lease.address}</div>
                    </div>
                  </div>
                  <StatusBadge status={lease.leaseStatus} />
                </div>

                {/* Tenant */}
                <div className="mt-3 flex items-center gap-2 rounded-xl border border-slate-100 bg-slate-50 px-3 py-2">
                  <Mail className="h-3.5 w-3.5 text-slate-400" />
                  <span className="text-xs text-slate-600">{lease.tenantEmail}</span>
                </div>

                <div className="mt-3 grid grid-cols-3 gap-3">
                  <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="text-xs text-slate-500">{t("startDate")}</div>
                    <div className="mt-1 text-sm font-semibold text-slate-800">{fmtDate(lease.leaseStartDate)}</div>
                  </div>
                  <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="text-xs text-slate-500">{t("endDate")}</div>
                    <div className="mt-1 text-sm font-semibold text-slate-800">{fmtDate(lease.leaseEndDate)}</div>
                  </div>
                  <div className="rounded-xl border border-slate-100 bg-emerald-50 p-3">
                    <div className="text-xs text-slate-500">{t("rent")}</div>
                    <div className="mt-1 text-sm font-semibold text-emerald-700">
                      ${lease.rentAmount?.toLocaleString() ?? "—"}/mo
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
