"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "@/components/providers/ThemeProvider";
import { useI18n } from "@/components/providers/I18nProvider";
import type { Lang } from "@/lib/i18n";

export function HeaderControls() {
  const { theme, toggleTheme } = useTheme();
  const { lang, setLang } = useI18n();

  const nextLang: Lang = lang === "en" ? "fr" : "en";

  return (
    <div className="flex items-center gap-2">
      <button
  className="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
  onClick={() => setLang(lang === "en" ? "fr" : "en")}
  type="button"
  aria-label="Toggle language"
>
  {lang === "en" ? "FR" : "EN"}
</button>


      <button
        className="rounded-full border border-white/15 bg-white/5 p-2 text-white/80 hover:bg-white/10"
        onClick={toggleTheme}
        type="button"
        aria-label="Toggle theme"
      >
        {theme === "dark" ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
      </button>
    </div>
  );
}
