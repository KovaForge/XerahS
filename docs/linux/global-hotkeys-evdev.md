# Linux Global Hotkeys (direct evdev listener)

XerahS detects global hotkeys on Linux by listening directly to keyboard input
devices via **evdev** (`/dev/input/event*`). This is the approach described in
[XIP0080](../proposals/xip/XIP0080-linux-global-hotkeys-direct-evdev-listener.md)
and works uniformly across Wayland compositors (GNOME, KDE Plasma, Hyprland) and
on X11, without depending on the XDG GlobalShortcuts portal.

## How it works

- XerahS opens readable keyboard devices and receives every key event on the system.
- It keeps its own list of configured hotkeys and a small matching engine compares
  each incoming key + modifier combination against that list.
- Only exact matches trigger an action; everything else is ignored.

This means hotkey ownership is decided by XerahS, not by the compositor, which
removes the portal failure modes (app-ID mismatch, window-handle race conditions,
PrintScreen mapping issues, etc.).

## Backend selection

At startup XerahS chooses a hotkey backend in this order:

1. **evdev** direct listener — when at least one keyboard device is readable.
2. **XDG GlobalShortcuts portal** — legacy fallback on Wayland sessions that expose it.
3. **X11 key grabs** — final fallback on X11.

You can force a backend with an environment variable (mainly for troubleshooting):

```bash
XERAHS_LINUX_HOTKEY_BACKEND=evdev   # or: portal, x11
```

## Permissions

Reading `/dev/input/event*` requires permission. On most distributions these
devices are owned by `root:input` with group-read access, so the simplest setup is
to add your user to the `input` group:

```bash
sudo usermod -aG input $USER
# Log out and back in (or reboot) for the new group to take effect.
```

The `.deb` and `.rpm` packages install a udev rule
(`/usr/lib/udev/rules.d/99-xerahs-input.rules`) and reload udev automatically. For
manual or portable installs, copy the rule yourself:

```bash
sudo cp build/linux/packaging/99-xerahs-input.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo udevadm trigger --subsystem-match=input
```

### Per-session access without group membership

If you prefer not to add a permanent group, edit the udev rule to use the
systemd-logind ACL tag instead of group ownership:

```udev
KERNEL=="event*", SUBSYSTEM=="input", TAG+="uaccess"
```

This grants the active desktop user automatic access on login.

> Security note: granting read access to input devices lets locally installed
> software observe keystrokes. Apply only on systems where you trust your installed
> applications.

## Diagnostics

Check readiness and get actionable guidance with:

```bash
xerahs doctor --linux-input          # human-readable report
xerahs doctor --linux-input --json   # machine-readable
```

The report lists detected keyboard devices, whether each is readable, your
`input` group membership, the selected backend, and remediation steps when evdev
is unavailable. The command exits non-zero when no readable keyboard is found.

## Flatpak / sandboxed installs

Sandboxed Flatpak builds do not grant raw `/dev/input` access (not a
Flathub-standard permission), so the direct-evdev path is unavailable
inside the sandbox. Instead, the Flatpak requests `--socket=wayland`,
`--socket=fallback-x11`, and the
`org.freedesktop.portal.{Desktop,GlobalShortcuts,ScreenCast,Notification}`
D-Bus names via `finish-args:` in
[`flatpak/com.xerahs.XerahS.yml`](../../flatpak/com.xerahs.XerahS.yml).
That lets the runtime resolve the XDG GlobalShortcuts portal service
through the Flatpak session-bus proxy, so the on-screen hotkey capture
dialog renders natively on Wayland (GNOME, KDE, Hyprland) and the
portal-native triggers fire from any focused application.

Three active backends, picked at runtime by
[`LinuxPlatform.CreateHotkeyService`](../../src/platform/XerahS.Platform.Linux/LinuxPlatform.cs):

1. **evdev** — direct `/dev/input` listener (native `.deb`/`.rpm`/AUR only).
2. **XDG GlobalShortcuts portal** — the Flatpak and any Wayland session
   where the portal is exposed.
3. **X11 key grabs** — final fallback on X11 sessions (focus-only).

If a Flatpak install still shows the broken "three blank rectangles"
placeholder dialog after an upgrade, run
`flatpak run --command=xerahs com.xerahs.XerahS doctor --linux-input`
and check the `GlobalShortcuts:` line — it must read
`ok (source=dbus-introspect)`. If it reads
`no (source=…)`, verify that
`org.freedesktop.portal.GlobalShortcuts` is exposed by the active
xdg-desktop-portal backend (`busctl --user introspect
org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop | grep
GlobalShortcuts`).
