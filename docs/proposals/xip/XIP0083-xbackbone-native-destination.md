# XIP0083: Native XBackBone Destination and Copy-Link Workflow

**Status**: Implemented (live macOS/Linux verification pending)
**Priority**: High
**Area**: Uploaders | Plugins | Cross-platform | Clipboard
**Related**: XIP0024 (custom uploader integration), XIP0048 (Nextcloud native plugin), XIP0049 (Immich native plugin)
**Created**: 2026-08-16
**Implemented**: 2026-08-16
**Version**: v0.24.25

---

## Summary

Add XBackBone as a first-class bundled XerahS upload destination so users do not need to import or maintain a ShareX custom-uploader (`.sxcu`) configuration.

The destination will:

1. Appear in the existing destination catalog as a native uploader plugin.
2. Support image, text, and file uploads.
3. Support both currently proven XBackBone API generations through an explicit configuration choice.
4. Store the XBackBone token in XerahS's secret store rather than serialized destination JSON.
5. Return XBackBone's canonical share URL through `UploadResult.URL`.
6. Reuse XerahS's existing `CopyURLToClipboard` after-upload action, which is enabled by default for new workflows.

This XIP does not introduce destination-specific clipboard behavior, an architecture rewrite, or a broader ShareX feature clone.

## User Request

> Summary
> In the continuation of shareX an integration with xbackbone would be a must have. The feature with the automatic link copy after upload is also a cool feature from sharex.
>
> Use Case
> I think all users from xbackbone who are on macos or on linux will benefits of this.
>
> Benefit
> Remove the need of custom config for xbackbone integration.

## Motivation

XBackBone already generates ShareX-compatible custom-uploader configurations, and XerahS can import those configurations. That proves the integration is possible today, but it leaves users responsible for downloading, importing, selecting, and replacing a generic uploader definition that contains a plaintext token.

A native destination improves this flow by providing:

- a recognizable XBackBone entry in the XerahS destination catalog;
- a small purpose-built configuration UI;
- secure token storage;
- validation and error messages specific to XBackBone;
- stable response mapping into XerahS history and clipboard workflows;
- no requirement to understand `.sxcu` request fields or JSON selectors.

The main beneficiaries are macOS and Linux users who want a ShareX-like capture-to-XBackBone workflow without maintaining a custom uploader.

## Investigation Record

The API investigation used the requested primary-source checkout:

```text
C:\Users\Public\source\repos\SergiX44\XBackBone
```

The inspected revisions were:

| Source | Revision | Role |
|---|---|---|
| XBackBone tag `3.8.2` | `226af4f0cec218f563da6d53ececfa4708e6f3e7` | Current tagged 3.x contract |
| XBackBone `master` | `1c3e8646c6cf6460b6a6a25d5b7da4769f50bcdb` | Next-generation API v1 contract |

The XBackBone repository describes `master` as the next-generation application. Its current documentation also states that each deployed next-generation instance serves version-specific OpenAPI documentation at `/docs/api`.

## Current XerahS Architecture

XerahS already has the required extension points:

1. `IUploaderProvider` defines provider metadata, supported categories, settings, uploader creation, and optional configuration UI.
2. Bundled providers live under `src/desktop/plugins/*` and are loaded through `plugin.json` manifests.
3. `UploaderInstance` stores a provider ID, category, display name, and non-secret settings JSON.
4. `IProviderContext` gives plugins access to the shared `ISecretStore`.
5. `UploadJobProcessor` resolves the configured instance and dispatches `GenericUploader` or `FileUploader` implementations.
6. A successful `UploadResult.URL` flows into history, the after-upload window, and the existing clipboard action.

Amazon S3 is the requested first-class plugin precedent. For XBackBone's smaller "server URL + token + multipart request" contract, the Immich and Nextcloud plugins are the closest implementation references for HTTP handling, configuration UI, and secret storage.

No core destination enum, special dependency-injection registration, or provider-specific destination page is required.

## Proven XBackBone API Contracts

XBackBone 3.x and next-generation API v1 are not wire-compatible.

| Concern | XBackBone 3.x | Next-generation API v1 |
|---|---|---|
| Configuration label | `XBackBone 3.x` | `API v1 (next-generation)` |
| Upload endpoint | `POST {baseUrl}/upload` | `POST {baseUrl}/api/v1/upload` |
| Authentication | Multipart field `token` | `Authorization: Bearer {token}` |
| File field | `upload` in the official generated 3.x SXCU | `file` |
| Optional filename field | Not required for v1 scope | `name` |
| Success status | HTTP 201 | HTTP 201 |
| Response envelope | Top-level JSON object | JSON `data` object |
| Canonical share URL | `url` | `data.preview_ext_url` |
| Direct file URL | `raw_url` | `data.raw_url` |
| Deletion URL | Not supplied by the proven upload response | `data.deletion_url` |
| Error message | Top-level `message` | Top-level `message` |

### XBackBone 3.x evidence

The following paths are from tag `3.8.2` and should be inspected with `git show 3.8.2:<path>` from the reference checkout:

- `app/routes.php`: upload route at `/upload`.
- `app/Controllers/ClientController.php`: official SXCU fields, including `upload`, `token`, and response `url`.
- `app/Controllers/UploadController.php`: token validation, file handling, success response, and errors.
- `config.example.php`: public `base_url` configuration.

### Next-generation API v1 evidence

The following paths are from the inspected `master` revision:

- `core/bootstrap/app.php`: `/api/v1` prefix and Sanctum authentication.
- `core/routes/api/v1.php`: `POST upload` route.
- `core/app/Http/Requests/Api/V1/UploadResourceRequest.php`: `file`, `data`, and optional `name` fields.
- `core/app/Http/Controllers/Api/V1/UploadController.php`: upload request consumption.
- `core/app/Http/Resources/Api/V1/ResourceResource.php`: response schema.
- `core/app/Actions/Integration/GenerateSharexConfig.php`: official bearer header and XerahS/ShareX URL mappings.
- `docs/clients/api.md`: published API and authentication guidance.
- `docs/clients/xerahs.md`: current custom-uploader setup for XerahS.

## Compatibility Decision

The first native destination should support both proven API generations through an explicit `API generation` selector.

The default should be `XBackBone 3.x` because `3.8.2` is the current tagged release. Users of the next-generation deployment can select `API v1 (next-generation)`.

The plugin must not attempt upload-and-fallback autodetection. If the first request succeeds but response parsing fails, retrying against another endpoint could create a duplicate resource. No documented machine-readable version negotiation endpoint was found.

## Goals

1. Add XBackBone to XerahS as a bundled native destination.
2. Remove the need for `.sxcu` configuration for ordinary XBackBone uploads.
3. Support image, text, and file categories using the existing uploader abstraction.
4. Support the proven stable 3.x and next-generation API v1 upload contracts.
5. Keep tokens out of serialized destination settings and repository content.
6. Return the canonical share URL so existing history and clipboard behavior works unchanged.
7. Provide actionable authentication, quota, validation, malformed-response, and network errors.
8. Preserve behavior for all existing destinations and workflows.

## Non-Goals

- Do not replace or remove custom-uploader support.
- Do not import, generate, or rewrite XBackBone `.sxcu` files.
- Do not implement an XBackBone media explorer.
- Do not add remote deletion UI, even when API v1 returns a deletion URL.
- Do not implement URL shortening or URL-sharing-service categories.
- Do not add chunked or resumable uploads without a documented XBackBone contract.
- Do not add automatic API-generation probing or upload fallback.
- Do not add destination-specific clipboard settings or clipboard implementations.
- Do not bypass TLS certificate validation for self-signed deployments.
- Do not redesign the uploader plugin architecture.

## Proposed Plugin

Create a bundled plugin under:

```text
src/desktop/plugins/XBackBone.Plugin/
```

The provider should expose:

```text
ProviderId: xbackbone
Name: XBackBone
Categories: Image, Text, File
Explorer: No
```

The app's existing plugin discovery and catalog UI will expose the provider without a provider-specific host change.

### Configuration model

The serialized model should contain only non-secret values:

```csharp
public sealed class XBackBoneConfigModel
{
    public string SecretKey { get; set; } = Guid.NewGuid().ToString("N");
    public string ServerUrl { get; set; } = string.Empty;
    public XBackBoneApiGeneration ApiGeneration { get; set; } = XBackBoneApiGeneration.Stable3;
}
```

The exact type and property names may follow repository naming conventions during implementation, but the persisted shape must remain limited to these responsibilities.

### API-generation model

```csharp
public enum XBackBoneApiGeneration
{
    Stable3,
    ApiV1
}
```

The UI labels should make the deployment distinction clear:

- `XBackBone 3.x (stable releases)`
- `API v1 (next-generation)`

### Secret storage

Store the token through `ISecretStore` using:

```text
Provider: xbackbone
Instance key: XBackBoneConfigModel.SecretKey
Secret name: apiToken
```

The token must:

- be treated as an opaque string;
- be preserved exactly, including `|` in Sanctum tokens;
- never be written to `SettingsJson`;
- never be included in logs or exception messages;
- never be included in test fixtures as a real credential;
- be removable from the configuration UI.

Implement `IInstanceSecretMigrator` so any future or prototype plaintext `ApiToken`/`Token` property can be moved into the secret store safely.

## Configuration UX

The configuration view should remain intentionally small:

1. XBackBone status/description card.
2. Instance URL field.
3. API-generation selector.
4. Masked API-token field.
5. Clear stored token action.
6. Concise validation/status message.

The URL should represent the public XBackBone instance root, not a full upload endpoint. The client derives the fixed endpoint from `ApiGeneration`.

The first version should perform local validation only:

- URL is absolute HTTP or HTTPS.
- Token is present.
- API generation is recognized.

It should not offer a `Test connection` button unless a non-mutating authentication endpoint is proven for both generations. Testing credentials by uploading a resource would be surprising and could create unwanted data.

## Upload Flow

### Common flow

1. Resolve `XBackBoneConfigModel` from the destination instance.
2. Resolve `apiToken` from `ISecretStore`.
3. Normalize the public server URL by removing query, fragment, and trailing slash while preserving a valid application subpath.
4. Select the fixed adapter from `ApiGeneration`.
5. Submit the stream as multipart form data.
6. Require a successful HTTP status and valid JSON.
7. Require a non-empty canonical share URL.
8. Map available URLs into `UploadResult`.
9. Return a conventional non-error success result.
10. Allow the existing XerahS pipeline to update history and perform configured after-upload actions.

### XBackBone 3.x request

```http
POST {baseUrl}/upload
Content-Type: multipart/form-data

upload=<file stream>
token=<opaque token>
```

Success mapping:

```text
UploadResult.URL          <- url
UploadResult.ThumbnailURL <- raw_url
```

### Next-generation API v1 request

```http
POST {baseUrl}/api/v1/upload
Authorization: Bearer <opaque token>
Accept: application/json
Content-Type: multipart/form-data

file=<file stream>
name=<file name>
```

Success mapping:

```text
UploadResult.URL          <- data.preview_ext_url
UploadResult.ThumbnailURL <- data.raw_url
UploadResult.DeletionURL  <- data.deletion_url
```

The canonical share/preview URL is deliberately used for `UploadResult.URL`, matching XBackBone's official generated XerahS/ShareX configuration. The direct raw URL remains available separately.

## Text Uploads

XerahS dispatches image, text, and file destination instances through stream-based `GenericUploader`/`FileUploader` behavior. The XBackBone plugin should therefore upload the provided stream with its generated filename for all three categories.

API v1 also supports a `data` field for paste and link resources, but using it would require data-type-specific dispatch beyond the smallest native destination. It is not needed to upload a generated `.txt` stream successfully and is outside this XIP's first implementation.

## Error Handling

The client should surface concise errors without exposing secrets.

At minimum, distinguish:

- invalid or missing local configuration;
- 401/403 authentication or permission failure;
- 413 or deployment-specific quota/size rejection;
- 422 validation failure;
- other non-success HTTP statuses;
- malformed JSON;
- successful response missing the canonical URL;
- network, DNS, TLS, timeout, and cancellation failures.

When available, use the server's top-level `message` string. Bound the amount of response content included in an error so an HTML proxy response or server dump does not flood logs or UI.

HTTP success alone is insufficient. The result is successful only when the expected canonical share URL is present and valid.

## Copy Link After Upload

XerahS already implements ShareX-style copy-after-upload globally:

1. `TaskSettings.AfterUploadJob` defaults to `AfterUploadTasks.CopyURLToClipboard`.
2. Workflow settings expose a `Copy URL to clipboard` checkbox.
3. `UploadJobProcessor` invokes after-upload tasks only after a non-error upload with a URL.
4. It copies `UploadResult.URL` through `PlatformServices.Clipboard.SetTextAsync`.

The XBackBone plugin therefore must not call the clipboard directly. It only needs to return the canonical share link in `UploadResult.URL`.

This preserves user control:

- with the workflow option enabled, the XBackBone link is copied;
- with the option disabled, the upload succeeds without changing the clipboard;
- existing clipboard-monitor suppression and Linux persistence behavior remain in effect;
- all destinations continue to use the same after-upload semantics.

## Platform Behavior

No new platform code is required.

Desktop macOS and Linux use the existing Avalonia clipboard service on the UI thread. Headless macOS uses the existing `pbcopy` path. Headless Linux prefers `wl-copy` and falls back to `xclip`.

The HTTP uploader itself remains platform-neutral and belongs entirely in the plugin.

## Expected File Changes

New plugin files:

```text
src/desktop/plugins/XBackBone.Plugin/
  XerahS.XBackBone.Plugin.csproj
  plugin.json
  XBackBoneApiGeneration.cs
  XBackBoneClient.cs
  XBackBoneConfigModel.cs
  XBackBoneProvider.cs
  XBackBoneUploader.cs
  ViewModels/XBackBoneConfigViewModel.cs
  Views/XBackBoneConfigView.axaml
  Views/XBackBoneConfigView.axaml.cs
```

Test and solution integration:

```text
XerahS.sln
src/desktop/XerahS.sln
tests/XerahS.Tests/XerahS.Tests.csproj
tests/XerahS.Tests/Uploaders/XBackBoneClientTests.cs
tests/XerahS.Tests/Uploaders/XBackBoneConfigViewModelTests.cs
tests/XerahS.Tests/Uploaders/XBackBoneProviderTests.cs
```

The exact test split may be reduced if one fixture can cover the same behavior clearly. No production changes are expected outside the new plugin and solution registration.

## Automated Verification

Use an injectable HTTP handler so tests do not require a live XBackBone server.

Required focused coverage:

1. 3.x endpoint construction.
2. 3.x multipart fields `upload` and `token`.
3. 3.x top-level `url` and `raw_url` mapping.
4. API v1 endpoint construction.
5. API v1 bearer authorization and `Accept: application/json`.
6. API v1 multipart fields `file` and `name`.
7. API v1 `preview_ext_url`, `raw_url`, and `deletion_url` mapping.
8. Base URL normalization, including a preserved application subpath.
9. Non-success responses and top-level `message` handling.
10. Malformed JSON and missing canonical URL rejection.
11. Provider validation for missing URL/token.
12. Token persistence in `ISecretStore` and exclusion from serialized settings.
13. Supported categories and default configuration.

Run:

```powershell
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter "FullyQualifiedName~XBackBone"
dotnet build src/desktop/plugins/XBackBone.Plugin/XerahS.XBackBone.Plugin.csproj -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false
dotnet build src/desktop/XerahS.sln -m:1 -p:nodeReuse=false -p:UseSharedCompilation=false
```

Expected result: all focused tests pass and the full desktop solution builds with zero errors and without disabling warnings-as-errors.

### Implementation Verification

Verified on Windows on 2026-08-16:

- XBackBone-focused tests: 41 passed, 0 failed, 0 skipped.
- XBackBone plugin build: 0 warnings, 0 errors.
- Full desktop solution build: 0 warnings, 0 errors.
- Live macOS and Linux server/clipboard checks remain pending on those platforms.

## Manual macOS Verification

1. Add an XBackBone destination from the catalog.
2. Configure the instance URL, matching API generation, and token.
3. Select the instance for an image or file workflow.
4. Enable `Copy URL to clipboard`.
5. Upload a small image.
6. Run `pbpaste` immediately after completion.
7. Confirm the clipboard exactly matches the share URL shown in XerahS history.
8. Disable `Copy URL to clipboard`, set known clipboard text, and repeat.

Expected result: the first upload copies the canonical XBackBone share URL. The second upload succeeds and preserves the known clipboard text.

## Manual Linux Verification

Repeat the macOS scenario on both relevant desktop paths where available.

Wayland check:

```bash
wl-paste --no-newline
```

X11 check:

```bash
xclip -selection clipboard -o
```

Expected result: the command returns the canonical XBackBone share URL when the workflow option is enabled. When disabled, the previous clipboard content remains unchanged.

Linux clipboard persistence after process exit remains governed by the existing XerahS setting and availability of `wl-copy`/`xclip`; this XIP does not change that behavior.

## Acceptance Criteria

- [x] XBackBone appears as a bundled destination for Image, Text, and File.
- [x] A user can configure an instance URL, API generation, and token without importing `.sxcu`.
- [x] The token is stored only through `ISecretStore`.
- [x] Stable XBackBone 3.x uploads use `/upload`, multipart `token`, and file field `upload`.
- [x] Next-generation API v1 uploads use `/api/v1/upload`, bearer auth, and file field `file`.
- [x] Both adapters return the proven canonical share URL in `UploadResult.URL`.
- [x] Available raw/deletion URLs are preserved in their existing `UploadResult` fields.
- [x] Missing or malformed response URLs fail cleanly and do not trigger after-upload clipboard copy.
- [x] Existing `Copy URL to clipboard` behavior copies the successful XBackBone URL.
- [x] Disabling that workflow option prevents clipboard modification.
- [x] No existing destination behavior changes.
- [x] Focused tests pass.
- [x] The desktop solution builds with zero errors.
- [ ] macOS and Linux manual verification produce the expected clipboard results.

## Alternatives Considered

### Continue using `.sxcu`

Rejected as the primary experience. It already works but does not meet the goal of a first-class destination and leaves the token embedded in an imported custom-uploader definition.

### Support only next-generation API v1

Rejected for the proposed first version because API v1 is on next-generation `master`, while `3.8.2` is the current tagged release. A master-only plugin would exclude existing stable deployments.

### Support only stable XBackBone 3.x

Rejected because XBackBone's current primary documentation and official XerahS generator target API v1. Omitting it would create immediate follow-up work and an avoidable migration gap.

### Automatically detect the generation

Rejected because no documented machine-readable version negotiation endpoint was found. Upload-and-fallback is unsafe due to duplicate-upload risk, and token-shape inference is not a stable protocol.

### Add provider-specific automatic clipboard copy

Rejected because XerahS already provides a workflow-level after-upload action. A plugin-specific write could ignore user preferences, copy twice, and interfere with clipboard monitoring.

### Add explorer, deletion, and URL-shortener support

Deferred. These capabilities are not required to remove custom upload configuration and would expand the first version beyond the requested upload-and-copy workflow.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Users choose the wrong API generation | Clear UI labels and endpoint-specific authentication errors; no destructive fallback |
| Next-generation API changes before release | Keep API v1 isolated behind its adapter and rely on the versioned `/api/v1` contract; validate against the target instance's `/docs/api` during manual QA |
| Token leakage | Store only in `ISecretStore`; redact headers/form values; exclude token from serialized settings and tests |
| Reverse proxy returns HTML or oversized errors | Bound response excerpts and prefer JSON `message` when present |
| Successful HTTP response lacks a share URL | Treat as failure so history and clipboard do not receive an invalid value |
| Self-signed certificate failures | Use the system/.NET trust store and document certificate installation; do not disable validation |
| Large upload or quota failures | Surface server status/message; do not invent chunking or retry semantics |
| Clipboard tool unavailable on Linux | Preserve the existing XerahS warning and clipboard capability behavior |
| Existing uploaders regress | Keep changes inside the plugin boundary and run the full desktop build |

## Open Questions

1. Should the API-generation default remain `XBackBone 3.x` until a tagged next-generation release exists? This XIP recommends yes.
2. Should a later version offer an explicit raw-link preference? This XIP uses the canonical share/preview URL to match XBackBone's official configuration.
3. Should a later version support API v1 `data` uploads for native paste/link resources? This is intentionally deferred from the stream-based first version.
4. Should a later version expose deletion using the returned API v1 deletion URL? The URL should be preserved now, but UI/actions are outside this XIP.

## Definition of Done

This XIP is complete when the native plugin is shipped, both proven API generations pass focused contract tests, the solution builds cleanly, and macOS/Linux verification confirms that XerahS's existing workflow setting copies the returned XBackBone share URL only after a successful upload.
