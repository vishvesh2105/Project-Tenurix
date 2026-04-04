"use client";

import { Suspense, useEffect, useState, useRef } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/useAuth";
import { apiFetch } from "@/lib/api";
import {
  AlertTriangle, ArrowRight, Loader2, FileText, BedDouble, Bath, DollarSign,
  User, Briefcase, Home, Users, PawPrint, Phone, ImagePlus, X,
} from "lucide-react";

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

type ListingDetail = {
  listingId: number;
  addressLine1?: string | null;
  city?: string | null;
  province?: string | null;
  propertyType?: string | null;
  rentAmount?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
};

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400";

const EMPLOYMENT_STATUSES = ["Employed Full-Time", "Employed Part-Time", "Self-Employed", "Student", "Retired", "Unemployed"];

export default function ApplyPage() {
  return (
    <Suspense fallback={
      <AppShell>
        <div className="animate-pulse rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="h-6 w-48 rounded-lg bg-slate-200" />
          <div className="mt-4 h-10 w-full rounded-xl bg-slate-100" />
        </div>
      </AppShell>
    }>
      <ApplyPageInner />
    </Suspense>
  );
}

function ApplyPageInner() {
  const sp = useSearchParams();
  const listingId = Number(sp.get("listingId") || "0");

  const { token, isReady } = useAuth();

  // Lease dates
  const [startDate, setStartDate] = useState("2026-03-01");
  const [endDate, setEndDate] = useState("2027-02-28");

  // Personal info
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [currentAddress, setCurrentAddress] = useState("");

  // Employment
  const [employmentStatus, setEmploymentStatus] = useState("");
  const [employerName, setEmployerName] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [annualIncome, setAnnualIncome] = useState("");

  // Household
  const [numberOfOccupants, setNumberOfOccupants] = useState("1");
  const [hasPets, setHasPets] = useState(false);
  const [petDetails, setPetDetails] = useState("");

  // Emergency contact
  const [emergencyContactName, setEmergencyContactName] = useState("");
  const [emergencyContactPhone, setEmergencyContactPhone] = useState("");
  const [emergencyContactRelation, setEmergencyContactRelation] = useState("");

  // References
  const [referenceName, setReferenceName] = useState("");
  const [referencePhone, setReferencePhone] = useState("");
  const [referenceRelation, setReferenceRelation] = useState("");

  // Additional
  const [additionalNotes, setAdditionalNotes] = useState("");
  const [documents, setDocuments] = useState<File[]>([]);
  const docInputRef = useRef<HTMLInputElement>(null);

  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string>("");
  const [err, setErr] = useState<string>("");
  const [listing, setListing] = useState<ListingDetail | null>(null);

  // Auto-fill from profile
  useEffect(() => {
    (async () => {
      try {
        const res = await apiFetch("/account/me");
        if (!res.ok) return;
        const data = await res.json();
        if (data.fullName) setFullName(data.fullName);
        if (data.phone) setPhone(data.phone);
      } catch {
        // ignore
      }
    })();
  }, []);

  useEffect(() => {
    setErr("");
    setMsg("");

    if (!listingId || Number.isNaN(listingId)) {
      setErr("Missing or invalid listingId in URL.");
      return;
    }

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
    if (!listingId) { setErr("Missing listingId."); return; }
    if (!fullName.trim()) { setErr("Full name is required."); return; }
    if (!phone.trim()) { setErr("Phone number is required."); return; }
    if (!currentAddress.trim()) { setErr("Current address is required."); return; }
    if (!employmentStatus) { setErr("Please select your employment status."); return; }
    if (!annualIncome || Number(annualIncome) <= 0) { setErr("Please enter your annual income."); return; }
    if (!startDate || !endDate) { setErr("Start date and end date are required."); return; }
    if (endDate <= startDate) { setErr("End date must be after start date."); return; }
    if (!emergencyContactName.trim() || !emergencyContactPhone.trim()) {
      setErr("Emergency contact name and phone are required.");
      return;
    }

    try {
      setBusy(true);
      setMsg("Submitting application...");

      const hasFiles = documents.length > 0;

      let res: Response;

      if (hasFiles) {
        // Use FormData with file uploads → POST /client/applications/upload
        const fd = new FormData();
        fd.append("ListingId", String(listingId));
        fd.append("RequestedStartDate", startDate);
        fd.append("RequestedEndDate", endDate);
        fd.append("FullName", fullName.trim());
        fd.append("Phone", phone.trim());
        if (dateOfBirth) fd.append("DateOfBirth", dateOfBirth);
        fd.append("CurrentAddress", currentAddress.trim());
        fd.append("EmploymentStatus", employmentStatus);
        if (employerName.trim()) fd.append("EmployerName", employerName.trim());
        if (jobTitle.trim()) fd.append("JobTitle", jobTitle.trim());
        fd.append("AnnualIncome", annualIncome);
        fd.append("NumberOfOccupants", numberOfOccupants || "1");
        fd.append("HasPets", String(hasPets));
        if (hasPets && petDetails.trim()) fd.append("PetDetails", petDetails.trim());
        fd.append("EmergencyContactName", emergencyContactName.trim());
        fd.append("EmergencyContactPhone", emergencyContactPhone.trim());
        if (emergencyContactRelation.trim()) fd.append("EmergencyContactRelation", emergencyContactRelation.trim());
        if (referenceName.trim()) fd.append("ReferenceName", referenceName.trim());
        if (referencePhone.trim()) fd.append("ReferencePhone", referencePhone.trim());
        if (referenceRelation.trim()) fd.append("ReferenceRelation", referenceRelation.trim());
        if (additionalNotes.trim()) fd.append("AdditionalNotes", additionalNotes.trim());
        documents.forEach((f) => fd.append("Documents", f));

        res = await apiFetch("/client/applications/upload", {
          method: "POST",
          body: fd,
        });
      } else {
        // Use JSON → POST /client/applications
        res = await apiFetch("/client/applications", {
          method: "POST",
          body: JSON.stringify({
            listingId,
            requestedStartDate: startDate,
            requestedEndDate: endDate,
            fullName: fullName.trim(),
            phone: phone.trim(),
            dateOfBirth: dateOfBirth || null,
            currentAddress: currentAddress.trim(),
            employmentStatus,
            employerName: employerName.trim() || null,
            jobTitle: jobTitle.trim() || null,
            annualIncome: Number(annualIncome),
            numberOfOccupants: Number(numberOfOccupants) || 1,
            hasPets,
            petDetails: hasPets ? petDetails.trim() || null : null,
            emergencyContactName: emergencyContactName.trim(),
            emergencyContactPhone: emergencyContactPhone.trim(),
            emergencyContactRelation: emergencyContactRelation.trim() || null,
            referenceName: referenceName.trim() || null,
            referencePhone: referencePhone.trim() || null,
            referenceRelation: referenceRelation.trim() || null,
            additionalNotes: additionalNotes.trim() || null,
          }),
        });
      }

      const data = await res.json().catch(() => ({}));

      if (!res.ok) {
        throw new Error(data?.message || data?.title || "Failed to submit application. Please try again.");
      }

      setMsg("Application submitted successfully.");
      window.location.href = "/applications";
    } catch (e: any) {
      setErr(e?.message ?? "Failed to submit application.");
      setMsg("");
    } finally {
      setBusy(false);
    }
  }

  const listingAddress = listing
    ? [listing.addressLine1, listing.city, listing.province].filter(Boolean).join(", ")
    : "";

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl flex flex-col gap-5">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">Apply for Lease</h1>
            <p className="mt-1 text-sm text-slate-500">
              Submit your lease application for the property below.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Link href="/applications">
              <Button variant="outline" className="gap-2 border-slate-200 text-slate-600">
                Applications <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
          </div>
        </div>

        {/* Listing summary */}
        {listing && (
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center gap-3 mb-4">
              <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-indigo-100">
                <FileText className="h-5 w-5 text-indigo-600" />
              </div>
              <div>
                <div className="text-sm font-semibold text-slate-800">
                  {listingAddress || "Property"}
                </div>
                <div className="text-xs text-slate-400">
                  {listing.propertyType}{listing.propertyType ? " · " : ""}{listing.city || ""}
                </div>
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div className="rounded-xl border border-slate-100 bg-emerald-50 p-3">
                <div className="flex items-center gap-1 text-xs text-slate-500"><DollarSign className="h-3 w-3" />Rent</div>
                <div className="mt-1 text-sm font-semibold text-emerald-700">
                  {listing.rentAmount != null ? `$${listing.rentAmount}/mo` : "N/A"}
                </div>
              </div>
              <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                <div className="flex items-center gap-1 text-xs text-slate-500"><BedDouble className="h-3 w-3" />Beds</div>
                <div className="mt-1 text-sm font-semibold text-slate-800">{listing.bedrooms ?? "N/A"}</div>
              </div>
              <div className="rounded-xl border border-slate-100 bg-slate-50 p-3">
                <div className="flex items-center gap-1 text-xs text-slate-500"><Bath className="h-3 w-3" />Baths</div>
                <div className="mt-1 text-sm font-semibold text-slate-800">{listing.bathrooms ?? "N/A"}</div>
              </div>
            </div>
          </div>
        )}

        {/* Personal Information */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100">
              <User className="h-4 w-4 text-indigo-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-800">Personal Information</h2>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Full Name *</label>
              <input value={fullName} onChange={(e) => setFullName(e.target.value)} className={inputClass} placeholder="Your full legal name" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Phone Number *</label>
              <input value={phone} onChange={(e) => setPhone(e.target.value)} className={inputClass} placeholder="(416) 555-1234" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Date of Birth</label>
              <input type="date" value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} className={inputClass} />
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Current Address *</label>
              <input value={currentAddress} onChange={(e) => setCurrentAddress(e.target.value)} className={inputClass} placeholder="Your current home address" />
            </div>
          </div>
        </div>

        {/* Employment & Income */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-amber-100">
              <Briefcase className="h-4 w-4 text-amber-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-800">Employment & Income</h2>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Employment Status *</label>
              <select value={employmentStatus} onChange={(e) => setEmploymentStatus(e.target.value)} className={inputClass}>
                <option value="">Select...</option>
                {EMPLOYMENT_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Annual Income ($) *</label>
              <input inputMode="numeric" value={annualIncome} onChange={(e) => setAnnualIncome(e.target.value.replace(/[^0-9]/g, ""))} className={inputClass} placeholder="e.g. 55000" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Employer Name</label>
              <input value={employerName} onChange={(e) => setEmployerName(e.target.value)} className={inputClass} placeholder="Company name" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Job Title</label>
              <input value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} className={inputClass} placeholder="Your position" />
            </div>
          </div>
        </div>

        {/* Household Details */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-100">
              <Users className="h-4 w-4 text-emerald-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-800">Household Details</h2>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Number of Occupants</label>
              <input inputMode="numeric" value={numberOfOccupants} onChange={(e) => setNumberOfOccupants(e.target.value.replace(/[^0-9]/g, ""))} className={inputClass} placeholder="1" />
            </div>
            <div className="flex items-end">
              <button
                type="button"
                onClick={() => setHasPets(!hasPets)}
                className={`flex items-center gap-2 rounded-xl border px-4 py-2.5 text-sm font-medium transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2 ${
                  hasPets
                    ? "border-indigo-600 bg-indigo-50 text-indigo-700"
                    : "border-slate-200 bg-white text-slate-600 hover:border-indigo-300"
                }`}
              >
                <PawPrint className="h-4 w-4" />
                {hasPets ? "Has Pets" : "No Pets"}
              </button>
            </div>
          </div>

          {hasPets && (
            <div className="mt-4">
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Pet Details (type, breed, size)</label>
              <input value={petDetails} onChange={(e) => setPetDetails(e.target.value)} className={inputClass} placeholder="e.g. 1 small dog (Poodle, 15 lbs)" />
            </div>
          )}
        </div>

        {/* Emergency Contact */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-red-100">
              <Phone className="h-4 w-4 text-red-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-800">Emergency Contact *</h2>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Name *</label>
              <input value={emergencyContactName} onChange={(e) => setEmergencyContactName(e.target.value)} className={inputClass} placeholder="Contact name" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Phone *</label>
              <input value={emergencyContactPhone} onChange={(e) => setEmergencyContactPhone(e.target.value)} className={inputClass} placeholder="(416) 555-0000" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Relationship</label>
              <input value={emergencyContactRelation} onChange={(e) => setEmergencyContactRelation(e.target.value)} className={inputClass} placeholder="e.g. Parent, Spouse" />
            </div>
          </div>
        </div>

        {/* Reference */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-slate-100">
              <Home className="h-4 w-4 text-slate-600" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-800">Reference (optional)</h2>
              <p className="text-xs text-slate-400">Previous landlord or personal reference</p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Name</label>
              <input value={referenceName} onChange={(e) => setReferenceName(e.target.value)} className={inputClass} placeholder="Reference name" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Phone</label>
              <input value={referencePhone} onChange={(e) => setReferencePhone(e.target.value)} className={inputClass} placeholder="(416) 555-0000" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Relationship</label>
              <input value={referenceRelation} onChange={(e) => setReferenceRelation(e.target.value)} className={inputClass} placeholder="e.g. Previous Landlord" />
            </div>
          </div>
        </div>

        {/* Lease Dates & Documents */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-sm font-bold text-slate-800 mb-4">Lease Details</h2>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Requested Start Date *</label>
              <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} className={inputClass} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Requested End Date *</label>
              <input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} className={inputClass} />
            </div>
          </div>

          {/* Document Upload */}
          <div className="mt-4">
            <label className="block text-xs font-semibold text-slate-500 mb-1.5">Supporting Documents (optional)</label>
            <div className="rounded-xl border border-dashed border-indigo-200 bg-indigo-50 p-5">
              <div className="flex items-center gap-2 mb-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100">
                  <ImagePlus className="h-4 w-4 text-indigo-600" />
                </div>
                <div>
                  <div className="text-sm font-semibold text-slate-800">Upload Documents</div>
                  <div className="text-xs text-slate-400">Pay stubs, references, ID, etc.</div>
                </div>
              </div>
              <input
                ref={docInputRef}
                type="file"
                accept="image/*,.pdf"
                multiple
                className="text-xs text-slate-500"
                onChange={(e) => {
                  const newFiles = e.target.files ? Array.from(e.target.files) : [];
                  if (newFiles.length > 0) {
                    setDocuments((prev) => [...prev, ...newFiles]);
                  }
                  if (docInputRef.current) docInputRef.current.value = "";
                }}
              />
              {documents.length > 0 && (
                <div className="mt-3 space-y-2">
                  <p className="text-xs text-emerald-600 font-medium">
                    {documents.length} file(s) selected
                  </p>
                  {documents.map((file, idx) => (
                    <div key={`${file.name}-${idx}`} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white p-2">
                      {file.type.startsWith("image/") ? (
                        <img
                          src={URL.createObjectURL(file)}
                          alt={file.name}
                          className="h-10 w-10 rounded object-cover"
                        />
                      ) : (
                        <div className="flex h-10 w-10 items-center justify-center rounded bg-slate-100">
                          <FileText className="h-5 w-5 text-slate-400" />
                        </div>
                      )}
                      <span className="flex-1 truncate text-xs text-slate-700">{file.name}</span>
                      <button
                        type="button"
                        onClick={() => setDocuments((prev) => prev.filter((_, i) => i !== idx))}
                        className="rounded-full p-1 text-red-400 hover:bg-red-50 hover:text-red-600 transition"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="mt-4">
            <label className="block text-xs font-semibold text-slate-500 mb-1.5">Additional Notes</label>
            <textarea
              value={additionalNotes}
              onChange={(e) => setAdditionalNotes(e.target.value)}
              rows={3}
              placeholder="Anything else the landlord or management should know..."
              className={`${inputClass} resize-none`}
            />
          </div>
        </div>

        {/* Errors / Messages */}
        {err && (
          <div className="flex gap-3 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
            <div>
              <div className="font-semibold">Unable to submit</div>
              <div className="mt-0.5 text-red-500">{err}</div>
            </div>
          </div>
        )}

        {msg && (
          <div className="rounded-2xl border border-indigo-100 bg-indigo-50 p-4 text-sm text-indigo-600">
            {msg}
          </div>
        )}

        <Button className="w-full gap-2 bg-indigo-600 hover:bg-indigo-700 text-white py-3 text-sm font-semibold" onClick={submit} disabled={busy}>
          {busy && <Loader2 className="h-4 w-4 animate-spin" />}
          Submit Application
        </Button>
      </div>
    </AppShell>
  );
}
