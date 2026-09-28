# Omarchy and Hyprland integration

XerahS 0.31.0 uses OmaSnap, the native Wayland capture and annotation tool, as its capture front end on Omarchy and on other Hyprland systems where OmaSnap works ([XIP0088](../proposals/xip/XIP0088-omasnap-native-omarchy-capture-engine.md)). XerahS still owns workflows, hotkeys, after-capture tasks, uploads and history. Every other Linux desktop keeps the existing capture chain unchanged.

## What changes on Omarchy

- **Captures.** Region, window, fullscreen and scrolling captures open OmaSnap's overlay instead of the XerahS overlay, slurp or the portal dialog. OmaSnap hands XerahS a PNG plus the region, window class and scale; the After Capture window, image effects, annotation, save, clipboard, upload and history run exactly as before. OmaSnap never uploads, copies or saves anything itself when XerahS starts it.
- **Toggle.** Pressing the capture hotkey again while the overlay is open closes it without capturing.
- **Annotation editor.** Settings > Advanced > Linux Region Selector can open captures in OmaSnap's editor instead of the XerahS editor. The XerahS editor stays the default.
- **Pins.** Pin to screen uses OmaSnap pins. A pin's Upload button (`U`) uploads through `omaxerahs upload` to your XerahS destination and copies the link.
- **Hyprland keybindings (optional).** Workflow hotkeys can become Hyprland keybindings instead of portal or evdev shortcuts. See below.

## How XerahS decides

A probe decides, not the distro name. At startup XerahS runs `omasnap --host-capabilities` in the background and uses OmaSnap only when the session is Hyprland, XerahS is not sandboxed (Flatpak or Snap), and the probe reports host mode. Until the probe passes, or when it fails, captures use the existing chain and the log has one line saying why.

XerahS looks for OmaSnap in this order:

1. `XERAHS_OMASNAP_PATH`
2. `omasnap/omasnap` next to the XerahS binary (the linux-x64 tarball)
3. `/usr/lib/xerahs/omasnap/omasnap` (the AUR package)
4. `omasnap` on `PATH`

The linux-x64 tarball and the `xerahs-git` AUR package ship OmaSnap with its MIT, OFL and ISC notices under `omasnap/licenses`. Other packages (deb, rpm, AppImage, Flatpak, arm64) do not include it; installing OmaSnap 1.22.0 or later on `PATH` works too.

Check what XerahS sees:

```bash
xerahs doctor --linux-desktop          # add --json for machine-readable output
omaxerahs capabilities                 # lists capture.omasnap when OmaSnap is usable
```

## Switching the capture engine

Settings > Advanced > Linux Region Selector:

- **Automatic** uses OmaSnap on Omarchy-like systems and the existing order everywhere else.
- **OmaSnap (native Hyprland overlay)** uses OmaSnap whenever the probe passes, even outside the Automatic rule.
- Any other choice (XerahS overlay, portal, slurp) turns OmaSnap off for captures.

Jobs OmaSnap does not front (active window, a named window, colour picker, ruler, OCR and screen recording) always keep their XerahS paths.

## Hyprland keybindings

Portal shortcuts on Hyprland can steal keys such as F1 or PRINT from Omarchy. The Hyprland way is a compositor binding, so XerahS can manage one file of bindings for you.

Settings > Hotkeys > Use Hyprland keybindings (shown on Hyprland only):

1. **Check conflicts** reads `hyprctl binds -j` and lists keys that Omarchy or your own config already bind. Tick the keys XerahS may take over. Unticked keys keep their current binding and the matching XerahS hotkey is left out.
2. **Turn on** writes `~/.config/hypr/xerahs.lua`, copies `bindings.lua` (or `hyprland.lua` when there is no `bindings.lua`) to `bindings.lua.bak.<unix-time>`, appends one line that loads the managed file, runs `hyprctl reload` and then `hyprctl configerrors`. If Hyprland reports any error, XerahS restores your file from the backup, removes or restores the managed file, and reloads again.
3. While it is on, XerahS registers no portal or evdev shortcuts for workflows, so a key never fires twice. Each binding runs `omaxerahs workflow run <workflow id>`, which returns at once while the capture runs in XerahS. Editing a hotkey in XerahS rewrites the managed file and reloads Hyprland; your own config is not touched again.

The line added to your config is:

```lua
-- XerahS keybindings (added by XerahS; delete these two lines to stop loading them)
do local path = "/home/you/.config/hypr/xerahs.lua"; local file = io.open(path, "r"); if file then file:close(); dofile(path) end end
```

It loads the file only when it exists, so deleting `xerahs.lua` never breaks Hyprland. XerahS never edits anything under `/usr/share/omarchy`. The managed file uses Omarchy's `o.bind`, so the bindings appear in `omarchy menu keybindings`; without Omarchy it uses `hl.bind`. XerahS keybindings need Hyprland's Lua config (`hyprland.lua`).

## Reverting

- **Keybindings:** Settings > Hotkeys > Turn off empties `xerahs.lua` and reloads Hyprland, which also gives back any keys XerahS unbound, and XerahS registers its hotkeys itself again. To remove every trace, delete the two XerahS lines from `bindings.lua` (or restore the `.bak.<unix-time>` copy) and delete `xerahs.lua`. If the include line is gone, XerahS notices at startup and falls back to portal or evdev hotkeys.
- **Capture engine:** pick another region selector in Settings > Advanced > Linux Region Selector, or remove the bundled `omasnap` folder. Either way XerahS returns to the existing chain.
- **Annotation editor:** set it back to XerahS in Settings > Advanced > Linux Region Selector.
