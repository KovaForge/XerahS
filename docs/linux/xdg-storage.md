# Linux XDG Storage Locations

XerahS follows the XDG Base Directory Specification on Linux when the user has not explicitly provided `--settings-folder`.

| Purpose | Location |
|---------|----------|
| User config | `$XDG_CONFIG_HOME/xerahs`, defaulting to `$HOME/.config/xerahs` |
| App data | `$XDG_DATA_HOME/xerahs`, defaulting to `$HOME/.local/share/xerahs` |
| State, history, logs | `$XDG_STATE_HOME/xerahs`, defaulting to `$HOME/.local/state/xerahs` |
| Cache and derived thumbnails | `$XDG_CACHE_HOME/xerahs`, defaulting to `$HOME/.cache/xerahs` |
| Runtime files | `$XDG_RUNTIME_DIR` when the session provides it |

Relative or empty XDG environment variable values are ignored, as required by the XDG spec.

## XerahS Paths

| XerahS path | Linux default |
|-------------|---------------|
| Settings | `$XDG_CONFIG_HOME/xerahs` |
| Backups | `$XDG_CONFIG_HOME/xerahs/Backup` |
| History | `$XDG_STATE_HOME/xerahs/History` |
| Logs | `$XDG_STATE_HOME/xerahs/Logs/yyyy-MM` |
| Troubleshooting | `$XDG_STATE_HOME/xerahs/Troubleshooting` |
| Capture troubleshooting | `$XDG_STATE_HOME/xerahs/CaptureTroubleshooting` |
| Screenshots | `$XDG_DATA_HOME/xerahs/Screenshots` |
| Screencasts | `$XDG_DATA_HOME/xerahs/Screencasts` |
| Tools | `$XDG_DATA_HOME/xerahs/Tools` |
| Plugins | `$XDG_DATA_HOME/xerahs/Plugins` |
| Wallpaper conversion cache | `$XDG_CACHE_HOME/xerahs/wallpaper-conversion` |

User-selected capture/export folders are respected. The defaults above are deliberately inside the XDG app data tree so a first run does not create visible top-level folders in `$HOME`.

## Temporary-Home Smoke Test

Use this before Linux release promotion:

```bash
TMP_HOME="$(mktemp -d)"
HOME="$TMP_HOME" \
XDG_CONFIG_HOME="$TMP_HOME/.config" \
XDG_DATA_HOME="$TMP_HOME/.local/share" \
XDG_STATE_HOME="$TMP_HOME/.local/state" \
XDG_CACHE_HOME="$TMP_HOME/.cache" \
./xerahs --version

find "$TMP_HOME" -maxdepth 1 -mindepth 1 -print
```

Expected top-level entries are only XDG roots such as `.config`, `.local`, and `.cache`. Do not accept `XerahS`, `.XerahS`, `ShareX`, or `Screenshots` directly under `$HOME`.

