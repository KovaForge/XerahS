# XIP0065 Avalonia 12 Upgrade - Core Migration and Breaking Changes

**Status**: Draft
**Priority**: High
**Area**: Platform | UI Framework
**Related**: XIP0066 (composition engine performance), XIP0067 (Material.Avalonia compatibility)

---

## Summary

Upgrade XerahS and ShareX.ImageEditor from Avalonia 11.3.x to Avalonia 12 and treat the move as a foundation release, not just a package bump. The official Avalonia 12 announcement frames the release around performance, stability, platform maturity, compiled bindings by default, dispatcher and focus-management improvements, Linux accessibility, and themeable client-side decorations. This XIP adopts that same posture for XerahS.

The migration goal is twofold:

1. Keep the upgrade low-risk by addressing the small set of breaking changes that are visible in real app code.
2. Update XerahS conventions so new work is written in the Avalonia 12 style instead of carrying forward Avalonia 11-era patterns.

---

## Current State

```text
Directory.Packages.props (main repo)
  Avalonia                       -> 12.0.0
  Avalonia.Android               -> 12.0.0
  Avalonia.Controls.ColorPicker -> 12.0.0
  Avalonia.Controls.DataGrid    -> 12.0.0
  Avalonia.Desktop               -> 12.0.0
  Avalonia.Fonts.Inter           -> 12.0.0
  Avalonia.Headless.NUnit        -> 12.0.0
  Avalonia.iOS                   -> 12.0.0
  Avalonia.Skia                  -> 12.0.0
  Avalonia.Themes.Fluent         -> 12.0.0
  AvaloniaUI.DiagnosticsSupport -> 2.2.0
  SkiaSharp                      -> 3.119.3-preview.1.1

ShareX.ImageEditor/Directory.Packages.props (submodule)
  Avalonia                       -> 12.0.0
  Avalonia.Controls.ColorPicker -> 12.0.0
  Avalonia.Desktop               -> 12.0.0
  Avalonia.Fonts.Inter           -> 12.0.0
  Avalonia.Themes.Fluent         -> 12.0.0
  AvaloniaUI.DiagnosticsSupport -> 2.2.0
  SkiaSharp                      -> 3.119.3-preview.1.1
```

The package upgrade is already aligned with Avalonia 12's broader platform direction: .NET 10, SkiaSharp 3, and central package management.

---

## Research Update - 2026-04-12

Online research against the official Avalonia 12 release post and breaking-change guide adds several concrete migration checks beyond the original draft:

- C# binding code must move from removed `IBinding` / `InstancedBinding` assumptions to `BindingBase`, `BindingExpressionBase`, `CompiledBinding`, or `ReflectionBinding` as appropriate.
- `Binding` in C# now represents reflection binding; new binding code should use `CompiledBinding.Create(...)` where the type is known.
- binding plugins are no longer configurable, and the data-annotations binding plugin is disabled by default.
- projects that explicitly configure Skia with `UseSkia()` must also configure text shaping with `Avalonia.HarfBuzz` and `UseHarfBuzz()`.
- selection and focus timing changed for touch and pen input; selection containers now handle selection input directly.
- `TopLevel` is no longer guaranteed to be the visual-tree root; use `TopLevel.GetTopLevel(Visual)` or `GetPresentationSource(Visual)` intentionally.
- clipboard code must use the `IAsyncDataTransfer` path (`DataTransfer`, `DataTransferItem`, `SetDataAsync`, `TryGetTextAsync`, etc.); `IDataObject` has been removed and `DataObject` is inert.
- `DispatcherTimer` and `AvaloniaSynchronizationContext` now default to the current dispatcher, so they must be created on the intended thread or passed the target dispatcher explicitly.
- `Window.WindowState` is now a direct property and cannot be set from styles.
- `Gestures.*` attached events moved to `InputElement`, and focus event args moved to `FocusChangedEventArgs`.
- animations on invisible controls no longer tick unless `Animation.PlaybackBehavior` is deliberately set to `Always`.

The current repo scan already confirms some positive migration state: active XAML uses `WindowDecorations` rather than `SystemDecorations`, active clipboard paths use `DataTransfer` / `SetDataAsync`, and active desktop app builders use `UsePlatformDetect()`. The remaining items below should be treated as the outstanding XerahS enhancement backlog for this XIP.

---

## Why Avalonia 12 Matters Here

The Avalonia 12 release notes describe 12.0 as a foundational release focused on:

- much faster rendering, with especially large gains on heavy visual scenes
- lower idle CPU usage and less unnecessary work when visuals are not visible
- compiled bindings enabled by default
- a stronger dispatcher model with `Dispatcher.CurrentDispatcher`, `Dispatcher.FromThread`, `AvaloniaObject.Dispatcher`, `Dispatcher.Yield`, and background-processing support
- a major focus-management overhaul
- native Linux accessibility via AT-SPI2, plus automation support for validation errors and landmarks
- themeable client-side window decorations

For XerahS, that means the upgrade should change both runtime behavior and coding standards for future UI work.

---

## Migration-Sensitive Changes

### 1. Diagnostics package and attachment model

Avalonia 12 fully retires the legacy `Avalonia.Diagnostics` package path. XerahS must use `AvaloniaUI.DiagnosticsSupport` and `AttachDeveloperTools()`.

Avalonia 12 also does not allow multiple developer-tools attachments. The app must have exactly one dev-tools registration path in DEBUG builds.

**Repository expectation**

- keep `AttachDeveloperTools()` in the application layer
- do not also call `.WithDeveloperTools()` during app-builder startup
- treat duplicate registration as a startup regression

### 2. Data validation now sits on `Control`

The Avalonia 12 release notes call out a visible migration change: data validation handling moved to the base `Control` class. This is a net improvement, but XerahS must re-check any custom validation styling, especially where validation visuals were previously assumed only on specific input controls.

**Required audit**

- form fields in settings and workflow editors
- dialog validation states
- any custom styles targeting validation pseudo-classes or error templates

### 3. Renames and removed obsolete APIs

Avalonia 12 includes a small but real set of consistency renames and removed obsolete APIs. The release notes explicitly call out `SystemDecorations` becoming `WindowDecorations`.

**Required audit**

- window chrome configuration
- custom dialogs
- code and XAML still using removed or renamed Avalonia 11 members

### 4. Binding posture changed

Compiled bindings are enabled by default in Avalonia 12. That improves performance, but it also raises the bar for sloppy or ambiguous binding patterns.

The active migration concern in XerahS is not "compiled bindings are risky"; it is that old reflection-style or cast-heavy bindings should now be treated as technical debt. The known fragile path remains the onboarding wizard's parent lookup and cast sequence.

**Required action**

- keep `x:DataType` explicit on new views and templates
- prefer compiled bindings over reflection bindings in newly touched XAML
- re-test any binding paths that cast through `$parent` or rely on inferred types

### 5. Dispatcher model is better and should be used directly

Avalonia 12 adds dispatcher APIs that are closer to WPF expectations while staying cross-platform. XerahS should use those APIs for new UI-thread handoff work instead of continuing to centralize everything around older static helper patterns.

**Adoption rule**

- for control-owned work, prefer the control or window dispatcher when available
- use dispatcher yield/background scheduling for UI flows that parse, hash, load, or restore state
- stop treating every async UI continuation as an unconditional `Dispatcher.UIThread.Post(...)`

### 6. Focus management is now good enough to rely on

Avalonia 12 opens up `FocusManager`, adds cancellable focus transitions, and improves keyboard traversal behavior. XerahS should use that model intentionally for dialogs, onboarding, history interactions, and editor restore flows rather than relying on fragile delayed-focus workarounds.

### 7. Linux accessibility is now a first-class requirement

Avalonia 12 is the first .NET UI framework to ship a native Linux accessibility backend. It also adds automation support for validation errors and landmarks.

For XerahS, this changes the quality bar:

- validation errors should be surfaced in automation metadata
- navigation-heavy surfaces should expose meaningful landmarks
- new cross-platform UI work should not be reviewed from a Windows-only visual perspective

### 8. Themeable client decorations should replace title-bar workarounds

Avalonia 12 introduces themeable client-side decorations with forced client-side decoration support. XerahS should use the standard Avalonia 12 decoration model for custom windows and dialogs instead of preserving older custom chrome assumptions.

### 9. Binding code should be explicit about compiled vs reflection binding

The official breaking-change guide makes a sharper distinction than the initial draft: XAML `{Binding}` is unaffected structurally, but C# binding code must not assume the old `IBinding` hierarchy. XerahS still has dynamic binding hotspots that should be reviewed as deliberate technical debt:

- `src/desktop/app/XerahS.UI/Controls/PropertyGrid.axaml.cs` creates `new Binding(prop.Name)` dynamically and should either stay documented as a reflection-binding boundary or move to a typed generated-property model.
- `src/desktop/app/XerahS.UI/Views/ProviderExplorerView.axaml`, `src/desktop/app/XerahS.UI/Views/MainWindow.axaml`, and `src/desktop/app/XerahS.UI/Views/ColorPickerDialog.axaml` still use explicit `ReflectionBinding`.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Controls/AnnotationToolbar.axaml` and `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/EditorView.axaml` still use explicit `ReflectionBinding`.

The enhancement is not to remove every `ReflectionBinding` blindly. It is to classify each one as either a real dynamic boundary or a candidate for typed compiled binding.

### 10. Skia-only headless test builders need a text-shaping audit

Avalonia 12 uses HarfBuzz automatically through `UsePlatformDetect()`, but projects that call `UseSkia()` directly can fail startup without a text shaping subsystem. The repo scan shows `tests/XerahS.Tests/Avalonia/AvaloniaTestAppBuilder.cs` calls `UseSkia()` directly. Outstanding action: add `Avalonia.HarfBuzz` / `UseHarfBuzz()` if the headless test builder does not already configure equivalent text shaping through another helper.

### 11. Dispatcher timers and library dispatching need thread-affinity review

Avalonia 12's dispatcher model is an improvement, but it changes `DispatcherTimer` and `AvaloniaSynchronizationContext` defaults to the current dispatcher. XerahS and ShareX.ImageEditor still contain many `Dispatcher.UIThread.Post(...)` calls and several `DispatcherTimer` fields. Outstanding action: audit timer construction sites in toast, tray, auto-capture, after-upload, and mobile-experimental code so timers are created on the intended dispatcher, and prefer instance dispatchers in control/library code where practical.

### 12. Window state styles must be removed

Because `Window.WindowState` is now a direct property, XerahS must avoid setting it from styles. The repo scan found direct XAML property assignments in active windows, which are valid, but the audit should explicitly confirm no style setters target `WindowState` in app or theme dictionaries.

### 13. Selection, gesture, and focus event changes need input-surface testing

The official guide changes the review surface for pointer-heavy XerahS screens. Outstanding action: test history selection, tree/list selection, onboarding keyboard traversal, editor shortcut focus, and region-capture touch/pen behavior against Avalonia 12's release-on-touch selection and new focus event model.

---

## XerahS Adoption Requirements

This XIP is complete only when the repository is not merely "building on 12.0" but following these Avalonia 12-specific rules:

1. New XAML uses explicit `x:DataType` where practical.
2. New UI restore flows use Avalonia 12 dispatcher APIs for background work and UI-thread handoff.
3. Keyboard and dialog flows use real focus-management APIs instead of delayed hacks.
4. Validation and landmark metadata are treated as part of UI completion, not as optional polish.
5. Window and dialog chrome aligns with Avalonia 12 decorations rather than carrying old custom behavior forward.
6. New C# binding code uses `CompiledBinding.Create(...)` where types are known and documents any dynamic `Binding` / `ReflectionBinding` boundary.
7. Skia-only test or host builders are paired with a configured text shaping subsystem.
8. `DispatcherTimer` instances are created on the intended dispatcher, or the target dispatcher is passed explicitly.
9. Styles and theme dictionaries do not set `WindowState`.
10. Touch, pen, gesture, and focus behavior is validated on pointer-heavy surfaces.

---

## Implementation Steps

| # | Step | Status |
|---|---|---|
| 1 | Update main-repo Avalonia packages to 12.0.0 | Done |
| 2 | Update ShareX.ImageEditor Avalonia packages to 12.0.0 | Done |
| 3 | Update `AvaloniaUI.DiagnosticsSupport` in both package files | Done |
| 4 | Ensure there is only one DEBUG developer-tools attachment path | Done |
| 5 | Run full solution build after package update | Done |
| 6 | Re-run headless Avalonia test coverage | Pending |
| 7 | Re-test onboarding wizard bindings and startup XAML load paths | Pending |
| 8 | Audit validation visuals against `Control`-level validation behavior | Pending |
| 9 | Audit renamed or removed APIs, especially window-decoration usage | Pending |
| 10 | Establish Avalonia 12 coding conventions for compiled bindings, dispatchers, focus, and accessibility | Pending |
| 11 | Classify remaining explicit `ReflectionBinding` usage as intentional dynamic boundaries or compiled-binding cleanup | Pending |
| 12 | Audit C# binding creation for `BindingBase` / `CompiledBinding.Create(...)` readiness | Pending |
| 13 | Add or verify `Avalonia.HarfBuzz` / `UseHarfBuzz()` for Skia-only headless builders | Pending |
| 14 | Audit `DispatcherTimer` construction and library/control dispatcher usage | Pending |
| 15 | Verify no styles set `WindowState` | Pending |
| 16 | Test touch/pen selection, gesture events, and focus event behavior on history, onboarding, editor, and region capture | Pending |

---

## Verification Checklist

- [ ] `dotnet build` succeeds with no errors in the main repo
- [ ] `dotnet build` succeeds with no errors in ShareX.ImageEditor
- [ ] Application starts without XAML load exceptions
- [ ] Developer tools attach once in DEBUG builds and never twice
- [ ] Onboarding wizard bindings resolve correctly at runtime
- [ ] Theme rendering remains correct after the upgrade
- [ ] Validation visuals still appear correctly on settings and editor dialogs
- [ ] Keyboard focus is predictable across dialogs, onboarding, and restore flows
- [ ] Linux accessibility and automation metadata are included in follow-up UI work
- [ ] Skia-only headless builders have a text shaping subsystem configured
- [ ] Remaining explicit `ReflectionBinding` instances are classified or converted
- [ ] C# binding creation uses Avalonia 12 binding types intentionally
- [ ] `DispatcherTimer` instances are created on the intended dispatcher
- [ ] No theme or style dictionary sets `WindowState`
- [ ] Touch, pen, gesture, and focus behaviors are validated on pointer-heavy screens

---

## Open Questions

1. Which active views still rely on reflection-style bindings or cast-heavy parent lookup paths?
2. Which custom dialogs should explicitly adopt Avalonia 12 client decorations instead of preserving legacy chrome assumptions?
3. Which XerahS surfaces most need landmarks and validation automation metadata in the first post-upgrade pass?
4. Should the dynamic property-grid binding model remain reflection-based, or should it grow a typed adapter layer?
5. Should headless Avalonia tests add `Avalonia.HarfBuzz` centrally, or should the shared test app builder move back to `UsePlatformDetect()` where possible?

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>
- Avalonia Docs, "Breaking changes in Avalonia 12": <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>

---

*Author: Claude draft, revised for the Avalonia 12 release posture*
