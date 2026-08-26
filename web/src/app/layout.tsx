import type { Metadata } from "next";
import Link from "next/link";

import { getOptionalAuthenticatedUser } from "@/lib/auth";

import "./globals.css";

export const metadata: Metadata = {
  title: { default: "XerahS Cloud", template: "%s · XerahS Cloud" },
  description: "Your private, owner-only XerahS capture gallery.",
  robots: { index: false, follow: false, nocache: true },
  referrer: "no-referrer",
};

export const dynamic = "force-dynamic";

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const user = await getOptionalAuthenticatedUser();
  return (
    <html lang="en">
      <body>
        <header className="shell topbar">
          <Link className="brand" href="/">
            XerahS Cloud
          </Link>
          <nav className="topnav" aria-label="Primary navigation">
            {user ? (
              <>
                <span
                  aria-label={`Signed in as ${user.email}`}
                  className="session-status"
                  title={user.email}
                >
                  Signed in
                </span>
                <Link className="button primary" href="/settings">
                  Account
                </Link>
              </>
            ) : (
              <Link className="button primary" href="/auth">
                Sign in
              </Link>
            )}
          </nav>
        </header>
        <main className="shell main">{children}</main>
      </body>
    </html>
  );
}
