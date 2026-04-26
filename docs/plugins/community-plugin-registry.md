# Community Plugin Registry

XerahS can discover community uploader plugins from a GitHub-hosted `plugins-index.json` registry.

Official plugins are built into XerahS and do not need entries in this registry. The registry is only for community plugins distributed as `.xsdp` packages.

## Registry location

The default registry URL is:

```text
https://raw.githubusercontent.com/ShareX/XerahS/refs/heads/develop/plugins-index.json
```

## Package format

Community plugins must be downloadable `.xsdp` packages. An `.xsdp` package is the existing XerahS plugin archive format containing a root `plugin.json` manifest, the plugin DLL, and supporting files.

## Entry schema

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
  "isDraft": false,
  "minAppVersion": "1.0.0",
  "dependencies": []
}
```

## Security requirements

- `downloadUrl` must use HTTPS.
- `downloadUrl` must point to a `.xsdp` file.
- `checksum` must be a SHA-256 checksum, preferably prefixed with `sha256:`.
- Draft entries may set `isDraft` to `true` while waiting for a release package; XerahS lists draft plugins for visibility but disables installation until a package URL and checksum are published.
- `pluginId` may only contain letters, digits, `.`, `_`, and `-`.
- `apiVersion` must be compatible with the current plugin API major version.

The app downloads a selected `.xsdp`, verifies its SHA-256 checksum, previews the package manifest, then installs it through the existing `PluginPackager.InstallPackage()` flow.
