# XIP0079 Linux Improvement Plan

**Status**: Implemented (P1–P5 landed 2026-07-07 in v0.23.129; XIP0044 hotkey verification matrix and distro smoke tests remain manual)  
**Priority**: High  
**Area**: Linux | Platform | Hotkeys | Notifications | Clipboard | Packaging  
**Targets**: Ubuntu 24.04+, Fedora (current), Arch — X11 and Wayland (GNOME, KDE Plasma, wlroots)  
**Related**: XIP0014 (complete), XIP0044, XIP0046, XIP0047, XIP0051 (complete), XIP0058 (complete), XIP0075 (complete), KNOWN_ISSUES.md, `developers/linux/INSTALL.md`, `docs/linux/`  
**Created**: 2026-06-12  

This plan is grounded entirely in repo inspection (file paths cited inline). It supersedes the backlog sections of XIP0014 and consolidates the open ends of XIP0044/0046/0047 into a single prioritized, executable backlog.

---

## 1. Current state assessment (evidence-based)

Linux support is **substantially more mature than "port in progress"**. The platform layer (`src/platform/XerahS.Platform.Linux/`, ~75 source files) already implements:

| Area | State | Evidence |
|---|---|---|
| Screen capture (screenshot) | **Strong.** Multi-provider waterfall: Portal, GNOME D-Bus, KDE D-Bus, wlroots, X11 `XGetImage`, CLI (`grim`/`slurp`/`spectacle`/`gnome-screenshot`/`scrot`/`import`) with decision tracing | `Capture/Orchestration/LinuxCaptureCoordinator.cs`, `Capture/Providers/*`, `Capture/Orchestration/WaterfallCapturePolicy.cs` |
| Region selection | **Good with known limits.** In-app overlay default; `UseModernCapture` toggles portal path; mixed-DPI fixes for GNOME (logical) and KDE (physical) portal bitmaps | XIP0047; `ScreenCaptureService.cs`, `LinuxScreenCaptureService.cs`, `OverlayWindow.axaml.cs` |
| Global hotkeys | **Functional on portal-capable DEs; fragile elsewhere.** XDG GlobalShortcuts portal primary, `XGrabKey` X11 fallback; app-id/parentWindow/debounce fixes landed; verification still pending | XIP0044 changelog; `Services/WaylandPortalHotkeyService.cs` (1028 lines), `Services/LinuxHotkeyService.cs` |
| Screen recording | **Good.** ScreenCast portal + PipeWire via `wf-recorder`, GStreamer (GL + non-GL fallback), or FFmpeg | `Recording/WaylandPortalRecordingService.cs` |
| Clipboard | **Good in-app; weak edge cases.** UI uses Avalonia clipboard (`DataTransfer`/`SetBitmap`); CLI service (`wl-copy`/`xclip`) is the non-UI fallback and fails silently when tools are absent | `XerahS.UI/App.axaml.cs:231-241`, `XerahS.UI/Services/AvaloniaClipboardService.cs`, `Services/LinuxClipboardService.cs` |
| Clipboard monitor | **Good.** `wl-paste --watch` event-driven on Wayland; polling on X11; disabled in sandbox | `Services/LinuxClipboardMonitorService.cs`, `LinuxPlatform.cs:40-42` |
| Notifications | **Functional but lossy.** Portal `AddNotification` (sandbox-aware) → `notify-send` fallback; **action buttons are silently dropped** (flattened into body text) | `Services/PortalNotificationService.cs:80-86`, `Services/LinuxNotificationService.cs:46-52` |
| Tray | **Works where StatusNotifier exists.** Monochrome icon on Linux; GNOME needs appindicator extension (documented, `Suggests:` in .deb) | `XerahS.UI/TrayIconHelper.cs:140-144`, `build/linux/XerahS.Packaging/Program.cs:306-315` |
| Watch folders / upload triggers | **Strong.** Dedicated daemon managed via systemd user/system units | `Services/LinuxWatchFolderDaemonService.cs` (511 lines) |
| XDG compliance | **Done.** Config/state/cache/logs under XDG base dirs; no home-dir litter | XIP0075 (complete), `docs/linux/xdg-storage.md` |
| Packaging | **Multi-format, uneven docs.** `.tar.gz` + `.deb` + `.rpm` (`build/linux/package-linux.sh` + `XerahS.Packaging`), AUR (`package-aur.sh`), Flatpak manifest (`flatpak/com.xerahs.XerahS.yml`). No AppImage. Install docs are Arch-only | `build/linux/`, `developers/linux/INSTALL.md` |
| Theme/fonts/HiDPI | **Functional.** `gsettings`-based theme detection (pipe-drain hardened v0.23.92); per-monitor scale normalization with one known vertical-stacking bug | `Services/LinuxThemeService.cs`, `XerahS.RegionCapture/Services/WaylandMonitorLayoutNormalizer.cs:69` |
| Build targets | **Already cross-platform.** Conditional TFMs: `net10.0` on Linux/macOS, `net10.0-windows10.0.26100.0` on Windows; `LINUX` define + conditional `Platform.Linux` project reference | `XerahS.Bootstrap.csproj:4-22` |

**Current Linux support level: 6.5/10.** Capture, recording, XDG hygiene, and packaging plumbing are genuinely good. The score is held down by: unverified hotkey fixes (the #1 user-reported pain, issues #63/#64), silent degradation paths (clipboard, notifications) that make failures look like bugs, mixed-DPI edge cases, and Arch-only install documentation despite Ubuntu/Fedora being the larger audience.

---

## 2. Prioritized backlog

| # | Item | Impact | Effort | Risk | Section |
|---|---|---|---|---|---|
| P1 | Close XIP0044/0046 hotkey loop: diagnostics surface + verification matrix + `ConfigureShortcuts` fallback UX | **Very high** (top user complaint) | M | Low | §3.1 |
| P2 | Notification action buttons (portal `buttons` + `ActionInvoked`; `notify-send --action` fallback; stop blocking caller thread) | High (workflow parity: "Open URL" after upload) | M | Low | §3.2 |
| P3 | Clipboard out-of-box resilience: capability probe + user-facing warning, `Recommends:` in .deb/.rpm, post-exit persistence | High (silent failure on stock Ubuntu for non-UI paths) | S | Low | §3.3 |
| P4 | Mixed-DPI vertical-stacking fix in `WaylandMonitorLayoutNormalizer` | Medium (correctness bug, bounded) | S | Medium (geometry regressions) | §3.4 |
| P5 | Packaging & docs parity: Ubuntu/Fedora install docs, deb/rpm metadata, distro test matrix | High (adoption) | S–M | Low | §3.5 |
| P6 | Region-capture overlay polish: first-input latency telemetry follow-up, KDE physical-bitmap heuristic hardening | Medium | M | Medium | §4 |
| P7 | Native-Wayland future-proofing: `zxdg_exporter_v2` parentWindow export (only needed if/when Avalonia ships a Wayland backend) | Low today | L | High | §4 |
| P8 | AppImage decision (recommend: defer; document rationale) | Low | M | Medium | §4 |

---

## 3. Top 5 items — implementation outlines

### 3.1 P1 — Close the global-hotkey loop (XIP0044/XIP0046)

**Problem.** All four root causes from XIP0044 (app-id mismatch, packaging symlink, CTS dispose race, `parentWindow` startup race) have fixes landed, but: (a) end-to-end verification on GNOME/KDE/wlroots is still pending; (b) when the portal path fails, users get *silent* fallback to X11 grabs that don't fire under Wayland — indistinguishable from "hotkeys are broken"; (c) `ConfigureShortcuts` (portal v2) failure handling is log-only (`WaylandPortalHotkeyService.cs:173-178`).

**Implementation.**

1. **Surface hotkey delivery state in the UI.** Add to `IHotkeyService` (default-interface method, so Windows/macOS unaffected — same pattern as `NotifyWindowReady()`):

   ```csharp
   // XerahS.Platform.Abstractions/Services/IHotkeyService.cs
   public enum HotkeyBackendState { Native, PortalBound, PortalPending, X11FallbackFocusOnly, Unavailable }

   public record HotkeyDiagnostics(
       HotkeyBackendState State,
       string BackendName,          // "GlobalShortcuts portal", "XGrabKey (X11)", ...
       string? UserFacingWarning);  // null when healthy

   HotkeyDiagnostics GetDiagnostics() => new(HotkeyBackendState.Native, "native", null);
   ```

   `WaylandPortalHotkeyService` returns `PortalBound` after `BindShortcuts response=0`, `X11FallbackFocusOnly` when `_portalUnavailableForSession` is set, with warning text: *"Global shortcuts portal unavailable — hotkeys only fire while XerahS is focused. See Hotkey Troubleshooting."* The hotkey settings page and the existing diagnostic report (`LinuxDiagnosticService` already probes `org.freedesktop.portal.GlobalShortcuts`, lines 97/106) render this state with a warning banner.

2. **`ConfigureShortcuts` graceful fallback** (XIP0044 open question 1): probe portal `version` property before offering the native "configure in DE settings" button; if `< 2` or the call throws, fall back to the in-app hotkey recorder UI (which already exists) instead of only logging.

3. **Execute the XIP0044 verification matrix** and record results in the XIP:
   - GNOME ≥45 Wayland (Ubuntu 24.04, Fedora), KDE Plasma 6 Wayland, Hyprland/Sway (wlroots), X11 fallback session.
   - Per XIP0044 §Verification: expect `OnWindowOpened … descriptor=XID` → `NotifyWindowReady` → `CreateSession response=0` → `BindShortcuts response=0` → `Portal bind succeeded; releasing X11 fallback hotkeys.`
   - Confirm issue #63 Print-key fix (`aa579f0`) and #64 cancel-behavior fix (`ee6d0fa`) with the test matrices already posted on those issues.

**Verification.** Manual matrix above + unit test for `GetDiagnostics()` state transitions (bind success, bind fail, window-ready retry). Log assertions match XIP0044's expected sequence.

**Rollback.** Diagnostics API is additive (default interface method); UI banner behind the existing settings page — revert the two commits independently. No change to binding logic itself.

---

### 3.2 P2 — Notification actions (parity with Windows toasts)

**Problem.** `INotificationService.ShowNotification(title, message, actionText, action, …)` is the after-upload "Open URL / Open folder" path. On Linux both implementations destroy the action:

- `PortalNotificationService.cs:80-86` — flattens `actionText` into the body string and never registers a button or listens for `ActionInvoked`.
- `LinuxNotificationService.cs:46-52` — appends `({actionText})` to the message.

Also: `LinuxNotificationService.WaitForSuccessfulExit(process, 2000)` blocks the **calling thread up to 2 s** per notification (`LinuxNotificationService.cs:60-69`) — a UI stall when `notify-send` hangs.

**Implementation.**

1. **Portal path** — `org.freedesktop.portal.Notification` supports a `buttons` array (`aa{sv}` with `label` + `action`) and emits `ActionInvoked(string id, string action, av parameter)`:

   ```csharp
   // PortalNotificationService.cs — extend the existing proxy
   [DBusInterface("org.freedesktop.portal.Notification")]
   public interface INotificationPortal : IDBusObject
   {
       Task AddNotificationAsync(string id, IDictionary<string, object> notification);
       Task RemoveNotificationAsync(string id);
       Task<IDisposable> WatchActionInvokedAsync(Action<(string id, string action, object[] parameter)> handler);
   }

   // In ShowNotification(title, message, actionText, action, type):
   var notification = new Dictionary<string, object>
   {
       ["title"] = title,
       ["body"] = message,
       ["priority"] = MapPriority(type),
       // NOTE: drop "transient" display-hint for actionable notifications so they persist
       ["buttons"] = new[]
       {
           new Dictionary<string, object> { ["label"] = actionText, ["action"] = $"xerahs.act.{id}" }
       }
   };
   // Register `action` callback in a ConcurrentDictionary<string, Action> keyed by action id;
   // one WatchActionInvokedAsync subscription per service lifetime dispatches to it.
   // Evict entries on RemoveNotification / TTL (e.g. 10 min) to avoid leaks.
   ```

2. **`notify-send` fallback** — libnotify ≥ 0.7.9 (Ubuntu 24.04 ships 0.8.x) supports `--action=KEY=Label` and prints the chosen key to stdout when activated. Spawn with `RedirectStandardOutput = true`, `--wait`, parse the printed key, invoke the callback. Probe support once via `notify-send --help` contains `--action` (cache result). If unsupported, keep today's text-flattening behavior.

3. **Stop blocking the caller**: wrap both paths in `Task.Run` fire-and-forget with exception logging; `ShowNotification` returns immediately. (The action callback already must marshal to the UI thread via `Dispatcher.UIThread.Post` before touching UI.)

**Verification.**
- GNOME + KDE: upload a file with "copy URL + open in browser" toast configured → notification shows a button; clicking it opens the URL. `gdbus monitor --session --dest org.freedesktop.portal.Desktop` shows `ActionInvoked`.
- Stock Ubuntu (no portal kill switch): fallback path with `--action` works under GNOME's notification server.
- Sandboxed (Flatpak): portal path only; confirm no `notify-send` spawn attempt (`allowNativeFallback: false` already wired at `LinuxPlatform.cs:143`).
- Unit-test the action-registry dispatch and TTL eviction.

**Rollback.** Both changes are confined to the two Linux notification classes; revert restores text-flattening behavior. No schema/settings changes.

---

### 3.3 P3 — Clipboard out-of-box resilience

**Problem.** The UI path is healthy (Avalonia `DataTransfer` clipboard, `App.axaml.cs:231-241`). But:

1. `LinuxClipboardService` (used by non-UI contexts: CLI tool, daemon, pre-window startup) shells out to `wl-copy`/`xclip` only. On stock Ubuntu GNOME **neither `wl-clipboard` nor `xclip` is installed** — `SetTextAsync` exhausts its fallbacks and returns with **no error surfaced** (`LinuxClipboardService.cs:55-67`).
2. The `.deb` control file declares no `Recommends:` for these tools (`XerahS.Packaging/Program.cs:298-315` — only `Suggests: gnome-shell-extension-appindicator`).
3. X11/Wayland clipboard ownership dies with the app: copy → quit XerahS → paste fails (Avalonia owns the selection in-process; no clipboard-manager handoff). The CLI service already keeps `_clipboardOwnerProcess` for exactly this (`LinuxClipboardService.cs:44-45,294`), but the UI path never uses it.

**Implementation.**

1. **Capability probe + surfaced warning.** At Linux startup, probe `which wl-copy` / `which xclip` (per session type) once; expose via `LinuxDiagnosticService` and show a one-time settings-page hint when the relevant tool is missing:
   *"Install `wl-clipboard` (Wayland) / `xclip` (X11) for clipboard support in background workflows: `sudo apt install wl-clipboard`."*
   In `LinuxClipboardService`, when all fallbacks fail, log at warning level with the same hint (today: silent).

2. **Packaging metadata.** In `CreateDeb`: `Recommends: wl-clipboard, xclip` (apt installs Recommends by default → fixes stock Ubuntu installs). Mirror in the RPM spec (`Recommends:` weak dep, supported by dnf) and the AUR PKGBUILD `optdepends` (already documented in INSTALL.md).

3. **Post-exit persistence (Wayland first).** After a successful Avalonia image/text copy in `AvaloniaClipboardService`, if `wl-copy` exists, additionally pipe the same payload through the CLI service's owner-process mechanism (`wl-copy` daemonizes and survives app exit). Behind a setting (`PersistClipboardAfterExit`, default on for Wayland, off for X11 where clipboard managers are common). Skip in sandbox (portal/permissions).

**Verification.** Fresh Ubuntu 24.04 VM: install .deb → confirm apt pulls `wl-clipboard`; capture → copy → quit XerahS → paste in Firefox succeeds. Remove tools → settings page shows hint; debug log shows the warning on CLI-path copy.

**Rollback.** Probe/warning and packaging lines are independently revertible. Persistence is behind a setting; default-off ships if regressions appear (e.g., double-ownership flicker with KDE Klipper).

---

### 3.4 P4 — Mixed-DPI vertical-stacking fix in the monitor normalizer

**Problem.** XIP0047 "Known limit": `WaylandMonitorLayoutNormalizer.cs:69` computes

```csharp
double physicalY = ScaleToPhysical(screen.Layout.Bounds.Y - minLogicalY, screen.ScaleFactor);
```

i.e. *this monitor's* scale applied to the *global* logical offset. For vertically stacked monitors with different scales (e.g. 4K@200% above FHD@100%), the lower monitor's physical Y must be the **sum of the physical heights of the monitors above it**, not `logicalOffset × ownScale`. Result: overlay/crop misalignment on vertical mixed-DPI layouts (the same class of bug already fixed for the horizontal case).

**Implementation sketch.** Replace per-monitor linear scaling with cumulative physical layout along each axis:

```csharp
// Order screens by logical Y; walk the stack accumulating physical heights.
// For each screen, physicalY = physical bottom edge of the nearest screen above it
// that overlaps it horizontally (in logical space); analogous for X with left-neighbors.
// Fall back to the current formula when a screen has no neighbor on that axis
// (single row / single column cases keep today's behavior).
```

Concretely: build a small graph of adjacency in logical space (Avalonia/compositor coordinates are gap-free and consistent), then assign physical origins via topological walk from the top-left-most screen — `physicalOrigin(next) = physicalOrigin(prev) + physicalSize(prev)` along the shared edge. ~60 lines, fully unit-testable with no display required.

**Verification.** Unit tests in `tests/` covering: 2-wide horizontal mixed-DPI (existing behavior must not change), 2-high vertical mixed-DPI (new), 2×2 grid mixed-DPI, single monitor, equal scales (identity). Manual: Fedora GNOME VM with stacked virtual monitors at 100%/200% → region capture aligns.

**Rollback.** Pure function with golden-value unit tests; revert the single file. Keep the old path behind `XERAHS_LEGACY_MONITOR_NORMALIZER=1` env check for one release to de-risk field regressions.

---

### 3.5 P5 — Packaging & documentation parity (Ubuntu/Fedora/Arch)

**Problem.** `developers/linux/INSTALL.md` is Arch-only (pacman/makepkg). Ubuntu and Fedora — the stated primary targets — have no documented from-source or package path, even though `build/linux/package-linux.sh` already produces `.deb`/`.rpm`/`.tar.gz`. `scripts/fedora/` contains only a VS Code updater. Flatpak docs exist (`docs/linux/`) but the three paths are not connected anywhere.

**Implementation.**

1. **Rewrite `developers/linux/INSTALL.md`** (or split per-distro) with verified commands:

   ```bash
   # Prerequisites (Ubuntu 24.04+)
   sudo apt install dotnet-sdk-10.0 nodejs npm        # node 18+; use nodesource if archive lags
   # Fedora
   sudo dnf install dotnet-sdk-10.0 nodejs npm

   # Build + package from repo root (produces .tar.gz, .deb, .rpm in dist/)
   ./build/linux/package-linux.sh

   # Install
   sudo apt install ./dist/XerahS-<version>-linux-x64.deb     # Ubuntu
   sudo dnf install ./dist/XerahS-<version>-linux-x64.rpm     # Fedora

   # Run from source (development)
   dotnet run --project src/desktop/app/XerahS.App
   ```

   Include the **debug-build portal caveat** from XIP0044 Fix 2b verbatim: portal GlobalShortcuts reject binds from binaries not matching a `.desktop` `Exec=` line; document the `~/.local/share/applications/xerahs.desktop` + `update-desktop-database` workaround for `dotnet run` users — without this, every developer reproduces the "hotkeys broken" bug locally.

2. **Per-DE runtime dependency table** (one table, reused from INSTALL.md's optional-deps section, extended with apt/dnf names): `wl-clipboard`, `xclip`, `xdotool`, `grim`+`slurp`, `wf-recorder`, `gnome-shell-extension-appindicator`, `webkit2gtk`.

3. **Distro test matrix** as the release gate for Linux claims (this plan's §5 success criteria): Ubuntu 24.04 GNOME Wayland, Ubuntu 24.04 X11 session, Fedora (current) GNOME Wayland, Arch KDE Plasma 6 Wayland, Arch Hyprland. Smoke script: capture region → clipboard paste → notification → tray menu → hotkey while unfocused → record 10 s.

4. **Metadata fixes riding along:** `Recommends:` line (§3.3), and verify the `.deb` postinst/`.desktop`/icon layout matches the AUR layout that XIP0044's symlink fix established (`/usr/bin/xerahs` → real symlink — `WriteTarSymlinkEntry` already exists in `XerahS.Packaging/Program.cs`).

**Verification.** Run the documented commands verbatim in Ubuntu 24.04 and Fedora VMs (the repo's `docs/linux/flatpak-vm-validation.md` runbook pattern applies); each command block in the doc must be copy-paste-clean.

**Rollback.** Docs + packaging metadata only; no app behavior change.

---

## 4. Deliberately deferred (with rationale)

- **P7 `zxdg_exporter_v2` parentWindow export** (XIP0044 open question 4): Avalonia currently runs via the X11 backend under XWayland, so the handle descriptor is `XID` and the landed `NotifyWindowReady` retry covers it. The wayland-native path only matters if/when Avalonia ships a Wayland backend. Keep the XIP0044 sketch; do not build P/Invoke plumbing speculatively. **Trigger to revisit:** `OnWindowOpened` log shows `descriptor=wl_surface`.
- **P8 AppImage**: three formats (+AUR, +Flatpak) are already maintained; AppImage adds glibc/runtime-matrix burden for a .NET self-contained app whose portal integration depends on host `.desktop` registration (which AppImage complicates — same app-id matching problem as XIP0044 Fix 2). Recommend Flatpak as the "universal" answer; document this in FAQ.
- **InputCapture portal** (XIP0046 Issue E): correctly degrades; logging already improved (`c6e9dd21`). No action until compositor support matures.
- **Recording GStreamer DMABuf/GL negotiation polish**: fallback pipeline already handles `not-negotiated` (`WaylandPortalRecordingService.cs:135-152`); revisit after P1–P5.
- **Flathub submission**: readiness work is complete (XIP0075); the submission itself is a human-led process per that XIP's provenance requirements — explicitly out of agent scope.

---

## 5. Success criteria

1. Hotkey state is *visible*: a user on any DE can open settings and see whether global shortcuts are portal-bound, focus-only, or unavailable — with a fix-it hint. XIP0044 verification matrix executed and recorded on GNOME/KDE/wlroots.
2. After-upload notification actions work on GNOME and KDE (button click opens URL), natively and in Flatpak.
3. Fresh Ubuntu 24.04 `.deb` install: capture → auto-copy → paste in another app → quit → paste again, all succeed with zero manual package installs.
4. Vertical mixed-DPI region capture aligns (unit tests + Fedora VM check).
5. A developer on Ubuntu/Fedora/Arch can go from `git clone` to a running, hotkey-capable build using only `developers/linux/INSTALL.md`.
6. KNOWN_ISSUES.md Linux section updated to reflect post-fix reality (no stale "broken" claims, no unverified "fixed" claims).

## 6. Verification commands (conceptual, per target VM)

```bash
# Portal surface present?
busctl --user list | grep org.freedesktop.portal
gdbus introspect --session --dest org.freedesktop.portal.Desktop \
  --object-path /org/freedesktop/portal/desktop | grep -E "GlobalShortcuts|Screenshot|ScreenCast|Notification"

# Hotkey bind trace (expected sequence per XIP0044 §Verification)
./run-debug-app.sh 2>&1 | grep -E "WaylandPortalHotkeyService|OnWindowOpened"

# Clipboard tools
which wl-copy xclip; echo test | wl-copy && wl-paste

# Notification actions (after P2)
gdbus monitor --session --dest org.freedesktop.portal.Desktop | grep ActionInvoked
```

---

## 7. Remaining-gap roadmap (post P1–P5)

| Gap | Why it remains | Watch trigger |
|---|---|---|
| Pure-Wayland window handle export | No Avalonia Wayland backend yet | Avalonia release notes; `descriptor=wl_surface` in logs |
| Wayland cursor position / window enumeration limits | Protocol security model; per-compositor helpers already exist (`Wayland/WindowQuery/*`) | New portal interfaces (InputCapture maturing) |
| GNOME tray requires extension | GNOME removed StatusNotifier host | GNOME upstream policy change |
| Portal UI inconsistency across DEs | Freedesktop spec leaves UI to backend (XIP0046 Issue B) | Document, don't fight |
| KDE physical-bitmap 2% heuristic | Needs per-monitor logical↔physical mapping to remove (XIP0047 Issue 3) | Field reports of false positives |

---

## 8. Self-assessment

**Linux support level today: 6.5/10** (strong architecture and capture stack; unverified hotkey fixes, silent degradation paths, doc gaps).

**Linux support level after P1–P5: 8/10.** The remaining 2 points are structurally external: Avalonia's missing Wayland backend, GNOME tray policy, portal backend UI variance, and the long tail of compositor-specific behavior that only sustained field testing retires.

---

## 9. Implementation notes (2026-07-07, v0.23.129)

| Item | Landed | Notes |
|---|---|---|
| P1 Hotkey diagnostics | Yes | `HotkeyDiagnostics` on `IHotkeyService`; settings banner; `ConfigureShortcuts` gated on GlobalShortcuts portal v2+ via `PortalInterfaceChecker.TryGetInterfaceVersion`. Manual XIP0044 matrix not yet recorded. |
| P2 Notification actions | Yes | `PortalNotificationService` `buttons` + `ActionInvoked`; `notify-send --action --wait` async fallback; caller no longer blocks up to 2s. |
| P3 Clipboard resilience | Yes | `LinuxClipboardCapabilities` probe; settings + diagnostic warnings; `.deb`/`rpm` `Recommends: wl-clipboard, xclip`; `PersistClipboardAfterExit` + `LinuxClipboardExitPersistence` via `wl-copy` owner. |
| P4 Mixed-DPI normalizer | Yes | Cumulative row/column physical origins; `XERAHS_LEGACY_MONITOR_NORMALIZER=1` rollback; unit tests for vertical + 2×2 grid. |
| P5 Docs parity | Yes | `developers/linux/INSTALL.md` rewritten (Ubuntu/Fedora/Arch); `KNOWN_ISSUES.md` Linux section updated; distro smoke matrix table added (manual runs pending). |
| P7 zxdg_exporter_v2 | Deferred | Per §4 — no Avalonia Wayland backend yet. |
| P8 AppImage | Deferred | Per §4 — Flatpak preferred. |
