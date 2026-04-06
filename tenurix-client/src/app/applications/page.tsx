"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { ArrowRight, FileText, RefreshCw } from "lucide-react";

type AppRow = {
  applicationId: number;
  listingId: number;
  status: string;
  submittedAt: string;
  requestedStartDate: string;
  requestedEndDate: string;
  decisionNotes?: string | null;
  reviewNote?: string | null;
};

function fmtDate(d: string) {
  try { return new Date(d).toLocaleString(); } catch { return d; }
}

export default function ApplicationsPage() {
  const { isReady } = useAuth();
  const [rows, setRows] = useState<AppRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>("");

  async function load() {
    try {
      setError(""); setLoading(true);
      const res = await apiFetch("/client/applications");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load applications. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load applications");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { if (isReady) load(); }, [isReady]);

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">Applications</h1>
            <p className="mt-1 text-sm text-slate-500">Track your lease applications and decisions.</p>
          </div>
          <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
            Refresh
          </Button>
        </div>

        {/* Error */}
        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <div className="font-semibold">Unable to load applications</div>
            <div className="mt-0.5">{error}</div>
            <Button variant="outline" className="mt-3" onClick={load}>Retry</Button>
          </div>
        )}

        {/* Loading */}
        {loading && (
          <div className="animate-pulse rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <div className="border-b border-slate-100 px-5 py-4">
              <div className="h-4 w-40 rounded bg-slate-200" />
            </div>
            <div className="divide-y divide-slate-100">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="px-5 py-5 space-y-4">
                  <div className="flex items-center justify-between">
                    <div className="space-y-2">
                      <div className="flex items-center gap-3">
                        <div className="h-4 w-36 rounded bg-slate-200" />
                        <div className="h-5 w-16 rounded-full bg-slate-100" />
                      </div>
                      <div className="h-3 w-52 rounded bg-slate-100" />
                    </div>
                    <div className="h-8 w-20 rounded-lg bg-slate-100" />
                  </div>
                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    {[...Array(3)].map((_, j) => (
                      <div key={j} className="rounded-xl border border-slate-100 bg-slate-50 p-3 space-y-2">
                        <div className="h-3 w-20 rounded bg-slate-200" />
                        <div className="h-4 w-32 rounded bg-slate-100" />
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Empty */}
        {!loading && !error && rows.length === 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-50 mb-5">
              <FileText className="h-7 w-7 text-indigo-500" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">No applications yet</h2>
            <p className="mt-2 text-sm text-slate-500">Browse listings on Tenurix and submit your first application.</p>
            <Link href="/listings">
              <Button className="mt-5 gap-2 bg-indigo-600 hover:bg-indigo-700 text-white">
                Browse Listings <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        )}

        {/* Application cards */}
        {!loading && !error && rows.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <div className="border-b border-slate-100 px-5 py-4">
              <span className="text-sm text-slate-500">
                Showing <span className="font-semibold text-slate-800">{rows.length}</span> application(s)
              </span>
            </div>

            <div className="divide-y divide-slate-100">
              {rows.map((x) => (
                <div key={x.applicationId} className="px-5 py-5 hover:bg-slate-50 transition-colors">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm font-semibold text-slate-900">Application #{x.applicationId}</span>
                        <StatusBadge status={x.status} />
                      </div>
                      <div className="mt-1 text-xs text-slate-400">
                        Listing #{x.listingId} · Submitted: {fmtDate(x.submittedAt)}
                      </div>
                    </div>
                    <Link href={`/apply?listingId=${x.listingId}`}>
                      <Button variant="outline" className="gap-2 border-slate-200 text-slate-600 text-xs">
                        Re-apply <ArrowRight className="h-3.5 w-3.5" />
                      </Button>
                    </Link>
                  </div>

                  <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-3">
                    {[
                      { label: "Requested Term", value: `${x.requestedStartDate} → ${x.requestedEndDate}` },
                      { label: "Review Note", value: x.reviewNote || "—" },
                      { label: "Decision Notes", value: x.decisionNotes || "—" },
                    ].map((item) => (
                      <div key={item.label} className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                        <div className="text-xs text-slate-400 font-medium">{item.label}</div>
                        <div className="mt-1 text-sm text-slate-700">{item.value}</div>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </AppShell>
  );
}
