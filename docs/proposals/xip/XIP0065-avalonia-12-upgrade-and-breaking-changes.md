# XIP0065 Avalonia 12 Upgrade - Core Migration and Breaking Changes

**Status**: Complete
**Version**: v0.22.257

**Priority**: High
**Area**: Platform | UI Framework
**Related**: XIP0066 (composition engine performance), XIP0067 (Material scope cleanup)

---

## Summary

XerahS and `ShareX.ImageEditor` are now aligned with Avalonia 12 in the areas that required real repository work rather than a package-only bump. The migration is complete at the source level and the remaining verification items are runtime smoke checks on real hardware, not outstanding code changes.

This XIP is closed when the repository satisfies four conditions:

1. Avalonia 12 startup and diagnostics rules are implemented.
2. Skia-only test hosts are configured with text shaping.
3. active desktop/mobile/editor surfaces are cleaned up for binding, virtualization, and accessibility.
4. remaining dynamic binding edges are documented as intentional.

---

## Implemented Work

### 1. Developer tools follow the Avalonia 12 attachment model

Avalonia 12 only permits one developer-tools attachment path. XerahS now keeps that path in the application layer only:

- `src/desktop/app/XerahS.App/Program.cs` no longer calls `.WithDeveloperTools()`
- `src/desktop/app/XerahS.UI/App.axaml.cs` keeps the single DEBUG `AttachDeveloperTools()` call

This removes the duplicate-registration startup failure and matches Avalonia 12 guidance.

### 2. Skia-only headless tests now include HarfBuzz

The Avalonia headless test builder explicitly configures text shaping:

- `Directory.Packages.props` adds `Avalonia.HarfBuzz`
- `tests/XerahS.Tests/XerahS.Tests.csproj` references `Avalonia.HarfBuzz`
- `tests/XerahS.Tests/Avalonia/AvaloniaTestAppBuilder.cs` now calls `.UseHarfBuzz()` after `.UseSkia()`

That brings the test host into line with Avalonia 12's Skia requirements and avoids false negatives in text/icon rendering tests.

### 3. Desktop binding, virtualization, and accessibility cleanup landed on active surfaces

The main desktop changes shipped in `ProviderExplorerView`, `ColorPickerDialog`, `HistoryView`, and onboarding:

- flyout commands that were only using reflection because of popup-tree boundaries now bind through named roots where the type is known
- the history list mode no longer forces a non-virtualizing `StackPanel`, allowing Avalonia 12 list virtualization fixes to apply
- onboarding and dialog surfaces now expose landmarks, heading levels, and live-region metadata needed for Avalonia 12 accessibility expectations
- validation and status messaging now use automation metadata intentionally instead of leaving those surfaces silent

### 4. Android bootstrap now matches Avalonia 12's app model

The mobile-experimental Avalonia Android app was updated to the Avalonia 12 startup pattern:

- `Platforms/Android/MainActivity.cs` now derives from non-generic `AvaloniaMainActivity`
- `Platforms/Android/AndroidApp.cs` owns `AvaloniaAndroidApplication<MobileApp>` and the app-builder customization
- startup-only wiring moved into `OnCreate`
- the unused timer heartbeat was removed
- Android `SupportedOSPlatformVersion` was raised to `23`, matching current build requirements

### 5. `ShareX.ImageEditor` now separates typed editor bindings from shared dynamic toolbar bindings

The shared editor now uses a named-root binding for the gradient preset command path in:

- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.axaml`

The shared `AnnotationToolbar` remains a deliberate reflection-binding boundary because it is reused across multiple adapter implementations, including region-capture paths that do not expose the editor-only command surface.

---

## Intentional Dynamic Boundaries

Two Avalonia 12 binding edges remain by design:

- `src/desktop/app/XerahS.UI/Controls/PropertyGrid.axaml.cs` still creates `new Binding(prop.Name)` because the property grid is runtime-driven and does not have a static compiled-binding contract.
- `src/desktop/app/XerahS.UI/Views/MainWindow.axaml` still uses `ReflectionBinding` for `TreeViewItem.IsExpanded` because that style setter bridges tree item state from a style scope where the owning item type is not represented by the window-level `x:DataType`.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Controls/AnnotationToolbar.axaml` still uses `ReflectionBinding` for editor-only command/state paths because the control is shared across different adapter implementations.

These are documented boundaries, not accidental Avalonia 11 leftovers.

---

## Audit Results

### Validation behavior

The touched form/dialog surfaces compile and now expose live validation/status metadata. No repository-wide validation-style regression was found that required theme changes for the Avalonia 12 `Control`-level validation move.

### `WindowState` direct-property rule

The repo audit found direct property usage on windows, which is valid, and no style/theme setters targeting `WindowState`, which satisfies the Avalonia 12 breaking change.

### Dispatcher/timer construction

The active Avalonia timers in toast, tray, after-upload, and auto-capture are constructed from UI-owned view-model/service paths. No code change was required from the Avalonia 12 current-dispatcher timer behavior audit.

### Renamed/removed API pressure

The active app code was already on the correct decoration/property naming path for the areas this XIP covers. No additional rename migration was required beyond the startup and binding fixes above.

---

## Actionable Task Ledger

| # | Task | Outcome | Commit |
|---|---|---|---|
| 1 | Align Avalonia 12 diagnostics and headless Skia text shaping | Completed | `b55e14f7` |
| 2 | Remove avoidable desktop reflection bindings, restore virtualization, add accessibility metadata | Completed | `08968745` |
| 3 | Update Android bootstrap and mobile form/accessibility behavior for Avalonia 12 | Completed | `c6ece84c` |
| 4 | Convert the editor gradient preset command path to a typed named-root binding while keeping the shared toolbar dynamic across adapters | Completed | `0738ca9`, `841e388` |

---

## Verification

- `dotnet build tests/XerahS.Tests/XerahS.Tests.csproj -m:1` succeeds
- `dotnet build src/desktop/app/XerahS.UI/XerahS.UI.csproj -m:1` succeeds
- `dotnet build src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj -f net10.0-android -m:1` succeeds
- `dotnet build ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj -m:1` succeeds

---

## Remaining Runtime Validation

No additional source change is required for Avalonia 12 readiness. The remaining checks are release-style manual verification:

- touch and pen behavior on pointer-heavy surfaces
- desktop startup smoke on each target platform
- final UX review of accessibility landmarks and live regions with platform screen readers

Those are runtime acceptance checks, not open migration blockers.

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>
- Avalonia Docs, "Breaking changes in Avalonia 12": <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>
