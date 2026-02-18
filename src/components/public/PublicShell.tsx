"use client";


import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Building2 } from "lucide-react";
import { HeaderControls } from "@/components/ui/HeaderControls";
import { useI18n } from "@/components/providers/I18nProvider";

export function PublicShell({ children }: { children: React.ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-50 border-b border-white/10 bg-black/40 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link href="/" className="flex items-center gap-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl border border-white/10 bg-white/5">
              <Building2 className="h-5 w-5" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-semibold tracking-tight">{t("tenurix")}</div>
              <div className="text-xs text-white/60">{t("findHome")}</div>
            </div>
          </Link>

          <div className="flex items-center gap-2">
            <HeaderControls />
            <Link href="/listings">
              <Button variant="ghost">{t("browse")}</Button>
            </Link>
            <Link href="/auth">
              <Button variant="secondary">{t("login")}</Button>
            </Link>
          </div>
        </div>
      </header>

      <main>{children}</main>
    </div>
  );
}
