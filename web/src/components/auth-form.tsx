"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

import { createSupabaseBrowserClient } from "@/lib/supabase/browser";

export function AuthForm({ next = "/settings" }: { next?: string }) {
  const router = useRouter();
  const [mode, setMode] = useState<"signin" | "signup">("signin");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage("");
    const data = new FormData(event.currentTarget);
    const email = String(data.get("email") ?? "");
    const password = String(data.get("password") ?? "");
    const supabase = createSupabaseBrowserClient();
    const result =
      mode === "signin"
        ? await supabase.auth.signInWithPassword({ email, password })
        : await supabase.auth.signUp({
            email,
            password,
            options: {
              emailRedirectTo: (() => {
                const callback = new URL("/auth/callback", location.origin);
                callback.searchParams.set("next", next);
                return callback.href;
              })(),
            },
          });
    setBusy(false);
    if (result.error) {
      setMessage(result.error.message);
      return;
    }
    if (mode === "signup") {
      setMessage("Check your email to verify your account, then sign in.");
      return;
    }
    router.push(next);
    router.refresh();
  }

  return (
    <form className="stack" onSubmit={submit}>
      <label>
        Email
        <input autoComplete="email" name="email" required type="email" />
      </label>
      <label>
        Password
        <input
          autoComplete={mode === "signin" ? "current-password" : "new-password"}
          minLength={12}
          name="password"
          required
          type="password"
        />
      </label>
      <button className="primary" disabled={busy} type="submit">
        {busy ? "Working…" : mode === "signin" ? "Sign in" : "Create account"}
      </button>
      <button
        onClick={() => {
          setMode(mode === "signin" ? "signup" : "signin");
          setMessage("");
        }}
        type="button"
      >
        {mode === "signin" ? "Create a new account" : "Use an existing account"}
      </button>
      <p aria-live="polite" className={message ? "status error" : "status"}>
        {message}
      </p>
    </form>
  );
}
