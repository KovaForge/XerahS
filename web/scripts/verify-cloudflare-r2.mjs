import assert from "node:assert/strict";
import process from "node:process";

const accountId = process.env.CLOUDFLARE_ACCOUNT_ID;
const token = process.env.CLOUDFLARE_API_TOKEN;
const bucket = process.env.R2_BUCKET;
assert(
  accountId && token && bucket,
  "Cloudflare drift-check variables are required.",
);

const headers = {
  Authorization: `Bearer ${token}`,
  "Content-Type": "application/json",
};
async function cloudflare(path) {
  const response = await fetch(`https://api.cloudflare.com/client/v4${path}`, {
    headers,
  });
  const body = await response.json();
  if (!response.ok || !body.success)
    throw new Error(`Cloudflare drift check failed (${response.status}).`);
  return body.result;
}

const [locks, lifecycle, managedDomain, customDomains] = await Promise.all([
  cloudflare(`/accounts/${accountId}/r2/buckets/${bucket}/lock`),
  cloudflare(`/accounts/${accountId}/r2/buckets/${bucket}/lifecycle`),
  cloudflare(`/accounts/${accountId}/r2/buckets/${bucket}/domains/managed`),
  cloudflare(`/accounts/${accountId}/r2/buckets/${bucket}/domains/custom`),
]);

const rules = locks.rules ?? locks;
assert(Array.isArray(rules), "Bucket Lock rules are missing.");
assert(
  rules.some(
    (rule) =>
      rule.enabled &&
      rule.prefix === "trial-grants/v1/" &&
      rule.condition?.type === "Indefinite",
  ),
  "Enabled indefinite trial lock is missing.",
);
assert(
  rules.some(
    (rule) =>
      rule.enabled &&
      rule.prefix === "deletions/v1/" &&
      rule.condition?.type === "Age" &&
      rule.condition.maxAgeSeconds >= 180 * 24 * 60 * 60,
  ),
  "Enabled 180-day deletion lock is missing.",
);
assert(
  (managedDomain.enabled ?? false) === false,
  "The r2.dev public URL must be disabled.",
);
assert(
  (customDomains.domains ?? customDomains).length === 0,
  "The ledger bucket must not have a custom domain.",
);
const lifecycleRules = lifecycle.rules ?? lifecycle;
assert(
  Array.isArray(lifecycleRules) &&
    lifecycleRules.some(
      (rule) =>
        rule.enabled &&
        rule.conditions?.prefix === "deletions/v1/" &&
        rule.deleteObjectsTransition?.condition?.type === "Age" &&
        rule.deleteObjectsTransition.condition.maxAge >= 180 * 24 * 60 * 60,
    ),
  "A matching 180-day deletion lifecycle is missing.",
);
