# Flatpak Permission Review

Manifest: `flatpak/com.getsharex.XerahS.yml`

The Flatpak build is designed to work through XDG Desktop Portals and app-private XDG storage. Broad host filesystem access is intentionally avoided.

| Permission | Why required | Portal alternative | Impact if removed | Review risk |
|------------|--------------|--------------------|-------------------|-------------|
| `--socket=x11` | Allows Avalonia's current Linux desktop backend to display. `fallback-x11` withheld `DISPLAY` on the Fedora GNOME validation VM while Avalonia selected X11. | Future Avalonia Wayland backend validation may allow narrowing this to `fallback-x11` or switching to a raw Wayland display socket. | App fails at startup with `XOpenDisplay failed`. | Medium |
| `--share=ipc` | Required by Flathub policy when X11 is enabled, so X11 clients can share memory correctly. | None for X11. | X11 may fail or perform poorly; manifest lint fails with `finish-args-x11-without-ipc`. | Low-Medium |
| `--device=dri` | Enables GPU/Skia acceleration. | None practical for current Avalonia rendering. | Software rendering or startup/rendering failures on some systems. | Low |
| `--share=network` | Required for uploaders, update checks, and connected integrations. | No portal substitutes arbitrary network upload features. | Upload destinations and network integrations fail. | Medium |
| `--talk-name=org.kde.StatusNotifierWatcher` | Enables StatusNotifierItem tray integration where available. | No portal equivalent for current tray behavior. | Tray icon may not appear. Core capture/upload still works. | Medium |

## Removed Or Avoided Permissions

| Permission | Reason |
|------------|--------|
| `--filesystem=home` | Too broad for Flathub review and no longer needed for default storage. |
| `--filesystem=host` | Not needed and intentionally avoided. |
| `--socket=session-bus` | Too broad; use specific portal/status-notifier names. |
| `--filesystem=xdg-config/XerahS` / `--filesystem=xdg-data/XerahS` | App-private Flatpak storage and XDG paths cover config/data without host grants. |
| `--filesystem=xdg-pictures`, `xdg-videos`, `xdg-documents`, `xdg-download` | File access should be user-mediated through portals. |
| Explicit `org.freedesktop.portal.Desktop` / `org.freedesktop.portal.Documents` D-Bus names | XDG Desktop Portals are available through the standard Flatpak portal path; explicit portal `talk-name` grants fail Flathub lint. |
| Direct `org.kde.KWin.ScreenShot2` / `org.gnome.Shell.Screenshot` D-Bus names | Sandboxed capture uses XDG Screenshot/ScreenCast portals instead. |
| Direct `org.freedesktop.Notifications` | Sandboxed notifications use the portal notification interface. |
| `com.steampowered.steam.AppUpdate` | Game detection is not release-blocking and needs separate human review before any static D-Bus grant. |

## Linter Gates

Run these before Flathub submission:

```bash
flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest flatpak/com.getsharex.XerahS.yml
flatpak run --command=flatpak-builder-lint org.flatpak.Builder repo repo
```

Any remaining warning must be copied into [flathub-submission-checklist.md](flathub-submission-checklist.md) with a human-reviewed justification.
