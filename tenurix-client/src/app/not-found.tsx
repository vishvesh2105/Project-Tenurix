import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-900 text-white">
      <div className="text-center">
        <h1 className="text-6xl font-bold">404</h1>
        <p className="mt-4 text-lg text-white/60">Page not found</p>
        <Link
          href="/"
          className="mt-6 inline-block rounded-xl bg-white/10 px-6 py-3 text-sm font-medium hover:bg-white/20 transition"
        >
          Go Home
        </Link>
      </div>
    </div>
  );
}
