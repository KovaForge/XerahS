"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import QRCode from "qrcode";

import { authClient, authErrorMessage } from "@/lib/auth-client";

interface TotpEnrollment {
  qrCode: string;
  secret: string;
  backupCodes: string[];
}

function secretFromUri(totpUri: string): string {
  try {
    return new URL(totpUri).searchParams.get("secret") ?? "";
  } catch {
    return "";
  }
}

export function MfaControls({
  strongAuth,
  passkeysEnabled,
}: {
  strongAuth: boolean;
  passkeysEnabled: boolean;
}) {
  const router = useRouter();
  const session = authClient.useSession();
  const twoFactorEnabled = Boolean(
    (session.data?.user as { twoFactorEnabled?: boolean | null } | undefined)
      ?.twoFactorEnabled,
  );
  const [enrollment, setEnrollment] = useState<TotpEnrollment | null>(null);
  const [backupCodes, setBackupCodes] = useState<string[]>([]);
  const [code, setCode] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function enroll(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const password = String(
      new FormData(event.currentTarget).get("password") ?? "",
    );
    setBusy(true);
    setMessage("");
    const { data, error } = await authClient.twoFactor.enable({
      password,
      issuer: "XerahS Cloud",
    });
    if (error || !data || data.method !== "totp") {
      setBusy(false);
      return setMessage(authErrorMessage(error));
    }
    setEnrollment({
      // Rendered locally from the otpauth:// URI; never sent anywhere or logged.
      qrCode: await QRCode.toDataURL(data.totpURI, { margin: 1, width: 192 }),
      secret: secretFromUri(data.totpURI),
      backupCodes: data.backupCodes,
    });
    setBusy(false);
  }

  async function verify() {
    if (!/^\d{6}$/.test(code))
      return setMessage("Enter the six-digit authenticator code.");
    setBusy(true);
    setMessage("");
    const { error } = await authClient.twoFactor.verifyTotp({ code });
    setBusy(false);
    if (error) return setMessage(authErrorMessage(error));
    setCode("");
    if (enrollment) setBackupCodes(enrollment.backupCodes);
    setEnrollment(null);
    setMessage("Strong authentication is active for this session.");
    router.refresh();
  }

  async function regenerateBackupCodes(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const password = String(
      new FormData(event.currentTarget).get("password") ?? "",
    );
    setBusy(true);
    setMessage("");
    const { data, error } = await authClient.twoFactor.generateBackupCodes({
      password,
    });
    setBusy(false);
    if (error || !data) return setMessage(authErrorMessage(error));
    setBackupCodes(data.backupCodes);
    setMessage("New backup codes were created. The old ones no longer work.");
  }

  async function registerPasskey() {
    setBusy(true);
    setMessage("");
    const result = await authClient.passkey.addPasskey({
      name: "XerahS Cloud passkey",
    });
    setBusy(false);
    if (result?.error) return setMessage(authErrorMessage(result.error));
    setMessage("Passkey registered.");
  }

  async function authenticatePasskey() {
    setBusy(true);
    setMessage("");
    const result = await authClient.signIn.passkey();
    setBusy(false);
    if (result?.error) return setMessage(authErrorMessage(result.error));
    setMessage("Passkey authentication is active for this session.");
    router.refresh();
  }

  const codeInput = (
    <label>
      Authenticator code
      <input
        autoComplete="one-time-code"
        inputMode="numeric"
        maxLength={6}
        onChange={(event) => setCode(event.target.value.replaceAll(/\D/g, ""))}
        pattern="[0-9]{6}"
        value={code}
      />
    </label>
  );

  return (
    <section className="card stack">
      <h2>Two-factor authentication</h2>
      {strongAuth ? (
        <p>Authenticator verification is complete for this session.</p>
      ) : twoFactorEnabled && !enrollment ? (
        <>
          <p>Verify your authenticator to unlock your gallery and billing.</p>
          {codeInput}
          <button
            className="primary"
            disabled={busy || code.length !== 6}
            onClick={() => void verify()}
          >
            Verify authenticator
          </button>
        </>
      ) : enrollment ? (
        <div className="stack">
          <p>
            Scan this QR code with your authenticator app, or enter the secret
            manually, then enter the code it shows.
          </p>
          {/* A local data URI; must never be optimized, cached or logged. */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            alt="Authenticator setup QR code"
            height="192"
            src={enrollment.qrCode}
            width="192"
          />
          <code>{enrollment.secret}</code>
          {codeInput}
          <button
            className="primary"
            disabled={busy || code.length !== 6}
            onClick={() => void verify()}
          >
            Verify authenticator
          </button>
        </div>
      ) : (
        <form className="stack" onSubmit={enroll}>
          <p>
            Protect your account with an authenticator app. It is required for
            your gallery and billing.
          </p>
          <label>
            Current password
            <input
              autoComplete="current-password"
              name="password"
              required
              type="password"
            />
          </label>
          <button className="primary" disabled={busy} type="submit">
            Set up authenticator
          </button>
        </form>
      )}

      {backupCodes.length > 0 && (
        <div className="stack">
          <h3>Backup codes</h3>
          <p>
            Store these somewhere safe. Each one signs you in once if you lose
            your authenticator. They are shown only now.
          </p>
          <ul className="recovery-codes">
            {backupCodes.map((backupCode) => (
              <li key={backupCode}>
                <code>{backupCode}</code>
              </li>
            ))}
          </ul>
        </div>
      )}

      {strongAuth && twoFactorEnabled && (
        <form className="stack" onSubmit={regenerateBackupCodes}>
          <label>
            Current password
            <input
              autoComplete="current-password"
              name="password"
              required
              type="password"
            />
          </label>
          <button disabled={busy} type="submit">
            Create new backup codes
          </button>
        </form>
      )}

      {passkeysEnabled && (
        <div className="stack">
          <h3>Passkeys</h3>
          <p>
            A passkey on this device can replace the authenticator code when you
            sign in.
          </p>
          {!strongAuth && (
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
