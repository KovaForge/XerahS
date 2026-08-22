"use client";

import { useEffect } from "react";

export function DesktopOAuthRelay({
  code,
  state,
}: {
  code: string;
  state: string;
}) {
  useEffect(() => {
    history.replaceState(null, "", "/auth/desktop/callback");
    location.replace(
      `xerahs://oauth/callback?code=${encodeURIComponent(code)}&state=${encodeURIComponent(state)}`,
    );
  }, [code, state]);

  return (
    <section className="card sign-wall">
      <h2>Return to XerahS</h2>
      <p className="lead">
        The authorization response is being returned to the desktop app.
      </p>
    </section>
  );
}
