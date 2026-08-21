# KFIP0004: Community Plugin Registry and Installer

**Status**: Proposed
**Priority**: P1
**Area**: Plugin System | Uploaders | Distribution | Security | UX
**Created**: 2026-04-26
**Related**: XIP0011 (Plugin Packaging System), XIP0040 (Plugin Architecture Action Items), Pixelfox.cc uploader integration
**Owner**: KovaForge

---

## Summary

XerahS already has a local plugin package format (`.xsdp`) and installer path, but discovery is still manual: a user must know where to find a package, download it, then browse to the file locally. That is fine for developers; it is not good enough for community adoption.

This KFIP proposes a **community plugin registry**: a small, GitHub-hosted `plugins-index.json` file that lists verified community `.xsdp` packages. XerahS will show those entries directly inside the existing Plugin Installer dialog, let users refresh the list, inspect plugin metadata, download the selected package, verify its SHA-256 checksum, and install it through the existing package installer.

Pixelfox.cc is the immediate motivating use case, but the design is deliberately generic so future uploaders can be added without changing app code.

---

## Problem Statement

### Current plugin distribution is too manual

Today, community plugin installation requires several steps:

1. User discovers a plugin somewhere external.
2. User downloads a `.xsdp` file manually.
3. User opens the XerahS plugin installer.
4. User browses to the local package.
5. XerahS previews and installs it.

This creates friction and makes third-party uploader plugins feel unofficial even when they are compatible and safe to install.

### Community uploader support needs a scalable path

Adding every niche uploader directly into the main tree is not sustainable. Services such as Pixelfox.cc, smaller image hosts, private company endpoints, and experimental integrations should be able to ship as plugins without bloating the core app or requiring a full application release.

### Security cannot be an afterthought

A plugin registry is a supply-chain surface. The registry must be intentionally boring and auditable:

- no arbitrary scripts
- no dynamic code execution during discovery
- HTTPS-only downloads
- checksum verification before install
- package manifest preview before install
- size limits for registry and package downloads

---

## Goals

- Add first-class discovery for community uploader plugins.
- Reuse the existing `.xsdp` package format and `PluginPackager` install flow.
- Keep the first version simple enough to review and maintain.
- Make Pixelfox.cc installable through the same path as any future community plugin.
- Enforce basic supply-chain safety: HTTPS, package extension, SHA-256 checksum, duplicate ID detection, and API compatibility checks.
- Avoid creating a separate plugin marketplace service or backend.

## Non-Goals

- No paid marketplace.
- No ratings, comments, or user accounts.
- No automatic background plugin updates in v1.
- No remote execution of registry-provided commands.
- No dependency solver in v1 beyond displaying declared dependencies.
- No unsigned package trust model beyond checksum pinning in v1.

---

## Proposed Solution

### Registry file

Add a GitHub-hosted JSON registry under the destination plugin docs folder:

```text
plugins/destinations/plugins-index.json
```

Default raw URL:

```text
https://raw.githubusercontent.com/ShareX/XerahS/refs/heads/develop/plugins/destinations/plugins-index.json
```

Initial contents may be empty:

```json
{
  "indexVersion": "1.0",
  "lastUpdated": "2026-04-26T00:00:00Z",
  "plugins": []
}
```

Each plugin entry describes one downloadable `.xsdp` package:

```json
{
  "pluginId": "pixelfox",
  "name": "Pixelfox",
  "version": "1.0.0",
  "author": "Pixelfox",
  "description": "Pixelfox image uploader plugin.",
  "apiVersion": "1.0",
  "supportedCategories": ["Image"],
  "homepageUrl": "https://pixelfox.cc",
  "downloadUrl": "https://github.com/example/xerahs-pixelfox/releases/download/v1.0.0/pixelfox.xsdp",
  "checksum": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "minAppVersion": "1.0.0",
  "dependencies": []
}
```

### App integration

Update the existing Plugin Installer dialog rather than adding a separate marketplace window.

The dialog gains:

- **Refresh community plugins** button
- list of registry plugins
- selected plugin summary
- **Download and install selected community plugin** action
- existing local `.xsdp` browse/install path remains unchanged

This keeps plugin install UX in one place and avoids duplicating trust prompts or package-preview logic.

### Registry service

Add a `PluginIndexService` responsible for:

1. Validating the registry URL uses HTTPS.
2. Downloading the index with a maximum size limit.
3. Deserializing JSON into a typed registry model.
4. Validating index structure and every plugin entry.
5. Downloading selected `.xsdp` packages with a maximum size limit.
6. Verifying package SHA-256 checksum.
7. Cleaning up failed downloads.

Recommended v1 limits:

- registry index: **2 MB** maximum
- package download: **100 MB** maximum

### Registry validation rules

Registry-level validation:

- `indexVersion` is required.
- `plugins` list is required.
- duplicate `pluginId` values are rejected.

Plugin-level validation:

- `pluginId` is required.
- `pluginId` may only contain letters, digits, `.`, `_`, and `-`.
- `name`, `version`, `author`, and `apiVersion` are required.
- `apiVersion` must be compatible with the current XerahS plugin API major version.
- `downloadUrl` must be HTTPS.
- `downloadUrl` must end with `.xsdp`.
- `checksum` must be SHA-256, either raw 64-char hex or prefixed with `sha256:`.
- `homepageUrl`, when present, must be HTTPS.

### Install flow

```text
User opens Plugin Installer
        │
        ▼
Clicks Refresh Community Plugins
        │
        ▼
XerahS downloads and validates plugins-index.json
        │
        ▼
User selects plugin
        │
        ▼
XerahS downloads .xsdp to temp file
        │
        ▼
SHA-256 checksum verified against registry
        │
        ▼
PluginPackager.PreviewPackage validates plugin.json exists
        │
        ▼
Registry pluginId must match package manifest pluginId
        │
        ▼
PluginPackager.InstallPackage installs into Plugins folder
        │
        ▼
ProviderCatalog.LoadPlugins refreshes available uploaders
        │
        ▼
Temp package removed
```

---

## Implementation Plan

### Phase 1: Registry model and validation

Add:

- `CommunityPluginIndex`
- `CommunityPluginIndexEntry`

Responsibilities:

- model registry JSON
- expose helper display fields for UI
- validate plugin IDs, URLs, checksums, duplicate IDs, and API compatibility

### Phase 2: Registry fetch and package download service

Add:

- `PluginIndexService`

Responsibilities:

- fetch and parse registry JSON
- enforce index size limit
- download package with package size limit
- verify SHA-256 checksum
- delete failed downloads
- keep the API UI-friendly and testable

### Phase 3: Plugin Installer UI integration

Modify:

- `PluginInstallerViewModel`
- `PluginInstallerDialog.axaml`
- `XerahS.UI.csproj` references, if needed

Add UI state:

- `CommunityPlugins`
- `SelectedCommunityPlugin`
- `IsLoadingCommunityPlugins`
- `CommunityPluginSummary`
- `CanInstallCommunityPlugin`
- `CanRefreshCommunityPlugins`

Add commands:

- `RefreshCommunityPluginsCommand`
- `InstallCommunityPluginCommand`

### Phase 4: Documentation and default index

Add:

- `plugins/destinations/plugins-index.json`
- documentation under `docs/plugins/`

Documentation should cover:

- registry location
- plugin entry schema
- checksum generation
- package requirements
- security requirements
- Pixelfox-style example entry

### Phase 5: Tests

Add focused tests for:

- valid registry parsing
- HTTPS enforcement
- missing checksum rejection
- invalid package URLs
- duplicate plugin IDs
- unsupported API versions

---

## Acceptance Criteria

### Functional

- User can open Plugin Installer and refresh community plugins.
- Empty registry loads cleanly and displays `0 community plugins available`.
- Valid registry entries appear in the dialog.
- Draft registry entries appear in the dialog but cannot be installed.
- User can select a registry plugin and trigger download/install.
- Existing local `.xsdp` install still works.
- Installed community plugin is loaded into the provider catalog after install.

### Security

- HTTP registry URL is rejected.
- HTTP package URL is rejected.
- Non-`.xsdp` package URL is rejected.
- Missing or malformed SHA-256 checksum is rejected.
- Package checksum mismatch blocks install.
- Registry duplicate plugin IDs are rejected.
- Package manifest `pluginId` must match registry `pluginId`.
- Failed downloads are removed from temp storage.

### Quality

- `XerahS.Uploaders` builds cleanly.
- `XerahS.UI` builds cleanly.
- New registry validation tests pass.
- No unrelated dirty files are committed.

---

## Pixelfox.cc Use Case

Pixelfox.cc should ship as a normal community uploader plugin rather than a hard-coded built-in uploader unless there is a strong reason to merge it into core.

Expected flow:

1. Pixelfox plugin project builds a `.xsdp` package.
2. Release artifact is uploaded to a stable HTTPS location, preferably GitHub Releases.
3. SHA-256 checksum is generated for the `.xsdp`.
4. `plugins-index.json` receives a Pixelfox entry.
5. XerahS users refresh community plugins and install Pixelfox from the Plugin Installer dialog.

Example future entry:

```json
{
  "pluginId": "pixelfox",
  "name": "Pixelfox",
  "version": "1.0.0",
  "author": "Pixelfox",
  "description": "Upload images to Pixelfox.cc.",
  "apiVersion": "1.0",
  "supportedCategories": ["Image"],
  "homepageUrl": "https://pixelfox.cc",
  "downloadUrl": "https://github.com/KovaForge/XerahS-Plugins/releases/download/pixelfox-v1.0.0/pixelfox.xsdp",
  "checksum": "sha256:<64-char-package-sha256>",
  "minAppVersion": "0.22.78",
  "dependencies": []
}
```

---

## Security Considerations

### Threat: Registry compromise

A compromised index could point users at malicious packages.

Mitigations in v1:

- GitHub-hosted registry with normal repository review controls.
- Checksums pinned in the registry.
- Package manifest preview before install.
- No automatic install or update.

Future hardening:

- signed registry file
- signed `.xsdp` packages
- trust-on-first-use package publisher keys
- repository branch protection and CODEOWNERS review

### Threat: Package swap after registry publication

A download URL could serve different bytes after the registry entry is merged.

Mitigation:

- SHA-256 checksum verification blocks changed artifacts.

### Threat: Oversized download / resource exhaustion

Mitigations:

- 2 MB index size cap
- 100 MB package size cap
- streaming download limit checks

### Threat: Confusing plugin identity

A malicious package could claim a different identity in `plugin.json`.

Mitigation:

- registry `pluginId` must match package manifest `pluginId` before install.

---

## Open Questions

1. Should the canonical community registry live in the main XerahS repository or a separate `XerahS-Plugins` repository?
   - Recommendation: start in the main repository for simplicity; move to a dedicated registry repo when publishing cadence justifies it.
2. Should the registry default to `develop`, `master`, or versioned release branches?
   - Recommendation: use the app's release channel eventually; use `develop` for early KovaForge iteration.
3. Should plugin packages require signatures before leaving beta?
   - Recommendation: yes. Checksum verification is enough for v1, but signatures should be KFIP follow-up work.
4. Should XerahS support multiple registries?
   - Recommendation: not in v1. Add only after the official registry path is stable.

---

## Future Work

- Signed plugin packages.
- Optional plugin update checks.
- Multiple registries for enterprise/private plugin catalogs.
- Registry search/filter by category.
- Plugin screenshots and changelog fields.
- Compatibility matrix by XerahS version and plugin API version.
- CI validation for `plugins-index.json` entries.
- Automated package checksum verification in PR checks.

---

## Reference Implementation Notes

The first implementation should be intentionally small and boring:

- new registry/service code belongs in `XerahS.Uploaders.PluginSystem`
- existing installer UX should be extended, not replaced
- install should continue through `PluginPackager.InstallPackage`
- provider refresh should continue through `ProviderCatalog.LoadPlugins`
- network downloads should be explicit user actions only

This gives XerahS a practical community plugin path now, without prematurely building a marketplace.
