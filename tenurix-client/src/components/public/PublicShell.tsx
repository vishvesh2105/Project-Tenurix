"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { Button } from "@/components/ui/button";
import { Menu, X, Mail, Phone, MapPin, ArrowRight } from "lucide-react";

export function PublicShell({ children }: { children: React.ReactNode }) {
  const [loggedIn, setLoggedIn] = useState(false);
  const [portal, setPortal] = useState<string>("client");
  const [menuOpen, setMenuOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem("tenurix_token");
    const p = localStorage.getItem("tenurix_portal") || "client";
    setLoggedIn(!!token);
    setPortal(p);
  }, []);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    const onResize = () => { if (window.innerWidth >= 768) setMenuOpen(false); };
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  const dashboardHref = portal === "landlord" ? "/landlord/dashboard" : "/dashboard";

  function getListPropertyHref() {
    if (!loggedIn) return "/auth?role=landlord";
    if (portal === "landlord") return "/landlord/submissions/new";
    return "/auth?role=landlord";
  }

  return (
    <div className="min-h-screen bg-white text-slate-900">
      {/* Header */}
      <header
        className={`sticky top-0 z-50 bg-white/95 backdrop-blur-md border-b transition-all duration-200 ${scrolled ? "border-slate-200 shadow-lg shadow-black/5" : "border-transparent"}`}
      >
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3">
          <Link href="/" className="flex items-center gap-2.5 group">
            <Image src="/home-logo.svg" alt="Tenurix" width={50} height={50} className="transition-transform group-hover:scale-105" />
            <div>
              <div className="text-sm font-bold text-slate-800 leading-tight">Tenurix</div>
              <div className="text-[10px] text-[#B48E6A] leading-tight font-medium">Find your home</div>
            </div>
          </Link>

          <nav role="navigation" aria-label="Main navigation" className="hidden md:flex items-center gap-7 text-sm font-medium text-slate-600">
            <Link href="/listings" className="hover:text-indigo-600 transition-colors duration-200 relative after:absolute after:bottom-[-2px] after:left-0 after:w-0 after:h-0.5 after:bg-indigo-500 after:transition-all after:duration-200 hover:after:w-full">Browse Listings</Link>
            <a href="#about" className="hover:text-indigo-600 transition-colors duration-200 relative after:absolute after:bottom-[-2px] after:left-0 after:w-0 after:h-0.5 after:bg-indigo-500 after:transition-all after:duration-200 hover:after:w-full">About</a>
            <a href="#contact" className="hover:text-indigo-600 transition-colors duration-200 relative after:absolute after:bottom-[-2px] after:left-0 after:w-0 after:h-0.5 after:bg-indigo-500 after:transition-all after:duration-200 hover:after:w-full">Contact</a>
          </nav>

          <div className="hidden md:flex items-center gap-2">
            {loggedIn ? (
              <Link href={dashboardHref}>
                <Button className="bg-indigo-600 hover:bg-indigo-700 text-white">Dashboard</Button>
              </Link>
            ) : (
              <>
                <Link href="/auth?role=client">
                  <Button variant="ghost" className="text-slate-600 hover:text-indigo-600">Sign In</Button>
                </Link>
                <Link href="/auth?role=client">
                  <Button className="bg-indigo-600 hover:bg-indigo-700 text-white">Get Started</Button>
                </Link>
              </>
            )}
          </div>

          <button
            className="md:hidden p-2 rounded-lg hover:bg-slate-100 transition-colors duration-200"
            onClick={() => setMenuOpen((v) => !v)}
            aria-label="Toggle mobile menu"
          >
            {menuOpen ? <X className="h-5 w-5 text-slate-700" /> : <Menu className="h-5 w-5 text-slate-700" />}
          </button>
        </div>

        {menuOpen && (
          <div className="md:hidden border-t border-slate-100 bg-white px-4 py-4 space-y-1 animate-in slide-in-from-top-2 duration-200">
            <Link href="/listings" className="block py-2.5 text-sm font-medium text-slate-700 hover:text-indigo-600" onClick={() => setMenuOpen(false)}>Browse Listings</Link>
            <a href="#about" className="block py-2.5 text-sm font-medium text-slate-700 hover:text-indigo-600" onClick={() => setMenuOpen(false)}>About</a>
            <a href="#contact" className="block py-2.5 text-sm font-medium text-slate-700 hover:text-indigo-600" onClick={() => setMenuOpen(false)}>Contact</a>
            <div className="flex gap-2 pt-3 border-t border-slate-100 mt-2">
              <Link href="/auth?role=client" className="flex-1" onClick={() => setMenuOpen(false)}>
                <Button variant="outline" className="w-full text-sm">Sign In</Button>
              </Link>
              <Link href="/auth?role=client" className="flex-1" onClick={() => setMenuOpen(false)}>
                <Button className="w-full bg-indigo-600 text-white text-sm">Get Started</Button>
              </Link>
            </div>
          </div>
        )}
      </header>

      <main>{children}</main>

      {/* Footer */}
      <footer className="bg-slate-900 text-slate-300">
        <div className="mx-auto max-w-7xl px-4 py-14">
          <div className="grid grid-cols-1 gap-10 sm:grid-cols-2 md:grid-cols-4">
            <div>
              <Link href="/" className="inline-flex items-center gap-2 mb-4">
                <Image src="/dark-logo.svg" alt="Tenurix" width={28} height={28} />
                <span className="font-bold text-white text-sm">Tenurix</span>
              </Link>
              <p className="text-sm text-slate-400 leading-relaxed">
                A modern rental platform connecting tenants, landlords, and property managers across Canada.
              </p>
            </div>

            <div>
              <h4 className="font-semibold text-[#B48E6A] mb-4 text-xs tracking-widest uppercase">Quick Links</h4>
              <ul className="space-y-2.5 text-sm">
                <li><Link href="/listings" className="hover:text-white transition-colors duration-200">Browse Listings</Link></li>
                <li><Link href="/auth?role=client" className="hover:text-white transition-colors duration-200">Tenant Sign In</Link></li>
                <li><Link href="/auth?role=landlord" className="hover:text-white transition-colors duration-200">Landlord Portal</Link></li>
                <li><Link href={getListPropertyHref()} className="hover:text-white transition-colors duration-200">List Your Property</Link></li>
              </ul>
            </div>

            <div id="about">
              <h4 className="font-semibold text-[#B48E6A] mb-4 text-xs tracking-widest uppercase">About Us</h4>
              <p className="text-sm text-slate-400 leading-relaxed">
                Tenurix was built to simplify the Canadian rental market. We believe every renter deserves a smooth, transparent experience — from search to move-in.
              </p>
            </div>

            <div id="contact">
              <h4 className="font-semibold text-[#B48E6A] mb-4 text-xs tracking-widest uppercase">Contact Us</h4>
              <ul className="space-y-3 text-sm">
                <li className="flex items-center gap-3">
                  <Mail className="h-4 w-4 text-[#B48E6A] shrink-0" />
                  <span>support@tenurix.ca</span>
                </li>
                <li className="flex items-center gap-3">
                  <Phone className="h-4 w-4 text-[#B48E6A] shrink-0" />
                  <span>+1 (800) 123-4567</span>
                </li>
                <li className="flex items-center gap-3">
                  <MapPin className="h-4 w-4 text-[#B48E6A] shrink-0" />
                  <span>Toronto, Ontario, Canada</span>
                </li>
              </ul>
            </div>
          </div>

          <div className="mt-10 border-t border-slate-700/50 pt-6 flex flex-col md:flex-row items-center justify-between gap-3 text-xs text-slate-500">
            <p>&copy; {new Date().getFullYear()} Tenurix Inc. All rights reserved. Unauthorized reproduction is prohibited.</p>
            <nav aria-label="Footer navigation" className="flex items-center gap-5">
              <a href="#" className="hover:text-slate-300 transition-colors duration-200">Privacy Policy</a>
              <a href="#" className="hover:text-slate-300 transition-colors duration-200">Terms of Service</a>
              <a href="#" className="hover:text-slate-300 transition-colors duration-200">Copyright Notice</a>
            </nav>
          </div>
        </div>
      </footer>
    </div>
  );
}
