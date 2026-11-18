import type { Metadata } from "next";
import { Jost, Oswald } from "next/font/google";
import "./globals.css";

// P2E's fonts. next/font self-hosts them at build time: no request to Google from the learner's phone.
const oswald = Oswald({ subsets: ["latin"], weight: ["500", "600"], variable: "--font-oswald", display: "swap" });
const jost = Jost({ subsets: ["latin"], weight: ["400", "500"], variable: "--font-jost", display: "swap" });

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
    <html lang="en" className={`${oswald.variable} ${jost.variable}`}>
      <body className="antialiased">{children}</body>
    </html>
  );
}
