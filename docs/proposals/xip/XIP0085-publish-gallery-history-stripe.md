# XIP0085: Publish Gallery from History (xerahs.com Profile, Stripe)

**Status**: Accepted — staged implementation authorized 2026-08-22. Production launch remains blocked until every launch gate is closed.
**Priority**: Medium
**Area**: Desktop | History | Cloud | Billing | Web
**Related**: XIP0009 (History UI), XIP0083 (destination URL → history)
**Created**: 2026-08-22
**Updated**: 2026-08-22
**Version**: v0.28.0 (target)

---

## Summary

Add a **Publish** action to the existing History (and shared Toast) context menu. Publish is visible only for screenshot or screencast history items that already have a URL. Choosing Publish registers that URL in a remote XerahS gallery database. The **signed-in owner** then browses those captures on their profile (for example `https://xerahs.com/mcored/`) as thumbnail albums, similar to Google Photos. Nobody else can open the profile.

This is a **paid XerahS Cloud feature** with a one-time, application-managed **7-day trial**. First use — from Publish or from Application Settings — creates or signs in to a verified XerahS account. Email is the canonical login identifier; a unique username is the profile slug. Publishing requires either a current TOTP `aal2` session or a passkey-authenticated session. After the trial, billing is Stripe at **USD 1.99 / month** or **USD 19.99 / year**, excluding applicable tax.

Publish does **not** re-upload the full file. Destinations already produced `HistoryItem.URL`. The gallery stores metadata, a title derived from the filename, and a thumbnail reference (or a generic tile if none exists). The owner can **Unpublish** from the web app or from XerahS.

The web app is designed and implemented in this repo at `web/` (`C:\Users\Public\source\repos\KovaForge\XerahS\web\`).

## User Request

> Currently in History there is a context menu and I want a Publish entry; this entry is only visible for screenshots or screencasts (videos) that has a URL; when user presses publish, XerahS will then update a remote database with the url; this remote database is used by a web app that shows all the screenshots/screencasts published by the user in a secure user profile e.g. `https://xerahs.com/mcored/` so it can display the published screencasts/screenshots with the thumbnails like albums in Google Photos; the user profile must be secured with username+password plus 2FA or Passkeys configurable by user when user first uses the feature via Publish or via Application Settings; this is a paid feature so this feature will be paid by the user via Stripe; Stripe can be charged via monthly or yearly, monthly let's say $1.99 per month or $19.99 per year.

Product follow-up (2026-08-22):

- Only the owner can open the profile. It is a remote personal list, not a public album.
- The web app lets the owner right-click to **copy URL**, **copy markdown image URL**, and **download**.
- **Unpublish** is available on the web app and in XerahS.
- A generic tile is acceptable when no thumbnail exists; the **title must be derived from the filename**.
- Grid is **50 items per page**.
- Include a **calendar view**.
- Web app lives at `web/` in this repository.
- **7-day trial**.

## Motivation

XerahS already captures, uploads, and records a URL in history. There is no first-party place for the owner to **curate** those links into a personal gallery they can open from another machine. Users currently:

- copy URLs one by one;
- dump links into notes, Discord, or a random host;
- lose the visual album of “what I published this month.”

A paid profile gallery is a natural XerahS Cloud wedge: capture and upload stay free; **publishing a private, owner-only browsable album** is the subscription.

## Current XerahS Architecture

History already has the data and the menu surface this feature needs.

| Piece | Location | Relevant fields / behavior |
|---|---|---|
| History row | `src/desktop/core/XerahS.History/HistoryItem.cs` | `URL`, `ThumbnailURL`, `Type`, `FilePath`, `FileName`, `DateTime`, `Host` |
| Shared context menu | `src/desktop/app/XerahS.UI/App.axaml` `HistoryItemMenuFlyout` | Edit, Open, Upload, Copy Path/URL, Open URL, Delete. No Publish / Unpublish. |
| Menu context | `src/desktop/app/XerahS.UI/ViewModels/HistoryItemMenuContext.cs` | `IHistoryItemMenuTarget` exposes `URL`, file/image/annotation flags — not media kind or published state |
| History + Toast hosts | `HistoryView.axaml`, `ToastWindow.axaml` | Both bind the same flyout |
| File-type helpers | `FileHelpers.IsImageFile` / `IsVideoFile` | Used for menu visibility elsewhere |
| Secret store | `ISecretStore` | Precedent from destination plugins (tokens never in settings JSON) |
| Web tree | `web/` | Does not exist yet; this XIP creates it |

`Copy URL` / `Open URL` already hide when `URL` is empty. Publish is **stricter**: URL **and** screenshot/screencast.

There is **no** existing XerahS account, Stripe, 2FA, or passkey stack in the desktop app.

## Goals

1. Add **Publish** to the shared History/Toast context menu.
2. Show Publish only for screenshot or screencast items that have a non-empty URL and are not currently published.
3. Add **Unpublish** on the same menu when the item is published, and matching Unpublish on the web app.
4. On Publish, authenticate the user (create account / sign in / start trial or subscribe if needed) and upsert the item’s URL plus gallery metadata to the XerahS Cloud API.
5. Store gallery items so `https://xerahs.com/{slug}/` renders an **owner-only** thumbnail gallery (50 per page, calendar view, screenshot/screencast albums).
6. Web item actions: copy URL, copy markdown image URL, download.
7. Title is always derived from filename (extension stripped). Generic placeholder tile is allowed when no thumbnail URL exists.
8. Secure the profile with verified email + password, a unique username/slug, and either **TOTP MFA** or a **passkey**. Do not make the experimental passkey API the only launch path; TOTP remains the supported fallback until passkeys are accepted for production.
9. Gate publishing after a **7-day trial** on the paid-entitlement state machine backed by Stripe: **USD 1.99/month** or **USD 19.99/year**.
10. Keep capture, local save, and existing upload destinations free and unchanged.
11. Implement the web app under `web/` in this repository.

## Non-Goals (v1)

- Do not re-host the screenshot/video **full** bytes on XerahS Cloud. Publish registers the existing destination URL; web download opens that destination directly. Unpublish never deletes the remote destination object or a local file.
- Do not replace Imgur/S3/XBackBone/custom uploaders.
- Do not add Publish as an After Capture / After Upload job in v1 (explicit menu / web action only).
- Do not publish text, files, or URL-less captures.
- Do not make the profile public, unlisted, or shareable with outsiders.
- Do not build a social network (comments, likes, follows, public discovery).
- Do not implement desktop billing UI beyond Stripe Checkout / Customer Portal.
- Do not require Publish for any existing workflow.
- Do not add custom domains, family plans, or collaborative albums.
- Do not proxy, transcode, virus-scan, or cache arbitrary destination media in v1. The server stores metadata but does not fetch user-supplied media URLs.

## Product decisions (locked)

| Question | Decision |
|---|---|
| Who can open `https://xerahs.com/{slug}/`? | **Owner only.** Must be signed in as that account. Anyone else gets a sign-in wall, not a gallery. |
| What is the web app for? | Owner’s remote list of **their** published screenshots/screencasts. |
| Unpublish in v1? | **Yes**, from the web app and from XerahS History/Toast. |
| Missing thumbnail? | Generic tile is OK. **Title always comes from filename** (strip extension). |
| Pagination | **50 items per page.** |
| Calendar | **Yes** — calendar view in the web app. |
| Web location | `C:\Users\Public\source\repos\KovaForge\XerahS\web\` (repo path `web/`). |
| Trial | **7 days**, then Stripe monthly/yearly. |
| Web stack | Next.js App Router on Vercel, Supabase Auth + Postgres, Stripe Billing. |
| Login identity | Verified email is canonical; username is a unique, case-insensitive profile slug and is not an authorization key. |
| Strong authentication | TOTP at `aal2`, or a passkey-authenticated session. TOTP is the launch fallback while Supabase passkeys remain experimental. |
| Trial model | One server-clock, no-card application trial; no Stripe trial Subscription or dedicated trial Product. |
| Lapsed access | Owner reads, copy/download, and Unpublish stay available; new Publish and metadata-changing republish are blocked. |
| Media delivery | Browser loads or opens the stored HTTPS destination directly with no referrer; no server-side media/download proxy in v1. |
| Edge/DNS | Cloudflare is authoritative DNS in DNS-only mode; Vercel owns CDN, TLS, WAF, and rate limiting. |

## Proposed User Experience

### Desktop menu visibility

Add `Publish` and `Unpublish` to `HistoryItemMenuFlyout`, after `Open URL` and before the Delete separator.

**Publish** visible when **all** of:

1. `URL` is not null or whitespace.
2. The item is a screenshot **or** screencast:
   - image: `FileHelpers.IsImageFile(FilePath or FileName)` or `Type` is `Image`;
   - video: `FileHelpers.IsVideoFile(FilePath or FileName)` or `Type` is `Video` / `Screencast`.
3. The item is **not** tagged published in the local cache. The server remains authoritative and Publish is idempotent if local state is stale.

**Unpublish** visible when the item is a screenshot/screencast with a URL **and** is tagged published.

Invisible for: missing URL, text snippets, generic files, failed uploads.

Idempotent re-publish of the same URL updates metadata (title, thumbnail, timestamps) and does not duplicate gallery rows.

### First-run (Publish or Application Settings)

If the user is not signed in:

1. **Account** — create or sign in with verified email + password; choose an available username/slug.
2. **Strong authentication** — enroll and verify TOTP, or register/authenticate with a passkey when the passkey capability is enabled. Recovery codes are shown once, stored only as hashes server-side, and must be regenerated after use or suspected exposure.
3. **Trial / subscribe** — explicitly start the one-time **7-day trial** or go to Stripe Checkout (monthly USD 1.99 or yearly USD 19.99). Starting a trial requires verified email and completed strong authentication; no payment method is collected for the trial.
4. After success, complete the original Publish if that was the entry point.

If signed in but trial expired and subscription inactive: Publish opens Checkout / manage billing; do not write to the gallery.

Application Settings **XerahS Cloud** panel:

- signed-in identity and profile URL (`https://xerahs.com/{slug}/`);
- trial remaining / plan status;
- manage 2FA / passkeys;
- regenerate recovery codes and review/revoke active sessions;
- manage billing (Stripe Customer Portal);
- sign out.

### After a successful Publish

- Toast: “Published to your XerahS profile” with Open profile.
- History item tagged locally (`Tags["Published"]`).
- Web gallery shows the item (title from filename) in the grid / calendar.

### After Unpublish

- Desktop: clear the published tag; toast “Removed from your XerahS profile.”
- Web: item disappears from grid and calendar.
- The destination URL (Imgur, S3, …) is **not** deleted. Unpublish immediately removes the item from owner views and completes the physical gallery-row deletion after the operations-ledger acknowledgement.

### Web app (owner session)

Route: `https://xerahs.com/{slug}/` (example `https://xerahs.com/mcored/`). Unauthenticated or different user → sign-in, never another person’s tiles.

Views:

1. **Grid / album** — Screenshots, Screencasts, All. **50 items per page**, pager at the bottom.
2. **Calendar** — month view; days with publishes are marked; selecting a day filters the grid to that date (still 50 per page if a day is huge).

Tile:

- thumbnail if `thumbnailUrl` is usable;
- otherwise a **generic tile** (image vs video icon);
- **title = filename without extension** (never “Untitled”, never a UUID).

Item context menu (right-click):

| Action | Behavior |
|---|---|
| Copy URL | Clipboard: `HistoryItem.URL` / stored `url` |
| Copy markdown image | Clipboard: `![title](url)` using the filename-derived title |
| Download | Navigate to/open the destination URL directly. The browser may display rather than save cross-origin media; v1 does not proxy arbitrary URLs. |
| Unpublish | Confirm, then request idempotent removal; hide immediately and hard-delete after ledger acknowledgement |

Click tile → lightbox (image) or player (video) using the stored URL. All media elements use `referrerpolicy="no-referrer"`; the page never places destination URLs in analytics, logs, metadata, or preload hints.

## Proposed Architecture

```
Cloudflare DNS (DNS-only)
          │
          ▼
Vercel: xerahs.com (Next.js UI + Route Handler API) ───────► Stripe Checkout / Portal
          │                  │                                      │
          │                  ├──────────────────────────────────────┘ signed webhooks
          │                  ▼
          │          Supabase Auth + Postgres
          │                  │
          │                  └── transactional ledger outbox ──────► private Cloudflare R2 bucket
          │
          ├──── owner browser (cookie session)
          └──── XerahS desktop (Supabase OAuth 2.1 public client; bearer token)

Owner browser ── direct no-referrer media request/navigation ──► destination URL
```

### Production web platform (locked)

- A single **Next.js 16 App Router** application with React, strict **TypeScript 7.0**, Node.js 24 LTS, and a committed `pnpm-lock.yaml`. Next invokes the native TypeScript 7 CLI through `experimental.useTypeScriptCli`; because TypeScript 7 intentionally does not expose the JavaScript compiler API required by `typescript-eslint`, ESLint runs in the isolated `web/tooling/eslint` workspace against Microsoft's official `@typescript/typescript6` compatibility package. The application compiler and production build never fall back to TypeScript 6.
- **Vercel Pro** hosts the owner UI and Route Handler API. Database, Auth, Stripe, WebAuthn, and webhook routes use the default Node.js runtime; Edge Runtime is not used in v1.
- The Vercel Function region and Supabase primary region are Sydney (`syd1` / `ap-southeast-2`). Static assets remain globally distributed. Moving data regions requires a migration plan and an XIP amendment.
- **Supabase Auth** provides verified email/password and TOTP MFA. **Supabase Postgres** stores profiles, gallery metadata, entitlements, idempotency records, and audit events.
- The production origin is `https://xerahs.com`; `https://www.xerahs.com` permanently redirects to it. The desktop API is `https://xerahs.com/api/v1/...`; v1 does not introduce a separate API hostname or cross-origin credential flow.
- Owner pages, authenticated JSON, auth, billing, and webhook responses are dynamic and return `Cache-Control: private, no-store`. Only content-addressed framework assets use long-lived immutable caching.

```text
web/
  src/app/
    [slug]/page.tsx
    auth/
    oauth/consent/
    settings/
    api/v1/
    api/webhooks/stripe/route.ts
  src/lib/
  src/proxy.ts
  supabase/
    config.toml
    migrations/
    tests/
  next.config.ts
  package.json
  pnpm-lock.yaml
  vercel.json
  .env.example
  README.md
```

### Desktop

New small client, not a destination plugin:

- `XerahS.Cloud` (or `XerahS.Publish`) service in Core/UI: auth session, Publish/Unpublish, settings surface.
- Access tokens remain in memory. Refresh tokens are stored in `ISecretStore`, never in `ApplicationConfig` JSON, logs, URLs, or crash reports.
- Menu commands on `IHistoryItemMenuContext` + `HistoryViewModel` / `ToastViewModel`.
- Desktop generates a stable `clientItemId` UUID before first Publish and persists it in the history tags. Retries use the same ID and an `Idempotency-Key` header.

The Avalonia app never displays a web password form, embeds a web view, or contains an OAuth client secret. Register it as a Supabase OAuth 2.1 **public client** (`token_endpoint_auth_method=none`) and use Authorization Code + PKCE through the system browser. Supabase OAuth 2.1 Server is beta as of this review, so desktop launch is blocked on the beta acceptance gate below.

1. Generate a high-entropy `state`, nonce, and PKCE verifier/challenge (`S256`). Keep verifier/state only in memory for the pending attempt.
2. Open the Supabase `/auth/v1/oauth/authorize` URL with the registered desktop `client_id`, exact registered redirect URI, `openid email profile`, state/nonce, and PKCE challenge.
3. Supabase redirects to the XerahS authorization UI. Complete verified email/password and TOTP `aal2` (or an accepted passkey session), then show the named desktop client/scopes and approve only after the strong-session check passes.
4. Supabase returns only to the exact registered HTTPS redirect `https://xerahs.com/auth/desktop/callback`. That no-store/no-referrer page immediately relays the short-lived `code` and `state` to the registered `xerahs://oauth/callback` OS URI without exchanging or logging them. PKCE prevents a different process that intercepts the custom URI from exchanging the code.
5. Desktop validates state and receives a short-lived, single-use authorization code, never access or refresh tokens in either callback URL.
6. Exchange the code directly at Supabase `/auth/v1/oauth/token` with `client_id`, the original HTTPS redirect URI, and PKCE verifier; accept any successful 2xx response rather than hardcoding 201. No client secret is sent or stored. Verify issuer, audience/client ID, nonce, subject, expiry, `session_id`, and the tested strong-auth claim before accepting tokens.

Reject missing or mismatched state/nonce, PKCE failure, reuse, expiry, unregistered redirects, relay navigation from an unexpected origin, or unexpected issuer/audience. The callback page has no analytics or third-party scripts and clears the browser URL immediately. OAuth access tokens currently have the same underlying user-data authority as normal Supabase sessions, so RLS remains mandatory and policies may restrict the registered desktop `client_id`. Sign out revokes the current Supabase session; **Sign out all devices** revokes all sessions.

### Web + API (`web/`)

Supabase Auth owns registration, email verification, password login/reset, TOTP enrollment/challenge, passkeys, and session rotation. Do not build a parallel password database or custom `/register` and `/login` API. Email is the Auth credential; `profiles.slug` is the public-facing username.

Minimum v1 application endpoints:

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/me` | Slug, strong-auth state, trial and paid entitlement summary |
| POST | `/api/v1/trial/start` | Atomically grant the one-time seven-day trial |
| PUT | `/api/v1/items/{clientItemId}` | Idempotent Publish/upsert; owner and entitlement derived server-side |
| GET | `/api/v1/items` | Owner list: opaque cursor, limit fixed/max 50, kind and date filters |
| GET | `/api/v1/items/calendar` | Owner counts for a calendar month in the profile IANA time zone |
| DELETE | `/api/v1/items/{id}` | Idempotent Unpublish; a missing row is success; `202` means hidden and durably pending ledger acknowledgement |
| POST | `/api/v1/billing/checkout` | Create hosted subscription Checkout for allowlisted `monthly`/`annual` plan |
| POST | `/api/v1/billing/portal` | Create a Customer Portal session |
| POST | `/api/v1/account/export` | Export owner data after recent strong authentication |
| DELETE | `/api/v1/account` | Delete account after recent strong authentication and confirmation |
| POST | `/api/webhooks/stripe` | Public, signed Stripe webhook receiver |

Ledger-dependent mutations return success only after the database transaction commits. Trial start returns `202` with a bounded operation ID while ledger replication is pending and `/api/v1/me` reports `trial_pending`; it does not expose a usable trial entitlement. Unpublish returns `204` after R2 acknowledgement or `202` when the item is hidden and durable retry remains pending. Repeating the same request returns the same logical result. Pending responses include `Retry-After` and never expose an R2 object key or digest.

Publish payload (from `HistoryItem`):

```json
{
  "url": "https://i.imgur.com/….png",
  "thumbnailUrl": "https://…",
  "kind": "screenshot | screencast",
  "fileName": "screenshot-2026-08-22.png",
  "capturedAt": "2026-08-22T08:00:00Z",
  "host": "Imgur",
  "contentType": "image/png"
}
```

The client does not submit `ownerId`, `title`, trial dates, Stripe identifiers, subscription state, amount, currency, Price ID, return URL, or entitlement. `title` is generated server-side from a validated leaf `fileName`; the API never trusts a client-computed title.

The API rejects Publish and metadata-changing republish unless the caller has verified email, a recent strong session, and `can_publish=true`. Reads, export, and Unpublish remain available when billing lapses.

`GET /{slug}/` and all item/calendar endpoints require the authenticated user to own the slug. The sign-in wall must not reveal whether another slug or account exists, and no anonymous gallery JSON, metadata, Open Graph image, sitemap entry, or preload hint is generated.

### Supabase data model

All schema, grants, RLS policies, functions, Auth configuration, and seed data live in `web/supabase/`; production-only Dashboard edits are prohibited.

| Table | Schema | Required fields and constraints |
|---|---|---|
| `profiles` | `public` | `user_id` PK/FK to `auth.users`; unique case-insensitive `slug`; IANA `time_zone`; created/updated timestamps |
| `gallery_items` | `public` | UUID `id`; `owner_id`; stable `client_item_id`; exact HTTPS `url`; SHA-256 URL fingerprint; optional thumbnail URL; kind; validated leaf filename; generated title; captured/published/updated timestamps; host/content type; nullable unpublish-pending timestamp/event ID |
| `entitlements` | `app_private` | one row per user; immutable trial interval; Stripe customer/subscription/Price IDs; canonical status; paid-through/grace timestamps; dispute suspension |
| `trial_grants` | `app_private` | immutable one-time grant ledger keyed by normalized verified identity hash; no cascading FK to `auth.users` |
| `stripe_webhook_events` | `app_private` | unique Stripe event ID, type, Stripe timestamp, receive/process timestamps, bounded error state |
| `entitlement_transitions` | `app_private` | prior/result state, reason, Stripe object/event IDs, timestamps |
| `account_deletion_requests` | `app_private` | idempotency key, blocked/pending/canceling/tombstoned/deleting/completed/failed state, retry/error data, preserved billing mapping, timestamps |
| `operations_ledger_outbox` | `app_private` | immutable event ID/type, canonical versioned payload, payload SHA-256/HMAC key version, pending/leased/replicated/failed state, attempt/lease/error data, R2 key/ETag, created/replicated timestamps |
| `recovery_codes` | `app_private` | user FK with delete cascade; batch/version; keyed hash and pepper version; created/used/revoked timestamps; no plaintext code |
| `audit_events` | `app_private` | actor, action, target ID, request ID, success, bounded redacted metadata, timestamp |

Database requirements:

- `citext` slug, canonical lowercase ASCII, 3–30 characters, pattern `^[a-z0-9](?:[a-z0-9-]{1,28}[a-z0-9])?$`; reserve `api`, `auth`, `billing`, `settings`, `admin`, `login`, `logout`, `signup`, `support`, `legal`, `_next`, `assets`, `static`, and filename-like routes.
- `gallery_items` has unique `(owner_id, client_item_id)` and `(owner_id, url_sha256)` constraints. Two concurrent retries for the same URL converge to one row.
- Limit URL length to 8 KiB, thumbnail URL to 8 KiB, filename/title to 255 characters, host to 255, and content type to 127. Reject control characters and filenames containing `/` or `\`.
- Index `(owner_id, captured_at desc, id desc)`, `(owner_id, kind, captured_at desc, id desc)`, and `(owner_id, published_at desc, id desc)`.
- Use keyset pagination over `(captured_at, id)` with opaque cursors; the UI still presents 50-item previous/next pages.
- Calendar accepts a year/month and the profile IANA time zone. The server validates the zone, computes UTC boundaries, and groups only owner rows within that range.
- Trial start and entitled Publish are atomic database operations based on server UTC. No client clock or UI state grants access.
- Trial identity values use a versioned HMAC-SHA-256 over the application-defined normalization of the verified identity, never a bare email hash. Store the normalization and HMAC-key versions; do not apply provider-specific plus-address or dot rewriting. The clear identity and HMAC key never enter the R2 payload or logs.
- Recovery-code consumption is one atomic conditional update (`used_at is null` and `revoked_at is null`) returning exactly one row. Generating a new batch revokes every unused prior batch. Hash/HMAC values and plaintext codes never enter audit metadata; only batch ID, result, and timestamp are audited.

### Supabase RLS and privilege baseline

- Enable RLS on every table in an exposed schema. `profiles` owner policies compare `(select auth.uid())` with `user_id` for every permitted operation; updates use both `USING` and `WITH CHECK` so ownership cannot be reassigned. `gallery_items` owner `SELECT` additionally requires `unpublish_pending_at is null`.
- `anon` receives no profile or gallery privileges. Revoke direct `INSERT`, `UPDATE`, and `DELETE` on `gallery_items` from `authenticated` so Data API calls cannot bypass entitlement, idempotency, or the ledger outbox. Publish and Unpublish use narrowly granted transactional RPCs that derive the owner from `(select auth.uid())`, enforce current `aal2`/entitlement rules, prevent owner reassignment, and write the domain change plus outbox atomically. New tables/functions are explicitly exposed/granted if needed because Supabase projects no longer guarantee automatic Data API exposure.
- `app_private` is not exposed. Revoke schema/table/function access from `PUBLIC`, `anon`, and `authenticated` unless a narrowly documented function requires it.
- Normal web reads use a request-scoped caller JWT so RLS applies. Never keep a Supabase user client/session in module or global scope on a warm Vercel instance.
- Validate the OAuth `client_id` claim on desktop API tokens. Because Supabase OAuth custom scopes are not currently available, the desktop client receives standard user-token authority; RLS and route authorization must limit it to the same owner data and permitted operations as any other session.
- Service-role/secret access is server-only and limited to verified Stripe webhook processing, account bootstrap/deletion, and documented recovery jobs. It never appears in a browser, desktop build, generic API proxy, or `NEXT_PUBLIC_` variable.
- Any user-callable `SECURITY DEFINER` RPC pins an empty `search_path`, schema-qualifies every object, derives `auth.uid()` rather than accepting a user ID, exposes the smallest scalar result, and has `EXECUTE` revoked from `PUBLIC`/`anon` and granted only to the required role. Server-only functions remain in `app_private`. Run Supabase security/performance advisors after every schema change.
- Views are `security_invoker=true` or remain private. Authorization never uses user-editable `user_metadata`.
- Apply a restrictive strong-session policy to gallery `SELECT` and mutation RPCs, in addition to owner checks. The stable TOTP path requires `aal2`; an `aal1` owner is redirected to challenge and cannot view or Unpublish until step-up succeeds. Billing lapse does not block read/Unpublish, but authentication strength always applies. Passkeys may satisfy this policy only after the exact Supabase `aal`/`amr` contract is documented and covered by RLS/API tests.

### Thumbnails and titles

1. **Title:** validated filename minus the final extension. If `FileName` is empty, derive a safe filename from the URL path; reject Publish if neither produces a non-empty leaf name. Render title as text, never HTML.
2. **Tile image:** direct browser `<img>` for a valid HTTPS thumbnail; otherwise the destination URL only when the item is a direct image; otherwise a generic image/video tile. Video uses `preload="none"` and never autoplays.
3. **No server fetch:** never send an arbitrary item URL through a Vercel Function, Cloudflare Worker, `next/image` optimizer, metadata generator, or download proxy. `next/image` must not have wildcard user-controlled remote hosts.
4. **Safe embedding:** use only native `<img>` and `<video>` with `referrerPolicy="no-referrer"`. Never use `iframe`, `object`, `embed`, raw HTML, or `dangerouslySetInnerHTML` for destination content.
5. **External navigation:** direct HTTPS navigation or a fixed-ID redirect; always `noopener,noreferrer`. Cross-origin forced download is best effort and is not guaranteed in v1.

Accept only absolute HTTPS URLs. Reject embedded username/password credentials, control characters, and targets whose literal hostname is localhost, `.local`, loopback, link-local, private, multicast, or reserved address space. Strip fragments. The server does not DNS-resolve or fetch the URL in v1, but these checks prevent later features from accidentally inheriting obviously unsafe records. Query values are treated as secrets and never logged or sent to analytics.

Direct media loading means the destination host can observe the owner’s IP address, user agent, and request time. The UI and privacy policy disclose this; `Referrer-Policy: no-referrer` prevents disclosure of the private profile path.

Guaranteed proxy downloads or thumbnails require a later design with DNS and redirect revalidation at every hop, IPv4/IPv6 private-range rejection, byte/time/redirect limits, content-type enforcement, per-user quotas, concurrency controls, malware policy, and cost monitoring. A generic `?url=` proxy is prohibited.

### Local state

Before first Publish: generate and persist `Tags["PublishedClientId"] = uuid` so retries remain idempotent.

After a successful Publish: `Tags["Published"] = iso-timestamp` and `Tags["PublishedId"] = server id`.

After Unpublish: remove `Published` and `PublishedId`; keep the client ID so a later Publish remains stable.

Follow the existing `Favorite` / `AnnotationSidecarPath` tag pattern so SQLite/XML history does not need a schema migration.

Desktop Unpublish calls the API then clears tags on `204`, accepted-pending `202`, or already-missing success. A `202` is safe to present as removed because owner reads already exclude the pending row and durable retry owns physical deletion.

Local tags are an offline UI cache, not authorization or billing truth. A server conflict or `/me` refresh repairs them. Switching XerahS accounts invalidates the published-state cache until items are reconciled for the new owner.

### Authentication and session security

- Supabase Auth owns password hashing, sessions, TOTP secrets, and passkey public credentials. XerahS never stores or logs passwords, TOTP seeds/QR payloads, WebAuthn challenges/assertions, or recovery material.
- Email verification is mandatory before trial activation, Publish, Checkout, export, or account deletion. There is no username-to-email login lookup endpoint.
- TOTP is the guaranteed production path and the user must complete a challenge that yields `aal2`. Supabase passkeys are experimental as of 2026-08-22, so they are feature-flagged and cannot be the only recovery or launch path.
- XerahS issues ten high-entropy, single-use recovery codes after initial strong-factor enrollment. Store only salted hashes, show plaintext once, and audit use/regeneration. A recovery code plus verified email/password authorizes **factor reset only**; it never creates `aal2` or grants gallery/billing access. Recovery immediately revokes all sessions, suspends Publish, sends account notifications, waits a 24-hour cooldown, then requires password reauthentication and new TOTP enrollment/challenge before access resumes. Without a recovery code or second verified factor, support-assisted recovery uses documented identity checks, a 72-hour cooldown, global revocation, dual-control approval, and mandatory re-enrollment. Removing the last factor is prohibited outside this recovery workflow.
- Create a new `@supabase/ssr` server client per request. Protect server pages/data with `auth.getClaims()`, not the unverified user object from `getSession()`; use `getUser()` and current-session validation for billing, factor changes, export, deletion, and other destructive actions.
- Web uses the supported `@supabase/ssr` cookie model: Supabase session cookies are `Secure`, `SameSite=Lax`, and `Path=/` but intentionally **not `HttpOnly`**, because the browser client must rotate the refresh token. This makes strict CSP, dependency hygiene, no third-party script on owner/auth routes, and XSS testing launch requirements. Do not describe this as an opaque BFF session. Authenticated and session-refresh responses are `private, no-store` and are never statically generated or ISR-cached.
- `web/src/proxy.ts` creates the per-request CSP nonce and refreshes Supabase sessions. Its matcher excludes `_next/static`, `_next/image`, favicon, and static media; the nonce in request and response CSP must match. Proxy/session responses containing `Set-Cookie` are never cached.
- Desktop uses short-lived bearer access tokens and stores only the refresh token in `ISecretStore`. Sensitive operations verify the current Supabase `session_id`; deleting a user or revoking a refresh token does not instantly invalidate an already-issued JWT.
- Session policy is fixed at a 15-minute access-token lifetime, 30-day inactivity timeout, and 90-day absolute session lifetime. Concurrent web/desktop sessions are allowed in v1 and are listed with device/client and last-used time; users can revoke one or all sessions. Keep Supabase refresh-token rotation/reuse detection enabled with the recommended 10-second reuse interval. Recent-auth step-up means strong authentication within the last 10 minutes for billing, factor changes, export, deletion, and recovery.
- Cookie-authenticated mutations require non-GET methods, exact `Origin` validation, and CSRF protection. Same-origin web API does not enable CORS. Desktop bearer-token requests are not cookie-authenticated.
- HTTPS is mandatory with no TLS bypass. Resource ownership is checked on every read/write, and error bodies do not reveal another user’s resource existence.

### Stripe catalog, trial, and entitlement

XerahS uses one application-managed, no-card trial. It starts atomically only when a verified, strongly authenticated user explicitly selects **Start trial** or completes the first Publish. It lasts exactly seven days by server UTC, is granted once per verified identity/account, and is recorded in an immutable trial ledger. Deleting and recreating an account does not reset eligibility. Do not create a Stripe Customer, trial Subscription, dedicated trial Product, or payment method until conversion.

| Plan | Price | Stripe catalog |
|---|---|---|
| Trial | 7 days, no charge | Application entitlement only; not represented in Stripe |
| Monthly | USD 1.99 / month | One recurring monthly Price on Product **XerahS Cloud** |
| Yearly | USD 19.99 / year | One recurring yearly Price on the same Product |

Test and live Price IDs are separate server configuration. The client sends only the allowlisted enum `monthly` or `annual`. Prices are immutable: a price/currency/tax/interval change creates a new Price and archives the old one after deployment.

Owner reads, copy/download, export, and Unpublish remain available after trial expiry, payment failure, dispute suspension, or cancellation. New Publish and metadata-changing republish require an active trial or paid entitlement. Billing state never deletes existing gallery rows.

Paid Publish entitlement first requires exactly one Subscription item with quantity 1 and an entitlement-recognized XerahS Cloud Product/Price. Maintain separate allowlists for currently sellable Price IDs and archived legacy Price IDs that remain entitled for existing subscribers; an unrelated active Subscription on the same Customer never grants access.

Paid Publish entitlement is allowed when:

- Subscription is `active`, including an active Subscription scheduled to cancel at period end until its paid `current_period_end`; or
- Subscription is `past_due` within one 72-hour grace window starting at the first failed payment for that invoice. Retries do not extend the grace window.

`cancel_at_period_end` never grants access independently of an eligible status. `incomplete`, `incomplete_expired`, `unpaid`, `paused`, `canceled`, an expired grace window, or an open dispute do not allow Publish. A later successful invoice restores access when the Subscription is otherwise eligible. Customer Portal v1 permits payment-method updates, invoice access, and cancellation at period end. Immediate cancellation and plan switching remain disabled until refund, credit, proration, and effective-date behavior are specified and tested.

Configure Stripe Billing to use Smart Retries for at most seven days and then **cancel** the Subscription if recovery fails; do not leave it indefinitely `past_due` or `unpaid`. The application’s Publish grace remains 72 hours even while Stripe continues later retries.

### Stripe Checkout and Portal

- Checkout and Portal creation are authenticated, recent-strong-auth, CSRF-protected, rate-limited `POST` requests. `GET` never creates a Stripe resource.
- Maintain exactly one `stripe_customer_id` per XerahS user and enforce one relevant Subscription with database uniqueness/locking. Never associate webhook objects by email.
- Checkout uses `mode=subscription`, quantity 1, one server-selected Price, the existing Customer, fixed allowlisted success/cancel URLs, internal user ID in `client_reference_id`/metadata, and a persisted Stripe idempotency key.
- Omit `payment_method_types` so Stripe dynamic payment methods apply. On API version `2026-03-25.dahlia` or later, include an `integration_identifier` whose tracking label ends with eight random letters.
- The Checkout success redirect is display-only and polls `/api/v1/me`; it never grants entitlement.
- Portal sessions use a fixed environment-specific Portal configuration and fixed return URL.
- Pin the tested Stripe API version and SDK and upgrade deliberately. As of this review the current API version is `2026-07-29.dahlia`; instantiate `StripeClient` rather than using deprecated global API-key configuration.

### Stripe webhooks and reconciliation

Stripe sends to `POST https://xerahs.com/api/webhooks/stripe`. The route remains publicly reachable and is excluded from custom login challenges and application rate limits; signature verification is mandatory.

1. Read the unmodified raw body once and verify `Stripe-Signature` with the environment-specific webhook secret before JSON parsing.
2. Reject signature or `livemode` mismatch. Test/staging/live have separate endpoints and signing secrets.
3. Check `event.id` under a unique constraint. A duplicate is a successful no-op only when the prior record is `processed`; a `pending` or `failed` record is reclaimed and retried.
4. Retrieve any canonical Stripe objects needed for an unordered event, then commit the event record, entitlement snapshot, transition audit, and `processed` state in one database transaction. On failure, return 5xx so Stripe retries; repeated failures enter an alerted dead-letter state that reconciliation can safely replay. Webhooks are at-least-once and unordered.
5. Validate the known Customer/Subscription mapping. For ambiguous or out-of-order subscription/invoice events, retrieve current Stripe objects and recompute the local snapshot; a late event cannot overwrite newer state.
6. Return 2xx for already processed or intentionally ignored events and 5xx only for retryable persistence failures. Do no media fetch or unrelated work.
7. Run nightly reconciliation against Stripe so a missed webhook cannot leave indefinite incorrect access.

Allowlisted events include Checkout completion/expiration/async success/failure, Subscription create/update/delete, invoice paid/payment failed/payment action required/finalization failed, and dispute created/closed. Add pause/resume events only if pause is enabled. `checkout.session.expired` clears the matching pending-Checkout guard so a later attempt can proceed. `invoice.paid` grants or renews paid access; payment failure starts the single grace window; Subscription state governs cancellation and period end. Checkout completion links objects but does not by itself prove durable entitlement.

On `charge.dispute.created`, suspend new Publish and flag review while preserving reads/export/Unpublish. Restore only if the dispute closes in **XerahS/the merchant’s favor** and the Subscription is otherwise entitled. Refund behavior follows the published refund policy; a refund event alone does not silently change cancellation policy.

### Tax and merchant obligations

Advertised prices are USD 1.99 and USD 19.99 **excluding applicable tax**. Configure Prices with explicit `tax_behavior=exclusive`. The operating entity/tax adviser must select the Product tax code from Stripe’s canonical list; do not guess or hardcode a remembered code.

Enable Stripe Tax only after the live Stripe account has a valid head-office address and active registration in every jurisdiction where XerahS must collect. Once approved, subscription Checkout sets `automatic_tax.enabled=true`; returning-customer Checkout uses `customer_update.address=auto` and collects a fresh resolvable address where required so tax is not calculated from stale Customer data. `automatic_tax` alone collects nothing where no registration is active, and it must not be combined with manual tax rates. Support tax-ID collection if B2B sales are offered. Stripe Tax calculation does not replace registration, filing, remittance, or legal advice.

Production launch is blocked until the operating entity, merchant of record, registration obligations, tax display, invoice identity, refund/cancellation policy, filing process, Terms, and Privacy notice are approved.

### Secrets and environment separation

- Prefer a least-privilege Stripe restricted key (`rk_`) where supported; store it as a Vercel sensitive server variable or synchronized vault secret. Use separate keys, webhook secrets, Products, Prices, Portal configurations, SMTP credentials, and Supabase projects per environment.
- The browser and desktop receive only the Supabase URL and publishable key. Supabase service-role/secret keys and all Stripe secrets are server-only and never use `NEXT_PUBLIC_`.
- `.env.example` contains variable names only. `.env.local` and downloaded environment files are gitignored. Secret scanning covers Supabase secrets, `sk_`, `rk_`, and Stripe webhook-secret patterns.
- The production ledger writer uses a Cloudflare **Account R2 API token** with `Object Read & Write` scoped to the single production bucket and the S3-compatible endpoint (`region=auto`). Cloudflare does not offer a write-only bucket role, so this read/write capability is explicit and is constrained by Bucket Lock. Restore tooling uses a distinct bucket-scoped `Object Read only` token. A separate account-level R2 administration token configures locks/lifecycles and is available only to protected provisioning/break-glass automation, never application runtime. None of these credentials is exposed to browsers/desktops or shared with Cloudflare DNS automation.
- Server configuration names the ledger as `R2_LEDGER_ACCOUNT_ID`, `R2_LEDGER_BUCKET`, `R2_LEDGER_ACCESS_KEY_ID`, `R2_LEDGER_SECRET_ACCESS_KEY`, `LEDGER_HMAC_ACTIVE_VERSION`, versioned ledger-HMAC secrets, and `CRON_SECRET`. Only names and non-secret examples appear in `.env.example`; production/staging values are isolated Vercel sensitive variables or synchronized vault references. The endpoint is derived from the account ID and is not client-configurable.
- Never log authorization headers, cookies, tokens, callback codes, TOTP/passkey data, Stripe signatures/payloads, complete media URLs, or URL query strings.
- Document emergency rotation for Supabase secret keys/JWT signing keys, Stripe keys/webhook secrets, SMTP credentials, R2 writer/reader tokens, ledger HMAC keys, recovery-code peppers, and audit-hashing salts. Ledger HMAC-key rotation retains prior verification keys for every retained object; pepper rotation retains the version needed to validate still-active batches until they are regenerated/revoked. Require strong MFA/passkeys for Vercel, Supabase, Cloudflare, Stripe, source-control, and registrar administrators.

Stripe is authoritative for paid billing. The application database/server clock is authoritative for the no-card trial. The local entitlement snapshot is updated by verified webhooks and reconciliation; the desktop and Checkout redirect are never authoritative.

### Vercel and Cloudflare boundary

In the request-delivery path, Cloudflare is the authoritative **DNS provider only**. Records terminating at Vercel are DNS-only (grey cloud) and use the exact verification/destination values shown by Vercel. Enable DNSSEC, registrar lock, protected registrar MFA, and least-privilege scoped API tokens. Cloudflare Cache Rules, Workers, WAF, Bot Fight Mode, challenges, and rate limiting do not sit in front of Vercel. The independently locked R2 deletion/trial ledger is an operations datastore, not a web reverse proxy or media store.

A dedicated Cloudflare Cron Worker may originate authenticated calls to the internal ledger-dispatch, account-deletion, and Stripe-reconciliation routes when the Vercel plan does not support the required minute-level schedules. It holds only the shared `CRON_SECRET`, uses an exact allowlist of the three staging or production paths, follows the environment-specific `APP_ORIGIN`, and is never attached to the application hostname or request-delivery path. Its configuration, generated binding types, observability, and schedules are versioned under `web/infrastructure/cloudflare/scheduler`; the secret is set independently in Cloudflare and Vercel and rotated together.

Vercel owns TLS termination, CDN, automatic DDoS mitigation, WAF, Bot Protection, application delivery, and client-IP enforcement. This avoids a double-CDN reverse proxy that obscures client IPs, weakens Vercel firewall visibility, complicates cache invalidation, and can break WebAuthn, auth callbacks, or Stripe webhooks.

If Cloudflare proxying is later required, it needs a separate approved design using Vercel Verified Proxy, exact cache bypasses for `/.well-known/vercel/*`, auth, API, and webhook routes, real-client-IP validation, challenge exclusions, and origin protection. It must never be enabled as an incidental DNS change.

### Cloudflare R2 operations-ledger contract

The operations ledger is a private, Standard-class R2 bucket in a dedicated Cloudflare account/security boundary. Disable the `r2.dev` public URL, do not attach a custom domain or Worker, and do not store application media. R2 supplies automatic AES-256 encryption at rest and TLS in transit; the XIP does not claim customer-managed encryption or regulatory WORM certification.

Configure and version these prefix rules as infrastructure:

- `trial-grants/v1/`: indefinite Bucket Lock and no expiry lifecycle. This is required by the one-trial-per-verified-identity rule. Legal/privacy approval must explicitly accept indefinite retention of the keyed pseudonymous digest; otherwise the product rule and this XIP must change before launch.
- `deletions/v1/`: age-based Bucket Lock for at least `max(oldest_restorable_backup_age, Supabase backup/PITR age) + 90 days`, with a 180-day minimum. A matching lifecycle may expire objects only after that lock duration. The resolved production day count is checked into infrastructure configuration and the retention register before launch.

Bucket Lock applies to existing and new matching objects, blocks deletion/overwrite during retention, and takes precedence over lifecycle expiry. It is nevertheless an administratively removable bucket configuration, not an irrevocable legal hold. Keep the administration token out of Vercel, require dual-control administrator access, export the desired rules as reviewed configuration, and check them at least daily with read-only configuration access. Any missing, disabled, shortened, or broadened rule is a page-worthy incident that blocks new trial activation and deletion finalization until repaired.

Ledger writes use this protocol:

1. The same Postgres transaction that records a trial grant, requested Unpublish, or account deletion inserts one immutable `operations_ledger_outbox` row. Event types are `trial_grant_created`, `gallery_item_unpublished`, and `account_deleted`.
2. The canonical JSON payload contains only `schemaVersion`, UUID event ID, event type, server timestamp, the minimum internal row/user IDs needed for replay, and the versioned identity HMAC where applicable. It contains no email, slug, URL, filename, IP address, Stripe payload, or bearer credential. Sign the canonical payload with a versioned HMAC key held outside R2 and include its SHA-256 checksum.
3. A protected dispatcher makes an immediate attempt and a scheduled retry job reclaims expired leases with `FOR UPDATE SKIP LOCKED`. It writes `deletions/v1/YYYY/MM/DD/<event-id>.json` or `trial-grants/v1/YYYY/MM/DD/<event-id>.json` through the S3-compatible API using `If-None-Match: *` and `Content-MD5` for transport integrity; the signed canonical body carries the stronger SHA-256 digest. `412 PreconditionFailed` is success only when a read-back has the expected SHA-256 digest/signature; any mismatch is a security incident.
4. After a verified write, the dispatcher stores the object key, ETag, digest, and `replicated_at`. Trial entitlement does not become active, an Unpublish-pending row is not physically deleted, and account deletion does not delete the Auth user until this acknowledgement exists. Owner queries hide Unpublish-pending rows immediately; temporary R2 failure therefore produces a retryable pending operation rather than resurrectable data.
5. The durable outbox is the queue of record. The scheduled dispatcher must run at least once per minute, alert on the oldest pending age and repeated failure, and be safe to invoke concurrently. Do not rely on an in-memory callback or an unpersisted Vercel background task.

Restore runs with public traffic and all mutating jobs disabled. It paginates the entire R2 ledger, verifies the prefix/schema/key, checksum, and HMAC before staging rows by unique event ID, rejects unknown versions, then applies item/account deletion events and imports trial-grant digests before Stripe reconciliation. Only after counts and high-water marks reconcile may traffic reopen. A corrupt, unreadable, or incomplete ledger fails the restore closed and requires the recovery runbook; it is never silently skipped.

### Environments and deployment protection

| Environment | Vercel access | Supabase | Stripe | R2 ledger | WebAuthn |
|---|---|---|---|---|---|
| Production | Public origin; application auth enforced | Dedicated production project | Live mode | Dedicated locked production bucket/account | RP ID `xerahs.com`, exact origin `https://xerahs.com` |
| Stable staging | Public `staging.xerahs.com`; application auth and WAF enforced; noindex | Dedicated staging project | Test mode with public signed staging webhook | Separate non-production bucket and credentials; same protocol with shorter documented retention | RP ID/origin `staging.xerahs.com` in a separate credential namespace |
| Ephemeral PR Preview | Vercel Authentication; CI uses automation-bypass secret; noindex | Isolated preview branch/project with synthetic data | Signed fixtures only; no inbound Stripe endpoint | In-memory/local fake only; no Cloudflare credential | Passkeys disabled; no arbitrary `*.vercel.app` origin |
| Development | Local only | Local or dedicated development project | Stripe CLI/test mode | Local fake or dedicated development bucket only | Registered loopback development origin only |

Non-production deployments never receive production Supabase service credentials, Stripe live secrets, production signing keys, SMTP secrets, or production WebAuthn credentials. Production custom domains attach only to production deployments. Production and stable staging cannot be behind Vercel Deployment Protection because desktop/staging API traffic and Stripe webhooks must be reachable; application authentication, signature verification, and Vercel WAF protect them. Ephemeral Previews remain protected and use fixtures for external integrations.

The WebAuthn RP ID is a permanent credential boundary. Do not change `xerahs.com` after passkey enrollment or accept arbitrary `*.vercel.app` origins.

### Security headers and browser protections

All applicable responses send:

- `Strict-Transport-Security: max-age=31536000; includeSubDomains` after confirming every active subdomain supports HTTPS. HSTS preload is a separate operational decision.
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: no-referrer`
- `X-Frame-Options: DENY`
- `Cross-Origin-Opener-Policy: same-origin`
- `Permissions-Policy` denying camera, microphone, geolocation, payment, USB, and other unused capabilities; public-key credential create/get is limited to self.

Deploy CSP in Report-Only on staging, review violations, then enforce before launch. Baseline:

```text
default-src 'self';
base-uri 'none';
object-src 'none';
frame-ancestors 'none';
form-action 'self';
script-src 'self' 'nonce-{per-request}' 'strict-dynamic';
style-src 'self' 'nonce-{per-request}';
img-src 'self' data: blob: https:;
media-src 'self' blob: https:;
font-src 'self';
connect-src 'self' https://<project>.supabase.co wss://<project>.supabase.co <explicit-observability-origins>;
worker-src 'self' blob:;
manifest-src 'self';
upgrade-insecure-requests;
```

No `unsafe-eval`, wildcard script/frame source, inline event handler, or unsanitized HTML is allowed. Hosted Stripe Checkout and Portal are redirects, so Stripe script/frame origins are unnecessary unless the integration changes.

### Abuse prevention and rate limits

Vercel WAF is the edge plane. New rules progress through log-only, enforced Preview, logged Production, then enforced Production after traffic review. The application also enforces durable per-account/user limits and returns `429` with `Retry-After`.

| Route class | Initial edge limit per IP | Application limit |
|---|---:|---:|
| XerahS auth/consent/recovery UI routes | 30 / 10 min | 10 / account / 10 min where application-controlled |
| Trial start | 10 / hour | 3 / verified account / hour; immutable one-time grant remains authoritative |
| Checkout/Portal | 20 / 10 min | 5 / user / 10 min |
| Publish/Unpublish | 300 / 5 min | 60 / user / min |
| List/calendar | 600 / 5 min | 120 / user / min |
| Export/delete/factor changes | 20 / hour | 3 / user / hour plus recent strong-auth step-up |

Browser `supabase-js` Auth calls go directly to `<project>.supabase.co` and therefore bypass Vercel WAF. They are protected by configured Supabase Auth rate limits, CAPTCHA, email controls, and application recovery cooldowns; Vercel limits apply only to `xerahs.com` routes. The exact Stripe webhook path is excluded from every custom or managed challenge, Bot Protection rule, and rate limit while retaining Vercel platform DDoS mitigation. Attack Mode cannot be enabled without preserving that exception or intentionally pausing and later replaying Stripe events. Edge rate limiting is not exact or globally transactional; database-backed account rules remain authoritative. Use hosted Checkout and Stripe Radar; XerahS never handles card numbers.

### Data lifecycle, privacy, and recovery

- Profile and gallery metadata live for the account lifetime. Unpublish atomically marks the row pending and creates its ledger-outbox event; owner reads hide it and application caches invalidate immediately. The dispatcher hard-deletes the row only after the R2 acknowledgement. It never deletes the destination object.
- Account export requires recent strong authentication and includes profile/gallery metadata, subscription summary, security-method metadata, and legally disclosable audit history. It must not expose TOTP seeds, passkey credential material, token hashes, or secret operational fields.
- Account deletion is an idempotent outbox state machine, not one synchronous transaction. `DELETE /api/v1/account` immediately marks the account `deletion_pending`, blocks access, revokes sessions, and returns an operation ID. A worker retries Stripe cancellation, obtains the independently retained R2 tombstone acknowledgement, preserves the minimum statutory Customer/Subscription/cancellation mapping, deletes the Auth user, cascades profile/gallery data, and marks completion. Auth deletion cannot run until cancellation succeeded or is durably queued with retry/alert and the R2 tombstone is acknowledged; never orphan a charging Subscription. Target completion is 24 hours and the published maximum is 30 days except legally retained billing records.
- Replicate account-deletion and Unpublish tombstones plus pseudonymous trial-grant HMACs through the transactional outbox into the retention-protected **Cloudflare R2** operations ledger defined above. This ledger is outside the production Supabase PITR boundary and is replayed after restore. Application media is never stored in this bucket.
- Retain raw Stripe webhook payloads for at most 30 days, then purge the payload and retain only minimal event/type/result identifiers. Suggested security audit retention is 90 days. Billing/tax records follow the operating entity’s statutory schedule.
- Deleted data may remain in encrypted backups until backup expiry. Restore procedures must replay both account and item tombstones before reopening traffic so deleted users/items are not resurrected.
- Use a paid Supabase plan with PITR when targeting **RPO ≤ 5 minutes / RTO ≤ 4 hours**. Run a quarterly isolated restore drill covering Auth, RLS, entitlements, Stripe reconciliation, and deletion replay.
- Back up Supabase Auth/configuration, Vercel project configuration, Cloudflare DNS, Stripe catalog/Portal/webhook configuration, and SMTP configuration as code or encrypted runbook data; a database backup alone cannot rebuild the service.

### Observability and incident controls

Production requires structured JSON runtime logs, Sentry or equivalent error tracking with source maps, a durable Vercel log/trace drain where supported, performance monitoring, and synthetic checks for the sign-in wall, authenticated list, and shallow `/api/healthz`.

Logs may include timestamp, environment, deployment/commit, route template, status, duration, Vercel request ID, correlation ID, stable internal user ID when necessary, Stripe event ID/type, Customer/Subscription IDs, and bounded error code. Do not log raw paths containing slugs, emails, filenames, destination URLs, query strings, credentials, cookies, tokens, MFA/passkey material, or Stripe payloads.

Alert on elevated 5xx/latency, authentication failure spikes, RLS denials, service-role use outside allowlisted jobs, webhook signature/processing failures, oldest unprocessed event, dead-letter count, R2 ledger outbox age/failures, Bucket Lock or public-access drift, reconciliation mismatches, payment failures, grace expiry, disputes, duplicate billing objects, backup failures, deletion failures, and WAF limit volume.

Operational kill switches can independently block new trial grants, Checkout creation, and Publish without disabling owner reads, export, or Unpublish. Runbooks cover compromised-key rotation, webhook backlog/replay, entitlement mismatch, duplicate charge, database failover/restore, passkey rollback to TOTP, Vercel rollback, Cloudflare DNS rollback, and privacy incident response.

## Application Settings

New **XerahS Cloud** group (all desktop platforms):

- Sign in / Create account
- Profile URL (read-only + copy)
- Security: TOTP status; feature-flagged passkeys; recovery/regeneration; active-session review and revoke
- Subscription: trial days left, plan, Manage billing, Subscribe
- Export account data
- Delete XerahS Cloud account
- Sign out
- Sign out all devices

First Publish with no session launches the Supabase OAuth 2.1 system-browser flow and resumes the original action only after PKCE token exchange and `/api/v1/me` confirm the registered desktop client, strong authentication, and entitlement.

## Repository Implementation Snapshot (2026-08-23)

The staged repository implementation is complete through the code and automation boundary: desktop History/Toast actions, durable local identity, system-browser OAuth with PKCE and protocol activation, `/api/v1/me` verification, Application Settings integration, owner-only web gallery/calendar, OAuth consent and denial relay, TOTP plus feature-gated WebAuthn, recovery-code generation, trial and Stripe Checkout/Portal/webhook reconciliation, RLS/idempotency/outbox migrations, deletion workers, private R2 ledger verification/restore tooling, health checks, drift checks, and protected staging/production deployment workflows. The web application compiles with stable TypeScript 7.0 while ESLint remains isolated on the official TypeScript 6 compatibility package.

The dedicated staging Supabase project and migrations, OAuth public client, Stripe sandbox Product/Prices/Portal/webhook, Vercel staging project and environment variables, `staging.xerahs.com` DNS-only record, TLS deployment, and outbound Cloudflare Cron Worker were provisioned on 2026-08-23. The deployed health endpoint and a correctly signed non-mutating Stripe webhook probe returned HTTP 200. Live-mode Stripe, the apex production origin, and production traffic were not changed.

Cloudflare still rejects R2 API access until an account administrator enables the R2 subscription; its Dashboard activation control is unavailable to the current operator. Stable staging therefore remains explicitly incomplete: the deployed app runs with `APP_ENV=preview` and the local fake ledger so UI, Auth, and sandbox billing integration can be exercised, while durable trial, Unpublish, and account-deletion acceptance remains blocked. It must switch to `APP_ENV=staging`, `LEDGER_USE_LOCAL_FAKE=false`, and bucket-scoped staging R2 credentials before staging can satisfy the fail-closed configuration and ledger gates.

The feature remains fail-closed and disabled by default. This snapshot does **not** assert that all external infrastructure is complete or that production is launched. R2 enablement and credentials, recovery consumption/notification drills, WebAuthn acceptance, tax/legal approval, and every Production Launch Gate below still require their recorded owner approval and live verification. No code commit, staging deployment, or successful build may be used as a substitute for those gates.

## Implementation Phases (after acceptance)

### Phase 1 — Menu affordance (desktop-only, no network)

Publish / Unpublish visibility, stable local client ID, command stub, and local-state tests.

**Key files:**

- `src/desktop/app/XerahS.UI/App.axaml`
- `src/desktop/app/XerahS.UI/ViewModels/HistoryItemMenuContext.cs`
- `src/desktop/app/XerahS.UI/Converters/HistoryItemMenuContextConverter.cs`
- `tests/XerahS.Tests/Services/HistoryItemMenuContextTests.cs`

### Phase 2 — Platform, database, and authentication

Create the pinned `web/` app, local/preview/production environment separation, Supabase migrations/RLS tests, verified email/password, TOTP `aal2`, browser SSR sessions, desktop PKCE handoff, audit events, headers, CSP, transactional operations-ledger outbox, R2 bucket configuration, and local R2 test double. Keep passkeys disabled until the production acceptance test is met.

### Phase 3 — Trial and Stripe Billing

Implement the one-time trial grant with ledger acknowledgement, Stripe Product/Prices, hosted Checkout, Portal configuration, signed/idempotent webhook processing, entitlement state machine, dispute behavior, reconciliation, tax configuration, and billing observability.

### Phase 4 — Publish / Unpublish API + desktop client

Implement idempotent item upsert, pending-to-ledger-to-hard-delete Unpublish, URL validation, history tags, account-switch reconciliation, token storage, retry behavior, and toasts.

### Phase 5 — Web owner UI and settings

Owner-only 50-item keyset-paged grid, calendar, filename titles, generic tiles, safe direct media rendering, context menu, TOTP/passkey/session settings, billing settings, export, and deletion.

### Phase 6 — Production hardening and launch

WAF rollout, rate limits, R2 lock/public-access drift monitor, outbox dispatcher, backup/ledger restore drill, secret rotation drill, accessibility and browser tests, privacy/legal/tax approval, sandbox lifecycle tests, production smoke purchase, rollback drill, support runbooks, and go-live sign-off.

## Automated Verification

### Desktop

1. Publish hidden when URL empty.
2. Publish hidden for text/file items even with a URL.
3. Publish visible for image + URL and video + URL when not published.
4. Unpublish visible only when published.
5. History and Toast both surface the commands.
6. Secret store holds tokens; settings JSON has no password/refresh token.
7. API client tests with an injectable handler (no live Stripe in CI).
8. Idempotent re-publish of the same URL does not duplicate rows.
9. Title helper: `screenshot-2026-08-22.png` → `screenshot-2026-08-22`.
10. Desktop callback rejects state/nonce/PKCE mismatch, replay, expiry, hostile redirect, issuer/audience/client mismatch, and tokens in URLs/logs.
11. Access token remains in memory; refresh token is stored only in `ISecretStore` and is removed/revoked on sign-out.
12. Switching accounts invalidates/reconciles local published state before enabling Unpublish.
13. Retries reuse `PublishedClientId` and the same idempotency key.

### Supabase / API authorization

1. Unauthenticated GET of `/{slug}/` or `/api/v1/items` does not return items.
2. User A cannot list or mutate user B’s items.
3. pgTAP/RLS matrix covers `anon`, user A `aal1`, user A `aal2`, user B `aal2`, active/expired trial, active/lapsed subscription, and removed factor; `aal1` cannot select or invoke gallery mutations.
4. Owner ID cannot be reassigned; direct Data API gallery DML is denied, and the narrowly granted RPCs cannot bypass ownership, strong-auth, entitlement, idempotency, or outbox checks.
5. `limit` default/max is 50; cursors cannot cross owner/filter scope.
6. Calendar returns only owner counts for validated month/time-zone boundaries.
7. Unpublish hides the row and invalidates cache atomically, writes the ledger event, then hard-deletes after acknowledgement; a repeated DELETE converges to the same success/pending state.
8. Expired/lapsed accounts can read, export, and delete but cannot insert/update.
9. Two concurrent PUTs for the same owner/client ID or exact URL produce one row.
10. Trial start is server-clock atomic and cannot be replayed by concurrent requests, account recreation, or client-submitted dates.
11. Factor removal, global sign-out, account deletion, and stale access-token cases enforce current session state.
12. Recovery codes never mint `aal2`; factor reset revokes sessions, enforces cooldown/notification, and requires new TOTP enrollment/challenge. Support-assisted recovery exercises dual control and audit.
13. Web and desktop tests enforce 15-minute JWT, 30-day inactivity, 90-day maximum lifetime, refresh-token rotation/reuse behavior, recent-auth window, and per-session/global revocation.
14. SSR cross-user tests prove HTML/JSON/`Set-Cookie` responses are `private, no-store` and never leak across sessions; `proxy.ts` refresh and CSP nonces match without affecting static assets.
15. Account deletion outbox survives Stripe timeout, retries idempotently, never orphans billing, completes within policy, and writes/replays the independently locked R2 tombstone.
16. Trial, Unpublish, and account deletion remain pending until the expected R2 object is acknowledged; duplicate dispatch is idempotent, a mismatched existing object fails closed, and concurrent dispatchers do not process one lease twice.

### Web UI and media safety

1. Markdown copy format is `![filename-without-ext](url)` with correct escaping.
2. Titles render as text; filenames cannot inject HTML/script.
3. `next/image`, API routes, metadata generation, and Workers cannot fetch arbitrary item URLs.
4. Non-HTTPS, credential-bearing, control-character, localhost, `.local`, and literal private/reserved URLs are rejected; fragments are removed.
5. URL query values never appear in logs, analytics, errors, metadata, or referrers.
6. Remote image/video requests send no referrer; video does not autoplay or preload; arbitrary URLs never enter `iframe`, `object`, or `embed`.
7. Owner/API responses are `private, no-store`; hashed static assets remain immutable-cacheable.
8. CSP is enforced with no unexplained staging violations; CSRF/Origin tests cover every cookie-authenticated mutation.
9. Accessibility covers keyboard alternatives to right-click, focus management, reduced motion, screen-reader labels, and 200% zoom.

### Stripe Billing

Use Stripe sandbox/Test Clocks and signed test webhook fixtures; CI never calls live mode.

1. Monthly and annual Checkout select only server allowlisted Prices; concurrent requests do not create duplicate Customers/Subscriptions.
2. Checkout success redirect never grants access before the verified entitlement snapshot changes.
3. Invalid signature, live/test mismatch, duplicate delivery, out-of-order delivery, and transient database failure behave correctly.
4. Initial async success/failure, Checkout expiry/retry, `incomplete_expired`, invoice paid, renewal failure, 72-hour grace, seven-day dunning/cancellation, recovery, and cancellation at period end match the state table.
5. Dispute suspends Publish without deleting rows; eligible closed dispute restores only when billing otherwise allows it.
6. Reconciliation repairs a deliberately dropped webhook and alerts on mismatches.
7. Active and missing tax-registration cases and invalid customer location are tested before Stripe Tax is enabled live.
8. Test and live keys, Price IDs, Portal configurations, and event endpoints cannot be mixed.

### Infrastructure and operations

1. Cloudflare records terminating at Vercel are DNS-only; DNSSEC and registrar protections are verified.
2. Ephemeral Preview is protected/noindexed and cannot access production Supabase, Stripe, SMTP, or WebAuthn configuration; stable staging uses its exact origin and public signed test webhook.
3. Production/staging webhooks remain reachable without application login, Bot Protection, Attack Mode challenge, or custom rate limit and reject invalid signatures.
4. WAF rules are tested in log mode and Preview before production enforcement; limits return `429` and `Retry-After`.
5. Security headers, cache headers, synthetic checks, log redaction, alerts, and kill switches are exercised.
6. R2 has no public URL/custom domain/Worker; writer, reader, and administration credentials have the specified separation; prefix lock/lifecycle rules and their daily drift alert are verified.
7. Quarterly restore with traffic closed verifies every ledger object's key/schema/checksum/HMAC, rejects a corrupt/unknown event, replays account and item deletions plus trial grants, reconciles high-water marks and Stripe entitlements, and meets RPO/RTO before reopening.
8. Secret rotation and known-good Vercel/Cloudflare rollback drills complete without data loss or prolonged billing mismatch; production deployment tests acknowledge immediate alias assignment and trigger automatic rollback on failure.

## CI/CD and rollback

CI is the sole production deployment path; disable duplicate automatic production deployments.

Pull requests run frozen-lockfile install, lint, formatting check, strict type check, unit/integration tests, `next build`, dependency/secret scanning, Supabase local reset, pgTAP, database lint/advisors, generated-type drift checks, and Playwright smoke tests against a protected Preview using only non-production services.

Production CI pins Node, pnpm, and Vercel CLI versions; pulls the production environment; builds once with `vercel build --prod`; and, after protected-environment approval, runs `vercel deploy --prebuilt --prod`. This assigns the production alias immediately; do not promote a Preview built with non-production secrets. Kill switches are engaged during risky migrations, post-deploy smoke tests start immediately, and failed health/error thresholds automatically roll back to the recorded known-good production deployment. Verify the sign-in wall, authenticated item list, Stripe webhook reachability, security/cache headers, synthetic checks, and error logs.

Database migrations are forward-only and use expand/migrate/contract sequencing: additive migration first, compatible application deployment second, destructive cleanup in a later release. A Vercel rollback must remain compatible with the current database; do not assume a destructive down-migration is safe. Record the known-good deployment ID and use `vercel rollback <known-good-deployment>` when health/error thresholds fail.

Pin Supabase/Stripe/Next.js packages and commit lockfiles. Create migrations with the Supabase CLI; apply the same reviewed migration to local, Preview/staging, then production. No production schema change is made manually in the Dashboard.

## Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Publish vs re-upload | Register existing URL only | Destinations already host bytes |
| Audience | Owner-only, signed in | Personal remote library, not a public site |
| Unpublish | Desktop + web | User asked for both |
| Title | Filename without extension | User rejected generic titles |
| Missing thumbnail | Generic tile | User accepted placeholder art |
| Page size | 50 | Stated |
| Calendar view | Yes | Stated |
| Web home | `web/` in this repo | Stated |
| Web stack | Next.js App Router on Vercel; Node runtime | Concrete, same-origin UI/API with supported server dependencies |
| TypeScript | TypeScript 7 native CLI; isolated TypeScript 6 compatibility API for ESLint only | Uses the production-ready native compiler while respecting the current `typescript-eslint` compiler-API boundary |
| Data/Auth | Supabase Auth + Postgres with RLS | Managed verified email, TOTP, sessions, relational ownership, backups |
| Trial | One no-card app-managed seven-day grant | Avoids contradictory Stripe trial models and unwanted charges |
| Menu vs After Capture | Menu-only in v1 | Explicit curating |
| Same flyout as Toast | Yes | One `HistoryItemMenuFlyout` |
| Billing catalog | One Stripe Product, monthly/yearly recurring Prices | Same plan with interval variants |
| Billing UX | Hosted Checkout + restricted Portal | Lowest PCI scope and less custom billing code |
| Login | Verified email/password; username is slug | Supabase v1 compatibility; avoids username lookup leakage |
| Strong auth | TOTP `aal2`; passkey behind capability flag until accepted | Passkeys are experimental and passwordless semantics are not a TOTP-equivalent contract yet |
| Entitlement | Server trial clock plus webhook/reconciled paid snapshot | Explicit authorities and recoverable missed events |
| Lapsed account | Read/export/Unpublish remain; Publish/update blocked | Preserves user access without giving paid write capability |
| Download/media | Direct browser request/navigation only | Avoids SSRF, open proxy, and bandwidth abuse |
| Cloudflare | DNS-only delivery path; private retention-protected R2 operations ledger with transactional outbox; Vercel owns proxy/CDN/WAF | Avoids double-proxy loss of visibility while keeping verified deletion/trial replay outside Supabase PITR |
| History schema | Tags, no new SQLite column in v1 | Matches Favorite/sidecar |

## Production Launch Gates

These are not implementation choices. Production traffic is blocked until every owner and completion artifact is recorded:

1. **Product approval:** accept this XIP, prices, one-time trial, 72-hour payment grace, lapsed-account behavior, passkey feature-flag policy, Supabase OAuth 2.1 beta dependency, and direct-download limitation.
2. **Legal/privacy:** approve operating entity, merchant of record, Terms, Privacy, trial disclosure, external-host privacy disclosure, acceptable-use/abuse policy, refund/cancellation policy, support contact, data retention, export/deletion process, and age/region eligibility.
3. **Tax/accounting:** tax adviser confirms registrations, Product tax code, exclusive tax display, invoice identity, B2B/tax-ID policy, filing/remittance process, and reconciliation ownership before `automatic_tax` is enabled.
4. **Stripe live readiness:** activated business/payout details, branding/statement descriptor/support data, live Product/Prices, locked Portal, Radar/dunning settings, restricted key, signed live webhook, alerts, reconciliation, and a documented emergency rotation/duplicate-charge runbook.
5. **Supabase live readiness:** paid production plan, asymmetric signing keys, custom SMTP, migrations/RLS/advisors clean, PITR/backups, restore drill, Auth rate limits/CAPTCHA, session/JWT policy, and no production-only Dashboard drift. Explicitly accept OAuth 2.1 Server beta risk and verify in the deployed project: public-client registration, exact HTTPS callback/custom-scheme relay, `aal2` propagation, token response/status handling, refresh rotation, per-session/global revocation, and supported desktop platforms. If any fails or the beta is not accepted, desktop Cloud sign-in and this feature do not launch until a reviewed non-beta fallback exists.
6. **Vercel/Cloudflare readiness:** production domain/TLS, DNS-only Vercel records, DNSSEC/registrar lock, Vercel WAF, log drain/alerts, preview protection, environment isolation, security headers/CSP, kill switches, verified rollback, private dedicated R2 ledger, approved prefix retention values, separate writer/reader/admin credentials, lock/public-access drift alerts, and a successful closed-traffic restore/replay drill.
7. **Security review:** threat model covers BOLA/RLS, CSRF, SSRF/media URLs, account recovery, desktop PKCE, webhook replay/order, secrets, trial/payment abuse, deletion replay, incident response, and administrator access.
8. **End-to-end acceptance:** accessibility/browser matrix, desktop platforms, sandbox/Test Clock lifecycle, real signed webhook, migration/rollback, backup restore, secret rotation, and one low-value live purchase/cancel/refund/entitlement-removal support exercise.

## Platform References Reviewed

Validated against current platform guidance on 2026-08-22:

- [TypeScript 7.0 announcement and native compiler compatibility guidance](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/)
- [Next.js `experimental.useTypeScriptCli` configuration](https://nextjs.org/docs/app/api-reference/config/next-config-js/useTypeScriptCli)
- [Supabase passkeys (experimental)](https://supabase.com/docs/guides/auth/passkeys)
- [Supabase MFA](https://supabase.com/docs/guides/auth/auth-mfa)
- [Supabase SSR client and session validation](https://supabase.com/docs/guides/auth/server-side/creating-a-client)
- [Supabase OAuth 2.1 authorization-code flow with PKCE](https://supabase.com/docs/guides/auth/oauth-server/oauth-flows)
- [Supabase user sessions and refresh-token reuse detection](https://supabase.com/docs/guides/auth/sessions)
- [Stripe Billing subscriptions](https://docs.stripe.com/billing/subscriptions/design-an-integration)
- [Stripe webhook signatures](https://docs.stripe.com/webhooks#verify-events)
- [Stripe go-live checklist](https://docs.stripe.com/get-started/checklist/go-live)
- [Stripe Tax setup](https://docs.stripe.com/tax/set-up)
- [Vercel production checklist](https://vercel.com/docs/production-checklist)
- [Vercel reverse proxies and Verified Proxy](https://vercel.com/docs/security/reverse-proxy)
- [Cloudflare rate limiting behavior](https://developers.cloudflare.com/waf/rate-limiting-rules/)
- [Cloudflare DNS proxy status](https://developers.cloudflare.com/dns/proxy-status/)
- [Cloudflare DNSSEC](https://developers.cloudflare.com/dns/dnssec/)
- [Cloudflare R2 Bucket Locks](https://developers.cloudflare.com/r2/buckets/bucket-locks/)
- [Cloudflare R2 authentication and bucket-scoped tokens](https://developers.cloudflare.com/r2/api/tokens/)
- [Cloudflare R2 S3 API compatibility](https://developers.cloudflare.com/r2/api/s3/api/)
- [Cloudflare R2 data security](https://developers.cloudflare.com/r2/reference/data-security/)
- [Cloudflare R2 object lifecycles](https://developers.cloudflare.com/r2/buckets/object-lifecycles/)

## Implementation Authorization

Staged implementation was authorized on 2026-08-22. Local code, automated tests, dedicated non-production resources, and Stripe sandbox configuration may proceed. Production traffic, paid infrastructure, live-mode billing, Cloudflare R2 enablement, and replacement of the existing `xerahs.com` DNS remain subject to the explicit cost, account, legal/tax, verification, and launch gates above.
