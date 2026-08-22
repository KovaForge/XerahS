"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

import { createSupabaseBrowserClient } from "@/lib/supabase/browser";

interface Props {
  strongAuth: boolean;
  trialStatus: string;
}

export function SettingsControls({ strongAuth, trialStatus }: Props) {
  const router = useRouter();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function post(path: string, body?: unknown) {
    setBusy(true);
    setMessage("");
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json", Origin: location.origin },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const result = (await response.json().catch(() => ({}))) as {
      url?: string;
      error?: { message?: string };
    };
    setBusy(false);
    if (!response.ok) {
      setMessage(result.error?.message ?? "The request failed.");
      return;
    }
    if (result.url) location.assign(result.url);
    else {
      setMessage(
        response.status === 202 ? "The operation is safely queued." : "Done.",
      );
      router.refresh();
    }
  }

  async function signOut(scope: "local" | "global") {
    setBusy(true);
    await createSupabaseBrowserClient().auth.signOut({ scope });
    router.push("/");
    router.refresh();
  }

  async function exportAccount() {
    setBusy(true);
    setMessage("");
    const response = await fetch("/api/v1/account/export", {
      method: "POST",
      headers: { Origin: location.origin },
    });
    setBusy(false);
    if (!response.ok)
      return setMessage("The account export could not be created.");
    const url = URL.createObjectURL(await response.blob());
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "xerahs-cloud-export.json";
    anchor.click();
    URL.revokeObjectURL(url);
    setMessage("Account export downloaded.");
  }

  async function deleteAccount() {
    if (
      prompt(
        "Type DELETE to request permanent XerahS Cloud account deletion.",
      ) !== "DELETE"
    )
      return;
    setBusy(true);
    setMessage("");
    const response = await fetch("/api/v1/account", {
      method: "DELETE",
      headers: { "Content-Type": "application/json", Origin: location.origin },
      body: JSON.stringify({ confirmation: "DELETE" }),
    });
    setBusy(false);
    if (!response.ok)
      return setMessage("The deletion request could not be queued.");
    setMessage("Account deletion is safely queued. Access is being disabled.");
  }

  return (
    <section className="card stack">
      <h2>Actions</h2>
      {!strongAuth && (
        <p className="error">
          Complete a TOTP challenge in your Supabase Auth session before using
          protected gallery and billing actions.
        </p>
      )}
      {trialStatus === "not_started" && (
        <button
          className="primary"
          disabled={busy || !strongAuth}
          onClick={() => void post("/api/v1/trial/start")}
        >
          Start 7-day trial
        </button>
      )}
      <div className="actions">
        <button
          disabled={busy || !strongAuth}
          onClick={() =>
            void post("/api/v1/billing/checkout", { plan: "monthly" })
          }
        >
          Subscribe monthly · $1.99 USD
        </button>
        <button
          disabled={busy || !strongAuth}
          onClick={() =>
            void post("/api/v1/billing/checkout", { plan: "annual" })
          }
        >
          Subscribe yearly · $19.99 USD
        </button>
        <button
          disabled={busy || !strongAuth}
          onClick={() => void post("/api/v1/billing/portal")}
        >
          Manage billing
        </button>
        <button
          disabled={busy || !strongAuth}
          onClick={() => void exportAccount()}
        >
          Export account data
        </button>
        <button
          className="danger"
          disabled={busy || !strongAuth}
          onClick={() => void deleteAccount()}
        >
          Delete XerahS Cloud account
        </button>
        <button disabled={busy} onClick={() => void signOut("local")}>
          Sign out
        </button>
        <button disabled={busy} onClick={() => void signOut("global")}>
          Sign out all devices
        </button>
      </div>
      <p aria-live="polite" className="status">
        {message}
      </p>
    </section>
  );
}
