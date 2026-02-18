import { AppShell } from "@/components/shell/AppShell";

export default function DashboardPage() {
  return (
    <AppShell>
      <div className="rounded-2xl border border-white/10 bg-white/5 p-6">
        <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p className="mt-2 text-sm text-white/60">
          Welcome to Tenurix. Use the sidebar to manage applications and leases.
        </p>
      </div>
    </AppShell>
  );
}
