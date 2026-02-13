import React from "react";
import { HeaderControls } from "@/components/ui/HeaderControls";

export default function AuthShell({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}) {
  return (
    <main className="min-h-screen w-full px-4 py-10 auth-bg">
      <div className="mx-auto flex min-h-[calc(100vh-5rem)] max-w-md items-center justify-center">
        <div className="relative w-full rounded-2xl bg-white shadow-2xl">
          {/* top-right controls */}
          <div className="absolute right-4 top-4">
            <HeaderControls />
          </div>

          <div className="px-8 pt-10 text-center">
            <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-full bg-red-600">
              <svg viewBox="0 0 48 48" className="h-8 w-8" fill="none">
                <circle cx="24" cy="24" r="16" stroke="white" strokeWidth="2" opacity="0.9" />
                <path d="M12 22c8-8 16-8 24 0M14 30c6-6 14-6 20 0" stroke="white" strokeWidth="2" opacity="0.9" />
                <path d="M18 10l5 28M30 10l-5 28" stroke="white" strokeWidth="2" opacity="0.55" />
              </svg>
            </div>

            <h1 className="text-xl font-semibold text-slate-900">{title}</h1>
            {subtitle ? <p className="mt-2 text-sm text-slate-500">{subtitle}</p> : null}
          </div>

          <div className="px-8 pb-10 pt-6">{children}</div>
        </div>
      </div>
    </main>
  );
}
