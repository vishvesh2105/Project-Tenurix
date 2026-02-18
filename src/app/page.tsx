"use client";

import Link from "next/link";
import { PublicShell } from "@/components/public/PublicShell";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/components/providers/I18nProvider";
import { Search, ArrowRight } from "lucide-react";

export default function HomePage() {
  const { t } = useI18n();

  return (
    <PublicShell>
      {/* HERO */}
      <section className="relative">
        {/* background image-like gradient overlay */}
        <div className="relative h-[420px] w-full overflow-hidden">
          <div className="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1449844908441-8829872d2607?auto=format&fit=crop&w=1800&q=60')] bg-cover bg-center" />
          <div className="absolute inset-0 bg-black/55" />
          <div className="absolute inset-0 bg-gradient-to-b from-black/50 via-black/55 to-black/70" />

          <div className="relative mx-auto flex h-full max-w-6xl flex-col justify-center px-4">
            <h1 className="text-center text-4xl font-semibold tracking-tight text-white md:text-5xl">
              {t("findHome")}
            </h1>
            <p className="mx-auto mt-3 max-w-2xl text-center text-sm text-white/80 md:text-base">
              {t("tenurix")} connects renters, landlords, and management on one platform.
            </p>

            {/* Search bar */}
            <div className="mx-auto mt-8 w-full max-w-3xl">
              <div className="flex items-center gap-3 rounded-xl bg-white p-3 shadow-2xl">
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-slate-100 text-slate-600">
                  <Search className="h-5 w-5" />
                </div>
                <input
                  placeholder={
                    t("tenurix") +
                    " — Search city, neighbourhood, address..."
                  }
                  className="h-10 w-full border-0 bg-transparent text-sm text-slate-900 outline-none"
                />
                <Link href="/listings">
                  <Button className="h-10 gap-2">
                    {t("browse")} <ArrowRight className="h-4 w-4" />
                  </Button>
                </Link>
              </div>

              <div className="mt-3 flex flex-wrap items-center justify-center gap-3 text-xs text-white/80">
                <span className="rounded-full bg-white/10 px-3 py-1">Toronto</span>
                <span className="rounded-full bg-white/10 px-3 py-1">Mississauga</span>
                <span className="rounded-full bg-white/10 px-3 py-1">Brampton</span>
                <span className="rounded-full bg-white/10 px-3 py-1">Calgary</span>
              </div>

              <div className="mt-6 flex justify-center gap-3">
                <Link href="/auth?role=client">
                  <Button variant="secondary" className="gap-2">
                    {t("clientPortal")} <ArrowRight className="h-4 w-4" />
                  </Button>
                </Link>
                <Link href="/auth?role=landlord">
                  <Button variant="outline" className="gap-2 text-white border-white/30 hover:bg-white/10">
                    {t("landlordPortal")} <ArrowRight className="h-4 w-4" />
                  </Button>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* NEW RENTALS SECTION */}
      <section className="mx-auto max-w-6xl px-4 py-12">
<h2 className="text-center text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">

  {t("newRentalsTitle")}
</h2>


        <div className="mt-8 grid grid-cols-1 gap-6 md:grid-cols-3">
          {/* Card 1 */}
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-white/5 dark:shadow-none">
            <div className="h-44 bg-[url('https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=60')] bg-cover bg-center" />
            <div className="p-4">
              <div className="text-lg font-semibold">$1450</div>
              <div className="mt-1 text-sm text-slate-600 dark:text-white/70">
                Apartment • 2 Bed • 1 Bath
              </div>
              <div className="mt-2 text-xs text-slate-500 dark:text-white/60">London, ON</div>
              <div className="mt-4 flex justify-end">
                <Link href="/listings">
                  <Button size="sm" variant="secondary">Details</Button>
                </Link>
              </div>
            </div>
          </div>

          {/* Card 2 */}
          <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/5">
            <div className="h-44 bg-[url('https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6?auto=format&fit=crop&w=900&q=60')] bg-cover bg-center" />
            <div className="p-4">
              <div className="text-lg font-semibold">$3250</div>
              <div className="mt-1 text-sm text-white/70">
                Townhouse • 3 Bed • 2.5 Bath
              </div>
              <div className="mt-2 text-xs text-white/60">Scarborough, ON</div>
              <div className="mt-4 flex justify-end">
                <Link href="/listings">
                  <Button size="sm" variant="secondary">Details</Button>
                </Link>
              </div>
            </div>
          </div>

          {/* Card 3 */}
          <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/5">
            <div className="h-44 bg-[url('https://images.unsplash.com/photo-1493809842364-78817add7ffb?auto=format&fit=crop&w=900&q=60')] bg-cover bg-center" />
            <div className="p-4">
              <div className="text-lg font-semibold">$1000</div>
              <div className="mt-1 text-sm text-white/70">
                Room • 1 Bed • Shared Bath
              </div>
              <div className="mt-2 text-xs text-white/60">Calgary, AB</div>
              <div className="mt-4 flex justify-end">
                <Link href="/listings">
                  <Button size="sm" variant="secondary">Details</Button>
                </Link>
              </div>
            </div>
          </div>
        </div>

        <div className="mt-10 flex justify-center">
          <Link href="/listings">
<Button variant="secondary" className="rounded-full px-6">
  {t("seeAllRecent")}
</Button>

          </Link>
        </div>
      </section>
    </PublicShell>
  );
}
