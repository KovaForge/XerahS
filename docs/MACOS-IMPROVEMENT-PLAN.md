# XerahS macOS Improvement Plan

**Date:** 2026-06-13
**Targets:** macOS 15 Sequoia / 14 Sonoma (primary), 12.3 Monterey (floor for ScreenCaptureKit)
**Scope:** Desktop app (`XerahS.App` + `XerahS.Platform.MacOS`). iOS/mobile out of scope.
**Status of every claim below:** verified by repo inspection on 2026-06-13. File:line references are to `develop` @ `6eac1277`.

---

## 1. Evidence-based state assessment

The common assumption that XerahS is "Windows-centric with macOS stubs" is **stale**. macOS support is substantial:

| Area | State | Evidence |
|---|---|---|
| Platform project | 28 source files, ~5,400 lines | `src/platform/XerahS.Platform.MacOS/` |
| TFM / build | Plain `net10.0` + `MACOS` define on osx RIDs; Windows TFM only on Windows | `src/desktop/app/XerahS.Bootstrap/XerahS.Bootstrap.csproj:5-9`, `XerahS.App.csproj:9-12` |
| Screen capture | Triple stack: ScreenCaptureKit native bridge → Quartz `CGDisplayCreateImage` → `screencapture` CLI | `MacOSScreenCaptureKitService.cs`, `Capture/QuartzCaptureStrategy.cs:118`, `MacOSScreenshotService.cs:39` |
| Native bridge | Universal (arm64+x86_64) ObjC dylib: still capture + recording (`sck_start/stop/abort_recording`) | `native/macos/screencapturekit_bridge.{h,m}` (694 lines), `Makefile` (`-arch x86_64 -arch arm64`) |
| Recording | Native SCK recording service | `MacOSNativeRecordingService.cs` (305 lines) |
| Hotkeys | SharpHook global keyboard hook with event suppression; AX trust prompt via `AXIsProcessTrustedWithOptions` | `Services/MacOSHotkeyService.cs:82-84,420-446`, `Native/Accessibility.cs:63-113` |
| Window mgmt | AppleScript (`osascript` + System Events): frontmost info, activate, minimize/zoom, move/resize | `MacOSWindowService.cs`; limitations self-documented in `docs/planning/macos_support_analysis.md` |
| Clipboard | `pbcopy`/`pbpaste` + AppleScript for PNG/file lists | `MacOSClipboardService.cs` (414 lines) |
| Menu bar / Dock | Monochrome template-style tray icon; menu-bar-only mode via `NSApplication setActivationPolicy:` (objc_msgSend) | `XerahS.UI/TrayIconHelper.cs:140-144,643`, `Services/MacOSApplicationActivationPolicy.cs` |
| Run at login | LaunchAgent plist (`com.xerahs.app.startup`) | `Services/MacOSStartupService.cs` |
| Watch folders | launchd-managed daemon (`launchctl bootstrap`), bundled into the .app | `Services/MacOSWatchFolderDaemonService.cs:119-156`; CI validates daemon presence in archive |
| Packaging | `.app` bundle built by MSBuild target + `build/macos/package-mac.sh`; CI builds arm64+x64 tarballs on `macos-15` | `XerahS.App.csproj:59-104`, `.github/workflows/release-build-all-platforms.yml:132-183`, `dist/XerahS-0.23.65-mac-*.tar.gz` |
| Settings UI | macOS capture preferences (native crosshair vs in-app selector, capture sound) | `XerahS.UI/ViewModels/SettingsViewModel.MacOSCapture.cs` |
| Tests | Dedicated macOS test suites | `tests/XerahS.Tests/Platform/MacOS/` (5 files) |

**Current macOS support level: 6/10.** The capture/hotkey/tray core works for a developer who builds from source and clicks through permission prompts. What's missing is everything that makes it work for a *user*: trustworthy distribution (unsigned → "damaged" Gatekeeper error, documented as an `xattr -cr` workaround in `README.md:142-152`), a sane permission story, and closing the gaps the platform layer papers over with fallbacks.

### The real gaps (each verified)

- **G1 — No code signing, hardened runtime, or notarization anywhere.** `build/macos/package-mac.sh` (335 lines) has zero `codesign`/`notarytool`/`hdiutil` calls; it tars the bundle. CI uploads raw tarballs. Every download hits Gatekeeper "damaged" unless the user strips quarantine. Unsigned also means the app's TCC identity is unstable — every rebuild can invalidate previously granted Screen Recording/Accessibility permissions, forcing re-grants.
- **G2 — Generated Info.plist is minimal.** `XerahS.App.csproj:74-91` emits only `CFBundleExecutable/Identifier/Name/PackageType/Version`. Missing: reverse-DNS bundle ID (currently the literal string `XerahS.App`), `NSAppleEventsUsageDescription` (without it a hardened-runtime app's `osascript`/System Events automation — the entire `MacOSWindowService` and clipboard image path — is **denied without a prompt**), `LSMinimumSystemVersion`, `NSHighResolutionCapable`, `NSHumanReadableCopyright`, `LSApplicationCategoryType`. Also the plist is only written `Condition="!Exists(...)"` — stale plists survive republish.
- **G3 — No screen-recording permission preflight or guided UX.** `CGPreflightScreenCaptureAccess`/`CGRequestScreenCaptureAccess` appear nowhere in the repo. First SCK capture fails with `ERROR_PERMISSION_DENIED` (`Native/ScreenCaptureKitInterop.cs:103`) and silently falls back to the CLI (`MacOSScreenCaptureKitService.cs:177-180`) — and an unpermissioned `screencapture` child yields wallpaper-only frames, so the user gets a *wrong-looking screenshot* instead of an actionable prompt. `MacOSDiagnosticService` is a stub returning `string.Empty`.
- **G4 — Hotkeys demand Accessibility when they usually don't need to.** SharpHook's CGEventTap requires Accessibility (and Input Monitoring on some versions). Carbon `RegisterEventHotKey` registers global hotkeys **with no TCC permission at all** and natively suppresses the combo. Today every user must grant Accessibility before any hotkey works (`MacOSHotkeyService.cs:82-84`) — the single biggest onboarding cliff after Gatekeeper.
- **G5 — Native window capture and enumeration unwired.** The bridge exports `sck_capture_window(window_id, …)` (`screencapturekit_bridge.h:59`) but `CaptureActiveWindowAsync`/`CaptureWindowAsync` always fall back to CLI (`MacOSScreenCaptureKitService.cs:141-155`). `GetAllWindows` returns only the frontmost window via slow `osascript` (per `docs/planning/macos_support_analysis.md`, "Limitations"). `CGWindowListCopyWindowInfo` is referenced only in that doc's recommendations, never implemented.
- **G6 — Notifications via `osascript display notification`** (`Services/MacOSNotificationService.cs:88-105`): attributed to Script Editor in Notification Center, action buttons flattened into message text (`ShowNotification(... actionText, action)` discards the callback), and each notification spawns a process with a 2s kill timeout.
- **G7 — Sequoia deprecation exposure.** `QuartzCaptureStrategy` is built on `CGDisplayCreateImage` (`QuartzCaptureStrategy.cs:36,118`) — deprecated since macOS 14.4 and slated for removal; on Sequoia, apps using legacy capture APIs trigger recurring "is still able to record your screen" re-approval nags. The modern one-shot API is `SCScreenshotManager` (macOS 14+), which the bridge doesn't expose.
- **G8 — Dead/stale code:** `Capture/ScreenCaptureKitStrategy.cs` is a TODO stub in the **wrong namespace** (`ShareX.Avalonia.Platform.macOS.Capture`, line 28 — the project was renamed to `XerahS.*`) that silently delegates to Quartz. `MacOSOcrService` is an explicit stub ("Apple Vision framework support planned", `MacOSOcrService.cs:32,46`) while live OCR exists on Windows.

---

## 2. Prioritized backlog

Scores: Impact (user-visible benefit on macOS), Effort (S < 1 day, M 1–3 days, L > 3 days for a competent contributor with a Mac).

| # | Item | Impact | Effort | Needs human? |
|---|---|---|---|---|
| P1 | Info.plist completeness + stable bundle identity | High | S | Bundle-ID choice resets existing TCC grants — flag in release notes |
| P2 | Code signing + hardened runtime + notarization + DMG | Very high | M | **Yes** — Apple Developer Program account, Developer ID cert, notary credentials in CI secrets |
| P3 | Screen-recording permission preflight + guided flow | High | S–M | No |
| P4 | Carbon `RegisterEventHotKey` primary hotkey path (no TCC) | High | M | No (new P/Invoke to system Carbon framework only — no new deps) |
| P5 | Native window enumeration (`CGWindowListCopyWindowInfo`) + wire `sck_capture_window` | Med-high | M | No |
| P6 | `UNUserNotificationCenter` notifications with real actions | Medium | M | Requires P1+P2 (needs signed bundle to register) |
| P7 | `SCScreenshotManager` in bridge; retire Quartz on 14+ | Medium | M | No |
| P8 | Cleanup: delete/rename stale `ScreenCaptureKitStrategy` stub; implement `MacOSDiagnosticService` | Low-med | S | No |
| P9 | Vision-framework OCR (`VNRecognizeTextRequest` via bridge or objc interop) | Medium | M–L | No |
| P10 | `SMAppService` login item (modern replacement for raw LaunchAgent), file-association/`CFBundleDocumentTypes`, drag-out from history | Low | M | No |

Sequencing: P1 → P2 are the distribution spine (P1 is a prerequisite for P2: notarization requires a well-formed plist). P3/P4 are independent and can land in parallel. P6 depends on P1+P2. P7 piggybacks on the same bridge-edit workflow as P3/P5.

---

## 3. Top-5 implementation outlines

### P1 — Info.plist completeness + stable bundle identity

**Problem.** See G2. The hardened-runtime kicker: once P2 signs the app, every `osascript` call (window management, clipboard images, notifications) is silently denied unless `NSAppleEventsUsageDescription` exists.

**Change.** Replace the inline `_MacPlistLines` item group (`XerahS.App.csproj:73-92`) with a static template, and always overwrite on publish:

1. Add `build/macos/Info.plist.template` (provided in this commit, `plutil -lint` clean) with placeholders `__VERSION__` / `__MIN_OS__`.
2. In `CreateMacOSAppBundle`, replace `WriteLinesToFile` with a copy+substitute step:

```xml
<PropertyGroup>
  <_MacMinOS>12.3</_MacMinOS>
</PropertyGroup>
<Copy SourceFiles="$(RepoRoot)build/macos/Info.plist.template" DestinationFiles="$(_MacPlist)" />
<Exec Command="/usr/bin/sed -i '' -e 's/__VERSION__/$(Version)/g' -e 's/__MIN_OS__/$(_MacMinOS)/g' &quot;$(_MacPlist)&quot;" />
```

   (Drop the `Condition="!Exists('$(_MacPlist)')"` so republish refreshes it; remove the now-redundant PlistBuddy icon target or keep it — the template already carries `CFBundleIconFile`.)

Key contents (full file in `build/macos/Info.plist.template`):

- `CFBundleIdentifier` = `com.xerahs.app` — matches the existing LaunchAgent label family `com.xerahs.app.startup` (`MacOSStartupService.cs:41`), giving one coherent identity.
- `NSAppleEventsUsageDescription`, plus `NSAppleScriptEnabled` for the System Events path.
- `LSMinimumSystemVersion` = 12.3 (the SCK floor the code already assumes — `ScreenCaptureKitStrategy.IsSupported`).
- `NSHighResolutionCapable`, `NSHumanReadableCopyright`, `LSApplicationCategoryType` (`public.app-category.utilities`), `CFBundleIconFile/IconName`.
- Deliberately **no** `LSUIElement`: menu-bar-only mode is a user toggle handled at runtime by `MacOSApplicationActivationPolicy`, which would fight a static plist value.

**Verification.** `plutil -lint <bundle>/Contents/Info.plist`; `defaults read <bundle>/Contents/Info CFBundleIdentifier` → `com.xerahs.app`; after P2, trigger window capture and confirm the Automation permission prompt appears (it cannot without the usage string).

**Rollback.** Revert csproj target + delete template; the old inline plist generation returns. **Surface for human:** changing `CFBundleIdentifier` from `XerahS.App` resets any TCC grants existing users made — one-time re-grant, must be a release-note line.

### P2 — Code signing, hardened runtime, notarization, DMG

**Problem.** See G1. This is the difference between "works on the developer's machine" and "distributable".

**Decision: direct distribution (Developer ID), not App Store.** The app needs a global event hook fallback (CGEventTap — prohibited in App Sandbox), spawns `osascript`/`screencapture` children, loads plugin assemblies, and installs LaunchAgents/launchd daemons. None of that fits the sandbox. Hardened runtime + notarization, no sandbox.

**Entitlements** (file provided in this commit at `build/macos/entitlements.plist`, `plutil -lint` clean):

| Entitlement | Why |
|---|---|
| `com.apple.security.cs.allow-jit` | CoreCLR RyuJIT needs `MAP_JIT` under hardened runtime — without it .NET apps crash at startup |
| `com.apple.security.cs.allow-unsigned-executable-memory` | Belt-and-braces for older CoreCLR codepaths/SkiaSharp JIT shims; drop after testing if startup survives without it |
| `com.apple.security.cs.disable-library-validation` | Plugin system loads externally built assemblies/native deps (`PluginLoadContext.cs`); library validation would reject anything not signed with the same Team ID |
| `com.apple.security.automation.apple-events` | Required at *signing* time for the osascript/System Events paths (pairs with P1's usage string) |

**Pipeline addition** (sketch for `package-mac.sh`, gated on env so unsigned local builds keep working):

```bash
if [[ -n "${MACOS_SIGN_IDENTITY:-}" ]]; then
    # Sign innermost-first: dylib, daemon, plugins, then the bundle.
    codesign --force --options runtime --timestamp -s "$MACOS_SIGN_IDENTITY" \
        "$APP/Contents/MacOS/libscreencapturekit_bridge.dylib"
    codesign --force --options runtime --timestamp -s "$MACOS_SIGN_IDENTITY" \
        "$APP/Contents/MacOS/xerahs-watchfolder-daemon"
    find "$APP/Contents/MacOS/Plugins" -type f \( -name '*.dylib' -o -perm +111 \) -print0 2>/dev/null | \
        xargs -0 -I{} codesign --force --options runtime --timestamp -s "$MACOS_SIGN_IDENTITY" {}
    codesign --force --options runtime --timestamp \
        --entitlements "$ROOT/build/macos/entitlements.plist" \
        -s "$MACOS_SIGN_IDENTITY" "$APP"
    codesign --verify --deep --strict "$APP"

    hdiutil create -volname "XerahS" -srcfolder "$APP" -ov -format UDZO "$DIST_DIR/XerahS-$VERSION-mac-$arch.dmg"
    xcrun notarytool submit "$DIST_DIR/XerahS-$VERSION-mac-$arch.dmg" \
        --keychain-profile "xerahs-notary" --wait
    xcrun stapler staple "$DIST_DIR/XerahS-$VERSION-mac-$arch.dmg"
fi
```

Ship DMG as primary (survives quarantine with stapled ticket); keep the tarball as a secondary artifact. Prefer `ditto -c -k --keepParent` over `tar` if zip is wanted — `tar` is fine for keeping symlinks/permissions but AppleDouble metadata is safer through `ditto`.

**Interim step (no credentials needed):** ad-hoc sign with a *stable* self-identity (`codesign --force -s - --deep` is wrong; sign innermost-first with `-s -`). Doesn't fix Gatekeeper but makes the TCC identity stable across rebuilds for from-source users, ending the "permissions reset every build" pain.

**Verification.** `codesign -dv --verbose=4 <app>` (flags must include `runtime`); `codesign -d --entitlements - <app>`; `spctl --assess --type execute -vv <app>` → `accepted (Notarized Developer ID)`; on a clean VM: download DMG via browser, open, no Gatekeeper dialog; `xcrun stapler validate`.

**Rollback.** Pipeline is env-gated — unset `MACOS_SIGN_IDENTITY` and behavior is byte-identical to today.

**Surface for human (hard gate):** requires Apple Developer Program enrollment (US$99/yr), Developer ID Application cert, and `notarytool` keychain profile / CI secrets (`APPLE_ID`, team ID, app-specific password). This plan deliberately does **not** touch `.github/workflows/` per scope; the workflow diff is mechanical once secrets exist.

### P3 — Screen-recording permission preflight + guided flow

**Problem.** See G3. The failure mode today is *worse than an error*: wallpaper-only screenshots with no explanation.

**Change.**

1. **Bridge** — add to `screencapturekit_bridge.{h,m}` (pure CoreGraphics, available 10.15+):

```c
// returns 1 if the app already has Screen Recording permission
int sck_preflight_permission(void) {
    return CGPreflightScreenCaptureAccess() ? 1 : 0;
}
// triggers the system prompt (once per TCC lifetime); returns 1 if granted
int sck_request_permission(void) {
    return CGRequestScreenCaptureAccess() ? 1 : 0;
}
```

2. **Interop** — mirror in `Native/ScreenCaptureKitInterop.cs` with `[DllImport("libscreencapturekit_bridge.dylib")]`, plus a managed `MacOSScreenRecordingPermission.Status` helper. Guard with availability check like the existing pattern.
3. **Call sites** — in `MacOSScreenCaptureKitService` (and `QuartzCaptureStrategy`/`MacOSScreenshotService` entry points): preflight before first capture per session. If denied: request once; if still denied, **do not silently fall back to CLI** — raise a typed `ScreenRecordingPermissionDeniedException` the UI maps to a dialog with a deep link:

```csharp
Process.Start(new ProcessStartInfo(
    "open",
    "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")
    { UseShellExecute = false });
```

   and a note that macOS requires an app restart after granting.
4. **Diagnostics** — replace the `MacOSDiagnosticService` stub: write preflight result, AX trust state (`Accessibility.IsProcessTrusted(prompt: false)`), bridge availability (`sck_is_available`), bundle vs bare-binary execution (`AppContext.BaseDirectory` containing `.app/Contents/MacOS`), and macOS version. This is the file users attach to issues.

**Dev-mode caveat to document (FAQ):** under `dotnet run`, TCC attributes permission to the **terminal/dotnet host**, not XerahS — same class of trap as the Linux plan's XIP0044 caveat. Instruct contributors to test permissions from the published bundle.

**Verification.** `tccutil reset ScreenCapture com.xerahs.app`, launch bundle, trigger capture → system prompt appears exactly once; deny → dialog with deep link opens correct pane; grant + relaunch → native capture path logs `[ScreenCaptureKit] Fullscreen capture completed` (existing log line, `MacOSScreenCaptureKitService.cs:195`).

**Rollback.** New exports are additive to the dylib; revert the C# call sites and the old silent-fallback behavior returns. Bridge rebuild covered by the existing `BuildNativeMacOS` MSBuild target (`XerahS.App.csproj:34-40`).

### P4 — Carbon `RegisterEventHotKey` as the no-permission hotkey path

**Problem.** See G4. Accessibility is the most refused permission, and most users never need event *suppression beyond their own hotkey combos* — which Carbon hotkeys provide for free, with zero TCC.

**Change.** New `MacOSCarbonHotkeyBackend` used first by `MacOSHotkeyService`; SharpHook becomes the fallback for the cases Carbon can't serve (modifier-only or non-standard combos), keeping the existing AX-prompt flow only for that path.

```csharp
internal static partial class CarbonHotkeys
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHotKeyID { public uint Signature; public uint Id; }

    [LibraryImport(Carbon)]
    internal static partial int RegisterEventHotKey(uint keyCode, uint modifiers,
        EventHotKeyID hotKeyId, IntPtr eventTarget, uint options, out IntPtr hotKeyRef);

    [LibraryImport(Carbon)]
    internal static partial int UnregisterEventHotKey(IntPtr hotKeyRef);

    [LibraryImport(Carbon)]
    internal static partial IntPtr GetApplicationEventTarget();

    [LibraryImport(Carbon)]
    internal static partial int InstallEventHandler(IntPtr target, EventHandlerUPP handler,
        nint numTypes, in EventTypeSpec typeList, IntPtr userData, out IntPtr handlerRef);
    // kEventClassKeyboard / kEventHotKeyPressed = 5; modifiers: cmdKey=0x100,
    // shiftKey=0x200, optionKey=0x800, controlKey=0x1000
}
```

Implementation notes a contributor needs (so this is executable without research):

- Events arrive on the **main CFRunLoop**, which Avalonia already runs — install the handler from the UI thread at service init; no extra thread, unlike SharpHook's hook thread.
- Map `Avalonia.Input.Key` → Carbon virtual key codes via a static table (`kVK_ANSI_A = 0x00` …); the existing SharpHook keycode map in `MacOSHotkeyService` is the starting point.
- `RegisterEventHotKey` **fails with `eventHotKeyExistsErr (-9878)`** if another app owns the combo — map that to the existing `HotkeyStatus.Failed` so the settings UI shows the conflict (better than today, where SharpHook silently swallows contested combos).
- Keep `IsAccessibilityEnabled()` gating **only** the SharpHook path; Carbon path registers with no checks.
- Carbon is deprecated-but-stable; it is the same API every macOS hotkey utility (Raycast, Rectangle, iTerm2) ships on. Risk of removal within the support horizon: low; the SharpHook fallback is the hedge.

**Verification.** `tccutil reset Accessibility com.xerahs.app` → launch → register default hotkeys → **no AX prompt**, hotkey fires, combo does not leak to macOS (Carbon suppresses registered combos natively). Conflict test: register ⌘⇧3 → expect `Failed` status (owned by Screenshot.app). Fallback test: configure a modifier-only hotkey → AX prompt appears (SharpHook path) — existing behavior preserved.

**Rollback.** Feature-flag the backend order (`MacOSHotkeyBackend: Carbon|SharpHook` in settings); default `Carbon`, flip to `SharpHook` to restore today's behavior exactly.

### P5 — Native window enumeration + wire `sck_capture_window`

**Problem.** See G5. Window capture takes the interactive-CLI detour and `GetAllWindows` is a frontmost-only AppleScript call costing hundreds of ms (per the repo's own analysis doc) and an Automation prompt.

**Change.**

1. **Enumeration** — `CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, kCGNullWindowID)` P/Invoke in a new `Native/QuartzWindowList.cs`; parse `kCGWindowNumber`, `kCGWindowOwnerPID`, `kCGWindowName`, `kCGWindowBounds`, `kCGWindowLayer == 0` filter. Implement `MacOSWindowService.GetAllWindows()` on it; keep AppleScript only for *actions* (activate/minimize/move). Window **titles** require Screen Recording permission on 10.15+ — already handled by P3's preflight; without it, enumeration still works with empty titles (degrade gracefully, don't fail).
2. **Capture** — `MacOSScreenCaptureKitService.CaptureWindowAsync` currently ignores the bridge; thread the CGWindowID through: extend `IWindowService`-adjacent plumbing so the macOS window info carries `kCGWindowNumber`, then call the already-exported `sck_capture_window(window_id, …)` (`ScreenCaptureKitInterop.cs` already declares constants/error mapping; add the missing `[DllImport]` for `sck_capture_window`). CLI `-w` stays as fallback.

**Verification.** `GetAllWindows()` returns >1 window with correct bounds in <10 ms (vs ~300 ms osascript); window capture of a background window succeeds without the interactive crosshair; no Automation prompt during enumeration (`osascript` no longer invoked — verify via `log stream --predicate 'process == "osascript"'` silence).

**Rollback.** Both changes sit behind the existing strategy/fallback seams; reverting the two call sites restores AppleScript/CLI behavior. No bridge changes required (the export already exists).

---

## 4. Smaller items (outline only)

- **P6 Notifications:** objc-interop `UNUserNotificationCenter` (pattern proven in repo by `MacOSApplicationActivationPolicy`'s `objc_msgSend` use). Requires bundle identity (P1) and signing (P2) to register. Gives real action buttons (today's `actionText` is flattened — `MacOSNotificationService.cs:46-52`), correct app attribution and icon, and removes per-notification process spawns. Fallback: keep osascript path when `UNUserNotificationCenter` registration fails (bare-binary dev runs).
- **P7 Sequoia-proof stills:** add `sck_capture_screenshot` via `SCScreenshotManager` (macOS 14+) to the bridge; route Quartz strategy through it on 14+, keep `CGDisplayCreateImage` for 12.3–13.x. Removes deprecated-API usage that triggers Sequoia's recurring re-approval nags.
- **P8 Hygiene:** delete `Capture/ScreenCaptureKitStrategy.cs` (stale `ShareX.Avalonia.*` namespace, pure TODO-stub delegating to Quartz — its `BackendCapabilities` advertises HDR/cursor support it doesn't have) or rewrite it against the bridge; implement `MacOSDiagnosticService` (folded into P3.4).
- **P9 OCR:** Vision `VNRecognizeTextRequest` via a small bridge export (`sck`-style, same Makefile); brings macOS to parity with Windows OCR. The stub already sets user expectations (`MacOSOcrService.cs:46`).
- **P10 Login item modernization:** `SMAppService.mainApp` (macOS 13+) instead of raw LaunchAgent plist — survives app relocation, shows in System Settings → Login Items correctly. Keep plist path for 12.x.

---

## 5. Building and running on macOS from source

Verified against the repo's own scripts/targets (not executed in this run — full builds out of scope):

```bash
# Prereqs: .NET 10 SDK, Xcode CLT (xcode-select --install), Node 20.19+/22.12+ (VideoEditor frontend)
git clone <repo> && cd xerahs
git submodule update --init --recursive   # ShareX.ImageEditor + ShareX.VideoEditor are submodules (README: MSB3202 otherwise)

# Native bridge (also auto-built by the BuildNativeMacOS MSBuild target on macOS hosts)
make -C native/macos          # universal dylib, requires macOS 12.3+ SDK

# Dev run (NOTE: TCC permissions attach to the dotnet host, not XerahS — test permissions from the bundle)
dotnet run --project src/desktop/app/XerahS.App

# Release bundle + tarball, both arches (what CI runs on macos-15)
./build/macos/package-mac.sh
# → dist/XerahS-<version>-mac-{arm64,x64}.tar.gz, .app published from
#   src/desktop/app/XerahS.App/bin/Release/net10.0/osx-<arch>/publish

# Single-arch publish with bundle creation (CreateMacOSAppBundle target)
dotnet publish src/desktop/app/XerahS.App -c Release -r osx-arm64
```

Permission setup for testing (System Settings → Privacy & Security): Screen Recording, Accessibility (until P4), Automation→System Events (after P1/P2 surfaces the prompt). Reset between tests: `tccutil reset ScreenCapture com.xerahs.app` etc.

## 6. Success criteria

1. Fresh download of the DMG opens with **no Gatekeeper dialog and no `xattr` instructions** (P1+P2); `spctl --assess` returns notarized-accepted.
2. First capture attempt produces either a correct screenshot or **one** system permission prompt followed by a guided dialog — never a silent wallpaper-only image (P3).
3. Default hotkeys work on a fresh install **without any Accessibility grant** (P4).
4. Window list in region/window capture shows all on-screen windows with sub-50 ms enumeration; background-window capture works non-interactively (P5).
5. Re-running a rebuilt app does not invalidate previously granted TCC permissions (stable signed identity, P2 — interim: stable ad-hoc identity).
6. README's "App is damaged" troubleshooting section is deleted because the condition no longer occurs.

## 7. Needs human sign-off

- **Apple Developer Program enrollment + Developer ID cert + notary credentials** (P2). Until then: interim stable ad-hoc signing only.
- **Bundle ID switch to `com.xerahs.app`** (P1): one-time TCC re-grant for existing users; release-note required.
- **CI workflow edits** to consume signing secrets — out of scope for this plan per run contract (`.github/workflows/` untouched).
- Entitlement `disable-library-validation` weakens hardened-runtime guarantees to keep the plugin system working — accept, or require Team-ID-signed plugins (product decision).

## 8. Remaining gaps after P1–P5 (explicit)

- No App Store path (sandbox-incompatible by design; revisit only if the plugin/automation architecture changes).
- Avalonia-level items not addressed here: native macOS menu bar (NSMenu) population is whatever Avalonia provides; per-monitor fractional scaling quirks under Avalonia/SkiaSharp 3.119.3-preview remain upstream-dependent.
- OCR (P9) and notification actions (P6) still pending after the top-5.
- Recording UX (mic/system-audio entitlements, `NSMicrophoneUsageDescription`) not yet covered — the plist template includes the key as a placeholder, but audio capture plumbing wasn't audited in this run.
- Intel + Apple Silicon are both built and the dylib is universal; however **no Intel hardware test evidence exists in-repo** — x64 remains "should work".

---

**macOS support level after proposed changes: 8.5/10** (current: 6/10). P1–P3 take distribution and permissions from hostile to first-class (→8); P4–P5 remove the Accessibility cliff and the CLI detours (→8.5). The remaining 1.5 points are external or long-tail: Apple credential dependency, Avalonia upstream behaviors, OCR/notification parity, and Intel verification.
