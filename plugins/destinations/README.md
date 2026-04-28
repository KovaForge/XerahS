# Community Destination Plugins

This directory lists community-created destination plugins available for installation via the XerahS Plugin Installer.

## Available Plugins

For the authoritative and always-up-to-date list, see the [`plugins-index.json`](plugins-index.json) file in this folder.

### Pixelfox

- **pluginId:** `pixelfox`
- **version:** 1.0.0
- **author:** Pixelfox.cc
- **category:** Image
- **description:** Upload images to Pixelfox.cc.
- **homepage:** <https://pixelfox.cc>
- **min app version:** 0.22.78
- **status:** Draft (not yet available for installation)

---

## Installing a Community Plugin

1. Open XerahS → Settings → Uploaders
2. Click **Install Plugin...**
3. Click **Refresh Community Plugins**
4. Select a plugin from the list
5. Click **Install**

XerahS will download the `.xsdp` package, verify its SHA-256 checksum, and install it through the existing package installer.

## Security

- All community plugins are verified against SHA-256 checksums listed in `plugins-index.json`
- Downloads must use HTTPS and end in `.xsdp`
- The registry and packages are hosted on GitHub for transparency and review

## Creating a Community Plugin

See the [Destination Plugin Development Guide](../../developers/destination-plugins/README.md) for how to build, package, and list a plugin in the community registry.