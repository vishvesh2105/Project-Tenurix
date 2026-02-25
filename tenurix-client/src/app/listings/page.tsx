"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import {
  SlidersHorizontal, Building2, BedDouble, Bath,
  MapPin, ArrowRight, RefreshCw, ChevronDown,
  ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight,
} from "lucide-react";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

function imgSrc(mediaUrl: string | null): string | null {
  if (!mediaUrl) return null;
  if (mediaUrl.startsWith("http")) return mediaUrl;
  return `${API_BASE}${mediaUrl}`;
}

type ListingCard = {
  listingId: number;
  propertyId: number;
  addressLine1: string;
  city: string;
  province: string;
  propertyType: string;
  bedrooms: number | null;
  bathrooms: number | null;
  rentAmount: number | null;
  mediaUrl: string | null;
};

const PROPERTY_TYPES = ["", "Apartment", "House", "Condo", "Townhouse", "Studio"];

function digitsOnly(v: string): string {
  return v.replace(/\D/g, "");
}

function ListingsContent() {

  const [rows, setRows] = useState<ListingCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [showFilters, setShowFilters] = useState(true);

  // Pagination
  const PAGE_SIZE = 12;
  const [totalItems, setTotalItems] = useState(0);
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));

  // Initialize from URL search params (reactive — updates on navigation)

  // Sync state when URL search params change (e.g. city link from home page)
  useEffect(() => {

  const mountedRef = useRef(false);

  function buildQuery(page: number = currentPage) {
    if (city.trim()) p.set("city", city.trim());
    if (minRent) p.set("minRent", minRent);
    if (maxRent) p.set("maxRent", maxRent);
    if (bedrooms !== "") p.set("bedrooms", bedrooms);
    if (propertyType) p.set("propertyType", propertyType);
    p.set("page", String(page));
    p.set("pageSize", String(PAGE_SIZE));
    return p.toString();
  }

  const load = useCallback(async (qs: string) => {
    try {
      setErr("");
      setLoading(true);
      const res = await fetch(`${API_BASE}/public/listings?${qs}`);
      const text = await res.text();
      const data = text ? JSON.parse(text) : null;
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load listings. Please try again.");
      setRows(Array.isArray(data?.items) ? data.items : []);
      setTotalItems(typeof data?.total === "number" ? data.total : 0);
    } catch (e: any) {
      setErr(e?.message ?? "Failed to load listings");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const qs = buildQuery();

    // Reset to page 1 when filters change (not on first mount)
    if (mountedRef.current) {
      setCurrentPage(1);
    }

    // Update URL
    if (typeof window !== "undefined") {
      if (city.trim()) urlP.set("city", city.trim());
      if (minRent) urlP.set("minRent", minRent);
      if (maxRent) urlP.set("maxRent", maxRent);
      if (bedrooms !== "") urlP.set("bedrooms", bedrooms);
      if (propertyType) urlP.set("propertyType", propertyType);
      const urlQs = urlP.toString();
      const url = urlQs ? `${window.location.pathname}?${urlQs}` : window.location.pathname;
    }

    if (!mountedRef.current) {
      mountedRef.current = true;
      load(qs);
      return;
    }

  }, [city, minRent, maxRent, bedrooms, propertyType, load]); // eslint-disable-line react-hooks/exhaustive-deps

  // Reload when page changes
  useEffect(() => {
    if (!mountedRef.current) return; // skip on first mount (handled above)
    load(buildQuery(currentPage));
    // Scroll to top on page change
    window.scrollTo({ top: 0, behavior: "smooth" });
  }, [currentPage]); // eslint-disable-line react-hooks/exhaustive-deps

  const activeFilters = [city, minRent, maxRent, bedrooms, propertyType].filter((v) => v !== "").length;

  function clearFilters() {
    setCity(""); setMinRent(""); setMaxRent(""); setBedrooms(""); setPropertyType("");
    setCurrentPage(1);
  }

  function goToPage(p: number) {
    const clamped = Math.max(1, Math.min(p, totalPages));
    if (clamped !== currentPage) setCurrentPage(clamped);
  }

  return (
    <PublicShell>
      <div className="bg-slate-50 min-h-screen">
        {/* Page header banner */}
        <div className="bg-gradient-to-r from-slate-900 to-indigo-950">
          <div className="mx-auto max-w-7xl px-4 py-10">
            <h1 className="text-2xl font-extrabold text-white tracking-tight">Available Listings</h1>
            <p className="mt-1 text-sm text-white/70">
              {loading ? "Loading listings..." : `${totalItems} verified propert${totalItems !== 1 ? "ies" : "y"} available on Tenurix`}
            </p>
          </div>
        </div>

        <div className="mx-auto max-w-7xl px-4 py-8">
          <div className="flex flex-col gap-6 lg:flex-row lg:items-start">

            {/* Filters sidebar */}
            <aside className="lg:w-68 shrink-0">
              <div className="rounded-2xl bg-white border border-slate-200 overflow-hidden shadow-sm">
                <button
                  className="w-full flex items-center justify-between px-5 py-4 text-sm font-semibold text-slate-800 hover:bg-slate-50 transition-colors"
                  onClick={() => setShowFilters((v) => !v)}
                >
                  <span className="flex items-center gap-2">
                    <SlidersHorizontal className="h-4 w-4 text-amber-500" />
                    Filters
                    {activeFilters > 0 && (
                      <span className="flex h-5 w-5 items-center justify-center rounded-full bg-indigo-600 text-[10px] font-bold text-white">
                        {activeFilters}
                      </span>
                    )}
                  </span>
                  <ChevronDown className={`h-4 w-4 text-slate-400 transition-transform duration-200 ${showFilters ? "rotate-180" : ""}`} />
                </button>

                {showFilters && (
                  <div className="border-t border-slate-100 px-5 pb-5 space-y-4">
                    {/* City */}
                    <div className="pt-4">
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">City</label>
                      <div className="relative">
                        <MapPin className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
                        <input
                          value={city}
                          onChange={(e) => setCity(e.target.value)}
                          placeholder="e.g. Toronto"
                          className="w-full pl-9 pr-3 py-2.5 text-sm border border-slate-200 rounded-xl outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition bg-white text-slate-900 placeholder:text-slate-400"
                        />
                      </div>
                    </div>

                    {/* Rent range */}
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Rent Range</label>
                      <div className="flex gap-2">
                        <input
                          value={minRent}
                          onChange={(e) => setMinRent(digitsOnly(e.target.value))}
                          placeholder="Min $"
                          inputMode="numeric"
                          className="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-xl outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition bg-white text-slate-900 placeholder:text-slate-400"
                        />
                        <input
                          value={maxRent}
                          onChange={(e) => setMaxRent(digitsOnly(e.target.value))}
                          placeholder="Max $"
                          inputMode="numeric"
                          className="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-xl outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition bg-white text-slate-900 placeholder:text-slate-400"
                        />
                      </div>
                    </div>

                    {/* Bedrooms */}
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Bedrooms</label>
                      <input
                        value={bedrooms}
                        onChange={(e) => setBedrooms(digitsOnly(e.target.value))}
                        placeholder="Any"
                        inputMode="numeric"
                        className="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-xl outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition bg-white text-slate-900 placeholder:text-slate-400"
                      />
                    </div>

                    {/* Property type */}
                    <div>
                      <label className="block text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Property Type</label>
                      <select
                        value={propertyType}
                        onChange={(e) => setPropertyType(e.target.value)}
                        className="w-full px-3 py-2.5 text-sm border border-slate-200 rounded-xl outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 bg-white text-slate-900 transition"
                      >
                        {PROPERTY_TYPES.map((t) => (
                          <option key={t} value={t}>{t || "Any type"}</option>
                        ))}
                      </select>
                    </div>

                    {activeFilters > 0 && (
                      <button
                        onClick={clearFilters}
                        className="w-full text-xs text-indigo-600 hover:text-indigo-700 font-semibold py-1.5 transition-colors text-center"
                      >
                        Clear all filters
                      </button>
                    )}
                  </div>
                )}
              </div>
            </aside>

            {/* Results */}
            <div className="flex-1 min-w-0">
              {/* Toolbar */}
              <div className="flex items-center justify-between mb-5">
                <span className="text-sm text-slate-500">
                  {loading ? "Searching..." : `Showing ${rows.length} of ${totalItems} listing${totalItems !== 1 ? "s" : ""}${totalPages > 1 ? ` · Page ${currentPage} of ${totalPages}` : ""}`}
                </span>
                <Button variant="outline" size="sm" className="gap-2 text-slate-600 border-slate-200 hover:border-indigo-300" onClick={() => load(buildQuery())} disabled={loading}>
                  <RefreshCw className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
                  Refresh
                </Button>
              </div>

              {/* Error */}
              {err && (
                <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 mb-5">
                  <div className="font-semibold">Unable to load listings</div>
                  <div className="mt-0.5 text-red-500">{err}</div>
                </div>
              )}

              {/* Skeleton */}
              {loading && (
                <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <div key={i} className="rounded-2xl bg-white border border-slate-200 overflow-hidden shadow-sm">
                      <div className="h-48 animate-pulse bg-gradient-to-br from-slate-200 to-slate-100" />
                      <div className="p-5 space-y-3">
                        <div className="h-4 w-3/4 rounded-lg bg-slate-200 animate-pulse" />
                        <div className="h-3 w-1/2 rounded-lg bg-slate-100 animate-pulse" />
                        <div className="flex gap-2">
                          <div className="h-12 flex-1 rounded-xl bg-slate-100 animate-pulse" />
                          <div className="h-12 flex-1 rounded-xl bg-slate-100 animate-pulse" />
                          <div className="h-12 flex-1 rounded-xl bg-slate-100 animate-pulse" />
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {/* Empty state */}
              {!loading && !err && rows.length === 0 && (
                <div className="rounded-2xl border border-slate-200 bg-white p-14 text-center shadow-sm">
                  <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-100 mb-5">
                    <Building2 className="h-7 w-7 text-indigo-600" />
                  </div>
                  <h2 className="text-lg font-bold text-slate-900">No listings found</h2>
                  <p className="mt-2 text-sm text-slate-500 max-w-xs mx-auto">
                    Try adjusting your filters or check back later for newly approved properties.
                  </p>
                  {activeFilters > 0 && (
                    <Button variant="outline" className="mt-5 border-indigo-300 text-indigo-600 hover:bg-indigo-50" onClick={clearFilters}>Clear filters</Button>
                  )}
                </div>
              )}

              {/* Listing cards */}
              {!loading && !err && rows.length > 0 && (
                <>
                  <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    {rows.map((x) => {
                      const photo = imgSrc(x.mediaUrl);
                      return (
                        <Link
                          key={x.listingId}
                          href={`/listings/${x.listingId}`}
                          className="group rounded-2xl bg-white border border-slate-200 overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 hover:-translate-y-1.5"
                        >
                          {/* Image */}
                          <div className="relative h-48 overflow-hidden bg-gradient-to-br from-indigo-50 to-amber-50">
                            {photo ? (
                              <img
                                src={photo}
                                alt={x.addressLine1}
                                className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-110"
                                onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = "none"; }}
                              />
                            ) : (
                              <div className="h-full w-full flex items-center justify-center">
                                <Building2 className="h-12 w-12 text-indigo-200" />
                              </div>
                            )}
                            {x.propertyType && (
                              <span className="absolute top-3 left-3 rounded-full bg-white/90 px-2.5 py-1 text-[11px] font-semibold text-indigo-600 shadow-sm backdrop-blur-sm">
                                {x.propertyType}
                              </span>
                            )}
                          </div>

                          {/* Body */}
                          <div className="p-5">
                            <div className="font-semibold text-slate-900 text-sm leading-snug truncate">{x.addressLine1}</div>
                            <div className="flex items-center gap-1 mt-1 text-xs text-slate-500">
                              <MapPin className="h-3 w-3 shrink-0" />
                              <span className="truncate">{x.city}{x.province ? `, ${x.province}` : ""}</span>
                            </div>

                            <div className="mt-4 flex gap-2.5">
                              <div className="flex-1 rounded-xl bg-indigo-500/10 px-2 py-2.5 text-center">
                                <div className="text-[10px] text-slate-500">Rent/mo</div>
                                <div className="text-sm font-bold text-indigo-600 mt-0.5">
                                  {x.rentAmount != null ? `$${x.rentAmount.toLocaleString()}` : "—"}
                                </div>
                              </div>
                              <div className="flex-1 rounded-xl bg-slate-50 px-2 py-2.5 text-center">
                                <div className="text-[10px] text-slate-500">Beds</div>
                                <div className="text-sm font-bold text-slate-800 mt-0.5 flex items-center justify-center gap-1">
                                  <BedDouble className="h-3 w-3 text-slate-400" />{x.bedrooms ?? "—"}
                                </div>
                              </div>
                              <div className="flex-1 rounded-xl bg-slate-50 px-2 py-2.5 text-center">
                                <div className="text-[10px] text-slate-500">Baths</div>
                                <div className="text-sm font-bold text-slate-800 mt-0.5 flex items-center justify-center gap-1">
                                  <Bath className="h-3 w-3 text-slate-400" />{x.bathrooms ?? "—"}
                                </div>
                              </div>
                            </div>

                            <div className="mt-4 flex items-center justify-between">
                              <span className="text-xs text-slate-400">View details</span>
                              <ArrowRight className="h-4 w-4 text-indigo-500 transition-transform group-hover:translate-x-1" />
                            </div>
                          </div>
                        </Link>
                      );
                    })}
                  </div>

                  {/* Pagination */}
                  {totalPages > 1 && (
                    <div className="mt-8 flex items-center justify-center gap-1.5">
                      {/* First */}
                      <button
                        onClick={() => goToPage(1)}
                        disabled={currentPage === 1}
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        title="First page"
                      >
                        <ChevronsLeft className="h-4 w-4" />
                      </button>
                      {/* Previous */}
                      <button
                        onClick={() => goToPage(currentPage - 1)}
                        disabled={currentPage === 1}
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        title="Previous page"
                      >
                        <ChevronLeft className="h-4 w-4" />
                      </button>

                      {/* Page numbers */}
                      {(() => {
                        const pages: number[] = [];
                        let start = Math.max(1, currentPage - 2);
                        let end = Math.min(totalPages, currentPage + 2);
                        // Ensure we always show 5 pages if available
                        if (end - start < 4) {
                          if (start === 1) end = Math.min(totalPages, start + 4);
                          else start = Math.max(1, end - 4);
                        }
                        for (let i = start; i <= end; i++) pages.push(i);

                        return (
                          <>
                            {start > 1 && (
                              <>
                                <button onClick={() => goToPage(1)} className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-sm font-medium text-slate-600 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 transition-colors">1</button>
                                {start > 2 && <span className="flex h-9 w-5 items-center justify-center text-slate-400 text-xs">...</span>}
                              </>
                            )}
                            {pages.map((p) => (
                              <button
                                key={p}
                                onClick={() => goToPage(p)}
                                className={`flex h-9 w-9 items-center justify-center rounded-lg border text-sm font-medium transition-colors ${
                                  p === currentPage
                                    ? "bg-indigo-600 border-indigo-600 text-white shadow-sm"
                                    : "border-slate-200 bg-white text-slate-600 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600"
                                }`}
                              >
                                {p}
                              </button>
                            ))}
                            {end < totalPages && (
                              <>
                                {end < totalPages - 1 && <span className="flex h-9 w-5 items-center justify-center text-slate-400 text-xs">...</span>}
                                <button onClick={() => goToPage(totalPages)} className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-sm font-medium text-slate-600 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 transition-colors">{totalPages}</button>
                              </>
                            )}
                          </>
                        );
                      })()}

                      {/* Next */}
                      <button
                        onClick={() => goToPage(currentPage + 1)}
                        disabled={currentPage === totalPages}
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        title="Next page"
                      >
                        <ChevronRight className="h-4 w-4" />
                      </button>
                      {/* Last */}
                      <button
                        onClick={() => goToPage(totalPages)}
                        disabled={currentPage === totalPages}
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-indigo-50 hover:border-indigo-300 hover:text-indigo-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        title="Last page"
                      >
                        <ChevronsRight className="h-4 w-4" />
                      </button>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </PublicShell>
  );
}

export default function ListingsPage() {
  return (
    <Suspense fallback={
      <PublicShell>
        <div className="bg-slate-50 min-h-screen">
          <div className="bg-gradient-to-r from-slate-900 to-indigo-950">
            <div className="mx-auto max-w-7xl px-4 py-10">
              <h1 className="text-2xl font-extrabold text-white tracking-tight">Available Listings</h1>
              <p className="mt-1 text-sm text-white/70">Loading listings...</p>
            </div>
          </div>
          <div className="mx-auto max-w-7xl px-4 py-8">
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="rounded-2xl bg-white border border-slate-200 overflow-hidden shadow-sm">
                  <div className="h-48 animate-pulse bg-gradient-to-br from-slate-200 to-slate-100" />
                  <div className="p-5 space-y-3">
                    <div className="h-4 w-3/4 rounded-lg bg-slate-200 animate-pulse" />
                    <div className="h-3 w-1/2 rounded-lg bg-slate-100 animate-pulse" />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </PublicShell>
    }>
      <ListingsContent />
    </Suspense>
  );
}
