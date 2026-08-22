import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import process from "node:process";

const accountId = process.env.CLOUDFLARE_ACCOUNT_ID;
const token = process.env.CLOUDFLARE_API_TOKEN;
const bucket = process.env.R2_BUCKET;
assert(
  accountId && token && bucket,
  "Cloudflare provisioning variables are required.",
);
assert(
  /^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$/.test(bucket),
  "R2_BUCKET is invalid.",
);

const desired = JSON.parse(
  await readFile(
    new URL("../infrastructure/cloudflare/r2-ledger.json", import.meta.url),
    "utf8",
  ),
);
const headers = {
  Authorization: `Bearer ${token}`,
  "Content-Type": "application/json",
};
const base = `https://api.cloudflare.com/client/v4/accounts/${accountId}/r2/buckets`;

async function request(path, init = {}) {
  const response = await fetch(`${base}${path}`, { ...init, headers });
  const body = await response.json();
  if (!response.ok || !body.success) {
    const error = new Error(
      `Cloudflare R2 provisioning failed (${response.status}).`,
    );
    error.status = response.status;
    throw error;
  }
  return body.result;
}

try {
  await request(`/${bucket}`);
} catch (error) {
  if (error.status !== 404) throw error;
  await request("", {
    method: "POST",
    body: JSON.stringify({ name: bucket, ...desired.bucket }),
  });
}

await request(`/${bucket}/domains/managed`, {
  method: "PUT",
  body: JSON.stringify({ enabled: desired.publicAccess }),
});
await request(`/${bucket}/lock`, {
  method: "PUT",
  body: JSON.stringify(desired.locks),
});
await request(`/${bucket}/lifecycle`, {
  method: "PUT",
  body: JSON.stringify(desired.lifecycle),
});

const custom = await request(`/${bucket}/domains/custom`);
assert(
  custom.domains.length === 0,
  "Refusing to continue: the ledger bucket has a custom domain.",
);
