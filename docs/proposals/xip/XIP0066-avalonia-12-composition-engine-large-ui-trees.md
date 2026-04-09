# XIP0066 Avalonia 12 Composition Engine — Large UI Tree Performance

**Status**: Draft
**Priority**: Medium
**Area**: Performance | UI Framework
**Related**: XIP0065 (core Avalonia 12 upgrade)

---

## Summary

Avalonia 12 ships a revamped composition engine that significantly improves rendering performance for large UI trees on all desktop platforms (Windows, macOS, Linux). XerahS has several screens with complex UI trees — the settings page, history view, and the image editor toolbar — that are candidates for measurable latency improvements from this change. This XIP proposes measuring baseline performance before/after the Avalonia 12 upgrade and targeting specific UI trees for optimization.

---

## Background: Avalonia 12 Composition Engine

Avalonia 12's composition engine rewrite focuses on:

1. **Reduced draw call overhead** — batching improvements mean fewer round-trips to the GPU for complex visual trees
2. **Improved `IVisual`-tree traversal** — less time spent walking the visual tree during render passes
3. **Better dirty-rect tracking** — only invalid regions are redrawn rather than full control invalidation
4. **Thread-local composition** — composition work is better distributed across cores on multi-monitor/high-DPI setups

These changes are most impactful for:
- Controls with many children (e.g., DataGrid with hundreds of rows)
- Complex visual layers (overlays, toolbars, annotation layers)
- High-frequency update scenarios (live preview, scrolling, drag operations)

---

## Relevant XerahS UI Trees

### High-priority targets (complex, frequently updated)

| UI Area | Control | Tree Complexity | Update Frequency | Expected Gain |
|---|---|---|---|---|
| **Image Editor toolbar** | Tool buttons + panels | Medium | High (tool switch) | Medium |
| **Settings page** | DataGrid (uploaders list) | High | Low | Medium |
| **History view** | DataGrid (capture history) | High | Medium | High |
| **Region capture overlay** | Canvas + annotation layer | Medium | High (mouse move) | High |
| **Annotation effects panel** | Effect sliders + previews | Medium | High (slider drag) | Medium |

### Lower-priority targets

| UI Area | Control | Reason |
|---|---|---|
| Main window navigation | TabControl | Static, infrequent updates |
| About page | Simple layout | Not performance-sensitive |

---

## Proposed Investigation

### Phase 1: Baseline Measurement (before Avalonia 12 upgrade)

Establish baseline metrics for the high-priority targets using Avalonia's built-in diagnostics:

1. **`CompositingOverflow` diagnostic** — enable via `DebugAttachOptions` to detect composition overflow events (render calls that exceed the frame budget)
2. **Frame time profiling** — use `Avalonia.Diagnostics.OverlayDiagnostics` (DevTools) to observe frame times during:
   - Rapid tool switching in the image editor
   - Scrolling through history DataGrid
   - Dragging the region selector overlay
3. **Metrics to record**:
   - P95 frame time (ms) per scenario
   - Dropped frames (frames > 16.67ms on 60Hz)
   - Composition overflow count per minute

### Phase 2: Post-Upgrade Validation (after Avalonia 12)

After applying the Avalonia 12 upgrade (XIP0065), re-run the same scenarios and compare:

1. Frame time reduction (%) per scenario
2. Whether any previously-observed overflow events are eliminated
3. Whether any new rendering artifacts appear

### Phase 3: Optimization (if needed)

If Avalonia 12's composition engine alone does not fully address observed slowdowns:

| Optimization | Description |
|---|---|
| **Virtualization** | Ensure `DataGrid` in history/settings uses `VirtualizationMode.Simple` or `Physical` for large datasets |
| **Panel simplification** | Replace nested `StackPanel`/`Border` chains in toolbars with flatter `Grid`-based layouts |
| **Cached rendering** | Apply `RenderOptions.BitmapCacheMode="Qualified"` on stable but complex visuals (annotation canvas background) |
| **Deferred updates** | Batch slider/param updates in the annotation panel using a `50ms` throttle via `Dispatcher.UIThread.Post(..., DispatcherPriority.Render)` |
| **Custom compositing** | Override `CreateEffect()` on SkiaSharp-rendered controls to use hardware-accelerated layers |

---

## Implementation Plan

| Phase | Step | Deliverable |
|---|---|---|
| 1a | Enable `DebugAttachOptions.CompositingOverflow` in DevTools on a DEBUG build | Observable overflow count |
| 1b | Record baseline frame times for 3 key scenarios (tool switch, history scroll, region overlay drag) | Baseline metrics JSON |
| 2 | Upgrade to Avalonia 12 (XIP0065), re-run same scenarios | Post-upgrade metrics JSON |
| 2 | Diff baseline vs. post-upgrade metrics | Summary: % improvement per scenario |
| 3 | If improvements < 20% on any high-priority target, apply optimization techniques above | Optimized controls + re-measured metrics |

---

## Success Criteria

- No new rendering artifacts after Avalonia 12 upgrade
- ≥ 15% reduction in P95 frame time on at least one high-priority UI tree
- Compositing overflow count reduced to zero for region capture overlay during mouse drag
- History DataGrid scrolling remains smooth (≥ 30 FPS) with 10,000+ items

---

## Open Questions

1. **Baseline tooling**: Is `DebugAttachOptions` sufficient for frame time measurement, or should we add `System.Diagnostics.Stopwatch`-based instrumentation at key rendering hooks?
2. **History DataGrid size**: What is the expected maximum history items in production? (Affects whether virtualization is necessary or if a simpler fix suffices)
3. **Overlay dirty-rect**: Does the region capture overlay currently invalidate the entire canvas on mouse move, or does it use targeted invalidation?

---

*Author: Claude (performance investigation draft)*
