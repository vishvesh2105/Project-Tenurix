"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import { ArrowRight } from "lucide-react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;

type ListingDetail = any;

export default function ListingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const listingId = Number(id);

  const [row, setRow] = useState<ListingDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  useEffect(() => {
    (async () => {
      try {
        setErr("");
        setLoading(true);
        const res = await fetch(`${API_BASE}/public/listings/${listingId}`);
        const data = await res.json();
        if (!res.ok) throw new Error(data?.message || `Failed (${res.status})`);
        setRow(data);
      } catch (e: any) {
        setErr(e?.message ?? "Failed to load listing");
      } finally {
        setLoading(false);
      }
    })();
  }, [listingId]);

  return (
    <PublicShell>
      <section className="mx-auto max-w-6xl px-4 py-10">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {row?.title || `Listing #${listingId}`}
            </h1>
            <p className="mt-1 text-sm text-white/60">{row?.city || ""}</p>
          </div>

          <Link href={`/apply?listingId=${listingId}`}>
            <Button className="gap-2">
              Apply <ArrowRight className="h-4 w-4" />
            </Button>
          </Link>
        </div>

        {err ? (
          <div className="mt-6 rounded-2xl border border-red-400/20 bg-red-500/10 p-4 text-sm text-red-100">
            <div className="font-semibold">Unable to load listing</div>
            <div className="mt-1 text-red-100/80">{err}</div>
          </div>
        ) : null}

        {loading ? (
          <div className="mt-6 h-48 animate-pulse rounded-2xl border border-white/10 bg-white/5" />
        ) : null}

        {!loading && row ? (
          <div className="mt-6 rounded-2xl border border-white/10 bg-white/5 p-6">
            <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Rent</div>
                <div className="mt-1 text-sm font-semibold">${row?.monthlyRent ?? "—"}/mo</div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Bedrooms</div>
                <div className="mt-1 text-sm font-semibold">{row?.bedrooms ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Bathrooms</div>
                <div className="mt-1 text-sm font-semibold">{row?.bathrooms ?? "—"}</div>
              </div>
              <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                <div className="text-xs text-white/60">Listing ID</div>
                <div className="mt-1 text-sm font-semibold">{listingId}</div>
              </div>
            </div>

            <div className="mt-6 text-sm text-white/70">
              {row?.description || "Description coming soon."}
            </div>
          </div>
        ) : null}
      </section>
    </PublicShell>
  );
}
