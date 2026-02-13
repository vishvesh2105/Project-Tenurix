import "./globals.css";
import { Inter, Sora } from "next/font/google";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter" });
const sora = Sora({ subsets: ["latin"], variable: "--font-sora" });

export const metadata = {
  title: "Tenurix",
  description: "Property Management Platform",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  const bootScript = `
    (() => {
      try {
        const theme = localStorage.getItem("tenurix_theme") || "light";
        document.documentElement.classList.toggle("dark", theme === "dark");
      } catch {}
    })();
  `;

  return (
    <html lang="en" className={`${inter.variable} ${sora.variable}`}>
      <head>
        <script dangerouslySetInnerHTML={{ __html: bootScript }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
