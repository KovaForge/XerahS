# XerahS Linux Readiness

This directory documents the Linux desktop behavior needed for native packages, Flatpak builds, and eventual Flathub review.

## Guarantees

- XerahS stores Linux config, state, logs, cache, tools, and plugins under XDG base directories by default.
- XerahS does not create `~/XerahS`, `~/.XerahS`, `~/ShareX`, or `~/Screenshots` as implicit Linux app roots.
- Flatpak builds use XDG Desktop Portals for sandboxed screenshots, screencasts, file access, notifications, OpenURI, background startup, and global shortcuts where the user's portal backend supports them.
- Native Linux installs keep native X11 and CLI fallbacks outside sandboxed environments.

## Documents

- [XDG storage locations](xdg-storage.md)
- [Flatpak VM validation runbook](flatpak-vm-validation.md)
- [Flatpak permission review](flatpak-permissions.md)
- [Portal behavior and troubleshooting](portal-behavior.md)
- [Flathub submission checklist](flathub-submission-checklist.md)
- [PPA / COPR / OBS update channels](distro-repos.md)
