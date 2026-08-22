"use client";

import { useEffect } from "react";

export function DesktopOAuthRelay({
  code,
  error,
  state,
}: {
  code?: string;
  error?: string;
  state: string;
}) {
  useEffect(() => {
    history.replaceState(null, "", "/auth/desktop/callback");
    const result = code
      ? `code=${encodeURIComponent(code)}`
      : `error=${encodeURIComponent(error ?? "access_denied")}`;
    location.replace(
      `xerahs://oauth/callback?${result}&state=${encodeURIComponent(state)}`,
    );
  }, [code, error, state]);

  return (
    <section className="card sign-wall">
      <h2>Return to XerahS</h2>
      <p className="lead">
        The authorization response is being returned to the desktop app.
      </p>
    </section>
  );
}
