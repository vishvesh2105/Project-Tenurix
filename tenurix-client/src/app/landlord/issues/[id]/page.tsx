"use client";

import { useEffect, useMemo, useState, useRef } from "react";
import { useParams, useRouter } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/tenurix/StatusBadge";
import { useAuth } from "@/lib/useAuth";
import { apiFetch, safeJson } from "@/lib/api";
import {
  ArrowLeft, AlertCircle, MapPin, Calendar, User, Send,
  ImagePlus, X, CheckCircle2, Clock, Loader2, Wrench, Camera
} from "lucide-react";

type IssueDetail = {
  issueId: number;
  issueType: string;
  description: string;
  imageUrl: string | null;
  status: string;
  createdAt: string;
  updatedAt: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
  repairImageUrl: string | null;
  propertyAddress: string;
  reportedByName: string;
};

type Comment = {
  commentId: number;
  userId: number;
  authorName: string;
  authorRole: string | null;
  message: string;
  imageUrl: string | null;
  createdAt: string;
};

const API_BASE = (process.env.NEXT_PUBLIC_API_BASE_URL || "").replace(/\/+$/, "");

function fmtDate(d: string) {
  try { return new Date(d).toLocaleDateString(); } catch { return d; }
}

function fmtDateTime(d: string) {
  try {
    const dt = new Date(d);
    return dt.toLocaleDateString() + " at " + dt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  } catch { return d; }
}

function timeAgo(d: string) {
  try {
    const diff = Math.floor((Date.now() - new Date(d).getTime()) / 1000);
    if (diff < 60) return "just now";
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    if (diff < 604800) return `${Math.floor(diff / 86400)}d ago`;
    return fmtDate(d);
  } catch { return d; }
}

export default function LandlordIssueDetailPage() {
  const { id } = useParams();
  const router = useRouter();
  const { isReady } = useAuth();
  const bottomRef = useRef<HTMLDivElement>(null);

  const [issue, setIssue] = useState<IssueDetail | null>(null);
  const [comments, setComments] = useState<Comment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [commentImage, setCommentImage] = useState<File | null>(null);
  const commentImageUrl = useMemo(() => commentImage ? URL.createObjectURL(commentImage) : null, [commentImage]);
  useEffect(() => { return () => { if (commentImageUrl) URL.revokeObjectURL(commentImageUrl); }; }, [commentImageUrl]);
  const [sending, setSending] = useState(false);

  async function loadIssue() {
    try {
      setLoading(true); setError("");
      const [issueRes, commentsRes] = await Promise.all([
        apiFetch(`/issues/${id}`),
        apiFetch(`/issues/${id}/comments`),
      ]);
      const issueData = await safeJson<any>(issueRes);
      if (!issueRes.ok) throw new Error(issueData?.message || "Failed to load issue.");
      setIssue(issueData);

      const commentsData = await safeJson<any>(commentsRes);
      if (commentsRes.ok && Array.isArray(commentsData)) setComments(commentsData);
    } catch (e: any) {
      setError(e?.message ?? "Failed to load issue");
    } finally {
      setLoading(false);
    }
  }

  async function handleSendComment() {
    if (!message.trim()) return;
    try {
      setSending(true);
      let res: Response;
      if (commentImage) {
        const fd = new FormData();
        fd.append("Message", message.trim());
        fd.append("Image", commentImage);
        res = await apiFetch(`/issues/${id}/comments/upload`, { method: "POST", body: fd });
      } else {
        res = await apiFetch(`/issues/${id}/comments`, {
          method: "POST",
          body: JSON.stringify({ message: message.trim() }),
        });
      }
      if (!res.ok) {
        const data = await safeJson<any>(res);
        throw new Error(data?.message || "Failed to post comment.");
      }
      setMessage(""); setCommentImage(null);
      const commentsRes = await apiFetch(`/issues/${id}/comments`);
      const commentsData = await safeJson<any>(commentsRes);
      if (commentsRes.ok && Array.isArray(commentsData)) setComments(commentsData);
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: "smooth" }), 100);
    } catch (e: any) {
      alert(e?.message || "Failed to post comment.");
    } finally {
      setSending(false);
    }
  }

  useEffect(() => { if (isReady && id) loadIssue(); }, [isReady, id]);

  if (!isReady || loading) {
    return (
      <AppShell>
        <div className="animate-pulse space-y-4">
          <div className="h-8 w-48 rounded-lg bg-slate-200" />
          <div className="h-64 rounded-2xl bg-slate-100" />
        </div>
      </AppShell>
    );
  }

  if (error || !issue) {
    return (
      <AppShell>
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center">
          <AlertCircle className="mx-auto h-8 w-8 text-red-400 mb-3" />
          <p className="text-sm text-red-700">{error || "Issue not found."}</p>
          <Button variant="outline" className="mt-4" onClick={() => router.back()}>Go Back</Button>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="flex flex-col gap-6 max-w-3xl mx-auto">
        <div>
          <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-700 mb-4">
            <ArrowLeft className="h-4 w-4" /> Back to Issues
          </button>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-bold text-slate-900">{issue.issueType}</h1>
                <StatusBadge status={issue.status} />
              </div>
              <div className="flex items-center gap-3 mt-1 text-xs text-slate-400">
                <span className="flex items-center gap-1"><MapPin className="h-3 w-3" />{issue.propertyAddress}</span>
                <span className="flex items-center gap-1"><Calendar className="h-3 w-3" />{fmtDate(issue.createdAt)}</span>
              </div>
            </div>
            <span className="text-xs text-slate-400">Issue #{issue.issueId}</span>
          </div>
        </div>

        {/* Issue Detail Card */}
        <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <div className="px-5 py-3 bg-slate-50 border-b border-slate-100">
            <div className="flex items-center gap-4 text-xs">
              <span className="flex items-center gap-1.5 text-slate-600">
                <Clock className="h-3.5 w-3.5" /> Submitted {fmtDateTime(issue.createdAt)}
              </span>
              {issue.resolvedAt && (
                <span className="flex items-center gap-1.5 text-emerald-600">
                  <CheckCircle2 className="h-3.5 w-3.5" /> Resolved {fmtDateTime(issue.resolvedAt)}
                </span>
              )}
            </div>
          </div>

          <div className="p-5 space-y-5">
            <div>
              <div className="text-xs font-semibold text-slate-400 mb-1.5">Description</div>
              <p className="text-sm text-slate-700 leading-relaxed">{issue.description}</p>
            </div>

            {issue.imageUrl && (
              <div>
                <div className="text-xs font-semibold text-slate-400 mb-1.5">Attached Photo</div>
                <a href={`${API_BASE}${issue.imageUrl}`} target="_blank" rel="noopener noreferrer">
                  <img src={`${API_BASE}${issue.imageUrl}`} alt="Issue" className="rounded-xl max-h-64 object-cover border border-slate-200" />
                </a>
              </div>
            )}

            {issue.resolutionNote && (
              <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4">
                <div className="flex items-center gap-2 mb-1.5">
                  <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                  <span className="text-xs font-semibold text-emerald-700">Resolution Note</span>
                </div>
                <p className="text-sm text-emerald-800">{issue.resolutionNote}</p>
              </div>
            )}

            {issue.repairImageUrl && (
              <div>
                <div className="flex items-center gap-2 mb-1.5">
                  <Camera className="h-3.5 w-3.5 text-slate-400" />
                  <span className="text-xs font-semibold text-slate-400">Repair Photo</span>
                </div>
                <a href={`${API_BASE}${issue.repairImageUrl}`} target="_blank" rel="noopener noreferrer">
                  <img src={`${API_BASE}${issue.repairImageUrl}`} alt="Repair" className="rounded-xl max-h-64 object-cover border border-slate-200" />
                </a>
              </div>
            )}

            <div className="flex items-center gap-2 text-xs text-slate-400">
              <User className="h-3.5 w-3.5" />
              Reported by <span className="font-medium text-slate-600">{issue.reportedByName}</span>
            </div>
          </div>
        </div>

        {/* Comments */}
        <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <div className="border-b border-slate-100 px-5 py-3">
            <span className="text-sm font-semibold text-slate-700">Comments</span>
            <span className="ml-2 text-xs text-slate-400">({comments.length})</span>
          </div>

          <div className="divide-y divide-slate-50 max-h-[500px] overflow-y-auto">
            {comments.length === 0 && (
              <div className="px-5 py-8 text-center text-sm text-slate-400">
                No comments yet. Start the conversation below.
              </div>
            )}
            {comments.map((c) => (
              <div key={c.commentId} className="px-5 py-3">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-xs font-semibold text-slate-700">{c.authorName}</span>
                  <span className="text-[10px] text-slate-400 ml-auto">{timeAgo(c.createdAt)}</span>
                </div>
                <p className="text-sm text-slate-600 leading-relaxed">{c.message}</p>
                {c.imageUrl && (
                  <a href={`${API_BASE}${c.imageUrl}`} target="_blank" rel="noopener noreferrer" className="mt-2 block">
                    <img src={`${API_BASE}${c.imageUrl}`} alt="Attachment" className="rounded-lg max-h-40 object-cover border border-slate-200" />
                  </a>
                )}
              </div>
            ))}
            <div ref={bottomRef} />
          </div>

          <div className="border-t border-slate-100 p-4">
            {commentImage && (
              <div className="mb-3 flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 p-2">
                <img src={commentImageUrl!} alt="" className="h-10 w-10 rounded object-cover" />
                <span className="flex-1 truncate text-xs text-slate-600">{commentImage.name}</span>
                <button onClick={() => setCommentImage(null)} className="p-1 text-red-400 hover:text-red-600"><X className="h-3.5 w-3.5" /></button>
              </div>
            )}
            <div className="flex gap-2">
              <input
                type="text"
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); handleSendComment(); } }}
                placeholder="Type a comment..."
                className="flex-1 rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
              />
              <label className="flex h-10 w-10 cursor-pointer items-center justify-center rounded-xl border border-slate-200 text-slate-400 hover:bg-slate-50 hover:text-slate-600 transition">
                <ImagePlus className="h-4 w-4" />
                <input type="file" accept="image/*" className="hidden" onChange={(e) => { setCommentImage(e.target.files?.[0] || null); e.target.value = ""; }} />
              </label>
              <Button
                className="gap-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-4"
                onClick={handleSendComment}
                disabled={sending || !message.trim()}
              >
                {sending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
