"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { LogOut, User } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/components/providers/I18nProvider";
import { apiFetch } from "@/lib/api";

type UserInfo = {
  fullName: string;
  photoBase64: string | null;
  photoContentType: string | null;
};

export function TopNav() {
  const { t } = useI18n();
  const [portal, setPortal] = useState<"client" | "landlord">("client");
  const [user, setUser] = useState<UserInfo | null>(null);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const p = (localStorage.getItem("tenurix_portal") as any) || "client";
    setPortal(p === "landlord" ? "landlord" : "client");

    const token = localStorage.getItem("tenurix_token");
    if (token) {
      apiFetch("/account/me")
        .then((res) => (res.ok ? res.json() : null))
        .then((data) => { if (data) setUser(data); })
        .catch(() => {});
    }
  }, []);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 4);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const initials = user?.fullName
    ? user.fullName.split(" ").map((n) => n[0]).join("").slice(0, 2).toUpperCase()
    : null;

  const homeHref = portal === "landlord" ? "/landlord/dashboard" : "/dashboard";
  const portalLabel = portal === "landlord" ? "Landlord Portal" : "Client Portal";

  return (
    <header
      className={`sticky top-0 z-50 bg-white/95 backdrop-blur-md border-b transition-all duration-300 ${scrolled ? "border-slate-200 shadow-lg shadow-black/5" : "border-slate-100"}`}
    >
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3">
        {/* Logo */}
        <Link href={homeHref} className="flex items-center gap-2.5 group">
          <Image src="/home-logo.svg" alt="Tenurix" width={36} height={36} className="transition-transform group-hover:scale-105" />
          <div>
            <div className="text-sm font-bold text-indigo-600 leading-tight">Tenurix</div>
            <div className="text-[10px] text-amber-500 leading-tight font-medium">{portalLabel}</div>
          </div>
        </Link>

        {/* Right side */}
        <div className="flex items-center gap-1">
          {/* Browse listings link */}
          <Link href="/listings">
            <Button variant="ghost" className="text-slate-500 hover:text-indigo-600 text-sm hidden sm:flex">
              Browse Listings
            </Button>
          </Link>

          {/* Profile */}
          <Link href="/profile">
            <Button variant="ghost" className="gap-2 text-slate-700 hover:text-indigo-600 hover:bg-indigo-50">
              {user?.photoBase64 ? (
                <img
                  src={`data:${user.photoContentType};base64,${user.photoBase64}`}
                  alt=""
                  className="h-7 w-7 rounded-full object-cover ring-2 ring-amber-300"
                />
              ) : initials ? (
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-indigo-100 text-[11px] font-bold text-indigo-600">
                  {initials}
                </span>
              ) : (
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100">
                  <User className="h-4 w-4 text-slate-500" />
                </span>
              )}
              <span className="hidden sm:inline text-sm font-medium">{user?.fullName || "Profile"}</span>
            </Button>
          </Link>

          {/* Sign out */}
          <Button
            variant="ghost"
            className="gap-2 text-slate-500 hover:text-red-500 hover:bg-red-50"
            onClick={() => {
              localStorage.removeItem("tenurix_token");
              localStorage.removeItem("tenurix_portal");
              window.location.href = "/auth";
            }}
          >
            <LogOut className="h-4 w-4" />
            <span className="hidden sm:inline text-sm">{t("signout")}</span>
          </Button>
        </div>
      </div>
    </header>
  );
}
