# XIP0062 EagleShot Overlay Modernization

XIP0062: EagleShot-Inspired Overlay Modernization

## Priority
**HIGH** — Resolves KDE Plasma UX issues and improves cross-platform capture quality

## Assignee
**Viktor Hale** (primary), **Milena Petrova** (research)

## Branch
**Each recommendation ships in its own branch.** See §Recommendations below.

## Status
Draft

## Context

XerahS's overlay layer carries significant complexity inherited from ShareX's annotation system — JSON serialization, undo/redo mementos, rotation, Z-indexing — none of which apply to the ephemeral capture overlay. Meanwhile, EagleShot (Avalonia, ~1964 lines) solves the same problem with a clean `Shape.Draw()` pattern and a single unified `OverlayCanvas` control.

This XIP proposes five targeted improvements inspired by EagleShot's implementation. Each is independent and ships on its own branch.

---

## Source Reference

- EagleShot repo: `~/Documents/GitHub/Ref/eagleshot`
- Key files: `Core/Shapes.cs`, `Core/Shapes2.cs`, `Views/OverlayCanvas.cs`, `Core/GlobalHotkeyService.cs`
- Yoink repo (for comparison): `~/Documents/GitHub/Ref/yoink`

---

## Recommendations

### REC-001: SharpHook Global Hotkeys — Fix KDE Plasma Hotkey Death

**Branch:** `feature/sharphook-hotkeys`

**Problem:**  
XerahS uses the XDG Portal `GlobalShortcuts` DBus connection for global hotkeys. On KDE Plasma, the portal session is disposed when the annotation editor closes, tearing down the DBus connection and killing the hotkey listener. The `TaskCanceledException` crash in `DBusConnection.Dispose()` is the symptom. Additionally, KDE Plasma does not implement `org.freedesktop.portal.InputCapture` (response=2), forcing a less reliable cursor-tracking fallback.

**EagleShot solution:**  
`GlobalHotkeyService.cs` — 44 lines using **SharpHook** (`SimpleGlobalHook`), a cross-platform native keyboard hook library that bypasses the XDG Portal entirely:

```csharp
public class GlobalHotkeyService : IDisposable {
    private SimpleGlobalHook? _hook;
    public event Action? ScreenshotRequested;
    public void Start() {
        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        Task.Run(() => _hook.Run());
    }
    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e) {
        if (e.Data.KeyCode == KeyCode.VcPrintScreen)
            ScreenshotRequested?.Invoke();
    }
}
```

**Proposed change:**
1. Add `SharpHook` NuGet package to the XerahS hotkey service project
2. Implement `SharpHookGlobalHotkeyService` — mirrors EagleShot's pattern
3. Use as fallback when the Wayland portal is unavailable or fails (especially KDE Plasma)
4. Portal hotkey remains the primary path on GNOME and other portals that fully support `GlobalShortcuts`
5. `SimpleGlobalHook` handles Windows (low-level keyboard hook), macOS (CGEvent), and Linux (`libuiohook`) from a single code path

**DO:**
- Use SharpHook's `SimpleGlobalHook` for the initial implementation
- Detect KDE Plasma at startup via `$XDG_CURRENT_DESKTOP == KDE` and skip portal hotkey if SharpHook is available
- Keep the portal hotkey service as a fallback for non-KDE Wayland compositors
- Add `libuiohook` as a system dependency in Linux packaging documentation

**DO NOT:**
- Remove the Wayland portal hotkey path — it's still needed for portal-aware screenshot picker integration
- Change hotkey registration on Windows or macOS unless SharpHook proves more reliable

**Acceptance criteria:**
- Hotkey fires on KDE Plasma 6.2 after opening and closing the annotation editor
- `dotnet build` passes with 0 errors
- Existing hotkey behavior on GNOME/macOS/Windows unchanged

---

### REC-002: Unsafe Pixel Mosaic for Live Overlay Preview

**Branch:** `feature/unsafe-pixel-mosaic`

**Problem:**  
XerahS's `PixelateAnnotation` uses an `SKBitmap` resize pipeline (scale down, then upsample with `SKFilterQuality.None`) for pixelation. This is correct for final output quality but creates unacceptable CPU overhead during live drag — the resize runs on every pointer move event.

**EagleShot solution:**  
`MosaicShape.Draw()` — direct unsafe pointer pixel sampling, draws colored rectangles without intermediate bitmaps:

```csharp
unsafe {
    byte* ptr = (byte*)fb.Address;
    for (int y = 0; y < srcH; y += PixelSize) {
        for (int x = 0; x < srcW; x += PixelSize) {
            int sampleX = Math.Min(srcX + x + PixelSize/2, bmpW - 1);
            int sampleY = Math.Min(srcY + y + PixelSize/2, bmpH - 1);
            int offset = sampleY * stride + sampleX * 4;
            byte b = ptr[offset], g = ptr[offset+1], r = ptr[offset+2];
            var blockBrush = new SolidColorBrush(Color.FromRgb(r,g,b));
            ctx.DrawRectangle(blockBrush, null, new Rect(...));
        }
    }
}
```

**Proposed change:**
1. Add a fast-path overlay mosaic renderer in the overlay canvas layer
2. Use unsafe pixel access against the captured `WriteableBitmap` (Avalonia) or `SKBitmap` (SkiaSharp)
3. Render colored rectangles directly — no resize, no intermediate bitmap
4. Keep the full SkiaSharp `PixelateAnnotation` pipeline for the post-capture editor output
5. The overlay mosaic renderer is purely for 60fps-capable live drag feedback

**DO:**
- Enable `unsafe` blocks only in the overlay-specific renderer
- Use the same `PixelSize` parameter as the existing annotation
- Fall back to the existing resize pipeline if unsafe access is unavailable (e.g., sandboxed environments)

**DO NOT:**
- Change the final output quality of `PixelateAnnotation` in the editor
- Remove the existing SkiaSharp pixelation code

**Acceptance criteria:**
- Mosaic region drag is smooth (≥30fps) on a 1920×1080 capture
- Final saved image pixelation quality is identical to current output
- `dotnet build` passes (requires `/unsafe` flag in the project)

---

### REC-003: Auto-Increment Number Markers (One-Click)

**Branch:** `feature/auto-number-markers`

**Problem:**  
XerahS likely requires a drag gesture to place number markers. EagleShot's `#` tool places a numbered marker on single click and auto-increments the counter.

**EagleShot solution:**
```csharp
case ToolType.Number:
    var numShape = new NumberShape {
        StrokeColor = _currentColor,
        Center = loc,
        Number = _numberCounter++,  // auto-increment
        Radius = 16 + _currentPenWidth * 2
    };
    _shapes.Add(numShape);  // placed immediately, no drag
    InvalidateVisual();
    break;
```

**Proposed change:**
1. Add single-click placement to the number marker tool in the overlay canvas
2. Maintain `_numberCounter` state on the canvas (reset on new capture)
3. Counter resets when user starts a new capture session

**DO:**
- Implement as a new `ToolType.Number` behavior in the overlay canvas
- Auto-reset `_numberCounter` when a new capture overlay is opened
- Draw the circle + number in one pass using `DrawEllipse` + `FormattedText`

**DO NOT:**
- Change the editor's `NumberAnnotation` behavior (it may require drag for positioning in advanced cases)
- Change the counter reset logic outside of the overlay layer

**Acceptance criteria:**
- Clicking the number tool places marker at cursor position without drag
- Counter increments on each placement
- Counter resets on new capture

---

### REC-004: Lightweight Overlay Shape API (Draw-Only)

**Branch:** `feature/overlay-shape-api`

**Problem:**  
XerahS's `Annotation.cs` carries full ShareX complexity — JSON serialization attributes, `EditorTool` enum coupling, `Guid` Id tracking, `ShadowEnabled`, `RotationAngle`, `ZIndex`, `Clone()` patterns — none of which apply to the ephemeral overlay layer.

**EagleShot solution:**
```csharp
public abstract class Shape {
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 3;
    public abstract void Draw(DrawingContext ctx);
    public abstract Rect GetBounds();
}

public class ArrowShape : Shape {
    public Point Start { get; set; }
    public Point End { get; set; }
    public override void Draw(DrawingContext ctx) { /* ... */ }
    public override Rect GetBounds() => default;
}
```

**Proposed change:**
1. Create a parallel lightweight overlay shape hierarchy: `IOvlShape { void Draw(DrawingContext ctx); Rect GetBounds(); }`
2. Implement: `OvlPenShape`, `OvlLineShape`, `OvlArrowShape`, `OvlRectShape`, `OvlHighlightShape`, `OvlNumberShape`, `OvlTextShape`, `OvlMosaicShape`
3. The overlay canvas uses the lightweight shapes exclusively
4. The ShareX-style `Annotation` system remains in the post-capture editor only
5. The two systems do not share a base class or interface — they are intentionally separate

**DO:**
- Keep the overlay shape API in `XerahS.UI/Overlays/` or `XerahS.UI/Capture/`
- Make it draw-only (no Id, no serialization, no ZIndex, no Clone)
- Map `OvlArrowShape` output to `ArrowAnnotation` when the overlay hands off to the editor

**DO NOT:**
- Add serialization to the overlay shapes
- Share the base class with `Annotation.cs`
- Modify the existing editor annotation system

**Acceptance criteria:**
- All existing overlay annotation tools (pen, line, arrow, rect, highlight, number, text, mosaic) render correctly
- No regression in the post-capture editor
- `dotnet build` passes with 0 errors

---

### REC-005: Unified Overlay Canvas (Streamline Capture → Annotate → Output)

**Branch:** `feature/unified-overlay-canvas`

**Problem:**  
XerahS has a multi-layer pipeline between the capture overlay and the ShareX-style editor. EagleShot shows that for the common capture → annotate → output workflow (90% of use cases), one unified `OverlayCanvas` control handles everything.

**EagleShot solution:**  
`OverlayCanvas.cs` (371 lines, single `UserControl`) handles:
- Full-screen captured image display
- Dim overlay (4 rectangles around the selection)
- Selection border with marching-ants `DashStyle`
- Live pixel dimension label (`"W × H"`)
- All annotation drawing (`foreach (shape in _shapes) shape.Draw(ctx)`)
- All pointer events: selection-drag, shape drawing, text placement, shape moving

`OverlayWindow.axaml.cs` (429 lines) handles the toolbar: color picker grid, pen width buttons, tool buttons, text box with A+/A- controls, save/copy buttons.

**Proposed change:**
1. Audit XerahS's current overlay window architecture
2. Identify which components are necessary for advanced workflows vs. the simple capture → annotate → output path
3. Consider a unified overlay canvas using EagleShot's pattern as the **default** path
4. The ShareX editor remains accessible as a secondary view for advanced post-capture editing
5. This is a larger refactor — investigate before committing; see architecture note below

**Architecture note:**  
XerahS's overlay must support multiple capture modes (region, window, full screen) and has the scrollable capture workflow to consider. Before implementing, audit whether a unified canvas can absorb all current overlay modes without breaking scrollable capture. If architectural constraints prevent a full unification, implement REC-004 first to create the clean separation needed.

**DO:**
- Start with the scrollable capture overlay as the boundary — ensure it is not broken by this change
- Keep the ShareX editor accessible as a secondary view
- Match current XerahS UI behavior (shortcuts, toolbar layout) unless REC-001/REC-002/REC-003 provide a reason to change it

**DO NOT:**
- Merge the overlay canvas and the ShareX editor into one monolithic view
- Remove the scrollable capture functionality

**Acceptance criteria:**
- All existing capture modes (region, window, full screen) work via the unified canvas
- Annotation rendering on the overlay matches current behavior
- Scrollable capture continues to work
- `dotnet build` passes with 0 errors

---

## Cross-Cutting Concerns

- All RECs must pass `dotnet build` with 0 errors before merge
- All RECs are independent — merge order is flexible
- REC-004 and REC-005 are architecturally related: REC-004 (lightweight shape API) is a prerequisite for REC-005 (unified canvas). Complete REC-004 first.
- The SharpHook REC-001 requires `libuiohook` system dependency on Linux — update install/packaging docs

## Dependencies

| REC | Depends on |
|-----|-----------|
| REC-001 | None |
| REC-002 | None |
| REC-003 | None |
| REC-004 | None |
| REC-005 | REC-004 (prerequisite) |

---

## Rollback Plan

If REC-005 (unified overlay canvas) introduces regressions in scrollable capture or advanced workflows:
1. Revert to pre-REC-005 architecture (multi-layer pipeline)
2. Keep REC-004 (lightweight overlay shape API) as a standalone improvement
3. Re-evaluate REC-005 with additional scrollable capture-specific testing

---

## References

- EagleShot source: `~/Documents/GitHub/Ref/eagleshot`
- XIP0051: `docs/proposals/xip/XIP0051-linux-interactive-region-selector-preferences.md` (related Linux capture work)
- XIP0046: `docs/proposals/xip/XIP0046-linux-portal-hotkey-issues.md` (related KDE hotkey work)
- XIP0044: `docs/proposals/xip/XIP0044-linux-global-hotkeys-not-firing-when-app-is-backgrounded.md` (related hotkey work)
