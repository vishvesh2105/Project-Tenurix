"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { ArrowRight, FileText, RefreshCw } from "lucide-react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;

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
  try {
    return new Date(d).toLocaleString();
  } catch {
    return d;
  }
}

export default function ApplicationsPage() {
  const [rows, setRows] = useState<AppRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>("");

  const token = useMemo(() => {
    if (typeof window === "undefined") return "";
    return localStorage.getItem("tenurix_token") || "";
  }, []);

  async function load() {
    try {
      setError("");
      setLoading(true);

      if (!token) {
        setError("You are not signed in. Please login first.");
        setRows([]);
        return;
      }

      const res = await fetch(`${API_BASE}/client/applications`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data?.message || data?.title || `Request failed (${res.status})`);
      }

      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load applications");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Applications</h1>
            <p className="mt-1 text-sm text-white/60">
              Track your lease applications and decisions.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="outline" className="gap-2" onClick={load} disabled={loading}>
              <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
              Refresh
            </Button>
            <Link href="/dashboard">
              <Button variant="secondary" className="gap-2">
                Dashboard <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        </div>

        {/* States */}
        {error ? (
          <div className="rounded-2xl border border-red-400/20 bg-red-500/10 p-4 text-sm text-red-100">
            <div className="font-semibold">Unable to load applications</div>
            <div className="mt-1 text-red-100/80">{error}</div>
            <div className="mt-3">
              <Link href="/login">
                <Button variant="secondary">Go to Login</Button>
              </Link>
            </div>
          </div>
        ) : null}

        {loading ? (
          <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
            <div className="animate-pulse space-y-3">
              <div className="h-5 w-44 rounded bg-white/10" />
              <div className="h-10 w-full rounded bg-white/5" />
              <div className="h-10 w-full rounded bg-white/5" />
              <div className="h-10 w-full rounded bg-white/5" />
            </div>
          </div>
        ) : null}

        {!loading && !error && rows.length === 0 ? (
          <div className="rounded-2xl border border-white/10 bg-white/5 p-10 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl border border-white/10 bg-white/5">
              <FileText className="h-6 w-6 text-white/80" />
            </div>
            <h2 className="mt-4 text-lg font-semibold">No applications yet</h2>
            <p className="mt-1 text-sm text-white/60">
              Browse listings on Tenurix and submit your first application.
            </p>
            <div className="mt-6 flex justify-center">
              <Link href="/apply?listingId=7">
                <Button className="gap-2">
                  Apply to a Listing <ArrowRight className="h-4 w-4" />
                </Button>
              </Link>
            </div>
          </div>
        ) : null}

        {/* Table */}
        {!loading && !error && rows.length > 0 ? (
          <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/5">
            <div className="border-b border-white/10 px-5 py-4">
              <div className="text-sm text-white/60">
                Showing <span className="text-white/90 font-semibold">{rows.length}</span>{" "}
                application(s)
              </div>
            </div>

            <div className="divide-y divide-white/10">
              {rows.map((x) => (
                <div key={x.applicationId} className="px-5 py-4 hover:bg-white/5 transition">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-3">
                        <div className="text-sm font-semibold">
                          Application #{x.applicationId}
                        </div>
                        <StatusBadge status={x.status} />
                      </div>
                      <div className="mt-1 text-xs text-white/60">
                        Listing ID: <span className="text-white/80">{x.listingId}</span> • Submitted:{" "}
                        <span className="text-white/80">{fmtDate(x.submittedAt)}</span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Link href={`/apply?listingId=${x.listingId}`}>
                        <Button variant="outline" className="gap-2">
                          Re-apply <ArrowRight className="h-4 w-4" />
                        </Button>
                      </Link>
                    </div>
                  </div>

                  <div className="mt-3 grid grid-cols-1 gap-3 md:grid-cols-3">
                    <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                      <div className="text-xs text-white/60">Requested Term</div>
                      <div className="mt-1 text-sm">
                        {x.requestedStartDate} → {x.requestedEndDate}
                      </div>
                    </div>

                    <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                      <div className="text-xs text-white/60">Review Note</div>
                      <div className="mt-1 text-sm text-white/80">
                        {x.reviewNote ? x.reviewNote : "—"}
                      </div>
                    </div>

                    <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                      <div className="text-xs text-white/60">Decision Notes</div>
                      <div className="mt-1 text-sm text-white/80">
                        {x.decisionNotes ? x.decisionNotes : "—"}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </AppShell>
  );
}
