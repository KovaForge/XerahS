# XerahS on Omarchy and Hyprland

From 0.31.0, XerahS uses [OmaSnap](https://github.com/KovaForge/omasnap) as its capture front end on
Hyprland (XIP0088). OmaSnap is a native Qt6/LayerShellQt overlay: it captures the focused output
through `ext-image-copy-capture`, then offers smart region, window and monitor selection, scrolling
capture, an annotation editor and floating pins. XerahS keeps owning workflows, hotkeys,
after-capture tasks, uploads and history.

Nothing changes on GNOME, KDE, Sway, X11 or Flatpak. XerahS decides by probing OmaSnap, not by the
distribution name.

## When OmaSnap is used

XerahS runs `omasnap --host-capabilities` once, in the background, only when all of these hold:

- the session is Wayland and Hyprland (`HYPRLAND_INSTANCE_SIGNATURE` is set);
- XerahS is not sandboxed (Flatpak, Snap);
- an OmaSnap binary exists. Search order: the development override in settings
  (`LinuxOmaSnapPathOverride`), `XERAHS_OMASNAP_PATH`, `omasnap/omasnap` next to XerahS,
  `/usr/lib/xerahs/omasnap/omasnap`, then `omasnap` on `PATH`.

The probe must report `ok`, Hyprland, `ext-image-copy-capture`, layer-shell and `hostMode >= 1`
(OmaSnap 1.22.0 or newer). The linux-x64 portable tarball and the `xerahs-git` AUR package bundle
OmaSnap at `omasnap/omasnap` (built from the `native/omasnap` submodule), so it works out of the box
and takes precedence over any standalone copy. A standalone OmaSnap 1.21 in `~/.local/bin` has no host
mode and is ignored. A failed probe writes one line to the normal log, never to the error log, and XerahS keeps
its existing capture chain.

Check the result:

```bash
xerahs doctor --linux-desktop          # profile and probe JSON
omaxerahs capabilities                 # lists capture.omasnap when usable
```

Settings > Capture > Linux Region Selector also shows it, e.g. `Capture engine: OmaSnap 1.22.0 · Hyprland · ready`.

## Which captures use OmaSnap

With the region selector on **Automatic** (default) or **OmaSnap**:

| Workflow | OmaSnap |
|---|---|
| Region capture, transparent region | smart selection (`--capture-region` when the workflow's "OmaSnap: region only" option is on) |
| Custom region | the configured rectangle, preselected; without one, smart selection |
| Last region | the last region, preselected |
| Custom window (no title configured) | window pick |
| Print screen, active monitor | focused monitor |
| Scrolling capture | OmaSnap scroll capture |

Active window, custom window with a configured title, color picker, ruler, QR and OCR tools, and
screen recording keep their XerahS paths. Other selectors (XerahS overlay, portal dialog, slurp,
desktop native) keep today's behavior.

The PNG goes through the normal pipeline: After Capture window, image effects, annotation, save,
clipboard, upload and history. The window class and title OmaSnap reports fill `%pn` and the window
title in file names and history. Pressing the hotkey again while the overlay is open dismisses it;
`Esc` cancels. If OmaSnap fails (not cancels), XerahS falls back to its existing chain for that
capture.

OmaSnap in host mode never copies to the clipboard, saves to its screenshots folder, notifies,
uploads, or keeps a pin: XerahS does all of that according to the workflow.

## Annotation editor and pins

Settings > Capture > Linux Region Selector > **Annotation editor** chooses between the XerahS editor
(default) and the OmaSnap editor for the "Annotate" after-capture task. Sidecar annotation files and
re-editing from History work only with the XerahS editor.

Pin to screen uses `omasnap --pin` on Hyprland when OmaSnap is available. The pin's Upload button
runs `omaxerahs upload --url-only`, so it uploads to your XerahS image destination.

## Hyprland keybindings

Portal shortcuts (`xerahs:3`, `xerahs:13`) can take keys such as PRINT or F1 from Omarchy. On
Hyprland, let Hyprland own XerahS hotkeys instead:

1. Settings > Application > **Hyprland Keybindings** > **Review changes…** shows every binding
   XerahS will write, and every key Omarchy or you already bound.
2. Tick **Unbind the conflicting keys** only if XerahS should take those keys over.
3. **Apply to my Hyprland config** then:
   - backs up `~/.config/hypr/bindings.lua` to `bindings.lua.bak.<unix-time>`;
   - writes `~/.config/hypr/xerahs.lua` (generated; do not edit);
   - appends `require("xerahs")` to `bindings.lua` once;
   - runs `hyprctl reload` and `hyprctl configerrors`, and restores both files if Hyprland reports
     errors.

Each binding runs `omaxerahs workflow run <workflow id>`; with Omarchy it uses
`o.bind(keys, "XerahS: <workflow>", command)`, otherwise `hl.bind`. While this is on, XerahS does
not register portal, evdev or X11 hotkeys, so nothing triggers twice. Changing a hotkey in XerahS
regenerates `xerahs.lua` and reloads Hyprland. `/usr/share/omarchy` is never touched.

### Turning it off or reverting

- **Turn off** in the same card empties `xerahs.lua`, reloads Hyprland and registers portal/evdev
  hotkeys again. The `require("xerahs")` line stays and loads nothing; delete it if you like.
- To undo everything by hand, copy the newest `bindings.lua.bak.<time>` back to `bindings.lua`,
  delete `xerahs.lua` and run `hyprctl reload`.

## Switching engines

- Per workflow: Task settings > Capture > Preferred Linux region selector. Choose any selector other
  than Automatic or OmaSnap to keep OmaSnap out of that workflow.
- Everywhere: remove or rename the OmaSnap binary, or point `XERAHS_OMASNAP_PATH` at a file that is
  not executable. XerahS then reports OmaSnap as absent.

## Packaging

- The linux-x64 portable tarball and the AUR package include `omasnap/omasnap` with its MIT, OFL and
  ISC notices when it could be built (Arch container job in CI). It links against the system Qt6 and
  LayerShellQt; elsewhere the probe fails and nothing changes.
- deb, rpm, AppImage and Flatpak do not include OmaSnap.
- Build it locally with `build/linux/build-omasnap.sh <publish-dir>` (needs `cmake ninja pkgconf
  qt6-base layer-shell-qt wayland-protocols libdeflate`).
