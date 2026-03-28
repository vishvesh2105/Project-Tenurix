"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, FileText, ScrollText, AlertCircle,
  CreditCard, Shield, Send, Building2, User, Bell,
} from "lucide-react";
import { useI18n } from "@/components/providers/I18nProvider";

type NavItem = {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
};

export function Sidebar() {
  const path = usePathname();
  const { t } = useI18n();
  const [portal, setPortal] = useState<"client" | "landlord">("client");

  useEffect(() => {
    const p = localStorage.getItem("tenurix_portal");
    if (p === "landlord") setPortal("landlord");
  }, []);

  const clientItems: NavItem[] = [
    { href: "/dashboard", label: t("dashboard"), icon: LayoutDashboard },
    { href: "/applications", label: t("applications"), icon: FileText },
    { href: "/leases", label: t("leases"), icon: ScrollText },
    { href: "/issues", label: t("issues"), icon: AlertCircle },
    { href: "/payments", label: t("payments"), icon: CreditCard },
    { href: "/notifications", label: "Notifications", icon: Bell },
    { href: "/profile", label: t("profile"), icon: User },
    { href: "/security", label: t("security"), icon: Shield },
  ];

  const landlordItems: NavItem[] = [
    { href: "/landlord/dashboard", label: t("dashboard"), icon: LayoutDashboard },
    { href: "/landlord/submissions/new", label: t("newSubmission"), icon: Send },
    { href: "/landlord/properties", label: t("properties"), icon: Building2 },
    { href: "/landlord/leases", label: t("leases"), icon: ScrollText },
    { href: "/landlord/issues", label: t("issues"), icon: AlertCircle },
    { href: "/notifications", label: "Notifications", icon: Bell },
    { href: "/profile", label: t("profile"), icon: User },
    { href: "/security", label: t("security"), icon: Shield },
  ];

  const items = portal === "landlord" ? landlordItems : clientItems;

  const tipText =
    portal === "landlord"
      ? "Submit your property details and track approval status through the management team."
      : "Applications move from Pending → Approved once management verifies documents.";

  return (
    <aside className="hidden w-60 shrink-0 md:block">
      {/* Nav items */}
      <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="p-2 space-y-0.5">
          {items.map((x) => {
            const active = path === x.href || path.startsWith(x.href + "/");
            const Icon = x.icon;
            return (
              <Link
                key={x.href}
                href={x.href}
                className={[
                  "flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all duration-200",
                  active
                    ? "bg-indigo-600 text-white shadow-sm"
                    : "text-slate-600 hover:bg-indigo-50 hover:text-indigo-600",
                ].join(" ")}
              >
                <Icon className={`h-4 w-4 shrink-0 ${active ? "text-white" : "text-slate-400"}`} />
                {x.label}
              </Link>
            );
          })}
        </div>
      </div>

      {/* Tip card */}
      <div className="mt-4 rounded-2xl border border-amber-200 bg-amber-50 p-4">
        <div className="text-xs font-bold text-indigo-600 mb-1.5 uppercase tracking-wide">Tip</div>
        <div className="text-xs text-slate-600 leading-relaxed">{tipText}</div>
      </div>
    </aside>
  );
}
