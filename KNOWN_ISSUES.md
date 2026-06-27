# Known Issues

## Windows

### Region Capture
- **DPI Scaling Issue on Region Capture Background:** The dark background behind the region capture tool is not rendering correctly when any monitor connected to the system is set to a DPI scale greater than 100%. The background overlay appears shifted or misaligned in these high-DPI scenarios.

## Linux

> Prioritized fixes and the current Linux state assessment are tracked in
> [docs/LINUX-IMPROVEMENT-PLAN.md](docs/LINUX-IMPROVEMENT-PLAN.md).

### Region Capture / Screenshot
- **XDG Portal vs in-app overlay (commit 58283cb13900be85ede524022c5d5dc46877eebd):** Up to and including commit `58283cb13900be85ede524022c5d5dc46877eebd`, region capture on Linux used the XDG Portal to take a screenshot (system dialog). After that commit, XerahS uses its own overlay with crosshair for region selection by default. The overlay path can be sluggish (e.g. delay before the crosshair receives pointer events on Wayland) and may exhibit DPI/positioning issues in mixed-DPI setups. **Option:** Check **Use modern capture** in capture settings to use the XDG Portal / system dialog for region capture (old behaviour). Uncheck it to use the in-app overlay with crosshair.
- **Fedora GNOME mixed-DPI routing:** On Fedora GNOME mixed-DPI setups, region selection/capture may misalign unless `UseTransparentOverlay` is enabled. Runtime now forces transparent overlay on this platform combination. KDE/Plasma sessions (including EndeavourOS logs with `Routing hint: kde`) keep Windows-parity overlay behavior from commit `4688c1331739b7568b0cb9ad9270a961a965de0d`.

### Global Hotkeys
- **Global hotkeys not firing when app is backgrounded (XIP0044):** On Linux (Wayland / XWayland), global hotkeys currently only trigger when XerahS is the active window. When the app is minimised or another window has focus, registered shortcuts (e.g. screenshot/recording) do not fire, making them unusable for normal background usage. See `docs/proposals/xip/XIP0044-linux-global-hotkeys-not-firing-when-app-backgrounded.md` for analysis and planned fixes.

- **Workaround via PrintScreen + folder watch:** On most Linux desktops the **PrintScreen** hardware key still works through the system screenshot tool (often via the XDG portal), even when XerahS is not focused. Configure that tool to save captures into a dedicated folder, and configure XerahS to watch that folder and auto-upload new files as a practical workaround until true global hotkeys are fully fixed.

## macOS

> Prioritized fixes and the current macOS state assessment are tracked in
> [docs/MACOS-IMPROVEMENT-PLAN.md](docs/MACOS-IMPROVEMENT-PLAN.md).

### Distribution
- **"XerahS is damaged and can't be opened" on downloaded builds:** release archives are unsigned and not notarized, so Gatekeeper rejects quarantined downloads until the user runs `xattr -cr` (workaround documented in README). Unsigned builds also have an unstable TCC identity, so Screen Recording / Accessibility grants can reset after updates. Tracked as P1/P2 in the improvement plan.

### Permissions
- **Missing Screen Recording permission yields wallpaper-only screenshots instead of a prompt:** there is no `CGPreflightScreenCaptureAccess` preflight; the native capture path silently falls back to the `screencapture` CLI, which renders only the desktop wallpaper when permission is missing. Tracked as P3.
- **Global hotkeys require Accessibility permission:** hotkeys are powered by a SharpHook event tap, which needs Accessibility (see FAQ). A Carbon `RegisterEventHotKey` path that needs no permission is tracked as P4.

### Window capture
- **Window list shows only the frontmost window:** window enumeration uses AppleScript and returns a single window; background-window capture falls back to the interactive CLI. Tracked as P5.
