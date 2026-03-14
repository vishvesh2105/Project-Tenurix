"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import { useI18n } from "@/components/providers/I18nProvider";
import { AlertCircle, RefreshCw, Plus, X, Send, ImagePlus } from "lucide-react";

type IssueRow = {
  issueId: number;
  leaseId: number;
  issueType: string;
  description: string;
  status: string;
  createdAt: string;
  propertyAddress: string;
};

type LeaseOption = {
  leaseId: number;
  address: string;
  leaseStatus: string;
};

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString(); } catch { return d; }
}

const ISSUE_TYPES = ["Plumbing", "Electrical", "Heating/Cooling", "Appliance", "Pest Control", "Structural", "Other"];

export default function IssuesPage() {
  const { isReady } = useAuth();
  const { t } = useI18n();
  const [rows, setRows] = useState<IssueRow[]>([]);
  const [leases, setLeases] = useState<LeaseOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [showForm, setShowForm] = useState(false);
  const [formLeaseId, setFormLeaseId] = useState("");
  const [formType, setFormType] = useState("");
  const [formDesc, setFormDesc] = useState("");
  const [formImage, setFormImage] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [submitSuccess, setSubmitSuccess] = useState("");

  async function load() {
    try {
      setLoading(true); setError("");
      const [issuesRes, leasesRes] = await Promise.all([apiFetch("/client/issues"), apiFetch("/client/leases")]);
      const issuesData = await safeJson<any>(issuesRes);
      if (!issuesRes.ok) throw new Error(issuesData?.message || `Failed to load issues (${issuesRes.status})`);
      setRows(Array.isArray(issuesData) ? issuesData : []);
      const leasesData = await safeJson<any>(leasesRes);
      if (leasesRes.ok && Array.isArray(leasesData)) setLeases(leasesData);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load issues");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { if (isReady) load(); }, [isReady]);

  async function handleSubmit() {
    if (!formLeaseId || !formType.trim() || !formDesc.trim()) return;
    try {
      setSubmitting(true); setSubmitError(""); setSubmitSuccess("");

      let res: Response;

      if (formImage) {
        fd.append("LeaseId", formLeaseId);
        fd.append("IssueType", formType.trim());
        fd.append("Description", formDesc.trim());
        fd.append("Image", formImage);

          method: "POST",
          body: fd,
        });
      } else {
        // Use JSON → POST /client/issues
        res = await apiFetch("/client/issues", {
          method: "POST",
          body: JSON.stringify({
            leaseId: parseInt(formLeaseId),
            issueType: formType.trim(),
            description: formDesc.trim(),
          }),
        });
      }
      if (!res.ok) {
        const data = await safeJson<any>(res);
        throw new Error(data?.message || data?.title || "Failed to submit issue. Please try again.");
      }
      setSubmitSuccess("Issue reported successfully.");
      setFormLeaseId(""); setFormType(""); setFormDesc(""); setFormImage(null); setShowForm(false);
      load();
    } catch (e: any) {
      setSubmitError(e?.message ?? "Failed to submit issue");
    } finally {
      setSubmitting(false);
    }
  }

  if (!isReady) {
    return (
      <AppShell>
        <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="h-6 w-32 rounded-lg bg-slate-200" />
        </div>
      </AppShell>
    );
  }

  const inputClass = "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400";

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("issues")}</h1>
            <p className="mt-1 text-sm text-slate-500">Report and track maintenance issues.</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" className="gap-2 border-slate-200 text-slate-600" onClick={load} disabled={loading}>
              <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} /> Refresh
            </Button>
            <Button
              className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white"
              onClick={() => setShowForm(!showForm)}
              disabled={leases.length === 0}
            >
              {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {showForm ? "Cancel" : t("reportIssue")}
            </Button>
          </div>
        </div>

        {error && <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>}
        {submitSuccess && <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">{submitSuccess}</div>}

        {/* Report Form */}
        {showForm && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-sm font-bold text-slate-800 mb-5">{t("reportIssue")}</h2>
            {submitError && <div className="mb-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{submitError}</div>}
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Lease</label>
                <select value={formLeaseId} onChange={(e) => setFormLeaseId(e.target.value)} className={inputClass}>
                  <option value="">Select a lease...</option>
                  {leases.map((l) => <option key={l.leaseId} value={l.leaseId}>Lease #{l.leaseId} — {l.address}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("issueType")}</label>
                <select value={formType} onChange={(e) => setFormType(e.target.value)} className={inputClass}>
                  <option value="">Select type...</option>
                  {ISSUE_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">{t("description")}</label>
                <textarea
                  value={formDesc}
                  onChange={(e) => setFormDesc(e.target.value)}
                  rows={4}
                  placeholder="Describe the issue in detail..."
                  className={`${inputClass} resize-none`}
                />
              </div>
              <div>
                <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-4">
                  <div className="flex items-center gap-2 mb-2">
                    <ImagePlus className="h-4 w-4 text-slate-400" />
                    <span className="text-xs text-slate-500">Attach a photo of the issue</span>
                  </div>
                  <input
                    type="file"
                    accept="image/*"
                    className="text-xs text-slate-500"
                    onChange={(e) => {
                      const file = e.target.files?.[0] || null;
                      if (file) setFormImage(file);
                      e.target.value = "";
                    }}
                  />
                  {formImage && (
                    <div className="mt-3 flex items-center gap-2 rounded-lg border border-slate-200 bg-white p-2">
                      <img
                        src={URL.createObjectURL(formImage)}
                        alt={formImage.name}
                        className="h-10 w-10 rounded object-cover"
                      />
                      <span className="flex-1 truncate text-xs text-slate-700">{formImage.name}</span>
                      <button
                        type="button"
                        onClick={() => setFormImage(null)}
                        className="rounded-full p-1 text-red-400 hover:bg-red-50 hover:text-red-600 transition"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  )}
                </div>
              </div>
              <Button
                className="gap-2 bg-indigo-600 hover:bg-indigo-700 text-white"
                onClick={handleSubmit}
                disabled={submitting || !formLeaseId || !formType || !formDesc.trim()}
              >
                <Send className="h-4 w-4" />
                {submitting ? "Submitting..." : t("submit")}
              </Button>
            </div>
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

        {/* No leases */}
        {!loading && leases.length === 0 && (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4">
            <div className="text-sm font-semibold text-indigo-600">Note</div>
            <div className="mt-1 text-xs text-slate-600 leading-relaxed">
              You need an active lease to report issues. Once your lease application is approved, you can report maintenance issues here.
            </div>
          </div>
        )}

        {/* Empty */}
        {!loading && !error && rows.length === 0 && leases.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-50 mb-5">
              <AlertCircle className="h-7 w-7 text-indigo-300" />
            </div>
            <h2 className="text-lg font-bold text-slate-900">{t("noIssues")}</h2>
            <p className="mt-2 text-sm text-slate-500">No maintenance issues reported. Click &quot;Report Issue&quot; to submit one.</p>
          </div>
        )}

        {/* Issues list */}
        {!loading && !error && rows.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
            <div className="border-b border-slate-100 px-5 py-4">
              <span className="text-sm text-slate-500">
                Showing <span className="font-semibold text-slate-800">{rows.length}</span> issue(s)
              </span>
            </div>
            <div className="divide-y divide-slate-100">
              {rows.map((issue) => (
                <div key={issue.issueId} className="px-5 py-4 hover:bg-slate-50 transition-colors">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm font-semibold text-slate-900">{issue.issueType}</span>
                        <StatusBadge status={issue.status} />
                      </div>
                      <div className="mt-0.5 text-xs text-slate-400">{issue.propertyAddress} · {fmtDate(issue.createdAt)}</div>
                    </div>
                  </div>
                  <div className="mt-3 rounded-xl border border-slate-100 bg-slate-50 p-3">
                    <div className="text-xs text-slate-400 font-medium">{t("description")}</div>
                    <div className="mt-1 text-sm text-slate-700">{issue.description}</div>
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
