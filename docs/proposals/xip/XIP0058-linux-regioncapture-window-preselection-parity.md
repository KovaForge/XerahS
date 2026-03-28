# XIP0058 Linux RegionCapture Window Preselection Parity
**Status**: Completed
**Priority**: High
**Created**: 2026-03-28
**Updated**: 2026-03-28
**Area**: Linux, UI, RegionCapture
**Goal**: Bring Linux overlay window hover/preselection to Windows parity on KDE/GNOME X11 or XWayland sessions that expose global window metadata, without regressing unsupported sessions such as native Wayland.
**Related**: XIP0047, XIP0051

---

## Systems Analysis Summary
### 1. Core problem
Linux region capture overlay mode could not pre-select a hovered window before drag/confirm, while Windows could.

The direct cause was architectural, not visual:

- `XerahS.RegionCapture/Services/WindowDetectionService.cs` enumerated windows only behind `#if WINDOWS`.
- Non-Windows builds always refreshed an empty window list, so hover-state window snapping could never activate.
- Linux already had `PlatformServices.Window.GetAllWindows()`, but the overlay path was not using it.
- After the first parity wiring, Linux still relied on raw X11 root children plus `XFetchName`, which is too weak for KDE/GNOME managed windows and can collapse back to an empty hover list.

### 2. Assumptions
- Windows behavior is the feature reference: hover a window, highlight it, click or confirm to capture that window rectangle.
- Linux parity should be implemented where platform APIs can expose real top-level windows.
- Native Wayland sessions may not allow portable global window enumeration, so parity there is a capability problem, not only a missing code path.
- Overlay windows themselves must be excluded from detection or the feature becomes self-targeting.

### 3. Constraints and unknowns
- `WindowDetectionService` refreshes frequently, so Linux enumeration must be cheap enough for overlay interaction.
- KDE and GNOME commonly expose managed top-level windows through EWMH (`_NET_CLIENT_LIST_STACKING`, `_NET_WM_NAME`, `_NET_FRAME_EXTENTS`) rather than only through raw X11 root-child traversal.
- `LinuxWindowService.GetWindowBounds()` contained verbose debug logging that would become a hot-path cost if reused every refresh.
- Native Wayland support remains session-dependent and in many cases unavailable.

### 4. Sub-problems
1. Enable non-Windows window detection in the overlay.
2. Preserve Windows-specific DWM/visual-bounds behavior.
3. Make Linux X11 window enumeration meaningful for hover selection.
4. Exclude active overlay windows from detection across platforms.
5. Add targeted verification for detector behavior without depending on a live X11 session in tests.

### 5. Three approaches
1. Minimal wiring only:
   Use `PlatformServices.Window.GetAllWindows()` on Linux and stop there.
2. Session-specific Linux implementation:
   Add X11-aware ordering/filtering/exclusion while leaving unsupported sessions inert.
3. Full compositor integration:
   Add Wayland compositor-specific window enumeration for GNOME/KDE/wlroots in v1.

### 6. Tradeoffs
- Approach 1 is fast to ship but risky because raw Linux window lists can be noisy, unordered, and can include the overlay itself.
- Approach 2 reaches practical parity on X11/XWayland with bounded scope and preserves current behavior on unsupported sessions.
- Approach 3 aims at broader parity but is high-risk, compositor-fragmented, and too large for a safe v1 fix.

### 7. Chosen approach
Approach 2 was the best fit.

It solves the actual missing behavior for Linux sessions that can expose top-level windows, keeps Windows on its existing high-fidelity native path, and avoids promising parity where native Wayland security boundaries prevent it.

### 8. Step-by-step execution
1. Audit Windows overlay hover detection and confirm Linux returned no windows.
2. Confirm proposal numbering/style and related Linux capture XIPs.
3. Refactor `WindowDetectionService` into a platform-aware detector.
4. Keep Windows on `NativeWindowService.EnumerateVisibleWindows()`.
5. Project non-Windows `IWindowService.GetAllWindows()` results into RegionCapture window models.
6. Add shared overlay-handle exclusion so hover detection ignores overlay windows on every platform.
7. Replace raw X11 root-child enumeration with EWMH managed-window enumeration for KDE/GNOME X11 and XWayland sessions.
8. Read Linux titles and outer bounds from `_NET_WM_VISIBLE_NAME` / `_NET_WM_NAME` and `_NET_FRAME_EXTENTS`, while filtering `_NET_WM_WINDOW_TYPE` and `_NET_WM_STATE`.
9. Add focused detector tests plus region-capture regression verification.
10. Build the desktop solution and commit the verified runtime chunk before documenting the proposal.

### 9. Git staging strategy
The work was intentionally split into logical chunks:

1. Runtime implementation plus tests, staged and committed only after targeted tests and `dotnet build src/desktop/XerahS.sln -m:1` passed.
2. Proposal documentation, staged separately so review can distinguish runtime behavior from design record.

### 10. Failure points
- Native Wayland sessions still expose no portable global window list, so window preselection remains unavailable there without compositor-specific helpers.
- Wayland sessions that do expose an X11/XWayland display still only see X11/XWayland windows, not every native GNOME/KDE Wayland surface.
- Some X11 desktops or lightweight window managers may not publish the full EWMH properties, so Linux falls back to the older root-child heuristic.
- X11 window title sourcing is still only as good as the current Linux window service.
- If overlay handles are not available from the backend, exclusion falls back to normal platform filtering.

---

## Post-v1 Improvements
- Add compositor-aware diagnostics that explicitly tell users when window preselection is unavailable because the session is native Wayland.
- Improve Linux title retrieval by checking richer EWMH/ICCCM metadata when `XFetchName` is insufficient.
- Cache Linux window snapshots per refresh cycle with additional invalidation rules if enumeration cost becomes noticeable on busy desktops.
- Explore optional compositor-specific integrations for GNOME/KDE when they can be done without destabilizing the portable overlay path.
- Surface an overlay capability indicator in Linux diagnostics so support logs show whether window preselection was expected to work in that session.

---

## Overview
This XIP records the completed fix for Linux overlay window preselection parity.

Before this change, Linux overlay mode could display the crosshair and support rectangular selection, but it could not snap to the hovered window because the hover detector had no non-Windows source of top-level windows. The implementation now uses a platform-aware detection path:

- Windows continues to use the existing DWM-backed native enumerator.
- Non-Windows platforms can project `IWindowService.GetAllWindows()` into RegionCapture window models.
- Linux X11/XWayland window enumeration now prefers the EWMH managed-window stack and expands client bounds to frame bounds for hover-selection use.
- Overlay handles are excluded from detection so the overlay never becomes its own snap target.

This is intentionally capability-aware rather than pretending every Linux session can offer Windows-equivalent metadata. Where the platform cannot expose global window information, the overlay keeps region capture behavior without false preselection promises.

---

## Implemented Design
### Phase 1: Platform-aware detector
- `WindowDetectionService` now owns shared overlay-handle exclusion.
- Windows still uses `NativeWindowService.EnumerateVisibleWindows()` for visual-bounds fidelity.
- Non-Windows now converts `PlatformServices.Window.GetAllWindows()` into `XerahS.RegionCapture.Models.WindowInfo`.

### Phase 2: Overlay exclusion
- `OverlayManager` now registers and unregisters overlay platform handles with `WindowDetectionService` on all platforms.
- This removes the Windows-only exclusion path and makes the behavior consistent for Linux/X11 overlays as well.

### Phase 3: Linux X11 managed-window enumeration
- `LinuxWindowService.GetAllWindows()` now:
  - prefers `_NET_CLIENT_LIST_STACKING` so KDE/GNOME callers see the actual managed-window stack instead of raw root children,
  - falls back to root-child traversal only when the window manager does not expose EWMH stacking data,
  - reads window titles from `_NET_WM_VISIBLE_NAME` / `_NET_WM_NAME` before falling back to `XFetchName`,
  - filters out hidden or non-window surfaces using `_NET_WM_WINDOW_TYPE` and `_NET_WM_STATE`,
  - skips override-redirect and zero-sized windows.
- `LinuxWindowService.GetWindowBounds()` now applies `_NET_FRAME_EXTENTS` so hover highlights match the full outer window frame instead of only the client surface when the window manager exposes frame metadata.
- `NativeMethods` now includes the X11 property interop needed to read managed-window metadata.

### Phase 4: Verification
- Added focused detector tests for filtering/projection and topmost-hit behavior.
- Re-ran the RegionCapture test slice.
- Rebuilt the desktop solution successfully with zero warnings and zero errors.

### Phase 5: KDE/GNOME session guidance
- `RegionCaptureControl` now surfaces capability-aware instructions instead of always promising full window snapping.
- Linux X11 sessions keep the standard `Click to snap window` guidance.
- Wayland sessions with an available X11/XWayland display now show `Click to snap supported windows`.
- Wayland sessions without an exposed X display remove the snap-to-window promise and show that window snapping is unavailable.

---

## Non-Negotiable Rules
- Do not replace the Windows native DWM-based hover path with a lower-fidelity generic implementation.
- Do not claim portable native Wayland parity unless the session exposes a real global window list.
- Do not let overlay windows participate in hover preselection.
- Do not turn Linux window enumeration into a hot logging path during overlay interaction.
- Keep window-preselection behavior capability-aware: supported where available, inert where unsupported.

---

## Deliverables
1. Platform-aware overlay window detection in `XerahS.RegionCapture`.
2. Cross-platform overlay-handle exclusion wiring in `OverlayManager`.
3. Hardened Linux X11/EWMH managed-window enumeration for hover selection.
4. Automated tests covering detector filtering, topmost-hit behavior, and Linux frame/type/state helpers.
5. This proposal documenting scope, tradeoffs, and post-v1 follow-up.

---

## Affected Components
1. `src/desktop/app/XerahS.RegionCapture/Services/WindowDetectionService.cs`
2. `src/desktop/app/XerahS.RegionCapture/Services/OverlayManager.cs`
3. `src/platform/XerahS.Platform.Linux/LinuxWindowService.cs`
4. `src/platform/XerahS.Platform.Linux/NativeMethods.cs`
5. `tests/XerahS.Tests/RegionCapture/WindowDetectionServiceTests.cs`
6. `tests/XerahS.Tests/Platform/Linux/LinuxWindowServiceTests.cs`

---

## Architecture Summary
```text
OverlayManager
    |
    | registers overlay native handles for exclusion
    v
WindowDetectionService
    |
    +--> Windows: NativeWindowService.EnumerateVisibleWindows()
    |
    +--> Non-Windows: PlatformServices.Window.GetAllWindows()
              |
              v
        LinuxWindowService (EWMH-managed X11/XWayland windows when available)
              |
              v
        topmost filtered window list
              |
              v
RegionCaptureControl hover state
              |
              v
window highlight -> click/confirm -> snapped capture rect
```

---

## Verification Commands
```powershell
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter "FullyQualifiedName~WindowDetectionServiceTests" -m:1
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter "FullyQualifiedName~LinuxWindowServiceTests" -m:1
dotnet test tests/XerahS.Tests/XerahS.Tests.csproj --filter "FullyQualifiedName~XerahS.Tests.RegionCapture" -m:1
dotnet build src/desktop/XerahS.sln -m:1
```
