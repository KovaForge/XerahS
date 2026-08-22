import {
  GetObjectCommand,
  ListObjectsV2Command,
  S3Client,
} from "@aws-sdk/client-s3";
import { createHash, createHmac, timingSafeEqual } from "node:crypto";

const apply = process.argv.includes("--apply");
const accountId = required("R2_LEDGER_ACCOUNT_ID");
const bucket = required("R2_LEDGER_BUCKET");
const accessKeyId = required("R2_LEDGER_READ_ACCESS_KEY_ID");
const secretAccessKey = required("R2_LEDGER_READ_SECRET_ACCESS_KEY");
const keyRing = ledgerKeyRing();

if (apply && process.env.RESTORE_CONFIRM_CLOSED_TRAFFIC !== "YES") {
  throw new Error(
    "--apply requires RESTORE_CONFIRM_CLOSED_TRAFFIC=YES after traffic and mutating jobs are disabled.",
  );
}

const client = new S3Client({
  region: "auto",
  endpoint: `https://${accountId}.r2.cloudflarestorage.com`,
  credentials: { accessKeyId, secretAccessKey },
});

function required(name) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

function ledgerKeyRing() {
  let ring = {};
  if (process.env.LEDGER_HMAC_SECRETS_JSON) {
    const parsed = JSON.parse(process.env.LEDGER_HMAC_SECRETS_JSON);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed))
      throw new Error("LEDGER_HMAC_SECRETS_JSON must be an object.");
    ring = parsed;
  }
  if (process.env.LEDGER_HMAC_SECRET_V1)
    ring.v1 ??= process.env.LEDGER_HMAC_SECRET_V1;
  for (const [version, secret] of Object.entries(ring)) {
    if (
      !/^v[1-9][0-9]*$/.test(version) ||
      typeof secret !== "string" ||
      secret.length < 32
    )
      throw new Error("Ledger HMAC key ring contains an invalid entry.");
  }
  if (Object.keys(ring).length === 0)
    throw new Error("At least one ledger HMAC verification key is required.");
  return ring;
}

function canonicalValue(value) {
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

function canonicalJson(value) {
  return JSON.stringify(canonicalValue(value));
}

function hexDigest(algorithm, value) {
  return createHash(algorithm).update(value, "utf8").digest("hex");
}

function equalHex(expected, actual) {
  if (!/^[0-9a-f]{64}$/.test(expected) || !/^[0-9a-f]{64}$/.test(actual))
    return false;
  return timingSafeEqual(
    Buffer.from(expected, "hex"),
    Buffer.from(actual, "hex"),
  );
}

function uuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value ?? "",
  );
}

function expectedKey(payload) {
  if (
    payload?.schemaVersion !== 1 ||
    !uuid(payload.eventId) ||
    ![
      "trial_grant_created",
      "gallery_item_unpublished",
      "account_deleted",
    ].includes(payload.eventType)
  )
    throw new Error("Ledger payload has an unknown schema or event type.");
  const occurredAt = new Date(payload.occurredAt);
  if (Number.isNaN(occurredAt.getTime()) || !uuid(payload.userId))
    throw new Error("Ledger payload identifiers or timestamp are invalid.");
  if (
    payload.eventType === "trial_grant_created" &&
    (!/^[0-9a-f]{64}$/.test(payload.identityHmac ?? "") ||
      !Number.isInteger(payload.identityNormalizationVersion) ||
      !Number.isInteger(payload.identityHmacKeyVersion))
  )
    throw new Error("Trial ledger payload is invalid.");
  if (payload.eventType === "gallery_item_unpublished" && !uuid(payload.itemId))
    throw new Error("Unpublish ledger payload is invalid.");
  const commonKeys = [
    "eventId",
    "eventType",
    "occurredAt",
    "schemaVersion",
    "userId",
  ];
  const eventKeys =
    payload.eventType === "trial_grant_created"
      ? [
          "identityHmac",
          "identityHmacKeyVersion",
          "identityNormalizationVersion",
        ]
      : payload.eventType === "gallery_item_unpublished"
        ? ["itemId"]
        : [];
  const actualKeys = Object.keys(payload).sort();
  const allowedKeys = [...commonKeys, ...eventKeys].sort();
  if (canonicalJson(actualKeys) !== canonicalJson(allowedKeys))
    throw new Error("Ledger payload contains unknown or missing fields.");
  const prefix =
    payload.eventType === "trial_grant_created"
      ? "trial-grants/v1"
      : "deletions/v1";
  return `${prefix}/${occurredAt.getUTCFullYear()}/${String(occurredAt.getUTCMonth() + 1).padStart(2, "0")}/${String(occurredAt.getUTCDate()).padStart(2, "0")}/${payload.eventId}.json`;
}

async function listKeys(prefix) {
  const keys = [];
  let continuationToken;
  do {
    const page = await client.send(
      new ListObjectsV2Command({
        Bucket: bucket,
        Prefix: prefix,
        ContinuationToken: continuationToken,
      }),
    );
    for (const object of page.Contents ?? []) {
      if (!object.Key) throw new Error("R2 returned an object without a key.");
      keys.push(object.Key);
    }
    continuationToken = page.IsTruncated
      ? page.NextContinuationToken
      : undefined;
    if (page.IsTruncated && !continuationToken)
      throw new Error("R2 pagination ended without a continuation token.");
  } while (continuationToken);
  return keys;
}

async function verifyObject(key) {
  const result = await client.send(
    new GetObjectCommand({ Bucket: bucket, Key: key }),
  );
  if ((result.ContentLength ?? 0) > 65_536)
    throw new Error(`Ledger object exceeds the size limit: ${key}`);
  const body = await result.Body?.transformToString("utf8");
  if (!body) throw new Error(`Ledger object is empty: ${key}`);
  const envelope = JSON.parse(body);
  const payloadJson = canonicalJson(envelope.payload);
  const integrity = envelope.integrity;
  if (integrity?.algorithm !== "HMAC-SHA256")
    throw new Error(`Ledger object uses an unknown algorithm: ${key}`);
  const secret = keyRing[integrity.keyVersion];
  if (!secret)
    throw new Error(`Ledger object uses an unavailable key version: ${key}`);
  const digest = hexDigest("sha256", payloadJson);
  const signature = createHmac("sha256", secret)
    .update(payloadJson, "utf8")
    .digest("hex");
  if (
    !equalHex(digest, integrity.sha256) ||
    !equalHex(signature, integrity.signature)
  )
    throw new Error(`Ledger integrity verification failed: ${key}`);
  if (expectedKey(envelope.payload) !== key)
    throw new Error(`Ledger object key does not match its payload: ${key}`);
  return { payload: envelope.payload, digest };
}

async function replay(key, payload, digest) {
  const baseUrl = required("NEXT_PUBLIC_SUPABASE_URL").replace(/\/$/, "");
  const serviceKey = required("SUPABASE_SERVICE_ROLE_KEY");
  const response = await fetch(
    `${baseUrl}/rest/v1/rpc/replay_operations_ledger_event`,
    {
      method: "POST",
      headers: {
        apikey: serviceKey,
        authorization: `Bearer ${serviceKey}`,
        "content-type": "application/json",
      },
      body: JSON.stringify({
        p_object_key: key,
        p_payload: payload,
        p_payload_sha256: `\\x${digest}`,
      }),
    },
  );
  if (!response.ok)
    throw new Error(
      `Ledger replay failed with HTTP ${response.status}: ${key}`,
    );
  return (await response.json()) === true;
}

const keys = [
  ...(await listKeys("trial-grants/v1/")),
  ...(await listKeys("deletions/v1/")),
].sort();
let imported = 0;
let duplicates = 0;
let highWater = null;
for (const key of keys) {
  const verified = await verifyObject(key);
  if (
    !highWater ||
    new Date(verified.payload.occurredAt).getTime() >
      new Date(highWater).getTime()
  )
    highWater = verified.payload.occurredAt;
  if (apply) {
    if (await replay(key, verified.payload, verified.digest)) imported += 1;
    else duplicates += 1;
  }
}

console.log(
  JSON.stringify({
    mode: apply ? "apply" : "verify-only",
    verified: keys.length,
    imported,
    duplicates,
    highWater,
  }),
);
