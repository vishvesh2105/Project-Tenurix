"use client";

import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { AlertTriangle, ArrowRight, Loader2 } from "lucide-react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;

type ListingDetail = {
  listingId: number;
  title?: string | null;
  city?: string | null;
  address?: string | null;
  monthlyRent?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
  listingStatus?: string | null;
};

export default function ApplyPage() {
  const sp = useSearchParams();
  const listingId = Number(sp.get("listingId") || "0");

  const [startDate, setStartDate] = useState("2026-03-01");
  const [endDate, setEndDate] = useState("2027-02-28");
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string>("");
  const [err, setErr] = useState<string>("");

  // optional listing summary (if your API has /public/listings/{id})
  const [listing, setListing] = useState<ListingDetail | null>(null);

  const token = useMemo(() => {
    if (typeof window === "undefined") return "";
    return localStorage.getItem("tenurix_token") || "";
  }, []);

  useEffect(() => {
    setErr("");
    setMsg("");

    if (!listingId || Number.isNaN(listingId)) {
      setErr("Missing or invalid listingId in URL.");
      return;
    }

    // Try to load listing details if the endpoint exists
    // If your API doesn't have this endpoint, it will silently ignore.
    (async () => {
      try {
        const res = await fetch(`${API_BASE}/public/listings/${listingId}`);
        if (!res.ok) return;
        const data = await res.json();
        setListing(data);
      } catch {
        // ignore
      }
    })();
  }, [listingId]);

  async function submit() {
    setErr("");
    setMsg("");

    if (!token) {
      setErr("You are not signed in. Please login first.");
      return;
    }
    if (!listingId) {
      setErr("Missing listingId.");
      return;
    }
    if (!startDate || !endDate) {
      setErr("Start date and end date are required.");
      return;
    }
    if (endDate <= startDate) {
      setErr("End date must be after start date.");
      return;
    }

    try {
      setBusy(true);
      setMsg("Submitting application...");

      const res = await fetch(`${API_BASE}/client/applications`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          listingId,
          requestedStartDate: startDate,
          requestedEndDate: endDate,
          documentsUrl: null,
        }),
      });

      const data = await res.json().catch(() => ({}));

      if (!res.ok) {
        throw new Error(data?.message || data?.title || `Submit failed (${res.status})`);
      }

      setMsg("Application submitted successfully.");
      // redirect to applications
      window.location.href = "/applications";
    } catch (e: any) {
      setErr(e?.message ?? "Failed to submit application.");
      setMsg("");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Apply for Lease</h1>
            <p className="mt-1 text-sm text-white/60">
              Submit a lease application for Listing <span className="text-white/80 font-semibold">#{listingId}</span>.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Link href="/applications">
              <Button variant="outline" className="gap-2">
                Applications <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
            <Link href="/dashboard">
              <Button variant="secondary" className="gap-2">
                Dashboard <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        </div>

        {/* Listing summary (optional) */}
        {listing ? (
          <div className="rounded-2xl border border-white/10 bg-white/5 p-5">
            <div className="text-sm font-semibold text-white/90">
              {listing.title || `Listing #${listing.listingId}`}
            </div>
            <div className="mt-1 text-xs text-white/60">
              {listing.address ? listing.address : ""}{" "}
              {listing.city ? `• ${listing.city}` : ""}{" "}
              {listing.listingStatus ? `• Status: ${listing.listingStatus}` : ""}
            </div>
            <div className="mt-3 grid grid-cols-2 gap-3 md:grid-cols-4">
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Rent</div>
                <div className="mt-1 text-sm">
                  {listing.monthlyRent != null ? `$${listing.monthlyRent}/mo` : "—"}
                </div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Bedrooms</div>
                <div className="mt-1 text-sm">{listing.bedrooms ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Bathrooms</div>
                <div className="mt-1 text-sm">{listing.bathrooms ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Listing ID</div>
                <div className="mt-1 text-sm">{listing.listingId}</div>
              </div>
            </div>
          </div>
        ) : null}

        {/* Form */}
        <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="text-xs text-white/60">Requested Start Date</label>
              <input
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                className="mt-2 w-full rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
              />
            </div>

            <div>
              <label className="text-xs text-white/60">Requested End Date</label>
              <input
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                className="mt-2 w-full rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
              />
            </div>
          </div>

          {err ? (
            <div className="mt-4 flex gap-3 rounded-2xl border border-red-400/20 bg-red-500/10 p-4 text-sm text-red-100">
              <AlertTriangle className="mt-0.5 h-5 w-5" />
              <div>
                <div className="font-semibold">Unable to submit</div>
                <div className="mt-1 text-red-100/80">{err}</div>
              </div>
            </div>
          ) : null}

          {msg ? (
            <div className="mt-4 rounded-2xl border border-white/10 bg-black/20 p-4 text-sm text-white/70">
              {msg}
            </div>
          ) : null}

          <div className="mt-6 flex items-center justify-end">
            <Button className="gap-2" onClick={submit} disabled={busy}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              Submit Application
            </Button>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
