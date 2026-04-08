"use client";

import { useState, useEffect, useRef } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import {
  Search, ArrowRight, Home, ShieldCheck, Zap,
  Building2, CheckCircle2, Users, Star, MapPin,
} from "lucide-react";

/* ── Intersection-observer fade-in helper ─────────────────────────── */
function useFadeIn(delay = 0) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.opacity = "0";
    el.style.transform = "translateY(28px)";
    const obs = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setTimeout(() => {
            el.style.transition = "opacity 0.65s ease, transform 0.65s ease";
            el.style.opacity = "1";
            el.style.transform = "translateY(0)";
          }, delay);
          obs.disconnect();
        }
      },
      { threshold: 0.1 }
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, [delay]);
  return ref;
}

/* ── Data ─────────────────────────────────────────────────────────── */
const CITIES = ["Toronto", "Mississauga", "Brampton", "Calgary", "Vancouver"];

const STEPS = [
  { num: "01", Icon: Search, title: "Browse Listings", desc: "Explore verified rental properties filtered by city, price, bedrooms, and more." },
  { num: "02", Icon: CheckCircle2, title: "Apply Online", desc: "Submit your rental application digitally — no paperwork, no hassle." },
  { num: "03", Icon: Home, title: "Move In", desc: "Once approved, sign your lease and access your personal rental dashboard." },
];

const FEATURES = [
  { Icon: ShieldCheck, title: "Verified Properties", desc: "Every listing is reviewed and approved by management before going live." },
  { Icon: Zap, title: "Fast Applications", desc: "Apply to multiple listings in minutes and track your application status in real time." },
  { Icon: Building2, title: "Landlord Tools", desc: "Manage listings, leases, and tenants from one clean, modern dashboard." },
  { Icon: Users, title: "Management Portal", desc: "Work orders, staff management, and full property oversight in one place." },
];

const CHECKLIST = [
  "Browse & filter verified listings",
  "Submit rental applications online",
  "Sign leases digitally",
  "Pay rent & track payment history",
  "Submit maintenance requests",
  "Landlord submission & approval workflow",
];

const TESTIMONIALS = [
  { name: "Sarah M.", city: "Toronto", text: "Found my apartment in 3 days. The process was completely online — no meetings, no paperwork delays.", stars: 5 },
  { name: "James R.", city: "Calgary", text: "As a landlord, Tenurix handles everything. My listings get quality applicants fast.", stars: 5 },
  { name: "Priya L.", city: "Mississauga", text: "The tenant dashboard makes it so easy to pay rent and track my lease. Love it.", stars: 5 },
];

/* ── Page ─────────────────────────────────────────────────────────── */
export default function HomePage() {
  const router = useRouter();
  const [searchCity, setSearchCity] = useState("");
  const heroRef = useRef<HTMLDivElement>(null);
  const stepsRef = useFadeIn(0);
  const featRef = useFadeIn(80);
  const testiRef = useFadeIn(0);

  // Auth state for "List Your Property" button
  const [loggedIn, setLoggedIn] = useState(false);
  const [portal, setPortal] = useState("client");

  useEffect(() => {
    const token = localStorage.getItem("tenurix_token");
    const p = localStorage.getItem("tenurix_portal") || "client";
    setLoggedIn(!!token);
    setPortal(p);
  }, []);

  // hero entrance animation on mount
  useEffect(() => {
    const el = heroRef.current;
    if (!el) return;
    el.style.opacity = "0";
    el.style.transform = "translateY(20px)";
    const id = setTimeout(() => {
      el.style.transition = "opacity 0.75s ease, transform 0.75s ease";
      el.style.opacity = "1";
      el.style.transform = "translateY(0)";
    }, 60);
    return () => clearTimeout(id);
  }, []);

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const city = searchCity.trim();
    router.push(city ? `/listings?city=${encodeURIComponent(city)}` : "/listings");
  }

  function getListPropertyHref() {
    if (!loggedIn) return "/auth?role=landlord";
    if (portal === "landlord") return "/landlord/submissions/new";
    return "/auth?role=landlord";
  }

  return (
    <PublicShell>
      {/* ─── HERO ─────────────────────────────────────────────────── */}
      <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 min-h-[580px] flex items-center">
        {/* decorative glows */}
        <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden>
          <div className="absolute -top-32 -right-32 h-[500px] w-[500px] rounded-full bg-indigo-500/15 blur-3xl" />
          <div className="absolute -bottom-20 -left-20 h-80 w-80 rounded-full bg-indigo-500/10 blur-3xl" />
          <div className="absolute top-1/2 left-1/2 h-64 w-64 -translate-x-1/2 -translate-y-1/2 rounded-full bg-white/5 blur-2xl" />
        </div>

        <div className="relative mx-auto w-full max-w-7xl px-4 py-20 md:py-28">
          <div ref={heroRef} className="mx-auto max-w-3xl text-center">
            {/* Live badge */}
            <div className="inline-flex items-center gap-2 rounded-full bg-white/10 border border-white/15 px-4 py-1.5 text-xs font-medium text-[#B48E6A] mb-6 backdrop-blur-sm">
              <span className="h-2 w-2 rounded-full bg-green-400 animate-pulse" />
              Verified listings across Canada
            </div>

            <h1 className="text-4xl font-extrabold text-white leading-tight md:text-5xl lg:text-6xl">
              Find Your Perfect<br />
              <span className="text-[#B48E6A]">Rental Home</span>
            </h1>

            <p className="mt-5 text-lg text-white/70 max-w-xl mx-auto leading-relaxed">
              Tenurix connects renters and landlords on one trusted platform. Browse verified listings, apply online, and move in faster.
            </p>

            {/* Search bar */}
            <form onSubmit={handleSearch} className="mt-8">
              <div className="flex items-center gap-2 rounded-2xl bg-white p-2 shadow-2xl max-w-2xl mx-auto ring-4 ring-amber-200">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100 shrink-0">
                  <Search className="h-5 w-5 text-indigo-600" />
                </div>
                <input
                  value={searchCity}
                  onChange={(e) => setSearchCity(e.target.value)}
                  placeholder="Search by city, neighbourhood, or address..."
                  className="flex-1 min-w-0 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
                />
                <Button type="submit" className="shrink-0 bg-indigo-600 hover:bg-indigo-700 text-white gap-2 rounded-xl px-5 h-10">
                  Search <ArrowRight className="h-4 w-4" />
                </Button>
              </div>
            </form>

            {/* Quick city chips */}
            <div className="mt-4 flex flex-wrap items-center justify-center gap-2">
              {CITIES.map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => router.push(`/listings?city=${c}`)}
                  className="flex items-center gap-1 rounded-full bg-white/10 hover:bg-amber-500/30 border border-white/20 hover:border-amber-400/40 px-3.5 py-1 text-xs text-white transition-all duration-200 hover:scale-105"
                >
                  <MapPin className="h-3 w-3 opacity-70" />
                  {c}
                </button>
              ))}
            </div>

                {/* Portal CTAs */}
            <div className="mt-7 flex flex-wrap items-center justify-center gap-3">
              <Link href="/auth?role=client">
                <Button variant="outline" className="gap-2 border-amber-400 bg-transparent px-6 text-amber-500 transition-all hover:-translate-y-0.5 hover:border-[#a37e5d] hover:bg-[#a37e5d] hover:text-white">
                  Tenant Portal <ArrowRight className="h-4 w-4" />
                </Button>
              </Link>
              <Link href={getListPropertyHref()}>
                <Button variant="outline" className="gap-2 border-amber-400 bg-transparent px-6 text-amber-500 transition-all hover:-translate-y-0.5 hover:border-[#a37e5d] hover:bg-[#a37e5d] hover:text-white">
                  List Your Property <ArrowRight className="h-4 w-4" />
                </Button>
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* ─── STATS BAR ────────────────────────────────────────────── */}
      <section className="bg-white border-b border-slate-100">
        <div className="mx-auto max-w-7xl px-4 py-9">
          <div className="grid grid-cols-2 gap-6 md:grid-cols-4 text-center">
            {[
              { id: 1, label: "Active Listings" as React.ReactNode },
              { id: 2, label: "Happy Tenants" as React.ReactNode },
              { id: 3, label: "Verified Landlords" as React.ReactNode },
              { id: 4, label: <>Many<br />Cities Covered</> },
            ].map((s) => (
              <div key={s.id}>
                <div className="text-xl font-bold text-[#B48E6A] mt-1">{s.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ─── HOW IT WORKS ─────────────────────────────────────────── */}
      <section className="bg-slate-50 py-20">
        <div className="mx-auto max-w-7xl px-4">
          <div className="text-center mb-14">
            <span className="text-xs font-bold uppercase tracking-widest text-[#B48E6A]">Simple Process</span>
            <h2 className="mt-2 text-3xl font-extrabold text-slate-900">How Tenurix Works</h2>
            <p className="mt-2 text-slate-500 max-w-sm mx-auto">Three simple steps from browsing to move-in</p>
          </div>

          <div ref={stepsRef} className="grid grid-cols-1 gap-6 md:grid-cols-3">
            {STEPS.map((s) => (
              <div
                key={s.num}
                className="relative rounded-2xl bg-white border border-slate-100 p-7 shadow-sm hover:shadow-lg transition-all duration-300 hover:-translate-y-1.5 group"
              >
                <span className="absolute -top-3.5 left-6 flex h-7 w-12 items-center justify-center rounded-full bg-indigo-600 text-xs font-bold text-white shadow">
                  {s.num}
                </span>
                <div className="mt-3 mb-5 inline-flex h-12 w-12 items-center justify-center rounded-xl bg-indigo-100 group-hover:bg-indigo-500/15 transition-colors duration-300">
                  <s.Icon className="h-6 w-6 text-indigo-600" />
                </div>
                <h3 className="font-bold text-slate-900">{s.title}</h3>
                <p className="mt-2 text-sm text-slate-500 leading-relaxed">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ─── FEATURES ─────────────────────────────────────────────── */}
      <section className="bg-white py-20">
        <div className="mx-auto max-w-7xl px-4">
          <div ref={featRef} className="grid grid-cols-1 gap-14 md:grid-cols-2 items-center">
            {/* Text side */}
            <div>
              <span className="text-xs font-bold uppercase tracking-widest text-[#B48E6A]">Why Tenurix</span>
              <h2 className="mt-2 text-3xl font-extrabold text-slate-900 leading-tight">
                Everything You Need<br />
                <span className="text-indigo-600">In One Platform</span>
              </h2>
              <p className="mt-4 text-slate-500 leading-relaxed max-w-md">
                Tenurix brings renters, landlords, and property managers together in a transparent, efficient, and modern rental ecosystem.
              </p>
              <ul className="mt-8 space-y-5">
                {FEATURES.map((f) => (
                  <li key={f.title} className="flex items-start gap-4 group">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-indigo-100 group-hover:bg-indigo-500/15 transition-colors duration-300 mt-0.5">
                      <f.Icon className="h-5 w-5 text-indigo-600" />
                    </div>
                    <div>
                      <div className="font-semibold text-slate-800 text-sm">{f.title}</div>
                      <div className="text-sm text-slate-500 mt-0.5 leading-relaxed">{f.desc}</div>
                    </div>
                  </li>
                ))}
              </ul>
            </div>

            {/* Checklist card */}
            <div className="rounded-2xl bg-gradient-to-br from-slate-900 to-indigo-950 border border-indigo-900 p-8 shadow-xl">
              <p className="text-xs font-bold text-[#B48E6A] uppercase tracking-widest mb-5">Platform Features</p>
              <ul className="space-y-4">
                {CHECKLIST.map((item) => (
                  <li key={item} className="flex items-center gap-3">
                    <div className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-[#B48E6A]">
                      <CheckCircle2 className="h-3.5 w-3.5 text-white" />
                    </div>
                    <span className="text-sm font-medium text-white/90">{item}</span>
                  </li>
                ))}
              </ul>
              <div className="mt-8 pt-6 border-t border-white/15">
                <Link href="/listings">
                  <Button className="w-full bg-[#B48E6A] hover:bg-[#a37e5d] text-white gap-2 transition-transform hover:-translate-y-0.5">
                    Browse Available Listings <ArrowRight className="h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </section>


{/* ─── CTA ──────────────────────────────────────────────────── */}
      <section className="relative overflow-hidden bg-gradient-to-br from-slate-900 to-slate-900 py-20">
        <div className="pointer-events-none absolute inset-0" aria-hidden>
          <div className="absolute right-[-80px] top-[-60px] h-80 w-80 rounded-full bg-indigo-500/15 blur-3xl" />
          <div className="absolute left-[-60px] bottom-[-40px] h-64 w-64 rounded-full bg-indigo-500/10 blur-3xl" />
        </div>
        <div className="relative mx-auto max-w-3xl px-4 text-center">
          <h2 className="text-3xl font-extrabold text-white">Ready to Find Your Next Home?</h2>
          <p className="mt-3 text-white/70 text-lg">Join thousands of renters who found their perfect place on Tenurix.</p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link href="/listings">
              <Button className="bg-[#B48E6A] text-white hover:bg-[#a37e5d] gap-2 px-7 font-semibold shadow-lg shadow-black/20 transition-all hover:-translate-y-0.5 hover:shadow-xl">
                Browse Listings <ArrowRight className="h-4 w-4" />
              </Button>
            </Link>
            <Link href={getListPropertyHref()}>
              <Button variant="outline" className="border-[#B48E6A] text-[#B48E6A] hover:bg-indigo-500/15 gap-2 px-7 transition-all hover:-translate-y-0.5">
                List Your Property
              </Button>
            </Link>
          </div>
        </div>
      </section>
    </PublicShell>
  );
}
