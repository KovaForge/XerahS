# XIP0065 Avalonia 12 Upgrade — Core Migration and Breaking Changes

**Status**: Draft
**Priority**: High
**Area**: Platform | UI Framework
**Related**: XIP0066 (composition engine performance), XIP0067 (Material.Avalonia compatibility)

---

## Summary

Upgrade XerahS and ShareX.ImageEditor from Avalonia 11.3.13 to Avalonia 12.0.0. This is a foundation-paving upgrade that unlocks the Android high-refresh-rate dispatcher, iOS scene delegate, and composition engine improvements in subsequent XIPs. The primary migration work is verifying the XAML binding class hierarchy changes and confirming `AttachDeveloperTools()` is wired correctly (it already is in XerahS — no code change needed).

---

## Current State

```
Directory.Packages.props (main repo)
  Avalonia                        → 11.3.13
  Avalonia.Android                → 11.3.9
  Avalonia.Controls.ColorPicker  → 11.3.13
  Avalonia.Controls.DataGrid     → 11.3.9
  Avalonia.Desktop                → 11.3.13
  Avalonia.Fonts.Inter            → 11.3.13
  Avalonia.Headless.NUnit         → 11.3.13
  Avalonia.iOS                    → 11.3.9
  Avalonia.Skia                   → 11.3.13
  Avalonia.Themes.Fluent          → 11.3.13
  AvaloniaUI.DiagnosticsSupport  → 2.1.1

ShareX.ImageEditor/Directory.Packages.props (submodule)
  Avalonia                        → 11.3.13
  Avalonia.Controls.ColorPicker  → 11.3.13
  Avalonia.Desktop                → 11.3.13
  Avalonia.Fonts.Inter            → 11.3.13
  Avalonia.Themes.Fluent          → 11.3.13
  AvaloniaUI.DiagnosticsSupport  → 2.1.1
```

---

## Target State

```
  Avalonia                        → 12.0.0  (all packages)
  AvaloniaUI.DiagnosticsSupport  → 2.2.0
```

---

## Breaking Changes in Avalonia 12

### 1. `.NET Standard / .NET Framework support dropped`

XerahS targets .NET 10 — no action needed.

### 2. `Avalonia.Diagnostics` package fully retired

The legacy `Avalonia.Diagnostics` package (which provided `AttachDevTools()`) is removed. The codebase must use `AvaloniaUI.DiagnosticsSupport` + `AttachDeveloperTools()`.

**Status in XerahS: Already compliant.** `App.axaml.cs:77` calls `this.AttachDeveloperTools()` inside `#if DEBUG`. No code changes required.

### 3. XAML binding class hierarchy changes

Avalonia 12 changed the internal class hierarchy for XAML bindings. This can cause:
- `Binding` expressions that cast to internal types to break
- Custom `IMarkupExtension` implementations that rely on internal Avalonia types
- `.axaml` files that use `Style.x:Name` binding syntax dependent on the old hierarchy

**Required action**: Full build + runtime pass after updating packages. Watch for:
- `System.InvalidCastException` in any `.axaml` binding
- Missing or incorrect property values in styled controls
- Any `Avalonia.Markup.Xaml.XamlLoadException` at startup

### 4. `Avalonia.Headless.NUnit` updated to 12.0.0

Six headless NUnit test files confirmed in the repo:

| File | Using |
|---|---|
| `tests/XerahS.Tests/RegionCapture/RegionCaptureUiSmokeTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Hotkeys/WorkflowEditorViewModelTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Editor/EditorContextMenuSmokeTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Editor/EditorCloseConfirmationTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Editor/CreativeFilterDialogWiringTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Avalonia/ViewLocatorTests.cs` | `Avalonia.Headless.NUnit` |
| `tests/XerahS.Tests/Avalonia/AvaloniaTestAppBuilder.cs` | `Avalonia.Headless` + `Avalonia.Headless.NUnit` |

`Avalonia.Headless.NUnit` in `Directory.Packages.props` is already set to `12.0.0`. Tests must be re-run after the upgrade to verify compatibility.

### 5. Binding audit findings

**Fragile patterns confirmed in active code:**

`OnboardingWizardWindow.axaml:79,85,90,97` — cast inside a binding path:
```xml
ConverterParameter={Binding $parent[ItemsControl].((vm:OnboardingWizardViewModel)DataContext).CurrentStepIndex}
```
This pattern casts `DataContext` to `vm:OnboardingWizardViewModel` inside a `$parent` lookup path. If Avalonia 12's binding class hierarchy affects how `$parent` lookups or path-based casts resolve, these bindings could throw `InvalidCastException` at runtime.

**All other `x:DataType` usages** in ShareX.ImageEditor are explicit and type-safe (compiled bindings via `x:DataType` on `DataTemplate`, no internal Avalonia type casts). These are low-risk.

**`RelativeSource Ancestor` patterns** exist only in stale `Views_TEMP2`/`Views_PARTIAL` directories — no action needed for active code.

**Action required:** Re-test the Onboarding wizard flow after Avalonia 12 upgrade (`Settings → first-run wizard`).

---

## Implementation Steps

| # | Step | Status |
|---|---|---|
| 1 | Update `Directory.Packages.props` Avalonia packages to `12.0.0` | ✅ Done |
| 2 | Update `ShareX.ImageEditor/Directory.Packages.props` Avalonia packages to `12.0.0` | ✅ Done |
| 3 | Update `AvaloniaUI.DiagnosticsSupport` to `2.2.0` in both `Directory.Packages.props` files | ✅ Done |
| 4 | Run `dotnet restore && dotnet build` — verify no build errors | Pending |
| 5 | Run headless NUnit tests — verify all 6 test files still pass | Pending |
| 6 | Run application — verify startup, theme, dev tools, image editor, region capture, settings | Pending |
| 7 | Trigger first-run onboarding wizard — verify `$parent[ItemsControl]` cast bindings work | Pending |
| 8 | Update ShareX.ImageEditor submodule reference commit in XerahS | Pending |

---

## Verification Checklist

After updating packages, verify:

- [ ] `dotnet restore` completes with no version conflicts
- [ ] `dotnet build` completes with no errors (warnings are expected during migration)
- [ ] Application starts and shows the main window
- [ ] Dev tools attach correctly in DEBUG builds (`AttachDeveloperTools()`)
- [ ] Theme (Fluent) renders correctly
- [ ] Image editor loads and is interactive
- [ ] Region capture overlay is functional
- [ ] Settings page loads and saves correctly

---

## Open Questions

1. **Binding audit scope**: Proactive audit — confirmed yes. Key patterns to audit: `Binding` expressions with internal Avalonia type casts, `x:DataType` inference chains, and `Style` resource lookups that assume the old class hierarchy.
2. **Headless tests**: Confirmed yes. `Avalonia.Headless.NUnit` package version must be updated to `12.0.0`. Any existing headless UI tests in the repo must be verified to still pass after the package upgrade.

---

*Author: Claude (automated upgrade draft)*
