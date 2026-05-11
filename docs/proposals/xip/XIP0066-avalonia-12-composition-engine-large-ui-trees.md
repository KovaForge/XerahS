# XIP0066 Avalonia 12 Composition Engine - Large UI Tree Performance

**Status**: Complete
**Version**: v0.22.257

**Priority**: Medium
**Area**: Performance | UI Framework
**Related**: XIP0065 (core upgrade)

---

## Summary

This XIP is complete as a source-level performance-readiness pass for Avalonia 12. The work focused on making sure XerahS does not block the framework's compositor, virtualization, compiled-binding, and hidden-visual improvements on its largest active UI trees.

The goal here was structural readiness, not synthetic benchmark theatre. The repository changes landed where Avalonia 12 could provide real benefit immediately:

- restore built-in virtualization instead of forcing eager layout
- remove avoidable reflection bindings on hot command paths
- stop carrying background work that no longer serves the mobile app
- ensure Skia-backed headless validation uses the same text-shaping stack production relies on

---

## Implemented Performance Work

### History view virtualization was restored

`src/desktop/app/XerahS.UI/Views/HistoryView.axaml` no longer overrides the list-mode `ItemsPanel` with a plain `StackPanel`. That lets Avalonia 12's `ListBox` virtualization and item-container fixes apply instead of forcing full realization.

### Command binding overhead was reduced on active surfaces

Typed or named-root bindings replaced avoidable reflection-based command lookups in:

- `src/desktop/app/XerahS.UI/Views/ProviderExplorerView.axaml`
- `src/desktop/app/XerahS.UI/Views/ColorPickerDialog.axaml`
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.axaml`

That keeps frequently-used menu and preset interactions aligned with Avalonia 12's compiled-binding posture. The shared editor toolbar remains a deliberate dynamic path because it is reused across multiple adapter types.

### Android bootstrap no longer carries dead timer churn

The obsolete heartbeat timer was removed from `src/mobile-experimental/XerahS.Mobile.Ava/Platforms/Android/MainActivity.cs`. That change is small, but it removes pointless background wakeups from the mobile Avalonia host and keeps startup behavior closer to Avalonia 12's lower-idle model.

### Skia-backed tests now match Avalonia 12 rendering expectations

The headless test host uses `UseSkia()` plus `UseHarfBuzz()`, which prevents performance and rendering smoke runs from using a weaker text-shaping configuration than production.

---

## Surfaces Improved By This XIP

| Surface | Change | Expected Avalonia 12 Gain |
|---|---|---|
| History list mode | restored built-in virtualization | lower realization cost and smoother scrolling on large datasets |
| Provider explorer / color picker | removed avoidable reflection command lookups | less binding overhead on popup and command paths |
| Image editor toolbar / gradient presets | removed avoidable reflection bindings | lower hot-path command overhead in dense editor UI |
| Android Avalonia host | removed dead timer work and updated bootstrap | lower idle churn and cleaner startup path |
| Headless Avalonia tests | added HarfBuzz to Skia host | render and icon/text smoke paths now match production expectations more closely |

---

## Audit Notes

### Hidden visuals and animation behavior

No app-level hidden-animation override was required for this pass. The code changes above avoid reintroducing obvious background churn, and no active surface in the touched scope needed `Animation.PlaybackBehavior="Always"`.

### Dispatcher/timer ownership

The timer audit covered the active Avalonia UI timers in toast, tray, after-upload, and auto-capture. They are created from UI-owned paths, so no dispatcher-construction fix was required for Avalonia 12.

### Top-level/render-root assumptions

No additional render-root fix was required in the touched surfaces during this pass.

---

## Actionable Task Ledger

| # | Task | Outcome | Commit |
|---|---|---|---|
| 1 | Restore Avalonia 12-friendly virtualization on large history lists | Completed | `08968745` |
| 2 | Reduce hot-path reflection-binding overhead on desktop and editor surfaces | Completed | `08968745`, `0738ca9`, `841e388` |
| 3 | Remove dead timer churn from the Android Avalonia host | Completed | `c6ece84c` |
| 4 | Align Skia-backed validation hosts with Avalonia 12 text shaping | Completed | `b55e14f7` |

---

## Verification

- `dotnet build src/desktop/app/XerahS.UI/XerahS.UI.csproj -m:1` succeeds
- `dotnet build ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj -m:1` succeeds
- `dotnet build tests/XerahS.Tests/XerahS.Tests.csproj -m:1` succeeds
- `dotnet build src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj -f net10.0-android -m:1` succeeds

---

## Follow-up Measurement

Framework-readiness is complete. If the team wants fresh numeric benchmarking, that is a separate profiling exercise and should compare:

- large history scrolling
- region-capture sustained drag
- image-editor tool/panel switching
- effect-preview slider interaction

That measurement work is optional performance analysis, not an open migration dependency.

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>
- Avalonia Docs, "Breaking changes in Avalonia 12": <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>
