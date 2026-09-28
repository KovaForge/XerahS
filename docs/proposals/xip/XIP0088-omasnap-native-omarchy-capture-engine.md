# XIP0088 OmaSnap Native Omarchy Capture Engine

**Status**: Completed
**Created**: 2026-09-28
**Updated**: 2026-09-28
**Target version**: v0.31.0 (feature; bump minor)
**Area**: Linux | Capture | Hotkeys | Packaging | Omarchy | Architecture
**Related**: XIP0079 (Linux improvement plan), XIP0080 (evdev global hotkeys), XIP0086 (agent-native platform), XIP0087 (Omarchy screenshot upload plugin)
**Goal**: On Omarchy and Omarchy-like Hyprland systems, XerahS uses OmaSnap's native Wayland capture, annotation and pin architecture as its capture front end, while XerahS keeps owning workflows, hotkeys, after-capture tasks, uploads and history; every other Linux system keeps today's behavior unchanged.

---

## Overview

XerahS on Omarchy currently captures through a chain of generic Linux strategies (wlroots protocol, XDG portal dialog, slurp, XerahS Avalonia overlay). On the maintainer's Omarchy machine this is slow and fragile: the wlroots region path failed in 14 of 37 recent region captures and fell back to the portal dialog (3+ seconds), global shortcuts go through the XDG GlobalShortcuts portal and collide with Omarchy's own bindings (see Appendix A), and the Avalonia overlay cannot use layer-shell, so it cannot match a native Wayland overlay.

[OmaSnap](https://github.com/KovaForge/omasnap) (KovaForge fork of `tobi/omasnap`, MIT) is a native Qt6/LayerShellQt screenshot and annotation overlay built for exactly this environment: it captures the focused output in-process through `ext-image-copy-capture` before mapping an exclusive layer-shell surface, provides smart region/window/monitor selection, scrolling capture, a vector annotation editor, floating pins, and follows the Omarchy theme live. It is a specialized front end; XerahS is a workflow engine. This XIP makes OmaSnap the **capture engine** XerahS drives on Omarchy-like systems, instead of a second screenshot app the user must configure separately.

The integration is a **host contract**, not a rewrite: OmaSnap gains a machine-readable "host mode" in the KovaForge fork (capture or edit, write the PNG where the host says, report the result as one JSON object, and perform no copy/save/upload/notification side effects of its own). XerahS vendors a pinned OmaSnap build, detects capable sessions, maps its workflow jobs onto OmaSnap targets, and continues its normal pipeline (after-capture tasks, effects, annotation, save, upload, history). Hotkeys on Hyprland become Hyprland-managed bindings that call into XerahS, replacing the conflicting portal shortcuts. On any other system, or whenever OmaSnap is missing or fails, XerahS falls back to the existing chain with no behavior change.

Key principles: reuse XerahS's capture orchestration and task pipeline (one pipeline, no parallel "OmaSnap mode"); OmaSnap never uploads, stores credentials, or writes history when hosted; capability detection, not distro-name checks; the UI thread never waits on the OmaSnap process; no silent edits to the user's Hyprland config.

---

## Prerequisites

- Repositories (both on GitHub, both writable by the implementer):
  - `KovaForge/XerahS` branch `develop` (version `0.30.12` at the time of writing; bump to `0.31.0` for this feature per `.ai/skills/git-workflow/SKILL.md`).
  - `KovaForge/omasnap` branch `main` (at `614cdf5`, v1.21.0 + Unreleased). Branch `feature/uploader` adds OmaSnap's own upload hosts (`86680b5`, `6895aeb`, `5b5d547`); host mode must disable that feature when hosted, so base host-mode work on `main` and rebase `feature/uploader` onto it, or merge it first; decide in Phase 1 and note it in the PR.
- OmaSnap build/runtime dependencies (Arch package names): `base-devel cmake ninja pkgconf qt6-base layer-shell-qt wayland wayland-protocols libdeflate wl-clipboard xdg-utils`; optional `tesseract tesseract-data-eng` (OCR). Runtime also calls `hyprctl`, `wl-copy`, `wl-paste`, and `omarchy-notification-send` when present.
- Session requirements for OmaSnap (the capability gate): Wayland, Hyprland (`HYPRLAND_INSTANCE_SIGNATURE` set, `hyprctl` works), compositor advertises `ext_image_copy_capture_manager_v1` and `zwlr_layer_shell_v1`.
- License: OmaSnap is MIT (Copyright (c) 2026 Tobi Lütke); fonts are OFL (Neucha, JetBrains Mono, Inter) and icons ISC (Lucide). XerahS is GPL-3.0; MIT/OFL/ISC are compatible. Ship all notices.

---

## Definitions

| Term | Meaning |
|---|---|
| **Omarchy** | `/usr/share/omarchy` exists or `OMARCHY_PATH` is set (Omarchy 4.x, Hyprland with Lua config). |
| **Omarchy-like** | Any Hyprland Wayland session that passes the OmaSnap capability probe (Phase 1), whether or not Omarchy is installed. |
| **Other Linux** | Everything else (GNOME, KDE, Sway/other wlroots, X11, Flatpak). Behavior must not change. |
| **Host mode** | OmaSnap run by XerahS through the contract in Phase 1. |
| **Standalone mode** | OmaSnap run by a user or keybinding as today. Must not change. |

---

## Implementation Phases

### Phase 0: Groundwork and bugs found while investigating

Fix these first (each with a regression test where practical, one commit per fix). Evidence is from the maintainer's logs, `~/.local/state/xerahs/Logs/2026-09/XerahS-errors-*.log`, 2026-09-17..28; counts are occurrences.

1. **EIS error spam (207 errors).** `WaylandPortalInputService: Enable failed: Tmds.DBus.DBusException: org.freedesktop.DBus.Error.Failed: Not connected to EIS`, logged to the error log on every start (Hyprland's portal has no EIS). Detect the unsupported case once, log a single informational line, and stop retrying. Also handle `Portal watch error (e.g. service gone)` (8) and an `Unobserved task exception` wrapping `Tmds.DBus.Protocol.DBusConnectionClosedException: Connection closed by peer` (observe and log the portal task instead of leaking it).
2. **Tests write into the user's real error log.** Entries such as `ImageEffectPresetSerializer: Failed to load preset file '/tmp/xip0020-….xsie'` and `'/tmp/xerahs-automation-tests-…'` come from `dotnet test` runs. `DebugHelper`'s error log resolves the real XDG state path. Redirect the error log to a temporary folder for the test assembly (NUnit `SetUpFixture` or an env override honored by `PathsManager`).
3. **SQLite fails in a secondary process.** `Failed to add upload history item` (4-5) with `System.TypeInitializationException: The type initializer for 'Microsoft.Data.Sqlite.SqliteConnection' threw an exception`. Likely the single-file `omaxerahs` or `xerahs-watchfolder-daemon` publish cannot load the native `e_sqlite3` provider (SQLitePCLRaw bundle). Reproduce with `omaxerahs upload <png>` from the Linux tarball, fix the publish (`IncludeNativeLibrariesForSelfExtract` / provider init), and add a smoke test that opens the history DB from a published single-file build in CI.
4. **Startup ordering races.** `MainWindow: NotifyWindowReady failed: InvalidOperationException: Platform services not initialized. Call Initialize() first.` and `Failed to pre-warm settings search index: InvalidOperationException: UI view model factory is not available.` Defer both until bootstrap completes (or make them no-ops until ready).
5. **Handed-off screenshot missing at upload time.** `Upload failed for Amazon S3: FileNotFoundException: Could not find file '/home/mike/Pictures/screenshot-2026-09-27_06-11-45.png'` (the Omarchy `omarchy-capture-screenshot` file name pattern, written to `~/Pictures`). A path was accepted before the file existed or after it moved. Validate existence and size (brief wait with timeout for files still being written) before queueing, and report a clear error instead of a stack trace. With OmaSnap host mode the path is produced by XerahS, so this race goes away on Omarchy, but fix it for all hand-offs (`omaxerahs upload`, send-to, watch folder).
6. **Wayland-only startup.** `Critical application startup failure: System.Exception: XOpenDisplay failed` (2026-09-19/20). XerahS must start (or fail with a readable message and tray notification) when `DISPLAY` is unset/unusable on a pure Wayland session.
7. **Media Browser S3 redirect.** `S3 request failed: PermanentRedirect - The bucket you are attempting to access must be addressed using the specified endpoint`. Follow the region/endpoint hint (or show "wrong region/endpoint" with the value to use). The companion `AccessDenied … s3:ListBucket` is a real IAM limit; show it plainly.
8. **Consolidate Omarchy detection.** Today it lives in three places: `XerahS.Platform.Linux/Services/LinuxOsRelease.cs` (internal, `/etc/os-release`), `src/desktop/app/XerahS.App/AgentSkillBootstrapper.cs` (`OMARCHY_PATH` or `/usr/share/omarchy`), and the Omarchy plugin. Introduce one `LinuxDesktopProfile` (Phase 3) and use it everywhere.
9. **Known flaky build.** The full solution build intermittently fails with `Avalonia error AVLN9999: The process cannot access the file '…/obj/…/XerahS.RegionCapture.pdb' (or XerahS.UI.dll) because it is being used by another process` when the same project is built for two graph paths in parallel; a plain retry succeeds. Find the duplicate project reference/TFM path and remove it, so CI is deterministic.

Already fixed on `develop` before this XIP (do not redo): clipboard sync-over-async deadlock that froze the UI and tray menu (`dabe924e`), After Capture window raising the main window (`21b5761f`), dialogs targeting the wrong `MainViewModel` after the editor opened (`2d0c632e`), ShareX `.sxie` import (`93d40a0c`).

### Phase 1: OmaSnap host-mode contract (repo `KovaForge/omasnap`)

Add a host mode that is **off unless requested** so standalone behavior, keybindings and output stay byte-for-byte the same. Follow the repo's `AGENTS.md` principles: the main thread never blocks, no new dependencies, minimal configuration, Hyprland-only.

**CLI additions:**

```text
omasnap --host <name> --output <png-path> [--result-json <path>|-]
        [--capture-region|--capture-window|--capture-fullscreen|--scroll|smart]
        [--region x,y,w,h] [--editor overlay|window] [--no-recents]
omasnap --host <name> --file <png> --output <png-path> --editor overlay|window   # annotate an existing image
omasnap --host-capabilities                                                    # probe, prints JSON, exit 0/1
```

**Host-mode rules:**
- Capture exactly as today (same selection UI, same `ext-image-copy-capture` path, same toggle semantics: a second invocation dismisses the running overlay and reports `cancelled`).
- Write the flattened PNG (native pixels, logical-size metadata kept) to `--output` only. Do **not** copy to the clipboard, autosave to the screenshots directory, show the timed preview, send a notification, upload, or retain a pin. Recents default to on (the shelf is useful), `--no-recents` disables them.
- With `--editor`, open the annotation editor before output; Copy/Save in the editor mean "finish" (write `--output` and report). `Esc` reports `cancelled`.
- Report one JSON object (to `--result-json` path, or stdout when `-`), then exit. Exit codes: `0` ok, `3` cancelled, `1` failure, `2` usage.

```json
{
  "schemaVersion": 1,
  "status": "ok",
  "target": "region",
  "path": "/run/user/1000/xerahs/omasnap/cap-1234.png",
  "pixelWidth": 2006, "pixelHeight": 1600,
  "logicalWidth": 1003, "logicalHeight": 800,
  "scale": 2.0,
  "monitor": "eDP-1",
  "region": { "x": 120, "y": 80, "width": 1003, "height": 800 },
  "window": { "class": "firefox", "title": "…", "address": "0x5559…" },
  "annotated": false,
  "documentPath": null,
  "omasnapVersion": "1.22.0"
}
```

`--host-capabilities` checks the same things capture needs and reports them without mapping any surface:

```json
{ "schemaVersion": 1, "ok": true, "version": "1.22.0",
  "hyprland": true, "extImageCopyCapture": true, "layerShell": true,
  "hostMode": 1, "targets": ["smart","region","windows","fullscreen","scroll"],
  "editor": ["overlay","window"], "pin": true }
```

**Also:**
- `--pin <path>` keeps working for hosts. Add `--host` awareness so a hosted pin's **Upload** button (and `U`) runs the host's command instead of OmaSnap's own upload: env `OMASNAP_HOST_UPLOAD_COMMAND` (argv-split, receives the PNG path, prints a URL). XerahS sets it to `omaxerahs upload`. Without it, hide the upload control on hosted pins.
- Keep upload hosts, keyring and `omasnap.conf` behavior unchanged in standalone mode.
- Tests: extend the existing smoke tests (`tests/*-smoke.cpp`) for argument parsing, JSON result shape, cancelled/ok exit codes, and "no side effects in host mode" (clipboard untouched, no file in the screenshots directory). Document host mode in `README.md` and `CHANGELOG.md`; bump to `1.22.0`.
- Open an upstream PR to `tobi/omasnap` if the maintainer wants it; the fork is the source XerahS pins either way.

### Phase 2: Vendor and package OmaSnap with XerahS (repo `KovaForge/XerahS`)

- Add `native/omasnap` as a git submodule of `KovaForge/omasnap`, pinned to the Phase 1 release commit (the repo already uses submodules for `ShareX.ImageEditor` and `Omacut`; the `native/` folder exists and holds macOS native code).
- `build/linux/build-omasnap.sh`: configure/build with CMake + Ninja (`-DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX=<staging>`), install into the publish folder as `omasnap/omasnap` plus `omasnap/licenses/*` (MIT, OFL, ISC). Fail soft: if Qt6/LayerShellQt are missing, print a warning and produce the XerahS package without OmaSnap (the capability probe then reports it absent).
- **AUR `build/linux/aur/xerahs-git/PKGBUILD`:** add `makedepends` (`cmake ninja pkgconf qt6-base layer-shell-qt wayland-protocols libdeflate`), `depends` (`qt6-base layer-shell-qt libdeflate wl-clipboard`), `optdepends` (`tesseract: OCR in the OmaSnap editor`). Install to `/usr/lib/xerahs/omasnap/`.
- **GitHub release tarball (`linux-x64`, built on ubuntu-24.04):** Ubuntu 24.04's Qt is 6.4 and may not satisfy OmaSnap; build OmaSnap in an `archlinux:latest` container job and add the binary to the Linux x64 tarball as an **optional** component. It links against the host's Qt at runtime, which is correct for Arch/Omarchy; on other distros the probe fails and XerahS falls back. `linux-arm64`: same approach if an Arch ARM image is practical, otherwise omit.
- **Flatpak:** exclude OmaSnap (sandbox and layer-shell). The probe must report absent and nothing changes.
- **deb/rpm:** exclude initially; document.
- Update `build/ci/validate_release_assets.py` and the Linux archive validation step so OmaSnap files are checked **only when present** (do not make them mandatory). Record this in `.ai/skills/publish-release/SKILL.md` under "Known iteration foot-guns" (it is exactly the kind of check that broke v0.30.4-v0.30.10).
- Standalone OmaSnap already installed in `~/.local/bin` (the maintainer has v1.21.0) must not be used by XerahS unless it passes the probe **and** reports `hostMode >= 1`. Prefer the bundled binary; allow an override path in settings for development.

### Phase 3: Capture engine in XerahS

**Key files (new):**
- `src/platform/XerahS.Platform.Linux/Services/LinuxDesktopProfile.cs`: one source of truth: `IsOmarchy`, `IsHyprland`, `OmaSnapCapabilities` (cached probe result with version), `IsOmarchyLike` (Hyprland + probe ok). Replaces the three detections in Phase 0 item 8.
- `src/platform/XerahS.Platform.Linux/Capture/OmaSnap/OmaSnapClient.cs`: runs the binary with `ArgumentList` (never a shell string), async stdout/stderr, cancellation (kills the process tree), timeout only for the probe (captures wait for the user), parses the result JSON into `OmaSnapResult`. Runtime folder `$XDG_RUNTIME_DIR/xerahs/omasnap/` (fallback private `/tmp/xerahs-<uid>/omasnap/`), deleted after the pipeline takes the PNG.
- `src/platform/XerahS.Platform.Linux/Capture/OmaSnap/OmaSnapCaptureStrategy.cs`: implements the existing strategy interfaces used by `LinuxScreenCaptureService` (`Capture/ICaptureStrategy.cs`, `Capture/LinuxRegionCaptureBackend.cs`, `Capture/Orchestration`), so it appears in the existing decision trace as `stage=OmaSnap, provider=omasnap, outcome=…`.

**Selection:**
- Add `OmaSnap = 5` to `LinuxInteractiveRegionSelectorPreference` (`src/platform/XerahS.Platform.Abstractions/LinuxInteractiveRegionSelectorPreference.cs`, current members `Automatic 0, XerahSOverlay 1, DesktopNative 2, PortalDialog 3, Slurp 4`). Never renumber existing members (settings persist names, but keep values stable anyway).
- `Automatic` resolves to OmaSnap first **only** when `LinuxDesktopProfile.IsOmarchyLike`. Otherwise `Automatic` keeps today's exact order. On OmaSnap failure (not cancel), fall through to the existing chain and log the trace; a user cancel stays a cancel.
- Settings UI: add "OmaSnap (native Hyprland overlay)" to the per-workflow Linux selector list, visible only when available, with the probe result shown ("OmaSnap 1.22.0 · Hyprland · ready").

**Workflow mapping (`WorkflowType` → OmaSnap):**

| XerahS job | OmaSnap host invocation |
|---|---|
| `RectangleRegion`, `CustomRegion` | `smart` by default, or `--capture-region` when the workflow's "region only" option is set |
| `LastRegion` | `--capture-region --region <last>` (XerahS already stores the last region) |
| `ActiveWindow` | no overlay: capture the focused window rect from `hyprctl activewindow -j` through OmaSnap `--capture-window` preselected, or keep the existing non-interactive path if faster (measure) |
| `CustomWindow` | `--capture-window` |
| `PrintScreen`, `ActiveMonitor` | `--capture-fullscreen` (focused monitor) |
| `ScrollingCapture` | `--scroll` (replaces the XerahS scrolling-capture overlay on Omarchy-like systems) |
| `ScreenColorPicker`, `Ruler`, `QRCodeScanRegion`, OCR region jobs | keep XerahS overlays for now (OmaSnap has an eyedropper and OCR, but only inside its editor; revisit later) |
| Screen recording jobs | out of scope (region selection for recording could use OmaSnap `--capture-region` result later) |

The returned PNG enters the **existing** pipeline (`CaptureJobProcessor`), so After Capture window, image effects, annotation, save, clipboard, upload and history all behave exactly as for any other capture. Store `window.class`/`title` from the result in `TaskMetadata` (used for file naming and history tags).

### Phase 4: Annotation, pins and after-capture UX

- **Annotation editor choice.** Add a Linux setting "Annotation editor: XerahS editor | OmaSnap editor" (default XerahS editor until validated; the maintainer can flip it). For `AnnotateImage` with the OmaSnap editor: save the capture to the runtime folder, run `omasnap --host xerahs --file <png> --output <png> --editor overlay`, continue with the result. XerahS-only annotation features (sidecar annotation files, re-editing from History) stay with the XerahS editor; document the difference in the setting's description.
- **Pin to screen.** On Omarchy-like systems, `PinToScreen` and `PinToScreenFromFile` use `omasnap --pin <png>` with `OMASNAP_HOST_UPLOAD_COMMAND` set to the bundled `omaxerahs upload` (so a pin's Upload button uses the user's XerahS destination). Keep the XerahS pin window elsewhere.
- **After Capture window.** Unchanged. Do not replace it with OmaSnap's timed preview in this XIP; list it as an open question.

### Phase 5: Hyprland-managed keybindings

Today XerahS registers hotkeys through the XDG GlobalShortcuts portal (or evdev, XIP0080). On the maintainer's machine this produced `xerahs:3` / `xerahs:13` portal shortcuts that took F1 and PRINT, forcing workarounds in `~/.config/hypr/bindings.lua` (Appendix A). On Hyprland the native way is a compositor binding.

- New XerahS mode "Keybindings: Hyprland (recommended on Omarchy)" when `LinuxDesktopProfile.IsHyprland`. XerahS writes a **managed file** `~/.config/hypr/xerahs.lua` (generated, header says do not edit) containing, per enabled workflow with a hotkey:
  ```lua
  -- Managed by XerahS. Changes are overwritten; edit hotkeys in XerahS.
  hl.unbind("PRINT")
  o.bind("PRINT", "XerahS: Region capture", "/usr/lib/xerahs/omaxerahs workflow run bc904c7e-4edc-4726-b53e-4f6cf8646eee")
  ```
  Use Omarchy's `o.bind(key, description, command)` when Omarchy is present, plain `hl.bind` otherwise; convert XerahS `HotkeyInfo` to Hyprland key syntax (tested mapping table).
- Loading the file requires one line in the user's config. XerahS **asks first** (Settings → Hotkeys → "Use Hyprland keybindings"): back up `~/.config/hypr/bindings.lua` to `bindings.lua.bak.<unix-ts>`, append `require("xerahs")` (or the Omarchy-documented include form, verify against `/usr/share/omarchy/default/hypr/`), run `hyprctl reload`, then `hyprctl configerrors`; roll back from the backup if errors appear. Never edit anything in `/usr/share/omarchy`.
- While Hyprland mode is on, do **not** register portal or evdev shortcuts (no double triggers). Turning it off removes the managed file's bindings and restores portal/evdev registration.
- Show conflicts before applying: read `hyprctl binds -j`, report keys already bound by Omarchy or the user, offer to unbind (the managed file emits `hl.unbind` only for keys the user approved).
- **Trigger path.** New `omaxerahs workflow run <id|name>`: sends `--run-workflow <id>` over the existing single-instance channel (same mechanism as `--reload-workflows`: `SingleInstanceManager.TrySendToRunningInstance`, `AppContracts.Cli`), handled in `XerahS.App/Program.cs` `OnArgumentsReceived` **without** raising the main window. If XerahS is not running, start it with the argument. Must return in under 50 ms for the key press to feel native; the capture itself runs in the app.
- Update the bundled agent skill (`src/desktop/cli/XerahS.OmaXerahs/AgentSkill/xerahs/SKILL.md`) and `docs/linux/omaxerahs-cli.md` with `workflow run` and the keybinding behavior; add capability `workflow.run`.

### Phase 6: Documentation, telemetry-free diagnostics, agent support

- `docs/linux/omarchy.md`: what changes on Omarchy, how to switch engines, how keybindings are managed, how to revert.
- Diagnostics: the existing Linux diagnostics command should print `LinuxDesktopProfile` and the probe JSON.
- `omaxerahs capabilities` gains `capture.omasnap` when the probe is ok; add `omaxerahs capture <region|window|fullscreen|scroll> [--workflow <id>]` so agents (see the `xerahs` skill) can trigger captures through the same path.

### Phase 7: Tests and validation

**Automated (runs in CI and cloud sessions without a display):**
- `OmaSnapClient` against a **fake omasnap**: a small script in `tests/fixtures/fake-omasnap/` that prints fixture JSON for ok/cancelled/error/garbage/timeout, exits with the matching codes, and records its argv. Assert argv per workflow (mapping table), JSON parsing, cancellation kills the process, runtime files cleaned.
- `LinuxDesktopProfile` with injected environment and a fake probe: Omarchy, Hyprland-without-Omarchy, GNOME, KDE, Sway, X11, Flatpak. Assert `Automatic` resolution **unchanged** for every non-Omarchy-like case (golden test of the current order).
- Hyprland binding generator: `HotkeyInfo` → Lua mapping table, conflict detection from a fixture `hyprctl binds -j` output, managed-file content snapshot.
- `omaxerahs workflow run`: JSON contract, sends `--run-workflow`, does not start a second instance.
- OmaSnap repo: host-mode smoke tests (Phase 1).

**Manual on the maintainer's machine (Omarchy 4.0.4, iMac20,1, `nomodeset`/llvmpipe, see Appendix A). The cloud session cannot run these; list them in the PR:**
1. Region, window, fullscreen, scroll captures through XerahS hotkeys → correct PNG, After Capture window, upload, history.
2. Toggle: pressing the hotkey while the overlay is open dismisses it; no stray capture.
3. OmaSnap editor round trip; pin with Upload button → XerahS destination URL on clipboard.
4. Hyprland keybinding opt-in: backup created, `hyprctl configerrors` clean, portal shortcuts no longer registered, revert works.
5. Remove OmaSnap binary → XerahS falls back to the old chain with a trace line, no errors.
6. On a non-Hyprland machine or VM (GNOME): no visible change.

---

## Non-Negotiable Rules

- No behavior change on non-Omarchy-like systems. `Automatic` order there is golden-tested.
- One capture pipeline. OmaSnap returns a PNG and metadata; `CaptureJobProcessor` does the rest. No parallel after-capture, upload or history code.
- OmaSnap never uploads, stores credentials, writes XerahS history, or copies to the clipboard in host mode. The only upload path from OmaSnap UI is the host command (`omaxerahs upload`).
- Never block the Avalonia UI thread on the OmaSnap process or its result (see the clipboard deadlock fixed in `dabe924e`); all process I/O is async.
- Never edit the user's Hyprland config without explicit consent, a timestamped backup, `hyprctl configerrors` validation and automatic rollback. Never touch `/usr/share/omarchy`.
- Capability probe, not distro strings, decides OmaSnap use. A failed probe is a silent fallback plus one diagnostic line, not an error-log entry.
- Do not renumber existing enum members; add new ones.
- Pass process arguments with `ArgumentList`; never build shell strings from paths or workflow names.
- Ship OmaSnap's MIT, OFL and ISC notices with every artifact that contains it.
- Keep OmaSnap's standalone behavior identical; host mode is opt-in per invocation.

---

## Deliverables

1. Phase 0 bug fixes with tests (items 1-9).
2. OmaSnap 1.22.0 in `KovaForge/omasnap` with host mode, `--host-capabilities`, hosted pin upload command, tests, README/CHANGELOG.
3. `native/omasnap` submodule, `build/linux/build-omasnap.sh`, PKGBUILD changes, CI Arch container job, optional-asset validation, publish-release skill note.
4. `LinuxDesktopProfile`, `OmaSnapClient`, `OmaSnapCaptureStrategy`, `LinuxInteractiveRegionSelectorPreference.OmaSnap`, settings UI.
5. OmaSnap annotation-editor option and OmaSnap-backed pins on Omarchy-like systems.
6. Hyprland-managed keybindings with consent flow, `--run-workflow`, `omaxerahs workflow run`, `omaxerahs capture`.
7. Tests (fake omasnap, profile matrix, keybinding generator) and a manual validation checklist in the PR.
8. Docs: `docs/linux/omarchy.md`, `docs/linux/omaxerahs-cli.md`, agent skill update, this XIP updated to Completed.

---

## Affected Components

- `KovaForge/omasnap`: `src/main.cpp` (CLI), capture/editor output paths, `src/pin.cpp` (hosted upload), `tests/`, `README.md`, `CHANGELOG.md`.
- `XerahS.Platform.Abstractions`: `LinuxInteractiveRegionSelectorPreference`.
- `XerahS.Platform.Linux`: new `Services/LinuxDesktopProfile.cs`, `Capture/OmaSnap/*`; `LinuxScreenCaptureService.cs` (selection); `LinuxPlatform.cs` (hotkey service choice lines ~130-175: portal vs evdev, plus Hyprland mode); `Services/WaylandPortalInputService.cs` (EIS); `Services/LinuxOsRelease.cs`.
- `XerahS.Core`: `Tasks/Processors/CaptureJobProcessor.cs` (metadata only), hotkey/workflow model for Hyprland binding export, `Automation/WorkflowAutomation.cs` (run workflow).
- `XerahS.App`: `Program.cs` (`--run-workflow` handling), `AgentSkillBootstrapper.cs` (use `LinuxDesktopProfile`).
- `XerahS.UI`: capture settings (engine choice, annotation editor choice), hotkey settings (Hyprland mode, conflicts), pin service routing.
- `XerahS.OmaXerahs` (`omaxerahs`): `workflow run`, `capture`, capabilities, skill.
- `XerahS.Common`: `AppContracts.Cli` (`RunWorkflowFlag`), `SingleInstanceManager` (reuse).
- Build/CI: `build/linux/*`, `build/linux/aur/xerahs-git/PKGBUILD`, `.github/workflows/release-build-all-platforms.yml`, `build/ci/validate_release_assets.py`, `.ai/skills/publish-release/SKILL.md`.

---

## Architecture Summary

```
Hyprland key press
   │  (managed ~/.config/hypr/xerahs.lua, opt-in)
   ▼
omaxerahs workflow run <id> ──single-instance──▶ XerahS (running, tray)
                                                   │ WorkflowType → OmaSnap target
                                                   ▼
                                   LinuxScreenCaptureService (Automatic)
                                     │ LinuxDesktopProfile.IsOmarchyLike?
                         yes ────────┴──────── no
                          ▼                     ▼
                OmaSnapCaptureStrategy    existing chain (wlroots → portal → slurp → overlay)
                  │ omasnap --host xerahs --output <png> --result-json -
                  ▼
            PNG + result JSON (region, window class, scale)
                  │
                  ▼
        CaptureJobProcessor (unchanged pipeline)
   After Capture window → effects → annotate (XerahS or OmaSnap editor)
   → save / clipboard → upload (XerahS destinations) → history
                  │
                  └─ PinToScreen → omasnap --pin <png>
                        (Upload button → omaxerahs upload)
```

---

## Implementation Notes

Implemented in v0.31.0 on branch `claude/xip0088-omasnap-capture-0awcv6` (KovaForge/XerahS) with OmaSnap host mode in KovaForge/omasnap. User documentation: [docs/linux/omarchy.md](../../linux/omarchy.md) and [docs/linux/omaxerahs-cli.md](../../linux/omaxerahs-cli.md). Where the build differs from the plan above:

- **Include line.** The user config gets `do local path = "<abs>/xerahs.lua"; ... dofile(path) end` instead of `require("xerahs")`. Omarchy's module path is `~/.config/?.lua` (so `require` would need `hypr.xerahs`), plain Hyprland Lua setups have no such path, and the existence check keeps Hyprland loading if the managed file is deleted. The line goes into `bindings.lua` when it exists, otherwise `hyprland.lua`. Hyprland keybindings need the Lua config; `hyprland.conf` setups keep portal or evdev hotkeys.
- **Conflicts.** A key that Omarchy or the user already binds is left out of the managed file unless the user ticks it, in which case the file emits `hl.unbind` first. Binds from an earlier XerahS file are recognised by their `XerahS: ` description and never count as conflicts.
- **Hotkeys in Hyprland mode.** Only workflow hotkeys move to Hyprland. The assistant and capture command palette hotkeys keep the portal or evdev service. The mode is restored at startup only while the user config still includes the managed file.
- **`omaxerahs capture window`** runs a CustomWindow job with no window name when OmaSnap fronts captures (OmaSnap's window picker) and ActiveWindow otherwise.
- **Latency.** The plain forms of `workflow run` and `capture` bypass System.CommandLine and the reflection serializer. Measured in the cloud container on a Release single-file build: about 50 ms when XerahS is not reachable and 55 to 90 ms when the request is delivered, against a runtime start floor of about 30 ms; the 50 ms target is met only on the first path. ReadyToRun brought little and adds about 80 MB, so it is not used.
- **Diagnostics.** `xerahs doctor --linux-desktop [--json]` prints the profile, the OmaSnap search order and the probe JSON, and the Hyprland keybinding state. `omaxerahs capabilities` adds `workflow.run`, `capture`, and `capture.omasnap` when the probe passes.
- **Packaging.** OmaSnap ships in the linux-x64 tarball (built by the `build-omasnap` Arch container job, `continue-on-error`) and the `xerahs-git` AUR package. deb, rpm, AppImage, Flatpak and arm64 artifacts do not carry it (Open Question 5).
- **Open questions** keep this XIP's defaults: the After Capture window stays; the XerahS editor stays the default editor; `feature/uploader` is not merged into OmaSnap and XerahS-hosted OmaSnap has no uploader of its own; host mode is fork-only for now.

---

## Open Questions

1. Should OmaSnap's timed preview replace or complement the XerahS After Capture window on Omarchy? (Default in this XIP: keep After Capture.)
2. Default annotation editor on Omarchy once host mode is validated: XerahS or OmaSnap?
3. Merge `feature/uploader` into OmaSnap `main` before host mode, or keep uploads out of the XerahS-bundled build entirely?
4. Upstream host mode to `tobi/omasnap`, or keep it fork-only?
5. Release tarball: ship the Arch-built OmaSnap in the generic `linux-x64` tarball (probe-gated), or only in the AUR package?

---

## Appendix A: Maintainer machine facts (for sessions without local access)

Collected 2026-09-28. A cloud session cannot see these; treat them as the reference environment.

- Hardware/OS: iMac20,1, Omarchy 4.0.4-1, Hyprland with Lua config, kernel `7.2.6-arch2-Watanare-T2-4-t2`, boots with `nomodeset` (simple-framebuffer, llvmpipe; GPU capture tools such as gpu-screen-recorder fail, `wf-recorder` works).
- Omarchy screenshot command: `omarchy-capture-screenshot [smart|region|windows|fullscreen] [slurp|copy|save] [--editor=<name>]`, grim/slurp based, editor `OMARCHY_SCREENSHOT_EDITOR` default `tensaku-edit`, output `~/Pictures/screenshot-<date>_<time>.png`, notifications via `omarchy-notification-send`. Omarchy does not ship OmaSnap.
- OmaSnap: standalone v1.21.0 installed at `~/.local/bin/omasnap`; local checkout `/home/mike/Work/KovaForge/omasnap` on branch `feature/uploader` (pushed). OmaSnap's documented Omarchy binding: `o.bind("PRINT", "Screenshot", "omasnap")` plus `hl.layer_rule({ match = { namespace = "^omasnap$" }, no_anim = true, animation = "none" })`; do not add `no_screen_share` to that rule (it blacks out scroll capture).
- User Hyprland overrides in `~/.config/hypr/bindings.lua` caused by current XerahS portal shortcuts:
  ```lua
  -- Screenshot binding: plain F1 is taken by the XerahS portal
  -- (xerahs:3 / xerahs:13), so bind Shift+F1 instead.
  hl.unbind("PRINT")
  o.bind("SHIFT + F1", "Screenshot", "omarchy-capture-screenshot")
  -- Screencast: ALT+PRINT moved to SHIFT+F8 because XerahS claims PRINT
  hl.unbind("ALT + PRINT")
  o.bind("SHIFT + F8", "Screencast", "/home/mike/.local/bin/screencast-wfrec")
  ```
- XerahS install: `~/.local/lib/xerahs` (unpacked Linux tarball, not a pacman package), launched by `~/.config/autostart/XerahS.desktop` with SilentRun (tray). Region-capture workflow `Region capture` (`bc904c7e-4edc-4726-b53e-4f6cf8646eee`, job `RectangleRegion`, hotkey Ctrl+F1 via portal). Settings `~/.config/xerahs/WorkflowsConfig-omarchy.json`, logs `~/.local/state/xerahs/Logs/yyyy-MM/`.
- Recent capture traces (09-17..28): region `wlroots` succeeded 23, failed 14 (then no provider or portal fallback); portal region 8; fullscreen via portal 15.
- XerahS D-Bus: tray is `org.kde.StatusNotifierItem-<pid>-0` with an Avalonia dbusmenu; `busctl --user call <sni> <menu> com.canonical.dbusmenu GetLayout iias 0 1 0` lists the tray items (useful to verify the UI thread is responsive).

## Appendix B: Suggested cloud-session brief

> Implement XIP0088 (`docs/proposals/xip/XIP0088-omasnap-native-omarchy-capture-engine.md` in KovaForge/XerahS `develop`). Work in both `KovaForge/XerahS` and `KovaForge/omasnap`. Follow each repo's `AGENTS.md` (XerahS: commit prefixes `[v0.31.0] [Type] …`, `dotnet build src/desktop/XerahS.sln` must pass with 0 errors and `tests/XerahS.Tests` must pass before pushing; OmaSnap: main thread never blocks, no new dependencies). Do Phase 0 first, then Phases 1-7 in order, one commit per logical change, push after each phase. You have no access to a Hyprland session: use the fake-omasnap fixtures and unit tests, and put the Appendix A manual checklist in the PR description for the maintainer to run. Do not change behavior on non-Omarchy-like systems.
