"use client";

import { ReactNode, Suspense } from "react";
import { Sidebar } from "./Sidebar";
import { TopNav } from "./TopNav";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <TopNav />
      <div className="mx-auto flex w-full max-w-7xl gap-6 px-4 pb-12 pt-6">
        <Sidebar />
        <main id="main-content" className="min-w-0 flex-1">
          <ErrorBoundary>
            <Suspense fallback={<div className="animate-pulse p-8"><div className="h-8 w-48 rounded bg-slate-200" /></div>}>
              {children}
            </Suspense>
          </ErrorBoundary>
        </main>
      </div>
    </div>
  );
}
