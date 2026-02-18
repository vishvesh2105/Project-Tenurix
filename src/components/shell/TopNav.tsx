"use client";

import Link from "next/link";
import { Building2, LogOut, User } from "lucide-react";
import { Button } from "@/components/ui/button";
import { HeaderControls } from "@/components/ui/HeaderControls";
import { useI18n } from "@/components/providers/I18nProvider";
import { useEffect, useState } from "react";

export function TopNav() {
  const { t } = useI18n();
  const [portal, setPortal] = useState<"client" | "landlord">("client");

  useEffect(() => {
    const p = (localStorage.getItem("tenurix_portal") as any) || "client";
    setPortal(p === "landlord" ? "landlord" : "client");
  }, []);

  return (
    <header className="sticky top-0 z-50 border-b border-white/10 bg-black/40 backdrop-blur">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <Link href="/dashboard" className="flex items-center gap-2">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 bg-white/5">
            <Building2 className="h-5 w-5" />
          </div>
          <div className="leading-tight">
            <div className="text-sm font-semibold tracking-tight">{t("tenurix")}</div>
            <div className="text-xs text-white/60">
              {portal === "landlord" ? t("landlordPortal") : t("clientPortal")}
            </div>
          </div>
        </Link>

        <div className="flex items-center gap-2">
          <HeaderControls />

          <Link href="/profile">
            <Button variant="ghost" className="gap-2">
              <User className="h-4 w-4" />
              {t("profile")}
            </Button>
          </Link>

          <Button
            variant="ghost"
            className="gap-2 text-white/80 hover:text-white"
            onClick={() => {
              localStorage.removeItem("tenurix_token");
              localStorage.removeItem("tenurix_portal");
              window.location.href = "/auth";
            }}
          >
            <LogOut className="h-4 w-4" />
            {t("signout")}
          </Button>
        </div>
      </div>
    </header>
  );
}
