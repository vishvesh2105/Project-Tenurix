"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutDashboard, FileText, CreditCard, Shield } from "lucide-react";
import { useI18n } from "@/components/providers/I18nProvider";

export function Sidebar() {
  const path = usePathname();
  const { t } = useI18n();

  const items = [
    { href: "/dashboard", label: t("dashboard"), icon: LayoutDashboard },
    { href: "/applications", label: t("applications"), icon: FileText },
    { href: "/payments", label: "Payments", icon: CreditCard },
    { href: "/security", label: "Security", icon: Shield },
  ];

  return (
    <aside className="hidden w-64 shrink-0 md:block">
      <div className="rounded-2xl border border-white/10 bg-white/5 p-2">
        {items.map((x) => {
          const active = path === x.href;
          const Icon = x.icon;
          return (
            <Link
              key={x.href}
              href={x.href}
              className={[
                "flex items-center gap-3 rounded-xl px-3 py-2 text-sm transition",
                active
                  ? "bg-white/10 text-white"
                  : "text-white/70 hover:bg-white/5 hover:text-white",
              ].join(" ")}
            >
              <Icon className="h-4 w-4" />
              {x.label}
            </Link>
          );
        })}
      </div>

      <div className="mt-4 rounded-2xl border border-white/10 bg-black/30 p-4">
        <div className="text-sm font-semibold">Tip</div>
        <div className="mt-1 text-xs text-white/60">
          Applications move from <b>Pending</b> → <b>Approved</b> once management verifies documents.
        </div>
      </div>
    </aside>
  );
}
