"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { useRouter } from "next/navigation";
import { AlertCircle, RefreshCw, MapPin, User, ChevronRight, MessageSquare } from "lucide-react";

type IssueRow = {
  issueId: number;
  issueType: string;
  description: string;
  status: string;
  createdAt: string;
  propertyAddress: string;
  reportedByName: string | null;
};

function fmtDate(d: string) {
  try {
    return new Date(d).toLocaleDateString();
  } catch {
    return d;
  }
}

export default function LandlordIssuesPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const router = useRouter();
  const [rows, setRows] = useState<IssueRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    try {
      setLoading(true);
      setError("");
      const res = await apiFetch("/landlord/issues");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load issues. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load issues");
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
            <h1 className="text-2xl font-bold text-slate-900">{t("issues")}</h1>
            <p className="mt-1 text-sm text-slate-500">Issues reported by tenants on your properties.</p>
          </div>
          <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
            Refresh
          </Button>
        </div>

        {/* Error */}
        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <div className="font-semibold">Unable to load issues</div>
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
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-amber-50 mb-5">
              <AlertCircle className="h-7 w-7 text-amber-400" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">{t("noIssues")}</h2>
            <p className="mt-2 text-sm text-slate-500">No issues have been reported on your properties yet.</p>
          </div>
        )}

        {/* Issues List */}
        {!loading && !error && rows.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <div className="border-b border-slate-100 px-5 py-4">
              <span className="text-sm text-slate-500">
                Showing <span className="font-semibold text-slate-800">{rows.length}</span> issue(s)
              </span>
            </div>
            <div className="divide-y divide-slate-100">
              {rows.map((issue) => (
                <div
                  key={issue.issueId}
                  className="px-5 py-4 hover:bg-slate-50 transition-colors cursor-pointer"
                  onClick={() => router.push(`/landlord/issues/${issue.issueId}`)}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-50">
                        <AlertCircle className="h-5 w-5 text-amber-500" />
                      </div>
                      <div>
                        <div className="text-sm font-semibold text-slate-800">
                          {issue.issueType} <span className="text-slate-400">· #{issue.issueId}</span>
                        </div>
                        <div className="flex items-center gap-1.5 text-xs text-slate-400 mt-0.5">
                          <MapPin className="h-3 w-3" />
                          {issue.propertyAddress}
                        </div>
                      </div>
                    </div>
                    <StatusBadge status={issue.status} />
                    <div className="flex items-center gap-1 text-xs text-slate-400 ml-auto">
                      <MessageSquare className="h-3.5 w-3.5" /> View <ChevronRight className="h-3 w-3" />
                    </div>
                  </div>

                  <div className="mt-3 rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="text-xs font-medium text-slate-400">{t("description")}</div>
                    <p className="mt-1 text-sm text-slate-700 line-clamp-2">{issue.description}</p>
                  </div>

                  <div className="mt-3 flex items-center justify-between text-xs text-slate-400">
                    <div className="flex items-center gap-1.5">
                      <User className="h-3 w-3" />
                      {issue.reportedByName || "Unknown"}
                    </div>
                    <div>{fmtDate(issue.createdAt)}</div>
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
