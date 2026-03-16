# XIP0049 Immich Native Plugin Design

XIP0049: Immich Native Plugin Design

## Goal
Add a dedicated XerahS uploader plugin for Immich that uses Immich's native API for upload, duplicate detection, album placement, server/profile discovery, and shared links.

## Legacy ShareX Baseline
There is no existing Immich uploader in either classic ShareX or the current XerahS repo.

Searches performed:

- `rg -n -i "immich" "C:\Users\liveu\source\repos\ShareX Team\ShareX"`
- `rg -n -i "immich" .`

Result:

- no Immich file uploader support in `ShareX`
- no existing Immich plugin or legacy compatibility layer in `XerahS`

This is therefore a clean new plugin, not a migration of an older implementation.

## Official Immich Sources Used For This Design
The design is based on Immich's own docs, CLI docs, web app code, and current server controllers/DTOs from the official Immich repository.

1. CLI guidance for automation
   - Source: `https://raw.githubusercontent.com/immich-app/immich/main/docs/docs/features/command-line-interface.md`
   - Key finding: the official CLI authenticates with an API key and supports upload plus album assignment flows, which makes API keys the correct primary automation credential for a desktop uploader plugin.
2. Password login and session bootstrap
   - Source: `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/auth.controller.ts`
   - Key endpoint: `POST /api/auth/login`
   - Key finding: the server supports direct email/password login that returns an access token and session cookies, which can be used to bootstrap a scoped API key for the plugin.
3. API key management
   - Sources:
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/api-key.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/api-key.dto.ts`
   - Key endpoints:
     - `POST /api/api-keys`
     - `GET /api/api-keys/me`
   - Key finding: Immich supports scoped API keys with explicit permissions, which is better for a long-lived uploader plugin than storing the user's password.
4. Server profile and capabilities
   - Sources:
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/server.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/user.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/server.dto.ts`
   - Key endpoints:
     - `GET /api/server/about`
     - `GET /api/server/config`
     - `GET /api/server/features`
     - `GET /api/users/me`
   - Key finding: the plugin can display a verified profile card with version, external domain, feature flags, and current user info instead of asking the user to trust raw settings.
5. Native upload and duplicate detection
   - Sources:
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/asset-media.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/asset-media.dto.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/asset-media-response.dto.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/cli/src/commands/asset.ts`
   - Key endpoints:
     - `POST /api/assets/bulk-upload-check`
     - `POST /api/assets`
   - Key findings:
     - upload is native multipart form-data, not generic WebDAV/S3/FTP
     - duplicate detection is a first-class API based on SHA1 checksums
     - the official CLI uses duplicate check before upload, so the plugin should do the same
6. Album management
   - Sources:
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/album.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/album.dto.ts`
   - Key endpoints:
     - `GET /api/albums`
     - `POST /api/albums`
     - `PUT /api/albums/{id}/assets`
     - `GET /api/albums/{id}`
   - Key finding: Immich destinations are album-centric rather than path-centric, so the plugin should model destination selection around albums, not remote folders.
7. Shared links and public URL shape
   - Sources:
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/controllers/shared-link.controller.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/server/src/dtos/shared-link.dto.ts`
     - `https://raw.githubusercontent.com/immich-app/immich/main/web/src/lib/services/shared-link.service.ts`
   - Key endpoints:
     - `POST /api/shared-links`
     - `GET /api/shared-links`
   - Key findings:
     - public sharing is a dedicated native API
     - shared links support password, expiry, slug, upload/download toggles, and metadata visibility
     - canonical public URLs are built as `/s/{slug}` or `/share/{key}`, using `externalDomain` when configured

## Why Native Immich API Is The Correct Choice
There is no useful compatibility layer for Immich analogous to WebDAV or S3 that would preserve the product's real capabilities.

Using the native API is better because it gives the plugin:

1. first-class duplicate detection before upload
2. first-class album creation and asset placement
3. native public/shared link creation with expiry, password, and slug support
4. real server-profile and capability discovery
5. scoped API keys instead of storing account passwords for routine use
6. behavior that matches the official CLI and web app

## Proposed Plugin Scope
Implement under:

- `src/desktop/plugins/Immich.Plugin/`

Implement now:

1. Dedicated `ImmichProvider` for `Image`, `Text`, and `File`.
2. Dedicated `ImmichConfigView` and `ImmichConfigViewModel` with a staged setup flow similar in richness to Amazon S3.
3. Two auth paths:
   - recommended: paste an existing API key
   - native bootstrap: sign in with email/password once, create a scoped plugin API key, then store only the key
4. Server verification and profile refresh using:
   - `server/about`
   - `server/config`
   - `server/features`
   - `users/me`
5. SHA1-based duplicate check before upload using `bulk-upload-check`.
6. Native multipart upload to `POST /api/assets`.
7. Optional album selection or auto-create-album behavior.
8. Optional native shared link creation after upload.
9. Basic album-oriented explorer support:
   - list albums at the root
   - open an album to list its assets
   - create albums
   - delete albums or assets
   - download assets / thumbnails

## Upload Model
The plugin should follow Immich's native upload contract rather than adapting ShareX's path semantics onto it.

Recommended flow for each upload:

1. Normalize server URL to the Immich API root.
2. Resolve API key from `ISecretStore`.
3. Fetch current server/user context if the cached profile is stale.
4. Compute SHA1 for the outgoing file.
5. Call `POST /api/assets/bulk-upload-check` with the checksum.
6. If the asset is already present:
   - reuse the returned asset ID
   - optionally add it to the selected album
   - optionally create a shared link for it
7. If the asset is new:
   - submit multipart upload to `POST /api/assets`
   - use file timestamps for `fileCreatedAt` and `fileModifiedAt`
   - set a stable desktop `deviceId`
   - set `deviceAssetId` from file name and size or another deterministic local key
8. If album placement is configured:
   - create the album if allowed and missing
   - add the uploaded asset to that album
9. If link creation is configured:
   - create an individual asset shared link by default
   - optionally allow album-link mode when an album destination is in use

## UX Direction
The UI should borrow the staged, operator-friendly feel of Amazon S3 without pretending Immich is a bucket store.

The configuration window should have these sections:

1. Hero / status card
   - plugin title
   - connection summary
   - current server/user summary
   - capability highlights like API key, duplicate check, albums, and shared links
2. Connection
   - server URL
   - auth mode selector
   - API key paste path
   - email/password bootstrap path
   - verify / refresh profile actions
3. Server profile
   - user name / email
   - server version
   - external domain
   - feature flags from `server/features`
4. Destination
   - upload directly to library or target album
   - album picker / album name
   - auto-create album toggle
   - duplicate-check toggle
5. Share link policy
   - none / individual asset / album
   - optional slug
   - optional password
   - optional expiry
   - allow download toggle
   - allow upload toggle for album shares
   - show metadata toggle

## Explorer Model
Immich is not folder-native in the same way as S3, Dropbox, FTP, or Nextcloud. The plugin should therefore expose an album-first explorer:

1. Root view lists albums as folders.
2. Opening an album lists its assets.
3. Thumbnails come from Immich asset thumbnails or asset media endpoints.
4. Downloading content uses the native original download endpoint.
5. Creating a folder maps to creating an album.
6. Deleting an album deletes the album.
7. Deleting an asset deletes the asset from Immich, not just from the local album view.

This is a deliberate semantic mapping, not a fake file hierarchy.

## Secrets And Migration
Store the following in `ISecretStore`:

1. primary API key
2. bootstrap password, only if temporarily needed during key creation
3. optional shared-link password

The serialized config model should keep non-secret settings only:

1. server URL
2. user/profile snapshot
3. preferred auth mode
4. album targeting options
5. share-link policy
6. secret key identifier

Implement `IInstanceSecretMigrator` from the start so that any future plaintext prototype settings can be upgraded cleanly.

## Why This Is Better Than A Minimal Upload-Only Integration
1. It matches Immich's actual object model: assets, albums, and shared links.
2. It avoids wasteful uploads by using native duplicate detection first.
3. It uses least-privilege API keys for steady-state operation.
4. It lets the user target an album, which is how Immich users actually organize assets.
5. It generates public URLs using the same rules as the official web app.
6. It gives XerahS explorer support without pretending Immich is a generic remote filesystem.

## Staged Follow-Up Work
After the first dedicated pass:

1. Add album reuse intelligence for slugged album links.
2. Support richer shared-link editing and lookup so repeated uploads can update an existing album share instead of creating a new one.
3. Add sidecar upload support for XMP when ShareX/XerahS exports one.
4. Add search-backed explorer filtering using Immich search endpoints instead of client-side filtering only.
5. Consider session/OAuth-based bootstrap flows if the Immich server has password login disabled but OAuth enabled.

## Verification Plan
1. `dotnet build src\desktop\plugins\Immich.Plugin\XerahS.Immich.Plugin.csproj -m:1`
2. `dotnet build src\desktop\XerahS.sln -m:1`
3. Manual verification against a real Immich server:
   - API key verify
   - bootstrap login -> scoped key creation
   - duplicate upload skip
   - album creation and asset assignment
   - shared link creation
   - album explorer listing and asset download
