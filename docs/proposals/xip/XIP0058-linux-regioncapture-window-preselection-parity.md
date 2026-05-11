# XIP0058 Linux RegionCapture Native Wayland Window Preselection Parity

**Status**: Complete
**Version**: v0.22.257

**Priority**: High
**Created**: 2026-03-28
**Updated**: 2026-03-28
**Area**: Linux, Wayland, UI, RegionCapture
**Goal**: Extend XIP0058 from X11/XWayland window preselection parity to native Wayland parity by routing hover snapping through compositor-specific helpers while preserving the existing Windows and X11 paths.
**Related**: XIP0047, XIP0051

---

## Systems Analysis Summary
### 1. Core problem
XIP0058 previously solved Linux window preselection only for X11/XWayland sessions that could enumerate top-level windows globally.

Native Wayland still had a missing end-to-end path:

- the overlay cursor position existed in physical capture pixels,
- compositor/desktop helpers operate in logical compositor space,
- helper results needed to be mapped back into physical RegionCapture rectangles,
- and overlay windows needed to be excluded consistently so the overlay never snapped to itself.

Without that full path, native Wayland sessions still behaved as partial or unsupported even when compositor-specific helper APIs or tools were available.

### 2. Assumptions
- Windows remains the fidelity reference for hover-based window snapping.
- Native Wayland parity should use compositor-specific helpers, not pretend there is a portable global window enumeration API.
- A topmost-window-at-point query is sufficient for hover/click parity on native Wayland; a full desktop-wide window catalog is not required.
- Overlay windows must be excluded by a shared identifier, not by ad hoc title checks in each call site.

### 3. Constraints and unknowns
- Native Wayland helpers use logical compositor coordinates, while RegionCapture selection and snapping are expressed in physical pixels.
- Helper availability differs by desktop/compositor:
  - GNOME depends on Shell D-Bus access,
  - KDE depends on `kdotool`,
  - Hyprland depends on `hyprctl`,
  - Sway depends on `swaymsg`.
- The hot path must stay lightweight: no noisy per-refresh logging and only short-lived helper invocations.
- The repository test project is Windows-targeted; on this Linux host, `dotnet test` reached VSTest successfully but still reported `No test is available`, so verification needed an additional fallback.

### 4. Sub-problems
1. Define a platform abstraction for logical compositor-space point queries.
2. Implement compositor-specific helpers for native Wayland sessions.
3. Convert physical overlay cursor points into logical compositor points.
4. Convert logical helper window rectangles back into physical capture rectangles.
5. Expose capability-aware UI messaging for full, partial, and unsupported sessions.
6. Exclude RegionCapture overlay windows from helper results.
7. Add focused verification for helper parsers and logical/physical conversion.

### 5. Three approaches
1. Keep X11/XWayland-only parity and leave native Wayland unsupported.
2. Add helper-backed point query per compositor/desktop for native Wayland while retaining the existing X11 enumeration path.
3. Reimplement a portable Wayland toplevel-enumeration stack in-process.

### 6. Tradeoffs
- Approach 1 is the smallest change but fails the actual goal.
- Approach 2 fits the Wayland security model, keeps scope bounded, and avoids destabilizing the existing Windows/X11 path.
- Approach 3 may be attractive long-term, but it is materially larger, more brittle, and unnecessary for the hover-snapping goal.

### 7. Chosen approach
Approach 2 was the best fit.

It delivers native Wayland snapping where real compositor helpers exist, preserves X11/XWayland behavior, and keeps unsupported sessions honest instead of over-promising parity.

### 8. Step-by-step execution
1. Add a logical point-query platform abstraction and capability model.
2. Add a shared RegionCapture overlay title constant for helper-side self-exclusion.
3. Implement compositor-specific Wayland point-query helpers for:
   - GNOME Shell D-Bus eval,
   - KDE `kdotool`,
   - Hyprland `hyprctl`,
   - Sway `swaymsg`.
4. Add a factory that selects the right helper from the current desktop/compositor.
5. Expose helper capability and point-query calls from `LinuxWindowService`.
6. Extend `WindowDetectionService` with native Wayland direct-query support, caching, and physical/logical coordinate translation.
7. Keep the existing Windows/X11 list-based path intact for sessions that still enumerate windows natively.
8. Update Linux capability messaging so the overlay only promises full snapping when a helper is actually available.
9. Add focused parser/conversion tests for the helper and RegionCapture conversion paths.
10. Verify with `dotnet build`, a `dotnet test` attempt, and a temporary reflection-based verification harness for the helper/parser/conversion methods when VSTest discovery remained unavailable on this Linux host.

### 9. Git staging strategy
The work was intentionally split into two commits:

1. Runtime implementation plus focused tests.
2. This proposal update.

### 10. Failure points
- GNOME parity depends on `org.gnome.Shell` eval access; hardened or restricted shells can still disable the helper.
- KDE parity depends on `kdotool` being installed and returning parseable geometry.
- Hyprland/Sway parity depends on `hyprctl` / `swaymsg` being present and their JSON staying compatible with the parser assumptions.
- Native Wayland parity is implemented as a topmost-window-at-point query, not as a complete desktop-wide window inventory.
- On this Linux host, `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1` built the project graph but VSTest still reported `No test is available` for the Windows-targeted test assembly, so the helper/parser/conversion behavior was additionally verified with a temporary reflection harness.

---

## Overview
This proposal records the completion of native Wayland window preselection parity on top of the earlier X11/XWayland work.

Before this change, RegionCapture already had:

- Windows parity through the DWM-backed native path,
- Linux X11/XWayland parity through managed-window enumeration,
- capability-aware UI text for sessions that could only snap supported windows.

What it still lacked was the native Wayland bridge from cursor point to compositor-owned topmost window metadata. The completed implementation adds that bridge through compositor-specific helpers and translates the result back into physical RegionCapture space so hover highlights and click-to-snap behave like the existing Windows/X11 experience.

---

## Implemented Design
### Phase 1: Platform abstraction
- Added `ILogicalWindowPointQueryService` plus `WindowPointQueryCapability`.
- This keeps native Wayland point-query behavior explicit instead of overloading the older `GetAllWindows()` abstraction.

### Phase 2: Shared overlay exclusion
- Added `PlatformWindowTitles.RegionCaptureOverlay`.
- `OverlayWindow` now uses that shared title so every Wayland helper can exclude the overlay with one stable identifier.
- Added `WindowQueryConstants.RegionCaptureOverlayTitle` for helper-side reuse.

### Phase 3: Compositor-specific helpers
- Added `WaylandWindowPointQueryHelperFactory` to select the native Wayland helper based on the detected compositor/desktop.
- Added helper implementations for:
  - `GnomeShellWindowPointQueryHelper`
  - `KdeKdotoolWindowPointQueryHelper`
  - `HyprlandWindowPointQueryHelper`
  - `SwayWindowPointQueryHelper`
- Added `WaylandWindowPointQueryCommandRunner` for short-lived helper invocations and command availability checks.
- `DesktopCaptureInterfaceChecker.HasInterface(...)` was widened so the GNOME helper can reuse the existing D-Bus capability probe.

### Phase 4: Linux service wiring
- `LinuxWindowService` now implements `ILogicalWindowPointQueryService`.
- It exposes helper capability and helper-backed point queries without regressing the older X11 window-management implementation.

### Phase 5: RegionCapture integration
- `WindowDetectionService` now:
  - caches direct native Wayland point-query results,
  - converts physical overlay points to logical compositor points,
  - converts logical helper rectangles back into physical capture rectangles,
  - filters helper results that resolve back to the RegionCapture overlay,
  - keeps X11/Windows list-based behavior as the fallback path.
- Capability reporting now recognizes:
  - full support when a direct helper is enabled,
  - partial support when only X11/XWayland fallback remains,
  - unsupported sessions when neither helper nor X11 fallback is available.

### Phase 6: Verification
- Added focused tests for:
  - helper parser behavior (`Hyprland`, `Sway`, `GNOME`, `KDE`),
  - physical/logical coordinate conversion,
  - logical-window-to-physical-window projection,
  - capability mapping.
- `dotnet build src/desktop/XerahS.sln -m:1` succeeded with 0 warnings and 0 errors.
- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1` built successfully but VSTest still reported `No test is available` on this Linux host for the Windows-targeted test assembly.
- A temporary reflection-based verification harness successfully exercised:
  - `HyprlandWindowPointQueryHelper.SelectWindowFromClientsJson(...)`,
  - `SwayWindowPointQueryHelper.SelectWindowFromTreeJson(...)`,
  - `WindowDetectionService.TryConvertPhysicalToLogicalPoint(...)`,
  - `WindowDetectionService.ConvertLogicalPlatformWindow(...)`.

---

## Non-Negotiable Rules
- Do not regress the existing Windows DWM-backed snapping path.
- Do not degrade X11/XWayland behavior to fit native Wayland helpers.
- Do not claim native Wayland parity when helper capability is unavailable.
- Do not let the RegionCapture overlay participate in helper results.
- Keep helper usage lightweight and capability-aware.

---

## Deliverables
1. `ILogicalWindowPointQueryService` and capability model.
2. Shared RegionCapture overlay-title constant for helper filtering.
3. Native Wayland compositor/desktop helpers for GNOME, KDE, Hyprland, and Sway.
4. Linux service wiring from helper capability/query to RegionCapture.
5. Focused tests for helper parsing and logical/physical conversion.
6. This updated proposal.

---

## Affected Components
1. `src/platform/XerahS.Platform.Abstractions/ILogicalWindowPointQueryService.cs`
2. `src/platform/XerahS.Platform.Abstractions/PlatformWindowTitles.cs`
3. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/WaylandWindowPointQueryHelperFactory.cs`
4. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/WaylandWindowPointQueryCommandRunner.cs`
5. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/GnomeShellWindowPointQueryHelper.cs`
6. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/KdeKdotoolWindowPointQueryHelper.cs`
7. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/HyprlandWindowPointQueryHelper.cs`
8. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/SwayWindowPointQueryHelper.cs`
9. `src/platform/XerahS.Platform.Linux/Wayland/WindowQuery/WindowQueryConstants.cs`
10. `src/platform/XerahS.Platform.Linux/Capture/Detection/DesktopCaptureInterfaceChecker.cs`
11. `src/platform/XerahS.Platform.Linux/LinuxWindowService.cs`
12. `src/desktop/app/XerahS.RegionCapture/Services/WindowDetectionService.cs`
13. `src/desktop/app/XerahS.RegionCapture/UI/OverlayWindow.axaml.cs`
14. `tests/XerahS.Tests/RegionCapture/WindowDetectionServiceTests.cs`
15. `tests/XerahS.Tests/Platform/Linux/WaylandWindowPointQueryHelperTests.cs`

---

## Architecture Summary
```text
RegionCaptureControl (physical cursor point)
    |
    v
WindowDetectionService.GetWindowAtPoint(...)
    |
    +--> Windows / X11 / XWayland:
    |       existing list-based window detection
    |
    +--> Native Wayland:
            physical point
                |
                v
            TryConvertPhysicalToLogicalPoint(...)
                |
                v
            LinuxWindowService.GetWindowAtLogicalPoint(...)
                |
                v
            WaylandWindowPointQueryHelperFactory
                |
                +--> GNOME Shell helper
                +--> KDE kdotool helper
                +--> Hyprland hyprctl helper
                +--> Sway swaymsg helper
                |
                v
            logical compositor-space window
                |
                v
            ConvertLogicalPlatformWindow(...)
                |
                v
            physical RegionCapture window bounds
                |
                v
            hover highlight -> click -> snapped capture rect
```

---

## Verification
### Build
```powershell
dotnet build src/desktop/XerahS.sln -m:1
```

### Test attempt
```powershell
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -m:1
```

### Host-specific fallback verification
When VSTest discovery is unavailable on a Linux host for the Windows-targeted test assembly, run a temporary reflection harness that:

1. calls `HyprlandWindowPointQueryHelper.SelectWindowFromClientsJson(...)`,
2. calls `SwayWindowPointQueryHelper.SelectWindowFromTreeJson(...)`,
3. calls `WindowDetectionService.TryConvertPhysicalToLogicalPoint(...)`,
4. calls `WindowDetectionService.ConvertLogicalPlatformWindow(...)`.

The current implementation was verified that way and returned `verification-ok`.
