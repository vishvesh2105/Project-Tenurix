"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { ScrollText, RefreshCw, ArrowRight, Home, FileText, Download, PenTool, CheckCircle2, Loader2 } from "lucide-react";

type LeaseRow = {
  leaseId: number;
  listingId: number;
  address: string;
  leaseStartDate: string;
  leaseEndDate: string;
  leaseStatus: string;
  rentAmount: number;
  leaseDocumentUrl: string | null;
  tenantSignedAt: string | null;
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
  const [signingId, setSigningId] = useState<number | null>(null);
  const [downloadingId, setDownloadingId] = useState<number | null>(null);

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

  async function handleDownload(leaseId: number) {
    try {
      setDownloadingId(leaseId);
      const res = await apiFetch(`/lease-documents/${leaseId}/download`);
      if (!res.ok) {
        const err = await safeJson<any>(res);
        throw new Error(err?.message || "Failed to download lease.");
      }
      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Lease-${leaseId}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (e: any) {
      alert(e?.message || "Download failed.");
    } finally {
      setDownloadingId(null);
    }
  }

  async function handleSign(leaseId: number) {
    const confirmed = window.confirm(
      "By signing this lease, you agree to all the terms and conditions outlined in the lease agreement.\n\nThis action cannot be undone. Do you want to proceed?"
    );
    if (!confirmed) return;

    try {
      setSigningId(leaseId);
      const res = await apiFetch(`/lease-documents/${leaseId}/sign`, { method: "POST" });
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || "Failed to sign lease.");

      // Refresh to show updated signed status
      await load();
    } catch (e: any) {
      alert(e?.message || "Signing failed.");
    } finally {
      setSigningId(null);
    }
  }

  useEffect(() => { if (isReady) load(); }, [isReady]);

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
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="animate-pulse space-y-3">
              <div className="h-5 w-44 rounded-lg bg-slate-200" />
              <div className="h-20 w-full rounded-xl bg-slate-100" />
              <div className="h-20 w-full rounded-xl bg-slate-100" />
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
            <p className="mt-2 text-sm text-slate-500">Lease details will appear here once your application is approved.</p>
            <Link href="/listings">
              <Button className="mt-5 gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
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

                {/* Lease Document Section */}
                {lease.leaseStatus === "Active" && (
                  <div className="mt-4 rounded-xl border border-blue-100 bg-blue-50/50 p-3">
                    <div className="flex items-center gap-2 mb-2">
                      <FileText className="h-4 w-4 text-blue-600" />
                      <span className="text-xs font-semibold text-blue-700">Lease Agreement</span>
                      {lease.tenantSignedAt ? (
                        <span className="ml-auto flex items-center gap-1 text-[10px] font-semibold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
                          <CheckCircle2 className="h-3 w-3" /> Signed
                        </span>
                      ) : (
                        <span className="ml-auto text-[10px] font-semibold text-amber-600 bg-amber-50 px-2 py-0.5 rounded-full border border-amber-200">
                          Awaiting Signature
                        </span>
                      )}
                    </div>
                    <div className="flex gap-2">
                      <Button
                        variant="outline"
                        className="gap-1.5 text-xs border-blue-200 text-blue-700 hover:bg-blue-100 flex-1"
                        onClick={() => handleDownload(lease.leaseId)}
                        disabled={downloadingId === lease.leaseId}
                      >
                        {downloadingId === lease.leaseId ? (
                          <Loader2 className="h-3 w-3 animate-spin" />
                        ) : (
                          <Download className="h-3 w-3" />
                        )}
                        Download PDF
                      </Button>
                      {!lease.tenantSignedAt && (
                        <Button
                          className="gap-1.5 text-xs bg-blue-600 hover:bg-blue-700 text-white flex-1"
                          onClick={() => handleSign(lease.leaseId)}
                          disabled={signingId === lease.leaseId}
                        >
                          {signingId === lease.leaseId ? (
                            <Loader2 className="h-3 w-3 animate-spin" />
                          ) : (
                            <PenTool className="h-3 w-3" />
                          )}
                          Sign Lease
                        </Button>
                      )}
                    </div>
                    {lease.tenantSignedAt && (
                      <p className="mt-2 text-[10px] text-slate-500">
                        Signed on {fmtDate(lease.tenantSignedAt)}
                      </p>
                    )}
                  </div>
                )}

                {/* Only show View Property when lease is not Active */}
                {lease.leaseStatus !== "Active" && (
                  <div className="mt-4 flex justify-end">
                    <Link href={`/listings/${lease.listingId}`}>
                      <Button variant="outline" className="gap-2 text-xs border-slate-200 text-slate-600">
                        View Property <ArrowRight className="h-3 w-3" />
                      </Button>
                    </Link>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
