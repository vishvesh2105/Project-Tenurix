"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { CreditCard, RefreshCw } from "lucide-react";

type PaymentRow = {
  paymentId: number;
  leaseId: number;
  amount: number;
  paymentDate: string;
  status: string;
  method: string;
};

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString(); } catch { return d; }
}

export default function PaymentsPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [rows, setRows] = useState<PaymentRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    try {
      setLoading(true); setError("");
      const res = await apiFetch("/client/payments");
      const data = await safeJson<any>(res);
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load payments. Please try again.");
      setRows(Array.isArray(data) ? data : []);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load payments");
    } finally {
      setLoading(false);
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
            <h1 className="text-2xl font-bold text-slate-900">{t("payments")}</h1>
            <p className="mt-1 text-sm text-slate-500">{t("paymentHistory")}</p>
          </div>
          <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} /> Refresh
          </Button>
        </div>

        {error && <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>}

        {loading && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="animate-pulse space-y-3">
              <div className="h-5 w-44 rounded-lg bg-slate-200" />
              <div className="h-12 w-full rounded-xl bg-slate-100" />
              <div className="h-12 w-full rounded-xl bg-slate-100" />
            </div>
          </div>
        )}

        {!loading && !error && rows.length === 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-50 mb-5">
              <CreditCard className="h-7 w-7 text-indigo-500" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">{t("noPayments")}</h2>
            <p className="mt-2 text-sm text-slate-500">Payment records will appear here once the payment system is activated.</p>
          </div>
        )}

        {!loading && !error && rows.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <div className="border-b border-slate-100 px-5 py-4">
              <span className="text-sm text-slate-500">
                Showing <span className="font-semibold text-slate-800">{rows.length}</span> payment(s)
              </span>
            </div>
            <div className="divide-y divide-slate-100">
              {rows.map((pay) => (
                <div key={pay.paymentId} className="flex items-center justify-between px-5 py-4 hover:bg-slate-50 transition-colors">
                  <div>
                    <div className="flex items-center gap-3">
                      <span className="text-sm font-semibold text-slate-900">${pay.amount?.toLocaleString() ?? "—"}</span>
                      <StatusBadge status={pay.status} />
                    </div>
                    <div className="mt-0.5 text-xs text-slate-400">
                      Lease #{pay.leaseId} · {fmtDate(pay.paymentDate)}
                      {pay.method && <> · {pay.method}</>}
                    </div>
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
