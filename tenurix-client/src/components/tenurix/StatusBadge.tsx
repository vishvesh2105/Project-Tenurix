import { Badge } from "@/components/ui/badge";

type Status = string;

const BADGE_MAP: Record<string, string> = {
  // Green — positive outcomes
  approved: "bg-emerald-50 text-emerald-700 border-emerald-200",
  active: "bg-emerald-50 text-emerald-700 border-emerald-200",
  success: "bg-emerald-50 text-emerald-700 border-emerald-200",
  verified: "bg-emerald-50 text-emerald-700 border-emerald-200",
  completed: "bg-emerald-50 text-emerald-700 border-emerald-200",

  // Navy — in-progress
  inprogress: "bg-indigo-50 text-indigo-600 border-indigo-200",
  underverification: "bg-indigo-50 text-indigo-600 border-indigo-200",
  pendingreview: "bg-indigo-50 text-indigo-600 border-indigo-200",
  assigned: "bg-indigo-50 text-indigo-600 border-indigo-200",

  // Amber — waiting
  pending: "bg-amber-50 text-amber-700 border-amber-200",
  submitted: "bg-amber-50 text-amber-700 border-amber-200",
  open: "bg-amber-50 text-amber-700 border-amber-200",
  onhold: "bg-amber-50 text-amber-700 border-amber-200",

  // Violet — review / changes
  changesrequested: "bg-violet-50 text-violet-700 border-violet-200",
  review: "bg-violet-50 text-violet-700 border-violet-200",

  // Red — negative outcomes
  rejected: "bg-red-50 text-red-700 border-red-200",
  failed: "bg-red-50 text-red-700 border-red-200",
  cancelled: "bg-red-50 text-red-700 border-red-200",

  // Gray — ended / neutral
  ended: "bg-slate-100 text-slate-600 border-slate-200",
  inactive: "bg-slate-100 text-slate-600 border-slate-200",
  closed: "bg-slate-100 text-slate-600 border-slate-200",
};

const LABELS: Record<string, string> = {
  inprogress: "In Progress",
  underverification: "Under Verification",
  pendingreview: "Pending Review",
  changesrequested: "Changes Requested",
  onhold: "On Hold",
};

export function StatusBadge({ status }: { status: Status }) {
  const key = (status ?? "").toLowerCase().replace(/[^a-z]/g, "");
  const classes = BADGE_MAP[key] ?? "bg-slate-100 text-slate-600 border-slate-200";
  const label = LABELS[key] ?? status;

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ${classes}`}>
      {label}
    </span>
  );
}
