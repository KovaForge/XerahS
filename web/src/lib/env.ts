import "server-only";

import { z } from "zod";

const booleanString = z
  .enum(["true", "false"])
  .transform((value) => value === "true");

const serverSchema = z.object({
  APP_ENV: z
    .enum(["development", "preview", "staging", "production"])
    .default("development"),
  APP_ORIGIN: z.url().default("http://localhost:3000"),
  SUPABASE_SERVICE_ROLE_KEY: z.string().min(20).optional(),
  STRIPE_SECRET_KEY: z.string().min(8).optional(),
  STRIPE_WEBHOOK_SECRET: z.string().startsWith("whsec_").optional(),
  STRIPE_PRICE_MONTHLY: z.string().startsWith("price_").optional(),
  STRIPE_PRICE_ANNUAL: z.string().startsWith("price_").optional(),
  STRIPE_PORTAL_CONFIGURATION_ID: z.string().startsWith("bpc_").optional(),
  STRIPE_EXPECT_LIVEMODE: booleanString.default(false),
  STRIPE_TAX_ENABLED: booleanString.default(false),
  R2_LEDGER_ACCOUNT_ID: z.string().optional(),
  R2_LEDGER_BUCKET: z.string().optional(),
  R2_LEDGER_ACCESS_KEY_ID: z.string().optional(),
  R2_LEDGER_SECRET_ACCESS_KEY: z.string().optional(),
  LEDGER_HMAC_ACTIVE_VERSION: z
    .string()
    .regex(/^v\d+$/)
    .default("v1"),
  LEDGER_HMAC_SECRET_V1: z.string().min(32).optional(),
  LEDGER_USE_LOCAL_FAKE: booleanString.default(true),
  CRON_SECRET: z.string().min(32).optional(),
});

const publicSchema = z.object({
  NEXT_PUBLIC_SUPABASE_URL: z.url(),
  NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY: z.string().min(20),
  NEXT_PUBLIC_PASSKEYS_ENABLED: booleanString.default(false),
});

export type ServerEnv = z.infer<typeof serverSchema>;
export type PublicEnv = z.infer<typeof publicSchema>;

let cachedServer: ServerEnv | undefined;
let cachedPublic: PublicEnv | undefined;

export function getServerEnv(): ServerEnv {
  cachedServer ??= serverSchema.parse(process.env);
  return cachedServer;
}

export function getPublicEnv(): PublicEnv {
  cachedPublic ??= publicSchema.parse({
    NEXT_PUBLIC_SUPABASE_URL: process.env.NEXT_PUBLIC_SUPABASE_URL,
    NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY:
      process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY,
    NEXT_PUBLIC_PASSKEYS_ENABLED: process.env.NEXT_PUBLIC_PASSKEYS_ENABLED,
  });
  return cachedPublic;
}

export function assertProductionConfiguration(): void {
  const env = getServerEnv();
  if (env.APP_ENV !== "production") return;

  const required: Array<keyof ServerEnv> = [
    "SUPABASE_SERVICE_ROLE_KEY",
    "STRIPE_SECRET_KEY",
    "STRIPE_WEBHOOK_SECRET",
    "STRIPE_PRICE_MONTHLY",
    "STRIPE_PRICE_ANNUAL",
    "STRIPE_PORTAL_CONFIGURATION_ID",
    "R2_LEDGER_ACCOUNT_ID",
    "R2_LEDGER_BUCKET",
    "R2_LEDGER_ACCESS_KEY_ID",
    "R2_LEDGER_SECRET_ACCESS_KEY",
    "LEDGER_HMAC_SECRET_V1",
    "CRON_SECRET",
  ];
  const missing = required.filter((name) => !env[name]);
  if (missing.length > 0 || env.LEDGER_USE_LOCAL_FAKE) {
    throw new Error(
      `Production configuration is incomplete: ${missing.join(", ") || "local ledger fake enabled"}`,
    );
  }
}
