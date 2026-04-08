# XIP0067 Avalonia 12 — Material.Avalonia Compatibility Update

**Status**: Draft
**Priority**: High
**Area**: UI Framework | Theming
**Related**: XIP0065 (Avalonia 12 upgrade)

---

## Summary

Material.Avalonia 3.7.4 (currently used by XerahS) predates Avalonia 12 and must be updated to a compatible version to avoid runtime style breaks. The latest Material.Avalonia is 3.15.0. This XIP covers the upgrade path, breaking changes in Material.Avalonia's API, and necessary style adjustments in XerahS's `.axaml` theme files.

**Note on icons**: XerahS does not use `Material.Icons.Avalonia`. The desktop app uses a bundled Lucide font via `FontFamily="avares://ShareX.ImageEditor/Assets#lucide"` (see `ShareX.ImageEditor/Assets/LUCIDE.ttf` or similar). Icon glyph rendering goes through `TextBlock` with this font family, not `material:Icon` controls. The `Material.Icons.Avalonia 2.1.10` reference in `Directory.Packages.props` is only consumed by the mobile-experimental project (`XerahS.Mobile.Ava`), not the main desktop UI.

---

## Current State

```
Directory.Packages.props
  Material.Avalonia        → 3.7.4   (desktop main UI)
  Material.Icons.Avalonia  → 2.1.10  (mobile-experimental only)

ShareX.ImageEditor/Assets/
  LUCIDE.ttf (or similar)  ← bundled Lucide font
```

Active icon usages in desktop UI (`OnboardingWizardWindow.axaml`, `ThemeResources.axaml`):
```xml
FontFamily="avares://ShareX.ImageEditor/Assets#lucide"
```
Icons are rendered as `TextBlock` with Lucide glyph code points — no `Material.Icons.Avalonia` involvement.

---

## Breaking Changes in Material.Avalonia 3.15.0

### 1. Control API Changes

Material.Avalonia 3.x made several breaking changes to custom controls:

| Old API | New API | Notes |
|---|---|---|
| `Material-icon` attached property (pre-3.0) | `Icon` property on `MaterialIcon` | Namespace change — desktop UI does not use this |
| Custom `ColorLoader` singleton | `IMaterialThemePalette` DI-injected | Requires DI registration change — audit if DI-scoped |
| `RippleEffect` behavior on `Button` | Replaced by built-in `PressableControl` ripple | May need `.axaml` style update in overrides |
| `Card` control `Outlined` property | `Card` always outlined; `FilledCard` for filled variant | Check all `Card` usages in theme |

### 2. Theme Resource Dictionary Reorganization

Material.Avalonia 3.x reorganized its internal resource dictionaries. Imports that directly reference internal paths (e.g., `avares://Material.Avalonia/Themes/Internal/SomeInternal.xaml`) may break. Audit XerahS's theme include chain.

### 3. Mobile-experimental Icon Change (Non-Desktop)

`Material.Icons.Avalonia 2.1.10 → 3.0.1` (for `XerahS.Mobile.Ava`) may rename glyph identifiers. Since the desktop UI uses Lucide and not Material icons, this only affects the mobile-experimental project.

---

## Upgrade Steps

| # | Step | Notes |
|---|---|---|
| 1 | Update `Material.Avalonia` to `3.15.0` in `Directory.Packages.props` | Desktop main UI only; `Material.Icons.Avalonia` stays unless mobile needs it |
| 2 | Run `dotnet restore` and check for dependency conflicts | Material.Avalonia 3.15.0 requires Avalonia 12; confirms compatibility |
| 3 | Build and check for XAML warnings | Missing style resources will surface as build warnings |
| 4 | Audit `Themes/MaterialOverrides.axaml` for DI registration changes | `ColorLoader` → `IMaterialThemePalette` may need service registration |
| 5 | Audit all `Card` usages | Replace with `FilledCard` where filled variant is needed |
| 6 | Audit `Themes/` directory for hardcoded Material.Avalonia internal paths | Likely not present but verify |
| 7 | Verify all styled controls render correctly (buttons, cards, dialogs) | Focus on Material Design specific controls |
| 8 | Mobile-experimental only: update `Material.Icons.Avalonia` to `3.0.1` if needed | Only for `XerahS.Mobile.Ava`; desktop Lucide icons unaffected |

---

## Risk Assessment

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| `Card` style change causes layout shift | Medium | Low | Check all `Card` usages; use `FilledCard` where needed |
| `ColorLoader` → `IMaterialThemePalette` DI break | Medium | Medium | Audit DI registration in theme/bootstrap code |
| `RippleEffect` behavior change on buttons | Low | Low | Review `Button` styles in `Themes/MaterialOverrides.axaml` |
| Internal resource path break | Low | Low | Audit `avares://Material.Avalonia/Themes/Internal/` references |
| Lucide icon rendering affected | None | None | Desktop icons use Lucide font, not Material.Icons.Avalonia |

---

## Verification Checklist

After updating Material.Avalonia:

- [ ] `dotnet build` succeeds with no errors
- [ ] Application starts without XAML load exceptions
- [ ] All `Card` controls display correctly (outlined variant)
- [ ] Button ripple effects work on mouse press
- [ ] Theme colors (primary, secondary, surface) match existing palette
- [ ] Dark/Light theme toggle works correctly
- [ ] Lucide icons in onboarding wizard and main UI render correctly (no tofu)
- [ ] Mobile-experimental (`XerahS.Mobile.Ava`) builds and renders icons correctly

---

## Open Questions

1. **Style overrides**: Does XerahS have custom style overrides for Material.Avalonia controls in `Themes/MaterialOverrides.axaml`? These would be the first to break on upgrade.
2. **DI registration**: Is `ColorLoader` singleton used anywhere in the DI container? If so, switching to `IMaterialThemePalette` requires a service registration change.
3. **Mobile icons**: Should `Material.Icons.Avalonia` also be updated to `3.0.1` for the mobile-experimental project, or is that out of scope for this XIP?

---

*Author: Claude (compatibility upgrade draft)*
