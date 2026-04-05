"use client";
import { Suspense, useEffect } from "react";
import { useSearchParams } from "next/navigation";

export default function LandlordLoginRedirect() {
  return (
    <Suspense fallback={null}>
      <LandlordLoginRedirectInner />
    </Suspense>
  );
}

function LandlordLoginRedirectInner() {
  const sp = useSearchParams();
  const rawNext = sp.get("next");
  // Only allow relative URLs to prevent open redirect
  const next = rawNext && rawNext.startsWith("/") && !rawNext.startsWith("//") ? rawNext : null;
  useEffect(() => {
    const url = next ? `/auth?role=landlord&next=${encodeURIComponent(next)}` : `/auth?role=landlord`;
    window.location.replace(url);
  }, [next]);
  return null;
}
