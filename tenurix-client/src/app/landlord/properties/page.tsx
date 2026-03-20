"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { Building2, RefreshCw, Plus, Bed, Bath, ShieldAlert, ArrowRight } from "lucide-react";

type PropertyRow = {
  propertyId: number;
  address: string;
  propertyType: string;
  bedrooms: number | null;
  bathrooms: number | null;
  rentAmount: number | null;
  submissionStatus: string;
  mediaUrl: string | null;
  listingId: number | null;
  listingStatus: string | null;
};

export default function LandlordPropertiesPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [rows, setRows] = useState<PropertyRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [hasIdRequest, setHasIdRequest] = useState(false);

  async function load() {
    try {
      setLoading(true);
      setError("");
      const res = await apiFetch("/landlord/properties");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load properties. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load properties");
    } finally {
      setLoading(false);
    }
  }

  async function loadIdRequests() {
    try {
      const res = await apiFetch("/landlord/id-requests");
      const data = await safeJson<any>(res);
      if (res.ok && data) setHasIdRequest(!!data.hasOpenRequest);
    } catch {}
  }

  useEffect(() => {
    if (isReady) {
      load();
      loadIdRequests();
    }
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
            <h1 className="text-2xl font-bold text-slate-900">{t("properties")}</h1>
            <p className="mt-1 text-sm text-slate-500">
              View your submitted properties and their listing status.
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
              <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
              Refresh
            </Button>
            <Link href="/landlord/submissions/new">
              <Button className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
                <Plus className="h-4 w-4" /> {t("newSubmission")}
              </Button>
            </Link>
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <div className="font-semibold">Unable to load properties</div>
            <div className="mt-1 text-red-500">{error}</div>
          </div>
        )}

        {/* NEW ID NEEDED Banner */}
        {hasIdRequest && (
          <Link href={rows.length > 0 ? `/landlord/properties/${rows[0].propertyId}` : "#"}>
            <div className="rounded-2xl border-2 border-amber-400 bg-amber-50 p-4 flex items-center gap-3 hover:bg-amber-100 transition cursor-pointer">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-100">
                <ShieldAlert className="h-5 w-5 text-amber-600" />
              </div>
              <div className="flex-1">
                <h3 className="text-sm font-bold text-amber-800 uppercase tracking-wide">New ID Needed</h3>
                <p className="text-xs text-amber-600 mt-0.5">Management has requested a new ID document. Click any property to upload.</p>
              </div>
              <ArrowRight className="h-5 w-5 text-amber-500" />
            </div>
          </Link>
        )}

        {/* Loading */}
        {loading && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="animate-pulse space-y-3">
              <div className="h-5 w-44 rounded-lg bg-slate-200" />
              <div className="h-24 w-full rounded-xl bg-slate-100" />
              <div className="h-24 w-full rounded-xl bg-slate-100" />
            </div>
          </div>
        )}

        {/* Empty */}
        {!loading && !error && rows.length === 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-50 mb-5">
              <Building2 className="h-7 w-7 text-indigo-300" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">{t("noProperties")}</h2>
            <p className="mt-2 text-sm text-slate-500">Submit a property to get started.</p>
            <div className="mt-6 flex justify-center">
              <Link href="/landlord/submissions/new">
                <Button className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
                  <Plus className="h-4 w-4" /> {t("newSubmission")}
                </Button>
              </Link>
            </div>
          </div>
        )}

        {/* Property Cards */}
        {!loading && !error && rows.length > 0 && (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {rows.map((p) => (
              <Link
                href={`/landlord/properties/${p.propertyId}`}
                key={p.propertyId}
                className="block rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-all hover:-translate-y-0.5 cursor-pointer"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100">
                      <Building2 className="h-5 w-5 text-indigo-600" />
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-slate-800">{p.address}</div>
                      <div className="text-xs text-slate-400">
                        {p.propertyType || "Property"} &middot; #{p.propertyId}
                      </div>
                    </div>
                  </div>
                  <StatusBadge status={p.submissionStatus} />
                </div>

                <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <div className="rounded-xl border border-slate-100 bg-emerald-50 p-3">
                    <div className="text-xs text-slate-500">{t("rent")}</div>
                    <div className="mt-1 text-sm font-semibold text-emerald-700">
                      {p.rentAmount != null ? `$${p.rentAmount.toLocaleString()}/mo` : "—"}
                    </div>
                  </div>
                  <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="flex items-center gap-1 text-xs text-slate-500">
                      <Bed className="h-3 w-3" /> Beds
                    </div>
                    <div className="mt-1 text-sm font-semibold text-slate-800">{p.bedrooms ?? "—"}</div>
                  </div>
                  <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="flex items-center gap-1 text-xs text-slate-500">
                      <Bath className="h-3 w-3" /> Baths
                    </div>
                    <div className="mt-1 text-sm font-semibold text-slate-800">{p.bathrooms ?? "—"}</div>
                  </div>
                  <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="text-xs text-slate-500">Listing</div>
                    <div className="mt-1">
                      {p.listingStatus ? (
                        <StatusBadge status={p.listingStatus} />
                      ) : (
                        <span className="text-sm text-slate-400">—</span>
                      )}
                    </div>
                  </div>
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
