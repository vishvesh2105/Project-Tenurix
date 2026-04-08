"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

type MapListing = {
  listingId: number;
  propertyId?: number;
  addressLine1: string;
  city: string;
  province: string;
  propertyType: string;
  bedrooms: number | null;
  bathrooms: number | null;
  rentAmount: number | null;
  latitude: number | null;
  longitude: number | null;
  mediaUrl: string | null;
};

type Props = {
  listings: MapListing[];
  apiBase: string;
};

// Custom marker icon using SVG data URI (no external image files needed)
function createIcon(color: string = "#4f46e5") {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 28 40">
    <path d="M14 0C6.268 0 0 6.268 0 14c0 10.5 14 26 14 26s14-15.5 14-26C28 6.268 21.732 0 14 0z" fill="${color}"/>
    <circle cx="14" cy="14" r="7" fill="white"/>
    <circle cx="14" cy="14" r="4" fill="${color}"/>
  </svg>`;
  return L.divIcon({
    html: svg,
    className: "custom-map-marker",
    iconSize: [28, 40],
    iconAnchor: [14, 40],
    popupAnchor: [0, -36],
  });
}

// Save geocoded coordinates back to the database so we don't have to geocode again
async function saveCoordinatesToDb(apiBase: string, entries: { propertyId: number; latitude: number; longitude: number }[]) {
  if (entries.length === 0) return;
  try {
    await fetch(`${apiBase}/public/listings/geocode`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(entries),
    });
  } catch (err) {
    console.warn("Failed to save coordinates:", err);
  }
}

export default function ListingsMap({ listings, apiBase }: Props) {
  const mapRef = useRef<HTMLDivElement>(null);
  const leafletMap = useRef<L.Map | null>(null);
  const router = useRouter();
  const [geocoded, setGeocoded] = useState<Map<number, [number, number]>>(new Map());
  const geocodeCache = useRef<Map<string, [number, number] | null>>(new Map());

  // Geocode addresses that don't have lat/lng — progressively update state
  useEffect(() => {
    setGeocoded(prev => {
      const validIds = new Set(listings.map(l => l.listingId));
      const next = new Map<number, [number, number]>();
      prev.forEach((v, k) => { if (validIds.has(k)) next.set(k, v); });
      return next.size !== prev.size ? next : prev;
    });

    const toGeocode = listings.filter(
      (l) => l.latitude == null && l.longitude == null && !geocoded.has(l.listingId)
    );

    if (toGeocode.length === 0) return;

    let cancelled = false;

    // First pass: resolve from cache instantly
    const fromCache = new Map(geocoded);
    const needApi: typeof toGeocode = [];
    for (const listing of toGeocode) {
      const address = `${listing.addressLine1}, ${listing.city}, ${listing.province}, Canada`;
      const cacheKey = address.toLowerCase();
      if (geocodeCache.current.has(cacheKey)) {
        const cached = geocodeCache.current.get(cacheKey);
        if (cached) fromCache.set(listing.listingId, cached);
      } else {
        needApi.push(listing);
      }
    }
    if (fromCache.size > geocoded.size) setGeocoded(new Map(fromCache));

    if (needApi.length === 0) return;

    // Second pass: geocode via API — show pin + save to DB immediately per result
    async function geocodeRemaining() {
      const batch = new Map(fromCache);

      for (const listing of needApi) {
        if (cancelled) break;

        const address = `${listing.addressLine1}, ${listing.city}, ${listing.province}, Canada`;
        const cacheKey = address.toLowerCase();

        try {
          const res = await fetch(
            `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(address)}&limit=1`,
            { headers: { "User-Agent": "Tenurix/1.0" } }
          );
          if (res.status === 429) {
            await new Promise((r) => setTimeout(r, 5000));
            continue;
          }
          const data = await res.json();

          if (data && data.length > 0) {
            const coords: [number, number] = [parseFloat(data[0].lat), parseFloat(data[0].lon)];
            geocodeCache.current.set(cacheKey, coords);
            batch.set(listing.listingId, coords);

            // Show pin on map immediately
            if (!cancelled) setGeocoded(new Map(batch));

            // Save to database immediately (fire and forget)
            if (listing.propertyId) {
              saveCoordinatesToDb(apiBase, [{ propertyId: listing.propertyId, latitude: coords[0], longitude: coords[1] }]);
            }
          } else {
            geocodeCache.current.set(cacheKey, null);
          }

          // Rate limit: 1 request per second (Nominatim policy)
          await new Promise((r) => setTimeout(r, 1100));
        } catch {
          geocodeCache.current.set(cacheKey, null);
        }
      }
    }

    geocodeRemaining();
    return () => { cancelled = true; };
  }, [listings]); // eslint-disable-line react-hooks/exhaustive-deps

  // Initialize & update map
  useEffect(() => {
    if (!mapRef.current) return;

    // Initialize map once
    if (!leafletMap.current) {
      leafletMap.current = L.map(mapRef.current, {
        center: [43.65, -79.38], // Default: Toronto
        zoom: 10,
        zoomControl: true,
        scrollWheelZoom: true,
      });

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
        maxZoom: 19,
      }).addTo(leafletMap.current);
    }

    const map = leafletMap.current;

    // Clear existing markers
    map.eachLayer((layer) => {
      if (layer instanceof L.Marker) map.removeLayer(layer);
    });

    // Add markers
    const bounds: L.LatLngExpression[] = [];
    const icon = createIcon("#4f46e5");

    listings.forEach((listing) => {
      let lat = listing.latitude;
      let lng = listing.longitude;

      // Use geocoded coords if no stored coords
      if ((lat == null || lng == null) && geocoded.has(listing.listingId)) {
        const coords = geocoded.get(listing.listingId)!;
        lat = coords[0];
        lng = coords[1];
      }

      if (lat == null || lng == null) return;

      const position: L.LatLngExpression = [lat, lng];
      bounds.push(position);

      const imgSrc = listing.mediaUrl
        ? listing.mediaUrl.startsWith("http") ? listing.mediaUrl : `${apiBase}${listing.mediaUrl}`
        : null;

      const popupHtml = `
        <div style="min-width:220px;font-family:system-ui,sans-serif;">
          ${imgSrc ? `<img src="${imgSrc}" alt="Property at ${listing.addressLine1}" style="width:100%;height:120px;object-fit:cover;border-radius:8px 8px 0 0;margin:-14px -14px 10px -14px;width:calc(100% + 28px);" onerror="this.style.display='none'" />` : ""}
          <div style="font-weight:700;font-size:13px;color:#1e293b;margin-bottom:4px;">${listing.addressLine1}</div>
          <div style="font-size:11px;color:#64748b;margin-bottom:8px;">${listing.city}${listing.province ? `, ${listing.province}` : ""}</div>
          <div style="display:flex;gap:8px;margin-bottom:8px;">
            ${listing.rentAmount != null ? `<span style="background:#eef2ff;color:#4f46e5;padding:3px 8px;border-radius:6px;font-size:11px;font-weight:700;">$${listing.rentAmount.toLocaleString()}/mo</span>` : ""}
            ${listing.bedrooms != null ? `<span style="background:#f8fafc;color:#475569;padding:3px 8px;border-radius:6px;font-size:11px;font-weight:600;">${listing.bedrooms} bed</span>` : ""}
            ${listing.bathrooms != null ? `<span style="background:#f8fafc;color:#475569;padding:3px 8px;border-radius:6px;font-size:11px;font-weight:600;">${listing.bathrooms} bath</span>` : ""}
          </div>
          <a href="/listings/${listing.listingId}" style="display:block;text-align:center;background:#4f46e5;color:white;padding:6px 12px;border-radius:8px;font-size:12px;font-weight:600;text-decoration:none;">View Details</a>
        </div>
      `;

      L.marker(position, { icon })
        .addTo(map)
        .bindPopup(popupHtml, { maxWidth: 280, className: "tenurix-popup" });
    });

    // Fit map to markers
    if (bounds.length > 0) {
      map.fitBounds(L.latLngBounds(bounds), { padding: [40, 40], maxZoom: 14 });
    }

    return () => {};
  }, [listings, geocoded, apiBase, router]);

  // Cleanup map on unmount
  useEffect(() => {
    return () => {
      if (leafletMap.current) {
        leafletMap.current.remove();
        leafletMap.current = null;
      }
    };
  }, []);

  return (
    <>
      <style>{`
        .custom-map-marker { background: none !important; border: none !important; }
        .tenurix-popup .leaflet-popup-content-wrapper { border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.12); padding: 0; overflow: hidden; }
        .tenurix-popup .leaflet-popup-content { margin: 14px; }
        .tenurix-popup .leaflet-popup-tip { box-shadow: 0 4px 8px rgba(0,0,0,0.08); }
      `}</style>
      <div ref={mapRef} className="w-full h-full rounded-2xl" style={{ minHeight: 500 }} />
    </>
  );
}
