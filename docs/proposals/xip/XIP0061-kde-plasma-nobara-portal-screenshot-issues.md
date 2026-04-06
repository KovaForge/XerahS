# XIP0061 KDE Plasma / Nobara Linux: Portal Issues, Fixes & Version Dependencies

**Status**: Implemented (fixes merged in `v0.21.3`)  
**Area**: Linux | Wayland | Screen Capture | Hotkeys  
**Related**: XIP0044, XIP0046, XIP0029, Issues [#63](https://github.com/ShareX/XerahS/issues/63), [#64](https://github.com/ShareX/XerahS/issues/64)  
**CEO Mission**: Bug report thread — `#xerahs-kde-plasma-6-2-bug-report-portal-hijack-hotkey-death`

---

## Summary

This XIP documents four bug fixes shipped in `v0.21.3` for KDE Plasma / Nobara Linux Wayland users, provides the consolidated research picture (MiniMax + Kimi k2.5, independent), and maps remaining open issues against confirmed KDE-specific vs general Wayland root causes.

Nobara Linux shifted to KDE Plasma as its default desktop in version 39 (December 2023), making KDE-specific Wayland portal behaviors increasingly relevant for XerahS's user base. The four fixes address real stress-case bugs discovered on a real KDE Plasma 6.6.2 / Wayland / Nobara 43 session.

---

## Part 1: The Four v0.21.3 Fixes

All four fixes are on `origin/develop` as of 2026-04-06.

| # | Commit | Fix | Severity |
|---|--------|-----|----------|
| 1 | `cb75a733` | Resolver bypass — XerahSOverlay preference now respected on KDE Wayland | High |
| 2 | `1412b67f` | `ConfigureShortcuts` UnknownMethod → silent KDE fallback | Low |
| 3 | `c83ed6b3` + `c5e19538` | Rapid hotkey debounce (1500ms, atomic AddOrUpdate) | Medium |
| 4 | `6f72f244` + `c5e19538` | DBus `ObjectDisposedException` guards in hotkey service | Medium |

### Fix #1 — Resolver Override Bypassed (Commit `cb75a733`)

**Problem**: Task set to `XerahSOverlay` → KDE Plasma → portal dialog appears instead.

**Root cause** (found by Viktor, confirmed by code review):  
`CaptureStage.ShouldUseDefaultLinuxRegionSelectorPreferenceForDesktop()` unconditionally overwrites the task's `XerahSOverlay` preference to `Automatic` on KDE Plasma — *before* the resolver ever sees it. The resolver then resolves `Automatic → PortalDialog` via its `AutomaticPreference` path.

**Fix**: `LinuxRegionSelectorResolver.IsKdePlasmaWaylandWithPortal()` detects the upstream override and compensates — returns `XerahSOverlay` when the overlay is actually viable on KDE Wayland. Detection mirrors the same env-var checks used in `CaptureStage` so the two stay in sync.

**Note**: Requires `xdg-desktop-portal-kde ≥ 6.4.2` for the overlay to work correctly (self-capture bug fixed in that version).

---

### Fix #2 — ConfigureShortcuts Silent Fallback (Commit `1412b67f`)

**Problem**: User clicks hotkey button in workflow editor → KDE portal throws `UnknownMethod` → stack trace in logs.

**Root cause**: KDE Plasma's GlobalShortcuts portal implementation supports `BindShortcuts` but not `ConfigureShortcuts` (portal v2 method). The method is invoked via `ShowInteractiveConfigurationAsync()`.

**Fix**: Catch `DBusException` with `ErrorName == "org.freedesktop.DBus.Error.UnknownMethod"` in `WaylandPortalHotkeyService.ShowInteractiveConfigurationAsync()`. Return `false` to trigger the native app UI path. Log a clear message: *"ConfigureShortcuts not available on KDE Plasma — use XerahS workflow editor to set hotkeys."*

**Research confirmation**: ArchWiki's [portal backend comparison table](https://wiki.archlinux.org/title/XDG_Desktop_Portal) confirms `xdg-desktop-portal-kde` implements GlobalShortcuts, but the `InputCapture` portal is notably absent. The `ConfigureShortcuts` gap is a KDE-specific method gap, not a full portal absence.

---

### Fix #3 — Rapid Hotkey Debounce (Commits `c83ed6b3` + `c5e19538`)

**Problem**: User presses Ctrl+F1 rapidly → each press spawns a new capture task → DBus exceptions cascade.

**Root cause**: No cooldown between hotkey fires. On KDE Plasma where portal screenshots are cancelled quickly (Response=1), the hotkey can be re-pressed before the prior task unwinds.

**Fix**: `ConcurrentDictionary<string, long>` tracks per-shortcut-ID last-fire timestamps. `AddOrUpdate` with atomic value factory prevents two threads from both seeing "expired" simultaneously (close race condition flagged in code review). 1500ms window.

**Note**: Milena's review flagged the initial `TryGetValue` + direct-write pattern as a race condition. Viktor fixed it in `c5e19538` using the atomic `AddOrUpdate` pattern before the commit was pushed.

---

### Fix #4 — DBus ObjectDisposedException Guards (Commits `6f72f244` + `c5e19538`)

**Problem**: Unobserved `ObjectDisposedException` in `Tmds.DBus.Connection.CallMethodAsync` during rapid portal captures.

**Root cause**: Portal screenshot cancelled/timeout → portal session/connection disposed → pending async DBus call hits disposed connection. Stress-case only — won't appear on healthy sessions.

**Fix**: 
1. Early `_disposed` check in `OnActivated` — skips if service is disposing.
2. Try-catch moved **inside** the `Dispatcher.UIThread.Post` lambda (code review caught the original catch was around `Post` itself — which is async and won't throw synchronously; the exception fires *inside* the lambda).
3. `_disposed` flag checked again inside the debounce-critical section.

**Scope**: Defensive guards only. No connection pooling, no session reuse, no portal lifecycle changes.

---

## Part 2: KDE Plasma / Nobara Research Findings

### Nobara Linux Context

Nobara Linux (GloriousEggroll, Fedora-based) shifted from GNOME to **KDE Plasma as default in Nobara 39** (December 2023). Reasons relevant to XerahS:
- VRR/Variable Refresh Rate works natively in KDE (no patches needed)
- DRM leasing for VR headsets works better in KDE
- Steam Deck uses KDE — patches benefit both

Nobara's portal behavior mirrors Fedora. The bug reporter's issues are **KDE Plasma / Wayland specific**, not Nobara-distro-specific.

**Portal backend on Nobara**: Ships with `xdg-desktop-portal-kde`. On KDE sessions, `$XDG_CURRENT_DESKTOP=KDE`, `$XDG_PORTAL_BACKEND=kde` — routing selects the KDE backend. Both `gtk` and `kde` backends can be installed simultaneously (confirmed from [GitHub Issue #64](https://github.com/ShareX/XerahS/issues/64) diagnostics).

### Critical Version: `xdg-desktop-portal-kde ≥ 6.4.2`

Per [Repology](https://repology.org/project/xdg-desktop-portal-kde/versions) and ArchWiki:

| Distro | portal-kde version | Meets minimum? |
|--------|-------------------|----------------|
| Fedora 42 | 6.6.3 | ✅ Yes |
| Fedora 41 | 6.4.4 | ✅ Yes |
| Nobara 43 (approx) | 6.x | ✅ Yes |
| Ubuntu 25.04 | 6.3.4 | ⚠️ Marginal |
| Ubuntu 24.04 | 5.27.11 | ❌ No |
| Debian 13 | 6.3.5 | ⚠️ Marginal |
| Arch Linux | 6.6.3 | ✅ Yes |

Key fixes in `xdg-desktop-portal-kde ≥ 6.4.2`:
- **Self-capture bug fixed**: Portal no longer captures itself in the screenshot
- **Region selection UI improvements**: Better `interactive=true` behavior
- **`handle_token` format fixes**: Better `parent_window` support for X11 and Wayland surfaces

### KDE Plasma GlobalShortcuts Portal — What Works, What Doesn't

From ArchWiki portal backend table and [XDG Portal GlobalShortcuts docs](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.GlobalShortcuts.html):

| Method / Signal | KDE portal support |
|----------------|-------------------|
| `CreateSession` | ✅ |
| `BindShortcuts` | ✅ |
| `ListShortcuts` | ✅ |
| `Activated` signal | ✅ |
| `Deactivated` signal | ✅ |
| `ShortcutsChanged` signal | ✅ |
| `ConfigureShortcuts` | ❌ Not implemented — throws `UnknownMethod` |
| `InputCapture` portal | ❌ Not implemented — `CreateSession` returns response=2 |

**Practical implication**: `ConfigureShortcuts` not being implemented means the KDE System Settings → Shortcuts panel is the correct UX path for KDE users. Fix #2 (above) handles this gracefully.

### KWin.ScreenShot2 DBus API — Available as KDE-Specific Fallback

KDE's KWin provides `org.kde.KWin.ScreenShot2` DBus interface:
- `CaptureArea` — rectangular region capture
- `CaptureActiveScreen` — current screen
- `CaptureWindow` — specific window by ID

Unlike the portal (which is cross-desktop), this is KDE-only. It bypasses the portal entirely. It could serve as a KDE-specific fast path in future, but requires `KDE_FULL_SESSION` detection and is out of scope for current fixes.

### Portal Backend Routing — Both gtk + kde Can Run Simultaneously

From [GitHub Issue #64](https://github.com/ShareX/XerahS/issues/64) diagnostics on Arch KDE:
```
org.freedesktop.impl.portal.desktop.gtk  PID 8174  xdg-desktop-portal-gtk
org.freedesktop.impl.portal.desktop.kde  PID 8321  xdg-desktop-portal-kde
```

Routing is determined by `$XDG_CURRENT_DESKTOP`. On KDE sessions this is `KDE` — the KDE backend is selected. The GTK backend being present doesn't interfere unless `$XDG_CURRENT_DESKTOP` is unset or set to a generic value.

### InputCapture Portal — Confirmed Not Supported on KDE

`WaylandPortalInputService.CreateSession` returns response=2 on KDE Plasma. Per [input-leap GitHub discussion](https://github.com/input-leap/input-leap/discussions/1976), the `InputCapture` portal requires compositor support that KDE Plasma hasn't fully implemented. This is **expected behavior on KDE**, not a bug in XerahS.

### Plasma 6.8 (Expected October 2026) — X11 Session Removal

KDE Plasma is planning to drop X11 session support in Plasma 6.8. This means:
- Pure Wayland becomes the only option
- XerahS's X11-based capture paths will need full Wayland equivalents
- `zxdg_exporter_v2` handle tokens for Wayland windows become critical
- Relevant for long-term roadmap — not immediate

---

## Part 3: Issue Map — KDE-Specific vs General Wayland

| Issue | Category | KDE-specific? | Status |
|-------|----------|---------------|--------|
| Resolver overrides XerahSOverlay to PortalDialog | Screenshot capture | ✅ KDE | **Fixed** — `cb75a733` |
| `ConfigureShortcuts` throws UnknownMethod | Hotkey config | ✅ KDE | **Fixed** — `1412b67f` |
| Rapid hotkey fires duplicate captures | Hotkey | ⚠️ Stress-case | **Fixed** — `c83ed6b3` + `c5e19538` |
| DBus ObjectDisposedException during rapid captures | DBus/hotkey | ⚠️ Stress-case | **Fixed** — `6f72f244` + `c5e19538` |
| Portal self-capture (portal window in shot) | Screenshot | ✅ KDE | Fixed in portal-kde ≥ 6.4.2 |
| Portal lacks region selection UI | Screenshot | ✅ KDE | CLI fallback (`spectacle --region`) in XIP0046 |
| InputCapture CreateSession response=2 | Input capture | ✅ KDE | Expected — not a bug |
| Cancel portal opens Spectacle unexpectedly | Screenshot | ✅ KDE | Fixed in `ee6d0fa` (XIP0046) |
| GlobalShortcuts hotkey silently doesn't fire | Hotkey | ❌ General Wayland | Fixed in XIP0044 |
| `parentWindow` empty at startup race | Hotkey | ❌ General Wayland | Fixed in XIP0044 |
| Print key maps to wrong Key enum value | Hotkey | ❌ X11/XWayland | Fixed in XIP0046 |
| DBus duplicate type / signal mismatch errors | DBus | ❌ General Wayland | Fixed in XIP0029 |

---

## Part 4: Code Review Findings (Milena + Vladislava)

Two correctness issues were caught and fixed before the fixes shipped:

### Issue A — Debounce Race Condition (Fixed in `c5e19538`)

**Caught by**: Milena code review  
**Original code**:
```csharp
if (_hotkeyDebounceTimes.TryGetValue(data.shortcutId, out var lastTicks) &&
    nowTicks - lastTicks < debounceWindowTicks) { return; }
_hotkeyDebounceTimes[data.shortcutId] = nowTicks; // RACE: two threads both pass
```

**Fixed code**:
```csharp
var shouldProceed = _hotkeyDebounceTimes.AddOrUpdate(
    data.shortcutId,
    nowTicks,
    (key, lastTicks) => nowTicks - lastTicks < debounceWindowTicks
        ? lastTicks  // still in window — keep old, caller skips
        : nowTicks); // expired — update, caller proceeds
if (shouldProceed != nowTicks) return;
```
`AddOrUpdate`'s value factory runs inside the dictionary's lock — no two threads can both see "expired" simultaneously.

### Issue B — Try-Catch in Wrong Place (Fixed in `c5e19538`)

**Caught by**: Milena + Vladislava code review  
**Original code**:
```csharp
try { Dispatcher.UIThread.Post(() => HotkeyTriggered?.Invoke(this, args)); }
catch (ObjectDisposedException) { } // WRONG: Post() is async — won't throw here
```

**Fixed code**:
```csharp
Dispatcher.UIThread.Post(() =>
{
    try { HotkeyTriggered?.Invoke(this, args); }
    catch (ObjectDisposedException) { /* inside the lambda — now caught */ }
});
```

---

## Part 5: Remaining Open Items

### Cannot Fix in XerahS (Require Upstream Changes)

1. **KDE portal `ConfigureShortcuts`**: KDE hasn't implemented this method. Workaround (Fix #2) is shipped. Ideal fix: KDE implements the method in `xdg-desktop-portal-kde`.

2. **KDE portal lacks `InputCapture`**: Response=2 is expected. No workaround needed — XerahS doesn't depend on this.

3. **Portal region selection UX on KDE**: The portal dialog's selection UI differs from GNOME's. Not a XerahS bug. Could be improved upstream in KDE.

4. **Portal self-capture on old KDE**: Users on `xdg-desktop-portal-kde < 6.4.2` will see the portal window in screenshots. Recommendation: update the portal package.

### Could Be Improved in Future Releases

1. **`KWin.ScreenShot2` as KDE-specific fast path**: Skip portal entirely for KDE users, use KWin's native DBus API. Lower priority — portal path works fine on modern KDE.

2. **Portal version detection + user warning**: Check `xdg-desktop-portal-kde` version at startup and warn users on < 6.4.2. Could be added to diagnostics UI.

3. **Plasma 6.8 X11 removal preparation**: Audit X11 capture code paths and ensure full Wayland equivalents exist before KDE drops X11 support.

---

## References

1. [GitHub Issue #63 — Print key registration failure](https://github.com/ShareX/XerahS/issues/63)
2. [GitHub Issue #64 — XDG Portal UI differences across Linux systems](https://github.com/ShareX/XerahS/issues/64)
3. [ArchWiki — XDG Desktop Portal (backend comparison table)](https://wiki.archlinux.org/title/XDG_Desktop_Portal)
4. [flatpak/xdg-desktop-portal#950 — Screenshot permission requirements](https://github.com/flatpak/xdg-desktop-portal/issues/950)
5. [input-leap/input-leap#1976 — Wayland InputCapture portal status](https://github.com/input-leap/input-leap/discussions/1976)
6. [Repology — xdg-desktop-portal-kde versions](https://repology.org/project/xdg-desktop-portal-kde/versions)
7. [KDE Plasma Wayland Known Issues](https://community.kde.org/Plasma/Wayland_Known_Significant_Issues)
8. [Nobara Linux](https://nobaraproject.org/)
9. [Linuxiac — Nobara 39 release coverage](https://linuxiac.com/nobara-linux-39-released/)
10. [GlobalShortcuts Portal Documentation](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.GlobalShortcuts.html)

---

## Changelog

| Date | Author | Description |
|------|--------|-------------|
| 2026-04-06 | Vladislava Kova + Milena Petrova | Consolidated research from MiniMax + Kimi k2.5 independent passes |
| 2026-04-06 | Viktor Hale | Fixes #1–#4 committed to `origin/develop` |
