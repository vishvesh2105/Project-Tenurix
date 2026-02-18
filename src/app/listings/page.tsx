"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import { Search, SlidersHorizontal } from "lucide-react";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;

type ListingCard = {
  listingId: number;
  title: string;
  city: string;
  monthlyRent: number;
  bedrooms: number;
  bathrooms: number;
  thumbnailUrl?: string | null;
};

export default function ListingsPage() {
  const [rows, setRows] = useState<ListingCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  const [city, setCity] = useState("");
  const [minRent, setMinRent] = useState("");
  const [maxRent, setMaxRent] = useState("");
  const [bedrooms, setBedrooms] = useState("");

  const query = useMemo(() => {
    const p = new URLSearchParams();
    if (city.trim()) p.set("city", city.trim());
    if (minRent) p.set("minRent", minRent);
    if (maxRent) p.set("maxRent", maxRent);
    if (bedrooms) p.set("bedrooms", bedrooms);
    p.set("page", "1");
    p.set("pageSize", "12");
    return p.toString();
  }, [city, minRent, maxRent, bedrooms]);

  async function load() {
    try {
      setErr("");
      setLoading(true);
      const res = await fetch(`${API_BASE}/public/listings?${query}`);
      const data = await res.json();

      if (!res.ok) throw new Error(data?.message || data?.title || `Failed (${res.status})`);
setRows(Array.isArray(data?.items) ? data.items : []);
    } catch (e: any) {
      setErr(e?.message ?? "Failed to load listings");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query]);

  return (
    <PublicShell>
      <section className="mx-auto max-w-6xl px-4 py-10">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Approved Listings</h1>
            <p className="mt-1 text-sm text-white/60">Browse verified properties available on Tenurix.</p>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="outline" className="gap-2" onClick={load} disabled={loading}>
              <Search className="h-4 w-4" />
              Search
            </Button>
          </div>
        </div>

        {/* Filters */}
        <div className="mt-6 rounded-2xl border border-white/10 bg-white/5 p-4">
          <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-white/80">
            <SlidersHorizontal className="h-4 w-4" /> Filters
          </div>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
            <input
              value={city}
              onChange={(e) => setCity(e.target.value)}
              placeholder="City"
              className="rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
            />
            <input
              value={minRent}
              onChange={(e) => setMinRent(e.target.value)}
              placeholder="Min rent"
              inputMode="numeric"
              className="rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
            />
            <input
              value={maxRent}
              onChange={(e) => setMaxRent(e.target.value)}
              placeholder="Max rent"
              inputMode="numeric"
              className="rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
            />
            <input
              value={bedrooms}
              onChange={(e) => setBedrooms(e.target.value)}
              placeholder="Bedrooms"
              inputMode="numeric"
              className="rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-sm outline-none focus:border-white/20"
            />
          </div>
        </div>

        {err ? (
          <div className="mt-6 rounded-2xl border border-red-400/20 bg-red-500/10 p-4 text-sm text-red-100">
            <div className="font-semibold">Unable to load listings</div>
            <div className="mt-1 text-red-100/80">{err}</div>
          </div>
        ) : null}

        {loading ? (
          <div className="mt-6 grid grid-cols-1 gap-4 md:grid-cols-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="h-40 animate-pulse rounded-2xl border border-white/10 bg-white/5" />
            ))}
          </div>
        ) : null}

        {!loading && !err ? (
          <div className="mt-6 grid grid-cols-1 gap-4 md:grid-cols-3">
            {rows.map((x) => (
              <Link
                key={x.listingId}
                href={`/listings/${x.listingId}`}
                className="group rounded-2xl border border-white/10 bg-white/5 p-5 transition hover:bg-white/10"
              >
                <div className="text-sm font-semibold">{x.title}</div>
                <div className="mt-1 text-xs text-white/60">{x.city}</div>

                <div className="mt-4 grid grid-cols-3 gap-2">
                  <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                    <div className="text-[11px] text-white/60">Rent</div>
                    <div className="mt-1 text-sm font-semibold">${x.monthlyRent}/mo</div>
                  </div>
                  <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                    <div className="text-[11px] text-white/60">Beds</div>
                    <div className="mt-1 text-sm font-semibold">{x.bedrooms}</div>
                  </div>
                  <div className="rounded-xl border border-white/10 bg-black/20 p-3">
                    <div className="text-[11px] text-white/60">Baths</div>
                    <div className="mt-1 text-sm font-semibold">{x.bathrooms}</div>
                  </div>
                </div>

                <div className="mt-4 flex justify-end">
                  <span className="text-xs text-white/70 group-hover:text-white">
                    View details →
                  </span>
                </div>
              </Link>
            ))}
          </div>
        ) : null}
      </section>
    </PublicShell>
  );
}
