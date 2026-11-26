import type { Metadata } from "next";
import { Nunito, Open_Sans } from "next/font/google";
import "./globals.css";

// Yoma's fonts (yoma.world). next/font self-hosts them at build time: no request to Google from the learner's phone.
const nunito = Nunito({ subsets: ["latin"], weight: ["700", "800"], variable: "--font-nunito", display: "swap" });
const openSans = Open_Sans({ subsets: ["latin"], weight: ["400", "600"], variable: "--font-open-sans", display: "swap" });

export const metadata: Metadata = {
  title: "SkillLens",
  description: "A 10-minute level-up challenge at the end of a P2E course",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={`${nunito.variable} ${openSans.variable}`}>
      <body className="antialiased">{children}</body>
    </html>
  );
}
