"use client";

import { useEffect, useState, useRef } from "react";
import { useParams, useRouter } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import {
  ArrowLeft, Building2, Bed, Bath, MapPin, Calendar,
  DollarSign, Car, Layers, ShieldAlert, Upload, CheckCircle2,
  Zap, Wifi, Flame, Droplets, Tv, Trash2, Snowflake,
  Home, ArrowUpDown,
  Users, Key, Shield,
  X,
} from "lucide-react";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

function imgSrc(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith("http")) return url;
  return `${API_BASE}${url}`;
}

type PropertyDetail = {
  propertyId: number;
  addressLine1: string;
  addressLine2: string | null;
  city: string;
  province: string;
  postalCode: string;
  propertyType: string;
  propertySubType: string | null;
  bedrooms: number | null;
  bathrooms: number | null;
  rentAmount: number | null;
  description: string | null;
  mediaUrl: string | null;
  photosJson: string | null;
  ownerIdPhotoUrl: string | null;
  ownerIdPhotosJson: string | null;
  submissionStatus: string;
  reviewNote: string | null;
  createdAt: string | null;
  reviewedAt: string | null;
  leaseTerm: string | null;
  isShortTerm: boolean | null;
  isFurnished: boolean | null;
  yearBuilt: number | null;
  numberOfFloors: number | null;
  numberOfUnits: number | null;
  parkingSpots: number | null;
  parkingType: string | null;
  availableDate: string | null;
  listingId: number | null;
  listingStatus: string | null;
};

type IdRequest = {
  requestId: number;
  docType: string;
  message: string | null;
  status: string;
  requestedBy: string;
};

const UTILITY_ICONS: Record<string, React.ReactNode> = {
  "Hydro/Electricity": <Zap className="h-4 w-4" />,
  "Water": <Droplets className="h-4 w-4" />,
  "Heating": <Flame className="h-4 w-4" />,
  "Gas": <Flame className="h-4 w-4" />,
  "Internet/WiFi": <Wifi className="h-4 w-4" />,
  "Cable TV": <Tv className="h-4 w-4" />,
  "Air Conditioning": <Snowflake className="h-4 w-4" />,
  "Trash Removal": <Trash2 className="h-4 w-4" />,
};

const AMENITY_ICONS: Record<string, React.ReactNode> = {
  "Gym": <Home className="h-4 w-4" />,
  "Pool": <Droplets className="h-4 w-4" />,
  "Balcony": <Home className="h-4 w-4" />,
  "AC": <Snowflake className="h-4 w-4" />,
  "Laundry In-Unit": <Home className="h-4 w-4" />,
  "Laundry Shared": <Home className="h-4 w-4" />,
  "Dishwasher": <Home className="h-4 w-4" />,
  "Elevator": <ArrowUpDown className="h-4 w-4" />,
  "Concierge": <Users className="h-4 w-4" />,
  "Rooftop Access": <Key className="h-4 w-4" />,
  "Storage Locker": <Key className="h-4 w-4" />,
  "Bike Storage": <Key className="h-4 w-4" />,
  "Pet-Friendly": <Home className="h-4 w-4" />,
  "BBQ Area": <Flame className="h-4 w-4" />,
  "Party Room": <Users className="h-4 w-4" />,
  "Visitor Parking": <Home className="h-4 w-4" />,
  "Security System": <Shield className="h-4 w-4" />,
  "Hardwood Floors": <Home className="h-4 w-4" />,
};

export default function LandlordPropertyDetailPage() {
  const { id } = useParams();
  const router = useRouter();
  const { isReady } = useAuth();

  const [property, setProperty] = useState<PropertyDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // ID request state
  const [hasIdRequest, setHasIdRequest] = useState(false);
  const [idRequests, setIdRequests] = useState<IdRequest[]>([]);
  const [uploading, setUploading] = useState(false);
  const [uploadMsg, setUploadMsg] = useState("");
  const [uploadErr, setUploadErr] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [idFiles, setIdFiles] = useState<File[]>([]);

  const [selectedPhoto, setSelectedPhoto] = useState(0);

  async function loadProperty() {
    try {
      setLoading(true);
      setError("");
      const res = await apiFetch(`/landlord/properties/${id}`);
      const data = await safeJson<PropertyDetail>(res);
      if (!res.ok) throw new Error((data as any)?.message || "Failed to load property.");
      setProperty(data);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load property details.");
    } finally {
      setLoading(false);
    }
  }

  async function loadIdRequests() {
    try {
      const res = await apiFetch("/landlord/id-requests");
      const data = await safeJson<any>(res);
      if (res.ok && data) {
        setHasIdRequest(!!data.hasOpenRequest);
        setIdRequests(Array.isArray(data.requests) ? data.requests : []);
      }
    } catch {
      // ignore
    }
  }

  async function handleUpload() {
    if (idFiles.length === 0) {
      setUploadErr("Please select at least one file.");
      return;
    }

    setUploading(true);
    setUploadErr("");
    setUploadMsg("");

    try {
      const fd = new FormData();
      idFiles.forEach((f) => fd.append("files", f));

      const res = await apiFetch("/landlord/id-upload", { method: "POST", body: fd });
      const data = await safeJson<any>(res);

      if (!res.ok) throw new Error(data?.message || "Upload failed.");

      setUploadMsg("ID documents uploaded successfully! Management will review them shortly.");
      setHasIdRequest(false);
      setIdRequests([]);
      setIdFiles([]);
      if (fileInputRef.current) fileInputRef.current.value = "";
    } catch (e: any) {
      setUploadErr(e?.message ?? "Upload failed.");
    } finally {
      setUploading(false);
    }
  }

  useEffect(() => {
    if (isReady && id) {
      loadProperty();
      loadIdRequests();
    }
  }, [isReady, id]);

  if (!isReady || loading) {
    return (
      <AppShell>
        <div className="space-y-4">
          <div className="h-8 w-48 rounded-lg bg-slate-200 animate-pulse" />
          <div className="h-64 rounded-2xl bg-slate-100 animate-pulse" />
          <div className="h-40 rounded-2xl bg-slate-100 animate-pulse" />
        </div>
      </AppShell>
    );
  }

  if (error || !property) {
    return (
      <AppShell>
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center">
          <ShieldAlert className="mx-auto h-10 w-10 text-red-300 mb-3" />
          <h2 className="text-lg font-bold text-red-800">Unable to load property</h2>
          <p className="mt-1 text-sm text-red-500">{error || "Property not found."}</p>
          <Button variant="outline" className="mt-4" onClick={() => router.push("/landlord/properties")}>
            <ArrowLeft className="h-4 w-4 mr-2" /> Back to Properties
          </Button>
        </div>
      </AppShell>
    );
  }

  const photos: string[] = (() => {
    try { return property.photosJson ? JSON.parse(property.photosJson) : []; } catch { return []; }
  })();
  })();
  })();

  const fullAddress = [
    property.addressLine1,
    property.addressLine2,
    property.city,
    property.province,
    property.postalCode,
  ].filter(Boolean).join(", ");

  return (
    <AppShell>
      <div className="flex flex-col gap-6 max-w-5xl mx-auto">
        {/* Back + Title */}
        <div className="flex items-center gap-3">
          <button
            onClick={() => router.push("/landlord/properties")}
            className="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 bg-white hover:bg-slate-50 transition"
          >
            <ArrowLeft className="h-4 w-4 text-slate-600" />
          </button>
          <div className="flex-1">
            <h1 className="text-xl font-bold text-slate-900">{property.addressLine1}</h1>
            <p className="text-sm text-slate-500">{property.city}, {property.province} {property.postalCode}</p>
          </div>
          <StatusBadge status={property.submissionStatus} />
        </div>

        {/* NEW ID NEEDED Banner */}
        {hasIdRequest && (
          <div className="rounded-2xl border-2 border-amber-400 bg-amber-50 p-5">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-100">
                <ShieldAlert className="h-5 w-5 text-amber-600" />
              </div>
              <div className="flex-1">
                <h3 className="text-sm font-bold text-amber-800 uppercase tracking-wide">New ID Needed</h3>
                <p className="mt-1 text-sm text-amber-700">
                  Management has requested a new ID document. Please upload your updated identification below.
                </p>
                {idRequests.map((r) => (
                  r.message && (
                    <p key={r.requestId} className="mt-2 text-xs text-amber-600 italic">
                      Message from {r.requestedBy}: &ldquo;{r.message}&rdquo;
                    </p>
                  )
                ))}

                <div className="mt-4 space-y-3">
                  <div className="flex items-center gap-3">
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/*,.pdf"
                      multiple
                      className="block w-full text-sm text-slate-600 file:mr-3 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-sm file:font-semibold file:bg-amber-100 file:text-amber-700 hover:file:bg-amber-200 transition"
                      onChange={(e) => {
                        const newFiles = e.target.files ? Array.from(e.target.files) : [];
                        if (newFiles.length > 0) {
                          setIdFiles((prev) => [...prev, ...newFiles]);
                        }
                        if (fileInputRef.current) fileInputRef.current.value = "";
                      }}
                    />
                  </div>

                  {idFiles.length > 0 && (
                    <div className="space-y-2">
                      <p className="text-xs text-amber-700 font-medium">{idFiles.length} file(s) selected</p>
                      {idFiles.map((file, idx) => (
                        <div key={`${file.name}-${idx}`} className="flex items-center gap-2 rounded-lg border border-amber-200 bg-white p-2">
                          {file.type.startsWith("image/") ? (
                            <img src={URL.createObjectURL(file)} alt={file.name} className="h-10 w-10 rounded object-cover" />
                          ) : (
                            <div className="flex h-10 w-10 items-center justify-center rounded bg-amber-50">
                              <Upload className="h-5 w-5 text-amber-400" />
                            </div>
                          )}
                          <span className="flex-1 truncate text-xs text-slate-700">{file.name}</span>
                          <button
                            type="button"
                            onClick={() => setIdFiles((prev) => prev.filter((_, i) => i !== idx))}
                            className="rounded-full p-1 text-red-400 hover:bg-red-50 hover:text-red-600 transition"
                          >
                            <X className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}

                  <Button
                    onClick={handleUpload}
                    disabled={uploading || idFiles.length === 0}
                    className="gap-2 bg-amber-600 hover:bg-amber-700 text-white"
                  >
                    <Upload className="h-4 w-4" />
                    {uploading ? "Uploading..." : "Upload ID Document"}
                  </Button>
                  {uploadErr && <p className="text-sm text-red-600">{uploadErr}</p>}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Upload success message */}
        {uploadMsg && (
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 flex items-center gap-3">
            <CheckCircle2 className="h-5 w-5 text-emerald-500 shrink-0" />
            <p className="text-sm text-emerald-700">{uploadMsg}</p>
            <button onClick={() => setUploadMsg("")} className="ml-auto">
              <X className="h-4 w-4 text-emerald-400" />
            </button>
          </div>
        )}

        {photos.length > 0 && (
          <div className="rounded-2xl border border-slate-200 bg-white overflow-hidden shadow-sm">
            <div className="relative h-72 sm:h-96 bg-slate-100">
              <img
                src={imgSrc(photos[selectedPhoto]) || ""}
                alt={`Property photo ${selectedPhoto + 1}`}
                className="h-full w-full object-cover"
                onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = "none"; }}
              />
            </div>
            {photos.length > 1 && (
              <div className="flex gap-2 p-3 overflow-x-auto">
                {photos.map((p, i) => (
                  <button
                    key={i}
                    onClick={() => setSelectedPhoto(i)}
                    className={`shrink-0 h-16 w-20 rounded-lg overflow-hidden border-2 transition ${
                      i === selectedPhoto ? "border-indigo-500" : "border-transparent hover:border-slate-300"
                    }`}
                  >
                    <img src={imgSrc(p) || ""} alt="" className="h-full w-full object-cover" />
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Status + Review Note */}
        {property.reviewNote && (
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Review Note</h3>
            <p className="text-sm text-slate-700">{property.reviewNote}</p>
          </div>
        )}

        {/* Property Overview */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-4">Property Overview</h3>

          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
            <InfoCard icon={<Building2 className="h-4 w-4 text-indigo-500" />} label="Type" value={property.propertySubType ? `${property.propertyType} · ${property.propertySubType}` : property.propertyType} />
            <InfoCard icon={<DollarSign className="h-4 w-4 text-emerald-500" />} label="Rent" value={property.rentAmount != null ? `$${property.rentAmount.toLocaleString()}/mo` : "—"} />
            <InfoCard icon={<Bed className="h-4 w-4 text-blue-500" />} label="Bedrooms" value={property.bedrooms?.toString() ?? "—"} />
            <InfoCard icon={<Bath className="h-4 w-4 text-blue-500" />} label="Bathrooms" value={property.bathrooms?.toString() ?? "—"} />
            <InfoCard icon={<Calendar className="h-4 w-4 text-orange-500" />} label="Lease Term" value={property.leaseTerm ?? "—"} />
            <InfoCard icon={<Calendar className="h-4 w-4 text-orange-500" />} label="Available" value={property.availableDate ? new Date(property.availableDate).toLocaleDateString() : "—"} />
            <InfoCard icon={<Car className="h-4 w-4 text-slate-500" />} label="Parking" value={property.parkingSpots != null ? `${property.parkingSpots} (${property.parkingType || "N/A"})` : "—"} />
            <InfoCard icon={<Layers className="h-4 w-4 text-slate-500" />} label="Year Built" value={property.yearBuilt?.toString() ?? "—"} />
          </div>

          <div className="mt-4 flex flex-wrap gap-3">
            {property.isFurnished && <span className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-600">Furnished</span>}
            {property.isShortTerm && <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-600">Short-Term</span>}
            {property.listingStatus && (
              <span className="flex items-center gap-1.5">
                <span className="text-xs text-slate-500">Listing:</span>
                <StatusBadge status={property.listingStatus} />
              </span>
            )}
          </div>
        </div>

        {/* Address */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Full Address</h3>
          <div className="flex items-center gap-2 text-sm text-slate-700">
            <MapPin className="h-4 w-4 text-red-400 shrink-0" />
            {fullAddress}
          </div>
        </div>

        {/* Description */}
        {property.description && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Description</h3>
            <p className="text-sm text-slate-700 leading-relaxed">{property.description}</p>
          </div>
        )}

          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="flex flex-wrap gap-2">
                <span key={u} className="flex items-center gap-1.5 rounded-full bg-emerald-50 border border-emerald-200 px-3 py-1.5 text-xs font-medium text-emerald-700">
                  {UTILITY_ICONS[u] || <Zap className="h-4 w-4" />}
                  {u}
                </span>
              ))}
            </div>
          </div>
        )}

          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="flex flex-wrap gap-2">
                <span key={a} className="flex items-center gap-1.5 rounded-full bg-indigo-50 border border-indigo-200 px-3 py-1.5 text-xs font-medium text-indigo-700">
                  {AMENITY_ICONS[a] || <CheckCircle2 className="h-4 w-4" />}
                  {a}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Timeline */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-3">Timeline</h3>
          <div className="space-y-2 text-sm">
            {property.createdAt && (
              <div className="flex items-center gap-2 text-slate-600">
                <div className="h-2 w-2 rounded-full bg-indigo-400" />
                Submitted on {new Date(property.createdAt).toLocaleDateString("en-CA", { year: "numeric", month: "long", day: "numeric" })}
              </div>
            )}
            {property.reviewedAt && (
              <div className="flex items-center gap-2 text-slate-600">
                <div className={`h-2 w-2 rounded-full ${property.submissionStatus === "Approved" ? "bg-emerald-400" : property.submissionStatus === "Rejected" ? "bg-red-400" : "bg-amber-400"}`} />
                {property.submissionStatus === "Approved" ? "Approved" : property.submissionStatus === "Rejected" ? "Rejected" : "Reviewed"} on {new Date(property.reviewedAt).toLocaleDateString("en-CA", { year: "numeric", month: "long", day: "numeric" })}
              </div>
            )}
            {property.listingId && (
              <div className="flex items-center gap-2 text-slate-600">
                <div className="h-2 w-2 rounded-full bg-emerald-400" />
                Listing #{property.listingId} &mdash; {property.listingStatus || "Active"}
              </div>
            )}
          </div>
        </div>
      </div>
    </AppShell>
  );
}

function InfoCard({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
      <div className="flex items-center gap-1.5 text-xs text-slate-500">{icon}{label}</div>
      <div className="mt-1 text-sm font-semibold text-slate-800">{value}</div>
    </div>
  );
}
