"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import {
  ArrowLeft, ArrowRight, ChevronLeft, ChevronRight,
  Building2, BedDouble, Bath, MapPin, DollarSign, Home,
  FileText, Car, Zap, Droplets, Flame, Wifi, Tv,
  Dumbbell, Waves, WashingMachine, Wind, Package,
  PawPrint, Plug, Calendar, Layers, Users, Sofa,
  CheckCircle2, Clock, Shield,
} from "lucide-react";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

function imgSrc(url: string | null): string | null {
  if (!url) return null;
  if (url.startsWith("http")) return url;
  return `${API_BASE}${url}`;
}

type ListingDetail = {
  listingId: number;
  propertyId: number;
  addressLine1: string;
  city: string;
  province: string | null;
  postalCode: string | null;
  propertyType: string;
  bedrooms: number | null;
  bathrooms: number | null;
  rentAmount: number | null;
  mediaUrl: string | null;
  description: string | null;
  photosJson: string | null;
  propertySubType: string | null;
  leaseTerm: string | null;
  isShortTerm: boolean | null;
  isFurnished: boolean | null;
  yearBuilt: number | null;
  numberOfFloors: number | null;
  numberOfUnits: number | null;
  availableDate: string | null;
};

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json) return [];
  try { const arr = JSON.parse(json); return Array.isArray(arr) ? arr : []; }
  catch { return []; }
}

const UTILITY_ICONS: Record<string, any> = {
  "Hydro/Electricity": Zap,
  "Water": Droplets,
  "Heating": Flame,
  "Internet/WiFi": Wifi,
  "Gas": Flame,
  "Cable TV": Tv,
};

const AMENITY_ICONS: Record<string, any> = {
  "Gym": Dumbbell,
  "Pool": Waves,
  "Laundry In-Unit": WashingMachine,
  "Laundry Shared": WashingMachine,
  "Balcony": Home,
  "Dishwasher": Droplets,
  "AC": Wind,
  "Storage": Package,
  "Elevator": Layers,
  "Concierge": Users,
  "Pet Friendly": PawPrint,
  "EV Charging": Plug,
};

export default function ListingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const listingId = Number(id);

  const [row, setRow] = useState<ListingDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [activePhoto, setActivePhoto] = useState(0);

  useEffect(() => {
    (async () => {
      try {
        setErr("");
        setLoading(true);
        const res = await fetch(`${API_BASE}/public/listings/${listingId}`);
        const text = await res.text();
        const data = text ? JSON.parse(text) : null;
        if (!res.ok) throw new Error(data?.message || data?.title || "Failed to load listing. Please try again.");
        setRow(data);
      } catch (e: any) {
        setErr(e?.message ?? "Failed to load listing");
      } finally {
        setLoading(false);
      }
    })();
  }, [listingId]);

  const photos: string[] = row ? parseJsonArray(row.photosJson).map((u) => imgSrc(u)!).filter(Boolean) : [];
  if (photos.length === 0 && row?.mediaUrl) {
    const src = imgSrc(row.mediaUrl);
    if (src) photos.push(src);
  }


  function prevPhoto() { setActivePhoto((p) => (p <= 0 ? photos.length - 1 : p - 1)); }
  function nextPhoto() { setActivePhoto((p) => (p >= photos.length - 1 ? 0 : p + 1)); }

  // About property table rows (only non-null, NO listing ID)
  const aboutRows: { label: string; value: string }[] = [];
  if (row) {
    aboutRows.push({ label: "Property Type", value: row.propertyType });
    if (row.propertySubType) aboutRows.push({ label: "Sub-Type", value: row.propertySubType });
    if (row.leaseTerm) aboutRows.push({ label: "Lease Term", value: row.leaseTerm });
    if (row.availableDate) aboutRows.push({ label: "Available Date", value: new Date(row.availableDate).toLocaleDateString("en-CA", { year: "numeric", month: "long", day: "numeric" }) });
    if (row.isFurnished != null) aboutRows.push({ label: "Furnished", value: row.isFurnished ? "Yes" : "No" });
    if (row.isShortTerm != null) aboutRows.push({ label: "Short-Term Rental", value: row.isShortTerm ? "Yes" : "No" });
    if (row.yearBuilt) aboutRows.push({ label: "Year Built", value: String(row.yearBuilt) });
    if (row.numberOfFloors) aboutRows.push({ label: "Number of Floors", value: String(row.numberOfFloors) });
    if (row.numberOfUnits) aboutRows.push({ label: "Number of Units", value: String(row.numberOfUnits) });
    }
  }

  return (
    <PublicShell>
      <div className="bg-slate-50 min-h-screen">
        {/* Breadcrumb */}
        <div className="bg-white border-b border-slate-200">
          <div className="mx-auto max-w-7xl px-4 py-3 flex items-center justify-between">
            <Link href="/listings" className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-indigo-600 transition-colors">
              <ArrowLeft className="h-4 w-4" />
              Back to listings
            </Link>
            {row && (
              <div className="flex items-center gap-1.5 text-xs text-slate-400">
                <Shield className="h-3.5 w-3.5 text-emerald-500" />
                <span className="text-emerald-600 font-medium">Verified Property</span>
              </div>
            )}
          </div>
        </div>

        <div className="mx-auto max-w-7xl px-4 py-8">
          {/* Error */}
          {err && (
            <div className="rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-700 mb-6">
              <div className="font-semibold">Unable to load listing</div>
              <div className="mt-0.5">{err}</div>
              <Link href="/listings"><Button variant="outline" className="mt-3 text-xs">Back to listings</Button></Link>
            </div>
          )}

          {/* Skeleton */}
          {loading && (
            <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
              <div className="lg:col-span-2 space-y-5">
                <div className="h-72 rounded-2xl animate-pulse bg-gradient-to-br from-slate-200 to-slate-100 md:h-[420px]" />
                <div className="h-8 w-2/3 rounded-xl animate-pulse bg-slate-200" />
                <div className="h-4 w-1/3 rounded-lg animate-pulse bg-slate-100" />
                <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
                  {Array.from({ length: 4 }).map((_, i) => (
                    <div key={i} className="h-24 rounded-2xl animate-pulse bg-slate-200" />
                  ))}
                </div>
              </div>
              <div className="lg:col-span-1">
                <div className="h-80 rounded-2xl animate-pulse bg-slate-200" />
              </div>
            </div>
          )}

          {/* Not found */}
          {!loading && !err && !row && (
            <div className="rounded-2xl border border-slate-200 bg-white p-14 text-center shadow-sm">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-indigo-100 mb-5">
                <Building2 className="h-7 w-7 text-indigo-600" />
              </div>
              <h2 className="text-lg font-bold text-slate-900">Listing not found</h2>
              <p className="mt-2 text-sm text-slate-500">This listing may have been removed or is no longer active.</p>
              <Link href="/listings"><Button variant="outline" className="mt-5">Browse listings</Button></Link>
            </div>
          )}

          {/* Listing detail */}
          {!loading && row && (
            <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
              {/* ── Main column ── */}
              <div className="lg:col-span-2 space-y-6">

                {/* Photo Gallery */}
                <div className="rounded-2xl overflow-hidden shadow-md border border-slate-200">
                  {/* Main image */}
                  <div className="relative h-72 md:h-[460px] bg-gradient-to-br from-slate-100 to-slate-50">
                    {photos.length > 0 ? (
                      <img
                        key={activePhoto}
                        src={photos[activePhoto]}
                        alt={`${row.addressLine1} photo ${activePhoto + 1}`}
                        className="h-full w-full object-cover animate-[fadeIn_0.3s_ease]"
                      />
                    ) : (
                      <div className="h-full w-full flex flex-col items-center justify-center gap-3">
                        <Building2 className="h-16 w-16 text-slate-200" />
                        <span className="text-sm text-slate-400 font-medium">No photos available</span>
                      </div>
                    )}

                    {photos.length > 1 && (
                      <>
                        <button
                          onClick={prevPhoto}
                          className="absolute left-3 top-1/2 -translate-y-1/2 flex h-11 w-11 items-center justify-center rounded-full bg-white/90 text-slate-700 shadow-lg backdrop-blur-sm hover:bg-white hover:scale-105 transition-all"
                        >
                          <ChevronLeft className="h-5 w-5" />
                        </button>
                        <button
                          onClick={nextPhoto}
                          className="absolute right-3 top-1/2 -translate-y-1/2 flex h-11 w-11 items-center justify-center rounded-full bg-white/90 text-slate-700 shadow-lg backdrop-blur-sm hover:bg-white hover:scale-105 transition-all"
                        >
                          <ChevronRight className="h-5 w-5" />
                        </button>
                        <div className="absolute bottom-4 right-4 rounded-full bg-black/60 px-3.5 py-1.5 text-xs text-white font-semibold backdrop-blur-sm">
                          {activePhoto + 1} / {photos.length}
                        </div>
                      </>
                    )}

                    {row.propertyType && (
                      <span className="absolute top-4 left-4 rounded-full bg-white/95 px-3.5 py-1.5 text-xs font-bold text-indigo-600 shadow-md backdrop-blur-sm border border-white/50">
                        {row.propertyType}{row.propertySubType ? ` - ${row.propertySubType}` : ""}
                      </span>
                    )}
                  </div>

                  {/* Thumbnail strip */}
                  {photos.length > 1 && (
                    <div className="flex gap-1.5 p-2.5 overflow-x-auto bg-white border-t border-slate-100">
                      {photos.map((src, i) => (
                        <button
                          key={i}
                          onClick={() => setActivePhoto(i)}
                          className={`shrink-0 h-16 w-20 rounded-lg overflow-hidden border-2 transition-all duration-200 ${
                            i === activePhoto
                              ? "border-indigo-600 shadow-md ring-2 ring-indigo-200"
                              : "border-transparent opacity-60 hover:opacity-100 hover:border-slate-300"
                          }`}
                        >
                          <img src={src} alt={`Thumb ${i + 1}`} className="h-full w-full object-cover" />
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {/* Title + Location + Badges */}
                <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                  <h1 className="text-2xl font-extrabold text-slate-900 leading-tight">{row.addressLine1}</h1>
                  <div className="flex items-center gap-1.5 mt-2 text-slate-500 text-sm">
                    <MapPin className="h-4 w-4 text-indigo-500 shrink-0" />
                    <span>{row.city}{row.province ? `, ${row.province}` : ""}{row.postalCode ? ` ${row.postalCode}` : ""}</span>
                  </div>

                  {/* Quick badges */}
                  <div className="flex flex-wrap gap-2 mt-4">
                    {row.isFurnished && (
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 border border-indigo-100 px-3 py-1.5 text-xs font-semibold text-indigo-600">
                        <Sofa className="h-3.5 w-3.5" /> Furnished
                      </span>
                    )}
                    {row.isShortTerm && (
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 border border-amber-100 px-3 py-1.5 text-xs font-semibold text-amber-600">
                        <Clock className="h-3.5 w-3.5" /> Short-Term
                      </span>
                    )}
                    {row.leaseTerm && (
                      <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600">
                        <Calendar className="h-3.5 w-3.5" /> {row.leaseTerm}
                      </span>
                    )}
                  </div>
                </div>

                {/* Key Stats Grid */}
                <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                  {[
                    { label: "Monthly Rent", value: row.rentAmount != null ? `$${row.rentAmount.toLocaleString()}` : "—", Icon: DollarSign, bg: "bg-indigo-600", iconColor: "text-white", valueColor: "text-indigo-600", border: "border-indigo-200", cardBg: "bg-indigo-50" },
                    { label: "Bedrooms", value: row.bedrooms != null ? String(row.bedrooms) : "—", Icon: BedDouble, bg: "bg-slate-100", iconColor: "text-slate-600", valueColor: "text-slate-800", border: "border-slate-200", cardBg: "bg-white" },
                    { label: "Bathrooms", value: row.bathrooms != null ? String(row.bathrooms) : "—", Icon: Bath, bg: "bg-slate-100", iconColor: "text-slate-600", valueColor: "text-slate-800", border: "border-slate-200", cardBg: "bg-white" },
                    { label: "Available", value: row.availableDate ? new Date(row.availableDate).toLocaleDateString("en-CA", { month: "short", day: "numeric" }) : "Now", Icon: Calendar, bg: "bg-emerald-100", iconColor: "text-emerald-600", valueColor: "text-emerald-700", border: "border-emerald-200", cardBg: "bg-emerald-50" },
                  ].map((s) => (
                    <div key={s.label} className={`rounded-2xl border ${s.border} ${s.cardBg} p-4 shadow-sm hover:shadow-md transition-shadow`}>
                      <div className={`flex h-8 w-8 items-center justify-center rounded-xl ${s.bg} mb-3`}>
                        <s.Icon className={`h-4 w-4 ${s.iconColor}`} />
                      </div>
                      <div className="text-[11px] font-medium text-slate-500 uppercase tracking-wide">{s.label}</div>
                      <div className={`mt-0.5 text-lg font-bold ${s.valueColor}`}>{s.value}</div>
                    </div>
                  ))}
                </div>

                  <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <div className="flex items-center gap-3 mb-4">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100">
                        <Car className="h-5 w-5 text-slate-600" />
                      </div>
                      <div>
                      </div>
                    </div>
                    <div className="flex items-center gap-4">
                      <div className="flex items-baseline gap-1.5">
                      </div>
                        <span className="rounded-full bg-indigo-50 border border-indigo-100 px-3 py-1 text-xs font-semibold text-indigo-600">
                        </span>
                      )}
                    </div>
                  </div>
                )}

                  <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <div className="flex items-center gap-3 mb-5">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100">
                        <Zap className="h-5 w-5 text-amber-600" />
                      </div>
                      <div>
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                        const Icon = UTILITY_ICONS[u] || Zap;
                        return (
                          <div key={u} className="flex items-center gap-2.5 rounded-xl border border-amber-100 bg-amber-50/50 px-3.5 py-3 text-sm">
                            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-100 shrink-0">
                              <Icon className="h-3.5 w-3.5 text-amber-600" />
                            </div>
                            <span className="font-medium text-slate-700">{u}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                  <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <div className="flex items-center gap-3 mb-5">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-100">
                        <Dumbbell className="h-5 w-5 text-emerald-600" />
                      </div>
                      <div>
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                        const Icon = AMENITY_ICONS[a] || CheckCircle2;
                        return (
                          <div key={a} className="flex items-center gap-2.5 rounded-xl border border-emerald-100 bg-emerald-50/50 px-3.5 py-3 text-sm">
                            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-100 shrink-0">
                              <Icon className="h-3.5 w-3.5 text-emerald-600" />
                            </div>
                            <span className="font-medium text-slate-700">{a}</span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Description */}
                {row.description && (
                  <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <div className="flex items-center gap-3 mb-4">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100">
                        <FileText className="h-5 w-5 text-indigo-600" />
                      </div>
                      <h2 className="font-bold text-slate-800 text-sm">About This Property</h2>
                    </div>
                    <p className="text-sm text-slate-600 leading-relaxed whitespace-pre-wrap">{row.description}</p>
                  </div>
                )}

                {/* Property Details Table */}
                {aboutRows.length > 0 && (
                  <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <div className="flex items-center gap-3 mb-5">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100">
                        <Building2 className="h-5 w-5 text-slate-600" />
                      </div>
                      <h2 className="font-bold text-slate-800 text-sm">Property Details</h2>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8">
                      {aboutRows.map((r, i) => (
                        <div key={r.label} className={`flex items-center justify-between py-3 ${i < aboutRows.length - (aboutRows.length % 2 === 0 ? 2 : 1) ? "border-b border-slate-100" : ""}`}>
                          <span className="text-sm text-slate-500">{r.label}</span>
                          <span className="text-sm font-semibold text-slate-800">{r.value}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              {/* ── Sidebar ── */}
              <div className="lg:col-span-1">
                <div className="rounded-2xl border border-slate-200 bg-white shadow-sm sticky top-24 overflow-hidden">
                  {/* Price header */}
                  <div className="bg-gradient-to-r from-indigo-600 to-indigo-700 p-6 text-white">
                    <div className="text-3xl font-extrabold">
                      {row.rentAmount != null ? `$${row.rentAmount.toLocaleString()}` : "—"}
                      <span className="text-base font-medium text-indigo-200 ml-1">/month</span>
                    </div>
                    <p className="mt-1.5 text-xs text-indigo-200">
                    </p>
                  </div>

                  {/* Property summary */}
                  <div className="p-6 space-y-3">
                    {[
                      { label: "Address", value: row.addressLine1 },
                      { label: "City", value: `${row.city}${row.province ? `, ${row.province}` : ""}` },
                      { label: "Postal Code", value: row.postalCode || "N/A" },
                      { label: "Type", value: `${row.propertyType}${row.propertySubType ? ` (${row.propertySubType})` : ""}` },
                      { label: "Bedrooms", value: row.bedrooms != null ? `${row.bedrooms} bed${row.bedrooms !== 1 ? "s" : ""}` : "N/A" },
                      { label: "Bathrooms", value: row.bathrooms != null ? `${row.bathrooms} bath${row.bathrooms !== 1 ? "s" : ""}` : "N/A" },
                      ...(row.availableDate ? [{ label: "Available", value: new Date(row.availableDate).toLocaleDateString("en-CA", { month: "short", day: "numeric", year: "numeric" }) }] : []),
                      ...(row.leaseTerm ? [{ label: "Lease", value: row.leaseTerm }] : []),
                    ].map((item) => (
                      <div key={item.label} className="flex justify-between text-sm py-1">
                        <span className="text-slate-500">{item.label}</span>
                        <span className="font-medium text-slate-800 text-right max-w-[55%] truncate">{item.value}</span>
                      </div>
                    ))}
                  </div>

                  {/* CTA */}
                  <div className="px-6 pb-6">
                    <Link href={`/apply?listingId=${listingId}`} className="block">
                      <Button className="w-full bg-indigo-600 hover:bg-indigo-700 text-white gap-2 h-12 text-sm font-bold shadow-lg shadow-indigo-600/25 transition-all hover:-translate-y-0.5 hover:shadow-xl rounded-xl">
                        Apply Now <ArrowRight className="h-4 w-4" />
                      </Button>
                    </Link>
                    <div className="mt-3 flex items-center justify-center gap-1.5 text-xs text-slate-400">
                      <Shield className="h-3.5 w-3.5 text-emerald-500" />
                      <span>No fees to apply. Approval within 3-5 days.</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </PublicShell>
  );
}
