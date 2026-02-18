"use client";
import { useEffect } from "react";
import { useSearchParams } from "next/navigation";

export default function LoginRedirect() {
  const sp = useSearchParams();
  const next = sp.get("next");
  useEffect(() => {
    const url = next ? `/auth?role=client&next=${encodeURIComponent(next)}` : `/auth?role=client`;
    window.location.replace(url);
  }, [next]);
  return null;
}
