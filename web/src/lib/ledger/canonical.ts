import "server-only";

import { createHash, createHmac } from "node:crypto";

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalValue);
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, item]) => [key, canonicalValue(item)]),
    );
  }
  return value;
}

export function canonicalJson(value: unknown): string {
  return JSON.stringify(canonicalValue(value));
}

export function sha256(value: string): string {
  return createHash("sha256").update(value, "utf8").digest("hex");
}

export function signLedgerPayload(
  payload: Record<string, unknown>,
  keyVersion: string,
  secret: string,
): string {
  const canonicalPayload = canonicalJson(payload);
  const digest = sha256(canonicalPayload);
  const signature = createHmac("sha256", secret)
    .update(canonicalPayload, "utf8")
    .digest("hex");
  return canonicalJson({
    payload,
    integrity: {
      algorithm: "HMAC-SHA256",
      keyVersion,
      sha256: digest,
      signature,
    },
  });
}

export function ledgerKey(
  eventType: string,
  occurredAt: string,
  eventId: string,
): string {
  const date = new Date(occurredAt);
  if (Number.isNaN(date.getTime()))
    throw new Error("Invalid ledger event timestamp.");
  const prefix =
    eventType === "trial_grant_created" ? "trial-grants/v1" : "deletions/v1";
  return `${prefix}/${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}/${eventId}.json`;
}
