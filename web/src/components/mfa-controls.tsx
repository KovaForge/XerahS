"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

import { createSupabaseBrowserClient } from "@/lib/supabase/browser";

interface TotpEnrollment {
  factorId: string;
  qrCode: string;
  secret: string;
}

export function MfaControls({
  strongAuth,
  passkeysEnabled,
}: {
  strongAuth: boolean;
  passkeysEnabled: boolean;
}) {
  const router = useRouter();
  const [verifiedFactorId, setVerifiedFactorId] = useState<string | null>(null);
  const [passkeyFactorId, setPasskeyFactorId] = useState<string | null>(null);
  const [enrollment, setEnrollment] = useState<TotpEnrollment | null>(null);
  const [code, setCode] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    async function loadFactors() {
      const result = await createSupabaseBrowserClient().auth.mfa.listFactors();
      if (result.error) setMessage(result.error.message);
      else {
        setVerifiedFactorId(
          result.data.totp.find(
            (factor: { id: string; status: string }) =>
              factor.status === "verified",
          )?.id ?? null,
        );
        setPasskeyFactorId(result.data.webauthn[0]?.id ?? null);
      }
    }
    void loadFactors();
  }, []);

  async function enroll() {
    setBusy(true);
    setMessage("");
    const { data, error } = await createSupabaseBrowserClient().auth.mfa.enroll(
      {
        factorType: "totp",
        friendlyName: "XerahS Cloud",
      },
    );
    setBusy(false);
    if (error) return setMessage(error.message);
    setEnrollment({
      factorId: data.id,
      qrCode: data.totp.qr_code,
      secret: data.totp.secret,
    });
  }

  async function verify() {
    const factorId = enrollment?.factorId ?? verifiedFactorId;
    if (!factorId || !/^\d{6}$/.test(code))
      return setMessage("Enter the six-digit authenticator code.");
    setBusy(true);
    setMessage("");
    const { error } =
      await createSupabaseBrowserClient().auth.mfa.challengeAndVerify({
        factorId,
        code,
      });
    setBusy(false);
    if (error) return setMessage(error.message);
    setCode("");
    setEnrollment(null);
    setMessage("Strong authentication is active for this session.");
    router.refresh();
  }

  async function registerPasskey() {
    setBusy(true);
    setMessage("");
    const { error } =
      await createSupabaseBrowserClient().auth.mfa.webauthn.register({
        friendlyName: "XerahS Cloud passkey",
      });
    setBusy(false);
    if (error) return setMessage(error.message);
    setMessage("Passkey registered and verified for this session.");
    router.refresh();
  }

  async function authenticatePasskey() {
    if (!passkeyFactorId) return;
    setBusy(true);
    setMessage("");
    const { error } =
      await createSupabaseBrowserClient().auth.mfa.webauthn.authenticate({
        factorId: passkeyFactorId,
      });
    setBusy(false);
    if (error) return setMessage(error.message);
    setMessage("Passkey authentication is active for this session.");
    router.refresh();
  }

  return (
    <section className="card stack">
      <h2>Two-factor authentication</h2>
      {strongAuth ? (
        <p>Authenticator verification is complete for this session.</p>
      ) : (
        <>
          {!verifiedFactorId && !enrollment && (
            <button
              className="primary"
              disabled={busy}
              onClick={() => void enroll()}
            >
              Set up authenticator
            </button>
          )}
          {enrollment && (
            <div className="stack">
              <p>
                Scan this private QR code with your authenticator app, or enter
                the secret manually.
              </p>
              {/* The QR code is a local data URI from Supabase Auth and must never be optimized or logged. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                alt="TOTP enrollment QR code"
                height="192"
                src={enrollment.qrCode}
                width="192"
              />
              <code>{enrollment.secret}</code>
            </div>
          )}
          {(verifiedFactorId || enrollment) && (
            <label>
              Authenticator code
              <input
                autoComplete="one-time-code"
                inputMode="numeric"
                maxLength={6}
                onChange={(event) =>
                  setCode(event.target.value.replaceAll(/\D/g, ""))
                }
                pattern="[0-9]{6}"
                value={code}
              />
            </label>
          )}
          {(verifiedFactorId || enrollment) && (
            <button
              className="primary"
              disabled={busy || code.length !== 6}
              onClick={() => void verify()}
            >
              Verify authenticator
            </button>
          )}
        </>
      )}
      {passkeysEnabled && (
        <div className="stack">
          <h3>Passkeys (preview)</h3>
          <p>
            Passkeys use Supabase&apos;s experimental WebAuthn support and
            remain disabled unless this deployment passes the RP ID and browser
            acceptance gate.
          </p>
          {passkeyFactorId && !strongAuth && (
            <button
              className="primary"
              disabled={busy}
              onClick={() => void authenticatePasskey()}
            >
              Verify with passkey
            </button>
          )}
          {strongAuth && (
            <button disabled={busy} onClick={() => void registerPasskey()}>
              Add passkey
            </button>
          )}
        </div>
      )}
      <p aria-live="polite" className="status">
        {message}
      </p>
    </section>
  );
}
