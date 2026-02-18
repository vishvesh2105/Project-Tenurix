import "./globals.css";
import { Inter, Sora } from "next/font/google";
import { AppProviders } from "@/components/providers/AppProviders";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter" });
const sora = Sora({ subsets: ["latin"], variable: "--font-sora" });

export default function RootLayout({ children }: { children: React.ReactNode }) {
  const googleClientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

  // applies theme/lang BEFORE React hydration (prevents flash)
  const bootScript = `
    (() => {
      try {
        const theme = localStorage.getItem("tenurix_theme") || "dark";
        document.documentElement.classList.toggle("dark", theme === "dark");
        const lang = localStorage.getItem("tenurix_lang") || "en";
        document.documentElement.lang = lang;
      } catch {}
    })();
  `;

  return (
    <html lang="en" className={`${inter.variable} ${sora.variable}`}>
      <head>
        <script dangerouslySetInnerHTML={{ __html: bootScript }} />
        {googleClientId ? <script src="https://accounts.google.com/gsi/client" async defer /> : null}
      </head>
      <body>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
