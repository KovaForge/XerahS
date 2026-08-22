import assert from "node:assert/strict";
import { readFile, writeFile } from "node:fs/promises";

const environment = process.env.APP_ENV;
const origin = new URL(process.env.APP_ORIGIN ?? "");
const expectedHost =
  environment === "production"
    ? "xerahs.com"
    : environment === "staging"
      ? "staging.xerahs.com"
      : null;
assert(expectedHost, "APP_ENV must be staging or production.");
assert(
  origin.protocol === "https:" &&
    origin.hostname === expectedHost &&
    !origin.port &&
    !origin.username &&
    !origin.password &&
    origin.pathname === "/" &&
    !origin.search &&
    !origin.hash,
  "APP_ORIGIN does not match the deployment environment.",
);

const configUrl = new URL("../supabase/config.toml", import.meta.url);
let config = await readFile(configUrl, "utf8");
config = config
  .replace(
    'site_url = "http://127.0.0.1:3000"',
    `site_url = "${origin.origin}"`,
  )
  .replace(
    '"http://127.0.0.1:3000/auth/callback"',
    `"${origin.origin}/auth/callback"`,
  )
  .replace(
    '"http://127.0.0.1:3000/auth/desktop/callback"',
    `"${origin.origin}/auth/desktop/callback"`,
  );
assert(
  !config.includes("http://127.0.0.1:3000"),
  "Local Auth URLs remain in rendered config.",
);
await writeFile(configUrl, config, "utf8");
