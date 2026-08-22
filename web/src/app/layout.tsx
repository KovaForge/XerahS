import type { Metadata } from "next";
import Link from "next/link";

import "./globals.css";

export const metadata: Metadata = {
  title: { default: "XerahS Cloud", template: "%s · XerahS Cloud" },
  description: "Your private, owner-only XerahS capture gallery.",
  robots: { index: false, follow: false, nocache: true },
  referrer: "no-referrer",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <header className="shell topbar">
          <Link className="brand" href="/">
            XerahS Cloud
          </Link>
          <nav className="topnav" aria-label="Primary navigation">
            <Link className="button" href="/settings">
              Settings
            </Link>
            <Link className="button primary" href="/auth">
              Sign in
            </Link>
          </nav>
        </header>
        <main className="shell main">{children}</main>
      </body>
    </html>
  );
}
