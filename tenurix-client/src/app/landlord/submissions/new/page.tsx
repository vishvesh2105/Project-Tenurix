"use client";

import { useEffect, useState, useRef } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/useAuth";
import { apiFetch } from "@/lib/api";
import {
  Send, ImagePlus, IdCard, Home, Car, Zap, Dumbbell,
  Calendar, Building2, Layers, User, X,
} from "lucide-react";

const inputClass =
  "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 transition placeholder:text-slate-400";

const PROPERTY_TYPES = ["Apartment", "House", "Condo", "Townhouse", "Studio", "Other"];
const PROPERTY_SUBTYPES = ["Detached", "Semi-Detached", "Duplex", "Triplex", "Fourplex", "Penthouse", "Loft", "Basement"];
const LEASE_TERMS = ["Monthly", "6 Months", "1 Year", "2 Years"];
const PARKING_TYPES = ["Underground", "Surface", "Garage", "Street", "None"];

const UTILITIES = ["Hydro/Electricity", "Water", "Heating", "Internet/WiFi", "Gas", "Cable TV"];
const AMENITIES = [
  "Gym", "Pool", "Laundry In-Unit", "Laundry Shared", "Balcony", "Dishwasher",
  "AC", "Storage", "Elevator", "Concierge", "Pet Friendly", "EV Charging",
];

const MIN_OWNER_ID_PHOTOS = 1;
const MAX_OWNER_ID_PHOTOS = 5;
const MIN_PROPERTY_PHOTOS = 2;
const MAX_PROPERTY_PHOTOS = 20;

function PillToggle({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-full px-3.5 py-1.5 text-xs font-medium border transition-all ${
        active
          ? "bg-indigo-600 text-white border-indigo-600 shadow-sm"
          : "bg-white text-slate-600 border-slate-200 hover:border-indigo-300 hover:text-indigo-600"
      }`}
    >
      {label}
    </button>
  );
}

export default function NewSubmission() {
  useAuth();

  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");
  const [ok, setOk] = useState("");

  // Landlord personal details
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [landlordPhone, setLandlordPhone] = useState("");
  const [landlordEmail, setLandlordEmail] = useState("");
  const [profileLoaded, setProfileLoaded] = useState(false);
  const [profilePreFilled, setProfilePreFilled] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const res = await apiFetch("/account/me");
        if (!res.ok) return;
        const data = await res.json();
        if (data.fullName) {
          const parts = (data.fullName as string).trim().split(/\s+/);
          setFirstName(parts[0] || "");
          setLastName(parts.slice(1).join(" ") || "");
        }
        if (data.email) setLandlordEmail(data.email);
        if (data.phone) setLandlordPhone(data.phone);
        if (data.fullName?.trim() && data.phone?.trim() && data.email?.trim()) {
          setProfilePreFilled(true);
        }
      } catch {
        // ignore - user can fill manually
      } finally {
        setProfileLoaded(true);
      }
    })();

    // Check if landlord already has ID on file
    (async () => {
      try {
        const res = await apiFetch("/landlord/id-status");
        if (res.ok) {
          const data = await res.json();
          setHasIdOnFile(!!data.hasId);
          setIdStatus(data.status || "None");
        }
      } catch {
        // assume no ID on file
      } finally {
        setIdStatusLoaded(true);
      }
    })();
  }, []);

  // Address
  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [city, setCity] = useState("");
  const [province, setProvince] = useState("");
  const [postalCode, setPostalCode] = useState("");

  // Basic details
  const [propertyType, setPropertyType] = useState("");
  const [bedrooms, setBedrooms] = useState("");
  const [bathrooms, setBathrooms] = useState("");
  const [rentAmount, setRentAmount] = useState("");
  const [description, setDescription] = useState("");

  // Additional details
  const [propertySubType, setPropertySubType] = useState("");
  const [leaseTerm, setLeaseTerm] = useState("");
  const [availableDate, setAvailableDate] = useState("");
  const [yearBuilt, setYearBuilt] = useState("");
  const [numberOfFloors, setNumberOfFloors] = useState("");
  const [numberOfUnits, setNumberOfUnits] = useState("");
  const [parkingSpots, setParkingSpots] = useState("");
  const [parkingType, setParkingType] = useState("");
  const [isFurnished, setIsFurnished] = useState(false);
  const [isShortTerm, setIsShortTerm] = useState(false);

  // Utilities & Amenities
  const [selectedUtilities, setSelectedUtilities] = useState<string[]>([]);
  const [selectedAmenities, setSelectedAmenities] = useState<string[]>([]);

  // ID status
  const [hasIdOnFile, setHasIdOnFile] = useState(false);
  const [idStatus, setIdStatus] = useState("None"); // None | Pending | Verified | Rejected
  const [idStatusLoaded, setIdStatusLoaded] = useState(false);

  // Photos
  const [ownerIdPhotos, setOwnerIdPhotos] = useState<File[]>([]);
  const [propertyPhotos, setPropertyPhotos] = useState<File[]>([]);
  const idInputRef = useRef<HTMLInputElement>(null);
  const photoInputRef = useRef<HTMLInputElement>(null);

  function toggleItem(list: string[], setList: (v: string[]) => void, item: string) {
    setList(list.includes(item) ? list.filter((i) => i !== item) : [...list, item]);
  }

  async function submit() {
    setErr("");
    setOk("");

    if (!firstName.trim()) return setErr("Please enter your first name.");
    if (!lastName.trim()) return setErr("Please enter your last name.");
    if (!landlordPhone.trim()) return setErr("Please enter your phone number.");
    if (!landlordEmail.trim()) return setErr("Please enter your email address.");
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(landlordEmail.trim())) return setErr("Please enter a valid email address.");
    const phoneRegex = /^[\d\s()+-]{7,20}$/;
    if (!phoneRegex.test(landlordPhone.trim())) return setErr("Please enter a valid phone number.");
    if (!addressLine1.trim()) return setErr("Address is required.");
    if (!city.trim()) return setErr("City is required.");
    if (!province.trim()) return setErr("Province is required.");
    if (!postalCode.trim()) return setErr("Postal code is required.");
    if (!propertyType.trim()) return setErr("Property type is required.");
    if (!rentAmount || Number(rentAmount) <= 0) return setErr("Rent amount must be > 0.");

    // Only require ID photos if landlord doesn't have ID on file
    if (!hasIdOnFile) {
      if (ownerIdPhotos.length < MIN_OWNER_ID_PHOTOS) {
        return setErr(`Please upload at least ${MIN_OWNER_ID_PHOTOS} ID photo. This is a one-time requirement.`);
      }
      if (ownerIdPhotos.length > MAX_OWNER_ID_PHOTOS) {
        return setErr(`You can upload up to ${MAX_OWNER_ID_PHOTOS} ID photos.`);
      }
    }

    if (propertyPhotos.length < MIN_PROPERTY_PHOTOS) {
      return setErr(`Please upload at least ${MIN_PROPERTY_PHOTOS} property photos.`);
    }
    if (propertyPhotos.length > MAX_PROPERTY_PHOTOS) {
      return setErr(`You can upload up to ${MAX_PROPERTY_PHOTOS} property photos.`);
    }

    // Update profile if not already pre-filled
    if (!profilePreFilled) {
      try {
        await apiFetch("/account/me", {
          method: "PUT",
          body: JSON.stringify({
            fullName: `${firstName.trim()} ${lastName.trim()}`,
            phone: landlordPhone.trim(),
          }),
        });
      } catch {
        // non-blocking — continue with submission
      }
    }

    const fd = new FormData();
    fd.append("AddressLine1", addressLine1);
    if (addressLine2.trim()) fd.append("AddressLine2", addressLine2);
    fd.append("City", city);
    fd.append("Province", province);
    fd.append("PostalCode", postalCode);
    fd.append("PropertyType", propertyType);
    if (bedrooms) fd.append("Bedrooms", bedrooms);
    if (bathrooms) fd.append("Bathrooms", bathrooms);
    fd.append("RentAmount", rentAmount);
    if (description.trim()) fd.append("Description", description);

    // Additional details
    if (propertySubType) fd.append("PropertySubType", propertySubType);
    if (leaseTerm) fd.append("LeaseTerm", leaseTerm);
    if (availableDate) fd.append("AvailableDate", availableDate);
    if (yearBuilt) fd.append("YearBuilt", yearBuilt);
    if (numberOfFloors) fd.append("NumberOfFloors", numberOfFloors);
    if (numberOfUnits) fd.append("NumberOfUnits", numberOfUnits);
    if (parkingSpots) fd.append("ParkingSpots", parkingSpots);
    if (parkingType) fd.append("ParkingType", parkingType);
    fd.append("IsShortTerm", String(isShortTerm));
    fd.append("IsFurnished", String(isFurnished));

    // JSON arrays
    if (selectedUtilities.length > 0) fd.append("UtilitiesJson", JSON.stringify(selectedUtilities));

    if (ownerIdPhotos.length > 0) {
      ownerIdPhotos.forEach((f) => fd.append("OwnerIdPhotos", f));
    }
    propertyPhotos.forEach((f) => fd.append("PropertyPhotos", f));

    setBusy(true);
    try {
      const res = await apiFetch("/landlord/submissions", {
        method: "POST",
        body: fd,
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data?.message || data?.title || "Failed to submit property. Please try again.");

      setOk(`Submitted! Property #${data.propertyId} is pending management review.`);
      setTimeout(() => (window.location.href = "/landlord/dashboard"), 1500);
    } catch (e: any) {
      setErr(e?.message ?? "Failed to submit");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl">
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-slate-900">Submit a Property</h1>
          <p className="mt-1 text-sm text-slate-500">
            Fill in your property details and upload required documents for management review.
          </p>
        </div>

        {err && (
          <div className="mb-5 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            {err}
          </div>
        )}

        {ok && (
          <div className="mb-5 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
            {ok}
          </div>
        )}

        {/* Landlord Personal Details */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100">
              <User className="h-4 w-4 text-indigo-600" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-800">Personal Details</h2>
              {profilePreFilled && (
                <p className="text-xs text-emerald-600">Auto-filled from your profile</p>
              )}
            </div>
          </div>

          {!profileLoaded ? (
            <div className="animate-pulse space-y-3">
              <div className="h-10 rounded-xl bg-slate-100" />
              <div className="h-10 rounded-xl bg-slate-100" />
            </div>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">First Name *</label>
                <input
                  className={inputClass}
                  value={firstName}
                  onChange={(e) => setFirstName(e.target.value)}
                  placeholder="First name"
                  readOnly={profilePreFilled}
                  style={profilePreFilled ? { backgroundColor: "#f8fafc" } : undefined}
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Last Name *</label>
                <input
                  className={inputClass}
                  value={lastName}
                  onChange={(e) => setLastName(e.target.value)}
                  placeholder="Last name"
                  readOnly={profilePreFilled}
                  style={profilePreFilled ? { backgroundColor: "#f8fafc" } : undefined}
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Phone Number *</label>
                <input
                  className={inputClass}
                  value={landlordPhone}
                  onChange={(e) => setLandlordPhone(e.target.value)}
                  placeholder="e.g. (416) 555-1234"
                  readOnly={profilePreFilled}
                  style={profilePreFilled ? { backgroundColor: "#f8fafc" } : undefined}
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-500 mb-1.5">Email Address *</label>
                <input
                  type="email"
                  className={inputClass}
                  value={landlordEmail}
                  onChange={(e) => setLandlordEmail(e.target.value)}
                  placeholder="your@email.com"
                  readOnly={profilePreFilled}
                  style={profilePreFilled ? { backgroundColor: "#f8fafc" } : undefined}
                />
              </div>
            </div>
          )}
        </div>

        {/* Property Address */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <h2 className="text-sm font-bold text-slate-800 mb-4">Property Address</h2>
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Address Line 1 *</label>
              <input className={inputClass} value={addressLine1} onChange={(e) => setAddressLine1(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Address Line 2</label>
              <input className={inputClass} value={addressLine2} onChange={(e) => setAddressLine2(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">City *</label>
              <input className={inputClass} value={city} onChange={(e) => setCity(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Province *</label>
              <input className={inputClass} value={province} onChange={(e) => setProvince(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Postal Code *</label>
              <input className={inputClass} value={postalCode} onChange={(e) => setPostalCode(e.target.value)} />
            </div>
          </div>
        </div>

        {/* Property Details */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <h2 className="text-sm font-bold text-slate-800 mb-4">Property Details</h2>
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Property Type *</label>
              <select className={inputClass} value={propertyType} onChange={(e) => setPropertyType(e.target.value)}>
                <option value="">Select type...</option>
                {PROPERTY_TYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Monthly Rent ($) *</label>
              <input className={inputClass} inputMode="numeric" value={rentAmount} onChange={(e) => setRentAmount(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Bedrooms</label>
              <input className={inputClass} inputMode="numeric" value={bedrooms} onChange={(e) => setBedrooms(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Bathrooms</label>
              <input className={inputClass} inputMode="numeric" value={bathrooms} onChange={(e) => setBathrooms(e.target.value)} />
            </div>
          </div>

          <div className="mt-4">
            <label className="block text-xs font-semibold text-slate-500 mb-1.5">Description</label>
            <textarea
              className={`${inputClass} resize-none`}
              rows={4}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
        </div>

        {/* Additional Details */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100">
              <Home className="h-4 w-4 text-indigo-600" />
            </div>
            <h2 className="text-sm font-bold text-slate-800">Additional Details</h2>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Property Sub-Type</label>
              <select className={inputClass} value={propertySubType} onChange={(e) => setPropertySubType(e.target.value)}>
                <option value="">Select...</option>
                {PROPERTY_SUBTYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Lease Term</label>
              <select className={inputClass} value={leaseTerm} onChange={(e) => setLeaseTerm(e.target.value)}>
                <option value="">Select...</option>
                {LEASE_TERMS.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Available Date</label>
              <input type="date" className={inputClass} value={availableDate} onChange={(e) => setAvailableDate(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Year Built</label>
              <input className={inputClass} inputMode="numeric" placeholder="e.g. 2020" value={yearBuilt} onChange={(e) => setYearBuilt(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Number of Floors</label>
              <input className={inputClass} inputMode="numeric" value={numberOfFloors} onChange={(e) => setNumberOfFloors(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Number of Units</label>
              <input className={inputClass} inputMode="numeric" value={numberOfUnits} onChange={(e) => setNumberOfUnits(e.target.value)} />
            </div>
          </div>

          {/* Parking */}
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">
                <span className="inline-flex items-center gap-1"><Car className="h-3.5 w-3.5" /> Parking Spots</span>
              </label>
              <input className={inputClass} inputMode="numeric" placeholder="0" value={parkingSpots} onChange={(e) => setParkingSpots(e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 mb-1.5">Parking Type</label>
              <select className={inputClass} value={parkingType} onChange={(e) => setParkingType(e.target.value)}>
                <option value="">Select...</option>
                {PARKING_TYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Toggles */}
          <div className="mt-4 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={() => setIsFurnished(!isFurnished)}
              className={`flex items-center gap-2 rounded-xl border px-4 py-2.5 text-sm font-medium transition-all ${
                isFurnished
                  ? "border-indigo-600 bg-indigo-50 text-indigo-700"
                  : "border-slate-200 bg-white text-slate-600 hover:border-indigo-300"
              }`}
            >
              <div className={`h-4 w-4 rounded border-2 flex items-center justify-center ${isFurnished ? "border-indigo-600 bg-indigo-600" : "border-slate-300"}`}>
                {isFurnished && <span className="text-white text-[10px]">✓</span>}
              </div>
              Furnished
            </button>
            <button
              type="button"
              onClick={() => setIsShortTerm(!isShortTerm)}
              className={`flex items-center gap-2 rounded-xl border px-4 py-2.5 text-sm font-medium transition-all ${
                isShortTerm
                  ? "border-indigo-600 bg-indigo-50 text-indigo-700"
                  : "border-slate-200 bg-white text-slate-600 hover:border-indigo-300"
              }`}
            >
              <div className={`h-4 w-4 rounded border-2 flex items-center justify-center ${isShortTerm ? "border-indigo-600 bg-indigo-600" : "border-slate-300"}`}>
                {isShortTerm && <span className="text-white text-[10px]">✓</span>}
              </div>
              Short-Term Rental
            </button>
          </div>
        </div>

        {/* Utilities */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-amber-100">
              <Zap className="h-4 w-4 text-amber-600" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-800">Utilities Included</h2>
              <p className="text-xs text-slate-400">Select utilities included in rent</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {UTILITIES.map((u) => (
              <PillToggle
                key={u}
                label={u}
                active={selectedUtilities.includes(u)}
                onClick={() => toggleItem(selectedUtilities, setSelectedUtilities, u)}
              />
            ))}
          </div>
        </div>

        {/* Amenities */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-4">
          <div className="flex items-center gap-2 mb-4">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-100">
              <Dumbbell className="h-4 w-4 text-emerald-600" />
            </div>
            <div>
              <h2 className="text-sm font-bold text-slate-800">Amenities</h2>
              <p className="text-xs text-slate-400">Select available amenities</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {AMENITIES.map((a) => (
              <PillToggle
                key={a}
                label={a}
                active={selectedAmenities.includes(a)}
                onClick={() => toggleItem(selectedAmenities, setSelectedAmenities, a)}
              />
            ))}
          </div>
        </div>

        {/* Required Documents */}
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm mb-6">
          <h2 className="text-sm font-bold text-slate-800 mb-4">Required Documents</h2>

          <div className={`grid gap-4 ${!hasIdOnFile ? "md:grid-cols-2" : ""}`}>
            {/* ID Upload — only show if landlord has NO ID on file */}
            {!hasIdOnFile && (
              <div className="rounded-xl border border-dashed border-indigo-200 bg-indigo-50 p-5">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100">
                    <IdCard className="h-4 w-4 text-indigo-600" />
                  </div>
                  <div>
                    <div className="text-sm font-semibold text-slate-800">Canada ID Photos *</div>
                    <div className="text-xs text-slate-400">One-time upload · 1 to 5 images</div>
                  </div>
                </div>

                <input
                  ref={idInputRef}
                  type="file"
                  accept="image/*"
                  multiple
                  className="text-xs text-slate-500"
                  onChange={(e) => {
                    const newFiles = e.target.files ? Array.from(e.target.files) : [];
                    if (newFiles.length > 0) {
                      setOwnerIdPhotos((prev) => [...prev, ...newFiles]);
                    }
                    if (idInputRef.current) idInputRef.current.value = "";
                  }}
                />

                {ownerIdPhotos.length > 0 && (
                  <div className="mt-3 space-y-2">
                    <p className="text-xs text-emerald-600 font-medium">
                      {ownerIdPhotos.length} ID photo(s) selected
                    </p>
                    {ownerIdPhotos.map((file, idx) => (
                      <div key={`${file.name}-${idx}`} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white p-2">
                        <img
                          src={URL.createObjectURL(file)}
                          alt={file.name}
                          className="h-10 w-10 rounded object-cover"
                        />
                        <span className="flex-1 truncate text-xs text-slate-700">{file.name}</span>
                        <button
                          type="button"
                          onClick={() => setOwnerIdPhotos((prev) => prev.filter((_, i) => i !== idx))}
                          className="rounded-full p-1 text-red-400 hover:bg-red-50 hover:text-red-600 transition"
                        >
                          <X className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {/* Show confirmation if ID already on file */}
            {hasIdOnFile && (
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 mb-4 flex items-center gap-3">
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-emerald-100">
                  <IdCard className="h-4 w-4 text-emerald-600" />
                </div>
                <div>
                  <div className="text-sm font-semibold text-emerald-800">ID on File</div>
                  <div className="text-xs text-emerald-600">
                    Your identification is already on file. No need to re-upload.
                  </div>
                </div>
              </div>
            )}

            <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-5">
              <div className="flex items-center gap-2 mb-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-slate-100">
                  <ImagePlus className="h-4 w-4 text-slate-500" />
                </div>
                <div>
                  <div className="text-sm font-semibold text-slate-800">Property Photos *</div>
                  <div className="text-xs text-slate-400">Upload 2 to 20 images</div>
                </div>
              </div>

              <input
                ref={photoInputRef}
                type="file"
                accept="image/*"
                multiple
                className="text-xs text-slate-500"
                onChange={(e) => {
                  const newFiles = e.target.files ? Array.from(e.target.files) : [];
                  if (newFiles.length > 0) {
                    setPropertyPhotos((prev) => [...prev, ...newFiles]);
                  }
                  if (photoInputRef.current) photoInputRef.current.value = "";
                }}
              />

              {propertyPhotos.length > 0 && (
                <div className="mt-3 space-y-2">
                  <p className="text-xs text-emerald-600 font-medium">
                    {propertyPhotos.length} property photo(s) selected
                  </p>
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                    {propertyPhotos.map((file, idx) => (
                      <div key={`${file.name}-${idx}`} className="relative group rounded-lg border border-slate-200 bg-white overflow-hidden">
                        <img
                          src={URL.createObjectURL(file)}
                          alt={file.name}
                          className="h-24 w-full object-cover"
                        />
                        <div className="px-2 py-1">
                          <span className="block truncate text-[10px] text-slate-500">{file.name}</span>
                        </div>
                        <button
                          type="button"
                          onClick={() => setPropertyPhotos((prev) => prev.filter((_, i) => i !== idx))}
                          className="absolute top-1 right-1 rounded-full bg-white/90 p-1 text-red-400 hover:bg-red-50 hover:text-red-600 shadow-sm transition opacity-0 group-hover:opacity-100"
                        >
                          <X className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        <Button onClick={submit} disabled={busy} className="w-full gap-2 bg-indigo-600 hover:bg-indigo-700 text-white py-3 text-sm font-semibold">
          <Send className="h-4 w-4" />
          {busy ? "Submitting..." : "Submit to Management"}
        </Button>
      </div>
    </AppShell>
  );
}
