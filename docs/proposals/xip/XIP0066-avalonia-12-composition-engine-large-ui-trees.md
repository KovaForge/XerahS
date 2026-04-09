# XIP0066 Avalonia 12 Composition Engine - Large UI Tree Performance

**Status**: Draft
**Priority**: Medium
**Area**: Performance | UI Framework
**Related**: XIP0065 (core Avalonia 12 upgrade)

---

## Summary

Avalonia 12 is explicitly positioned as a performance release. The official release notes claim major compositor work, lower idle CPU, compiled bindings by default, and up to 1,867% FPS improvement in extremely heavy visual scenes. XerahS should use this XIP to turn those framework gains into measurable wins on the screens that matter most: history, settings grids, region capture, and editor surfaces.

This XIP is not just "benchmark before and after." It is also about aligning XerahS UI architecture with the things Avalonia 12 is now good at:

- large visual trees
- hidden visuals that should stop doing work
- binding-heavy screens that benefit from compiled bindings
- async UI flows that should move expensive work off the hot path

---

## Avalonia 12 Performance Themes That Matter Here

The Avalonia 12 release notes describe the following changes as relevant to real applications:

1. The compositor was fundamentally reworked.
2. Render scaling lookups are cached in `PresentationSource`.
3. Animation processing is disabled when a visual is not visible.
4. Default window icon loading is deferred until the window is shown.
5. Compiled bindings are enabled by default.
6. Idle CPU usage is materially lower.

XerahS should evaluate performance through that lens. The question is not only "is Avalonia 12 faster?" The question is "are our UI trees structured so Avalonia 12 can actually help us?"

---

## Relevant XerahS UI Trees

### High-priority targets

| UI Area | Control Shape | Why It Matters | Expected Avalonia 12 Benefit |
|---|---|---|---|
| History view | Large item/grid surface | Heavy scrolling and frequent updates | Better compositor throughput, lower binding overhead |
| Region capture overlay | Frequently redrawn overlay | Mouse-move and drag sensitivity | Lower frame cost during drag and resize |
| Image editor toolbar and inspector panels | Dense control tree | Tool changes and panel updates | Better layout/render responsiveness |
| Settings surfaces with grids and lists | Binding-heavy forms and tables | Large forms, validation, list virtualization | Compiled-binding and compositor gains |
| Effect and preview panels | Slider-driven updates | High-frequency parameter changes | Lower redraw cost and less idle waste |

### Lower-priority targets

| UI Area | Reason |
|---|---|
| About and static informational pages | Not performance-sensitive |
| One-off dialogs with small visual trees | Useful to verify, but not primary bottlenecks |

---

## Measurement Strategy

### Phase 1: Post-upgrade baseline

Because Avalonia 12 is already the target runtime, the first useful baseline is the current 12.0 behavior on representative scenarios:

1. Scroll a large history dataset.
2. Drag and resize the region-capture overlay continuously.
3. Switch tools and open panels in the image editor.
4. Drag effect sliders while preview is visible.

For each scenario, record:

- P95 frame time
- dropped-frame rate at the target refresh rate
- CPU usage while actively interacting
- idle CPU once the surface is visible but not moving

### Phase 2: Confirm the framework is being used well

Inspect whether the slow surfaces are fighting Avalonia 12:

- views without explicit `x:DataType`
- hidden panels that continue animating or updating
- unnecessarily deep nested layouts
- full-surface invalidation where targeted invalidation would do
- avoidable UI-thread work during pointer-heavy or scroll-heavy interaction

### Phase 3: Apply focused fixes only where needed

If a surface remains slow after the framework upgrade, prefer changes that align with Avalonia 12 instead of piling on ad hoc rendering workarounds.

---

## Optimization Priorities

| Priority | Optimization | Why It Fits Avalonia 12 |
|---|---|---|
| High | Convert touched hot-path views to explicit compiled bindings with `x:DataType` | Avalonia 12 already defaults toward compiled bindings; XerahS should not leave hot paths on ambiguous reflection bindings |
| High | Stop updating hidden panels and previews | Avalonia 12 now avoids processing animations for non-visible visuals; app logic should not reintroduce background churn |
| High | Keep expensive parsing, hashing, and preview prep off the interaction path | Matches Avalonia 12's stronger dispatcher/background-processing model |
| Medium | Flatten deeply nested layout trees in history/editor panels | Lets compositor and layout improvements pay off more directly |
| Medium | Re-check virtualization and item-template cost on large grids/lists | Important for history and settings tables |
| Medium | Reduce full-canvas invalidation during region capture | Keeps pointer-heavy rendering aligned with compositor improvements |
| Low | Cache only genuinely stable complex visuals | Useful only after measurement; avoid speculative caching everywhere |

---

## Success Criteria

- No new rendering artifacts after the Avalonia 12 upgrade
- At least one high-priority surface shows a clear frame-time improvement over the pre-upgrade behavior already observed by users
- Region-capture drag feels smooth under sustained pointer movement
- History scrolling remains responsive at large item counts
- Hidden panels and inactive surfaces do not burn CPU unnecessarily

---

## Non-Goals

- Rewriting every XAML surface just because Avalonia 12 is faster
- Adding speculative cache flags or custom rendering layers without measurement
- Treating a framework speedup as a replacement for bad view structure or over-eager UI updates

---

## Implementation Plan

| Phase | Step | Deliverable |
|---|---|---|
| 1 | Capture current Avalonia 12 performance on history, region capture, editor, and effect-preview scenarios | Baseline notes and metrics |
| 2 | Audit those surfaces for compiled bindings, hidden-work churn, deep layout nesting, and invalidation patterns | Hot-path issue list |
| 3 | Apply the smallest structural fixes needed on the worst surfaces | Targeted UI changes |
| 4 | Re-measure the same scenarios | Before/after comparison |
| 5 | Record any remaining bottlenecks as follow-up work rather than bloating this XIP | Focused follow-up backlog |

---

## Open Questions

1. Which screen currently has the highest frame-time variance: history, region capture, or editor interaction?
2. Which views still rely on implicit or reflection-style bindings in hot paths?
3. Does the region capture overlay still invalidate more surface area than necessary during drag operations?
4. Are there hidden panels or previews in the editor that continue to update while off-screen or collapsed?

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>

---

*Author: Claude draft, revised to reflect the Avalonia 12 performance model*
