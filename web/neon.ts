import { defineConfig } from "@neon/config/v1";

export default defineConfig({
  functions: {
    // Crash-report ingest for the desktop Debug tab (functions/diagnostics).
    diagnostics: {
      name: "XerahS diagnostics ingest",
      source: "functions/diagnostics/index.ts",
      env: {
        // HMAC key for per-network rate limits; client IPs are never stored.
        DIAGNOSTICS_NETWORK_SECRET: process.env.DIAGNOSTICS_NETWORK_SECRET!,
      },
    },
  },
});
