# XerahS Cloud web

Owner-only screenshot and screencast gallery for XIP0085. This is a Next.js 16 App Router application deployed to Vercel Sydney (`syd1`) with Supabase Auth/Postgres, hosted Stripe Billing, and a private Cloudflare R2 operations ledger.

## Local development

Requirements: Node.js 24 and Corepack. From `web/`:

```powershell
corepack prepare pnpm@10.28.2 --activate
Copy-Item .env.example .env.local
pnpm install --frozen-lockfile
pnpm dev
```

Fill `.env.local` with a non-production Supabase project. Stripe and R2 can remain unavailable while working on non-billing UI; `LEDGER_USE_LOCAL_FAKE=true` is development/Preview only. Production startup fails closed when privileged configuration is incomplete or the fake ledger is enabled.

## Verification

```powershell
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

Database migrations, RLS policies, pgTAP tests, and local Supabase configuration live in `supabase/`. Apply migrations through the Supabase CLI, never by editing the production Dashboard. The web handlers depend on the narrowly granted RPCs documented in `openapi.yaml` and the migrations.

The root workflows run the Node 24 application checks and a Docker-backed Supabase reset/pgTAP suite. Staging deployment is manual and protected until the Vercel and Supabase environment secrets are connected.

## Security model

- Every owner/API response is dynamic and `private, no-store`.
- `src/proxy.ts` refreshes Supabase sessions and emits a per-request CSP nonce.
- Owner data requires a current `aal2` session. Email verification and recent strong authentication are checked for trial and billing operations.
- Cookie-authenticated mutations require the exact configured `Origin`; desktop bearer requests are not CORS-enabled.
- Publish accepts public HTTPS metadata only, derives titles from a leaf filename, strips URL fragments, and never server-fetches media.
- Stripe Checkout and Portal use fixed server configuration. The webhook validates the raw body, signature, mode, and event allowlist before a transactional database hook.
- Trial grants and deletion tombstones are written through a durable Postgres outbox. R2 writes use `If-None-Match: *`, `Content-MD5`, canonical SHA-256/HMAC envelopes, and fail closed on collisions.

Do not enable Stripe Tax until the operating entity has approved registrations and a product tax code. Do not place service-role, Stripe, R2, HMAC, webhook, or cron secrets in `NEXT_PUBLIC_*` variables.

## Provider setup

1. Create separate development, staging, and production Supabase projects in Sydney and apply the reviewed migrations.
2. Create one Stripe Product with separate test/live monthly and annual Prices. Configure a restricted Customer Portal and signed environment-specific webhook at `/api/webhooks/stripe`.
3. Enable R2 billing, then run `node scripts/configure-cloudflare-r2.mjs` with a protected administration token and environment-specific `R2_BUCKET`. The checked-in desired state creates a private Standard/APAC bucket, disables `r2.dev`, applies the indefinite trial lock, and applies matching 180-day deletion lock/lifecycle rules. Runtime uses a separate bucket-scoped Object Read & Write token.
4. Configure Vercel environments independently, keep Preview protected, and deploy production only through protected CI.
5. Point Cloudflare DNS-only records to Vercel only after the production launch gates in XIP0085 are complete.

R2 enablement, paid provider plans, live billing, tax, production DNS, and traffic require the explicit cost/legal/tax/launch approvals in XIP0085. Once approved, the checked-in scripts and protected workflows apply and continuously verify the desired state.
