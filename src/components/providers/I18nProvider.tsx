"use client";

import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { Lang } from "@/lib/i18n";
import { dict } from "@/lib/i18n";

type I18nCtx = {
  lang: Lang;
  setLang: (l: Lang) => void;
  t: (key: keyof typeof dict.en) => string;
};

const Ctx = createContext<I18nCtx | null>(null);

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Lang>("en");

  useEffect(() => {
    const saved = (localStorage.getItem("tenurix_lang") as Lang | null) || "en";
    setLangState(saved);
    document.documentElement.lang = saved;
  }, []);

  const setLang = (l: Lang) => {
    setLangState(l);
    localStorage.setItem("tenurix_lang", l);
    document.documentElement.lang = l;
  };

  const t = (key: keyof typeof dict.en) => dict[lang][key] ?? dict.en[key];

  const value = useMemo(() => ({ lang, setLang, t }), [lang]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useI18n() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useI18n must be used inside I18nProvider");
  return ctx;
}
