# XIP0084 Windows Region Capture Algorithm Parity

**Status**: Implemented
**Created**: 2026-08-19
**Updated**: 2026-08-19
**Implemented**: 2026-08-19
**Area**: Desktop | Capture | Windows
**Goal**: Cherry-pick ShareX Avalonia region-capture algorithms into XerahS's per-monitor overlay without adopting ShareX's single-window capture shell.

---

## Overview

ShareX Avalonia region capture is not a better overlay architecture than XerahS. XerahS already uses per-monitor `OverlayWindow` instances, DXGI freeze frames, and mixed-DPI coordinate translation. ShareX's `RegionCaptureWindow` is a single HWND that can lose Shift-square / Alt-from-center behavior and inherit the origin monitor's DPI.

The useful ShareX work is the algorithm layer that XerahS already declares in `TaskSettings.CaptureSettings.RegionCaptureOptions` but does not wire through the overlay:

1. Ignore NVIDIA GeForce overlay (`CEF-OSC-WIDGET`).
2. Hover-snap to child controls and window client rectangles.
3. Snap a drag to configured size presets (`426x240` … `1920x1080`).
4. Persist the last confirmed capture rectangle independently of `CaptureCustomRegion`.
5. Honor `QuickCrop` and hit-test resize handles instead of always confirming on mouse-up.
6. Tone-map HDR DXGI frames with `DuplicateOutput1` + WIC instead of forcing 8-bit `DuplicateOutput`.

This XIP keeps the XerahS overlay and DXGI freeze path. It does not port `RegionCaptureWindow`, GDI `BitBlt` freeze, or the embedded ShareX editor view.

## Prerequisites

- Windows 8+ for DXGI Desktop Duplication; Windows 10+ for `DuplicateOutput1` HDR formats.
- Existing `Vortice.DXGI` / `Vortice.Direct3D11` 3.8.3 line; add `Vortice.WIC` at the same version for tone mapping.
- No new platform abstraction. Child-control enumeration is Windows-only. Linux/macOS hover continues to use the existing window list.

## Investigation Record

Compared ShareX Avalonia (`C:\Users\Public\source\repos\ShareX Team\ShareX`) with XerahS (`C:\Users\Public\source\repos\KovaForge\XerahS`).

| ShareX source | XerahS counterpart | Verdict |
|---|---|---|
| `WindowsRectangleList` (`CEF-OSC-WIDGET`, `EnumChildWindows`, client rect) | `NativeWindowCaptureFilter` + `NativeWindowService` | Cherry-pick filter + children |
| `ShapeManager.SnapPosition` | `SelectionStateMachine` | Cherry-pick size-preset snap |
| `RegionCaptureIntegration.LastRegionRectangle` | `CaptureStage` LastRegion → `CaptureCustomRegion` | New session store |
| `RegionCaptureOptions.QuickCrop` + node hit-testing | `_quickCrop` stored unused; handles drawn only | Wire QuickCrop + handles |
| `HDRScreenCapture` (`DuplicateOutput1` + `IWICBitmapToneMapper`) | `CaptureFullScreenDxgi` uses `DuplicateOutput` 8-bit | Cherry-pick tone-map on DXGI |
| `RegionCaptureWindow.axaml` single HWND | Per-monitor `OverlayWindow` + DXGI freeze | Keep XerahS |

## Implementation Phases

### Phase 1: Window filter parity

Add `CEF-OSC-WIDGET` to the ignored class set. Split top-level vs child filters so empty-title child controls are eligible.

**Key Files:**
- `src/desktop/app/XerahS.RegionCapture/Platform/NativeWindowCaptureFilter.cs`
- `tests/XerahS.Tests/RegionCapture/NativeWindowServiceTests.cs`

**Rules:**
- Top-level windows still require a title.
- Child controls skip the title requirement and the no-activate / tool-window exclusions.
- Ignored classes apply to both.

### Phase 2: Child control and client-rect hover

When `DetectControls` is true, enumerate `EnumChildWindows` for each visible top-level window, clip children to the parent visual bounds, and insert them before the parent so first-match hover prefers the control. Add a client-rect hover target when it differs from the DWM frame.

**Key Files:**
- `src/desktop/app/XerahS.RegionCapture/Platform/Windows/NativeWindowService.cs`
- `src/desktop/app/XerahS.RegionCapture/Models/WindowInfo.cs`
- `src/desktop/app/XerahS.RegionCapture/Services/WindowDetectionService.cs`
- `src/desktop/app/XerahS.RegionCapture/NativeMethods.txt`

**Rules:**
- Children before parent, client rect before window.
- Cap children per parent to keep hover enumeration cheap.
- Linux/macOS lists stay top-level only.

### Phase 3: Size-preset snap and last region

During drag, if the current size is within `SnapDistance` (30 px) of a configured `SnapSize` and Shift is not held, snap the free corner to that preset. After a confirmed region capture, store the physical rectangle in a process-lifetime store. `WorkflowType.LastRegion` reads that store instead of `CaptureCustomRegion`.

**Key Files:**
- `src/desktop/app/XerahS.RegionCapture/Services/SelectionStateMachine.cs`
- `src/desktop/core/XerahS.Core/Capture/LastRegionStore.cs`
- `src/desktop/core/XerahS.Core/Tasks/Pipeline/CaptureStage.cs`
- `src/desktop/app/XerahS.UI/Services/Capture/OverlayRegionCaptureSession.cs`

**Rules:**
- Do not write last region into `CaptureCustomRegion`.
- Do not change `StartScreenRecorder` last-region, which is a recording workflow.
- Do not overwrite last region from color picker or ruler sessions.

### Phase 4: QuickCrop and handle hit-testing

When `QuickCrop` is true (default), mouse-up still confirms. When false, the machine stays in `Selected` so the user can move, resize by handles, then press Enter. Overlay Enter first confirms the current selection.

**Key Files:**
- `src/desktop/app/XerahS.RegionCapture/Services/SelectionStateMachine.cs`
- `src/desktop/app/XerahS.RegionCapture/UI/RegionCaptureControl.cs`
- `src/desktop/app/XerahS.RegionCapture/UI/OverlayWindow.axaml.cs`
- `src/desktop/app/XerahS.UI/Services/RulerToolService.cs` (already sets `QuickCrop = false`)

**Rules:**
- Handles are hit-tested in physical pixels.
- Shift still locks aspect ratio; Alt still expands from center.
- Annotation Enter (`ConfirmCaptureWithAnnotations`) remains the fallback when no live selection exists.

### Phase 5: HDR tone-map on DXGI

Prefer `IDXGIOutput5.DuplicateOutput1` with `R16G16B16A16_Float`, `R10G10B10A2_UNorm`, then `B8G8R8A8_UNorm`. HDR frames are converted to BGRA8 through `IWICBitmapToneMapper` before the existing Skia compose path. SDR frames keep the current 8-bit copy. Mark DXGI capabilities as HDR-capable.

**Key Files:**
- `src/platform/XerahS.Platform.Windows/Capture/DxgiHdrToneMapper.cs`
- `src/platform/XerahS.Platform.Windows/Capture/DxgiOutputDuplicationHelper.cs`
- `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`
- `src/platform/XerahS.Platform.Windows/Capture/DxgiCaptureStrategy.cs`
- `src/platform/XerahS.Platform.Windows/Capture/DxgiCapabilitiesHelper.cs`

**Rules:**
- Do not replace the XerahS DXGI freeze with ShareX GDI + HDR overlay.
- If `DuplicateOutput1` or WIC fails, fall back to `DuplicateOutput` / existing 8-bit copy.
- Do not add a ShareX-style two-pass GDI correction.

## Non-Negotiable Rules

1. Keep per-monitor `OverlayWindow` + DXGI freeze. Do not adopt `RegionCaptureWindow`.
2. Reuse `TaskSettings.CaptureSettings.RegionCaptureOptions` (`DetectWindows`, `DetectControls`, `SnapSizes`, `QuickCrop`). Do not invent a second options model in settings.
3. Last region is independent of `CaptureCustomRegion`.
4. Platform-specific child enumeration stays under `XerahS.RegionCapture/Platform/Windows`.
5. HDR conversion happens on the existing DXGI path, not a parallel capture pipeline.
6. Do not regress Shift-square or Alt-from-center.

## Deliverables

1. XIP backup at `docs/proposals/xip/XIP0084-windows-region-capture-algorithm-parity.md`.
2. NVIDIA overlay class ignored during hover snap.
3. Child-control and client-rect hover on Windows when `DetectControls` is true.
4. Size-preset snap during region drag.
5. Session last-region store used by `WorkflowType.LastRegion`.
6. `QuickCrop=false` editable selection with handle hit-testing.
7. DXGI HDR tone-map with WIC, SDR fallback intact.
8. Unit tests for filter, snap, QuickCrop, last-region, and handle hit-testing.

## Affected Components

- `XerahS.RegionCapture`: filter, native enum, state machine, overlay input
- `XerahS.Core`: `LastRegionStore`, `CaptureStage`
- `XerahS.UI`: option mapping in `OverlayRegionCaptureSession`
- `XerahS.Platform.Windows`: DXGI duplication + HDR tone-map
- `XerahS.Tests`: region-capture and capture-stage tests

## Architecture Summary

```
TaskSettings.RegionCaptureOptions
        ↓
OverlayRegionCaptureSession  →  RegionCaptureOptions (overlay)
        ↓
OverlayManager / OverlayWindow / RegionCaptureControl
        ↓
SelectionStateMachine  ←  snap sizes, QuickCrop, handles
WindowDetectionService ←  NativeWindowService (windows + children + client)
        ↓
confirmed PixelRect  →  LastRegionStore
        ↓
CaptureStage.LastRegion  →  CaptureRectAsync
                            ↑
                 DXGI DuplicateOutput1
                 WIC tone-map if HDR
                 Skia BGRA compose
```

## Evolution History

| Date | Change | Rationale |
|------|--------|-----------|
| 2026-08-19 | Drafted from ShareX vs XerahS region-capture analysis | Cherry-pick algorithms; keep XerahS overlay |
