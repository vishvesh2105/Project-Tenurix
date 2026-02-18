import { Badge } from "@/components/ui/badge";

type Status = "Pending" | "Approved" | "Rejected" | "Assigned" | "In Review" | string;

export function StatusBadge({ status }: { status: Status }) {
  const s = (status ?? "").toLowerCase();

  if (s.includes("pending")) return <Badge className="bg-amber-500/15 text-amber-200 border-amber-400/20">Pending</Badge>;
  if (s.includes("approved")) return <Badge className="bg-emerald-500/15 text-emerald-200 border-emerald-400/20">Approved</Badge>;
  if (s.includes("rejected")) return <Badge className="bg-red-500/15 text-red-200 border-red-400/20">Rejected</Badge>;
  if (s.includes("assigned")) return <Badge className="bg-blue-500/15 text-blue-200 border-blue-400/20">Assigned</Badge>;
  if (s.includes("review")) return <Badge className="bg-violet-500/15 text-violet-200 border-violet-400/20">In Review</Badge>;

  return <Badge className="bg-white/10 text-white/80 border-white/10">{status}</Badge>;
}
