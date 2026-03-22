# Installing XerahS on Linux (Arch / EndeavourOS)

This guide covers building and installing XerahS as a local Arch package from the repository using the reusable scripts in `build/linux/`.

---

## Prerequisites

| Tool | Notes |
|---|---|
| `dotnet-sdk` | Install via `sudo pacman -S dotnet-sdk` |
| `makepkg` / `bsdtar` | Included with `base-devel`: `sudo pacman -S base-devel` |
| `node` + `npm` | Required for the video editor UI build; install via `sudo pacman -S nodejs npm` or from [nodejs.org](https://nodejs.org/) if pacman mirrors are unavailable |

Verify your setup:

```bash
dotnet --version   # expect 10.x
node --version     # expect 18+ (22 recommended)
npm --version
makepkg --version
```

---

## Build the Arch package

From the repository root:

```bash
cd /path/to/XerahS
./build/linux/package-aur.sh
```

The script will:
1. Clone the local repo into a clean `makepkg` source directory
2. Run `dotnet publish` (all 11 plugins + video editor UI via Vite)
3. Compress everything into an installable `.pkg.tar.zst`
4. Copy the finished package to `dist/aur/`

The build takes roughly 7–10 minutes; progress is printed to stdout.

To control parallelism (useful on machines with limited RAM):

```bash
XERAHS_PLUGIN_JOBS=1 ./build/linux/package-aur.sh
```

---

## Install

```bash
sudo pacman -U dist/aur/xerahs-git-*.pkg.tar.zst
```

---

## Run

```bash
xerahs
```

Or launch **XerahS** from your application menu / app launcher.

---

## Reinstall after a rebuild

```bash
./build/linux/package-aur.sh
sudo pacman -U dist/aur/xerahs-git-*.pkg.tar.zst
```

---

## Installed file layout

| Path | Contents |
|---|---|
| `/usr/lib/xerahs/` | Application binary, plugins, video editor assets |
| `/usr/bin/xerahs` | Symlink → `/usr/lib/xerahs/XerahS` |
| `/usr/share/applications/xerahs.desktop` | Desktop entry |
| `/usr/share/icons/hicolor/256x256/apps/xerahs.png` | Application icon |
| `/usr/share/licenses/xerahs-git/LICENSE.txt` | GPL-3.0 licence |

---

## Optional runtime dependencies

Install any that apply to your desktop environment:

```bash
# Wayland
sudo pacman -S wl-clipboard grim slurp

# X11
sudo pacman -S xclip xdotool

# GNOME system tray
sudo pacman -S gnome-shell-extension-appindicator

# Video editor webview
sudo pacman -S webkit2gtk-4.1
```

---

## Uninstall

```bash
sudo pacman -R xerahs-git
```

---

## Related files

- **PKGBUILD**: `build/linux/aur/xerahs-git/PKGBUILD`
- **Build orchestrator**: `build/linux/package-aur.sh`
- **Build system docs**: `build/README.md`
