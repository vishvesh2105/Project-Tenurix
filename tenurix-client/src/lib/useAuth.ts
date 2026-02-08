"use client";

import { useEffect, useMemo, useState } from "react";
import { usePathname } from "next/navigation";

export function useAuth() {
  const pathname = usePathname();
  const [isReady, setIsReady] = useState(false);

  const token = useMemo(() => {
    if (typeof window === "undefined") return "";
    return localStorage.getItem("tenurix_token") || "";
  }, []);

  const portal = useMemo(() => {
    if (typeof window === "undefined") return "client";
    return (localStorage.getItem("tenurix_portal") || "client") as "client" | "landlord";
  }, []);

  useEffect(() => {
    if (!token) {
      const role = portal === "landlord" ? "landlord" : "client";
      window.location.href = `/auth?role=${role}&next=${encodeURIComponent(pathname)}`;
    } else {
      setIsReady(true);
    }
  }, [token, portal, pathname]);

  return { token, portal, isReady };
}
