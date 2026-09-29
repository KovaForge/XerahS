import { defineConfig, globalIgnores } from "eslint/config";
import nextCoreWebVitals from "eslint-config-next/core-web-vitals";
import nextTypeScript from "eslint-config-next/typescript";

export default defineConfig([
  ...nextCoreWebVitals,
  ...nextTypeScript,
  globalIgnores([
    ".next/**",
    "coverage/**",
    "infrastructure/cloudflare/scheduler/**/dist/**",
    "infrastructure/cloudflare/scheduler/worker-configuration.d.ts",
    "next-env.d.ts",
    "tooling/eslint/**",
  ]),
]);
