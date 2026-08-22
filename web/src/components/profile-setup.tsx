"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

export function ProfileSetup() {
  const router = useRouter();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setMessage("");
    const response = await fetch("/api/v1/profile", {
      method: "POST",
      headers: { "Content-Type": "application/json", Origin: location.origin },
      body: JSON.stringify({
        slug: String(data.get("slug") ?? ""),
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
      }),
    });
    const result = (await response.json().catch(() => ({}))) as {
      error?: { message?: string };
    };
    setBusy(false);
    if (!response.ok)
      return setMessage(
        result.error?.message ?? "Could not create the profile.",
      );
    router.refresh();
  }

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>Create your private profile</h2>
      <label>
        Profile name
        <input
          autoComplete="username"
          maxLength={30}
          minLength={3}
          name="slug"
          pattern="[a-z0-9](?:[a-z0-9-]{1,28}[a-z0-9])?"
          placeholder="your-name"
          required
        />
      </label>
      <button className="primary" disabled={busy} type="submit">
        Create profile
      </button>
      <p aria-live="polite" className="status">
        {message}
      </p>
    </form>
  );
}
