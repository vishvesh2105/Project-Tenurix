"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { useAuth } from "@/lib/useAuth";
import { apiFetch } from "@/lib/api";
import { useToast } from "@/components/ui/Toast";
import { Bell, BellOff, Check, CheckCheck, AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";

type Notification = {
  notificationId: number;
  type: string;
  title: string;
  message: string;
  linkUrl: string | null;
  isRead: boolean;
  createdAt: string;
};

export default function NotificationsPage() {
  const router = useRouter();
  const { isReady } = useAuth();
  const { toast } = useToast();
  const [items, setItems] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [actionError, setActionError] = useState("");

  const fetchPage = async (p: number) => {
    try {
      const res = await apiFetch(`/notifications?page=${p}&pageSize=20`);
      if (res.ok) {
        const data: Notification[] = await res.json();
        if (p === 1) {
          setItems(data);
        } else {
          setItems((prev) => [...prev, ...data]);
        }
        // Only show load more if we got a full page (could be more)
        // If we got less than 20, we know there are no more
        setHasMore(data.length >= 20);
      } else {
        setActionError("Failed to load notifications. Please try again.");
      }
    } catch {
      setActionError("Failed to load notifications. Please try again.");
    }
    setLoading(false);
  };

  useEffect(() => {
    if (isReady) fetchPage(1);
  }, [isReady]);

  const markRead = async (n: Notification) => {
    setActionError("");
    if (!n.isRead) {
      try {
        const res = await apiFetch(`/notifications/${n.notificationId}/read`, { method: "POST" });
        if (!res.ok) throw new Error();
        setItems((prev) => prev.map((x) => x.notificationId === n.notificationId ? { ...x, isRead: true } : x));
      } catch {
        setActionError("Failed to mark notification as read. Please try again.");
        return;
      }
    }
    if (n.linkUrl) router.push(n.linkUrl);
  };

  const markAllRead = async () => {
    setActionError("");
    try {
      const res = await apiFetch("/notifications/read-all", { method: "POST" });
      if (!res.ok) throw new Error();
      setItems((prev) => prev.map((x) => ({ ...x, isRead: true })));
      toast("All notifications marked as read", "success");
    } catch {
      toast("Failed to mark all as read", "error");
    }
  };

  const loadMore = () => {
    const next = page + 1;
    setPage(next);
    fetchPage(next);
  };

  const timeAgo = (dateStr: string) => {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return "just now";
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    const days = Math.floor(hrs / 24);
    if (days < 7) return `${days}d ago`;
    return new Date(dateStr).toLocaleDateString();
  };

  const typeIcon: Record<string, string> = {
    NewLeaseApplication: "📋",
    LeaseApplicationApproved: "✅",
    LeaseApplicationRejected: "❌",
    NewIssueReported: "🔧",
    IssueStatusChanged: "🔄",
    NewPropertySubmission: "🏠",
    PropertyApproved: "✅",
    PropertyRejected: "❌",
  };

  const unreadCount = items.filter((n) => !n.isRead).length;

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <Bell className="h-6 w-6 text-indigo-600" />
            <h1 className="text-2xl font-bold text-slate-900">Notifications</h1>
            {unreadCount > 0 && (
              <span className="rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-semibold text-red-600">
                {unreadCount} unread
              </span>
            )}
          </div>
          {unreadCount > 0 && (
            <Button variant="outline" className="gap-2 text-sm" onClick={markAllRead}>
              <CheckCheck className="h-4 w-4" />
              Mark all read
            </Button>
          )}
        </div>

        {/* Error */}
        {actionError && (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" /> {actionError}
          </div>
        )}

        {/* List */}
        {loading ? (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="h-20 animate-pulse rounded-xl bg-slate-100" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="rounded-full bg-indigo-50 p-4 mb-4">
              <BellOff className="h-8 w-8 text-indigo-400" />
            </div>
            <h3 className="text-lg font-semibold text-slate-700 mb-1">No notifications yet</h3>
            <p className="text-sm text-slate-500">You will be notified when something important happens.</p>
          </div>
        ) : (
          <div className="space-y-2">
            {items.map((n) => (
              <button
                key={n.notificationId}
                onClick={() => markRead(n)}
                className={`w-full text-left rounded-xl border px-5 py-4 transition-all hover:shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-2 ${
                  n.isRead
                    ? "border-slate-100 bg-white hover:border-slate-200"
                    : "border-indigo-200 bg-indigo-50/60 hover:border-indigo-300"
                }`}
              >
                <div className="flex items-start gap-3">
                  <span className="mt-0.5 text-lg">{typeIcon[n.type] || "🔔"}</span>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="text-sm font-semibold text-slate-800">{n.title}</p>
                      {!n.isRead && (
                        <span className="h-2 w-2 rounded-full bg-indigo-500 flex-shrink-0" />
                      )}
                    </div>
                    <p className="text-sm text-slate-600 mt-0.5">{n.message}</p>
                    <p className="text-xs text-slate-400 mt-1.5">{timeAgo(n.createdAt)}</p>
                  </div>
                  {n.isRead && <Check className="h-4 w-4 text-slate-300 mt-1 flex-shrink-0" />}
                </div>
              </button>
            ))}

            {hasMore && (
              <div className="pt-4 text-center">
                <Button variant="outline" onClick={loadMore} className="text-sm">
                  Load more
                </Button>
              </div>
            )}
          </div>
        )}
      </div>
    </AppShell>
  );
}
