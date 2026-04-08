# XIP0067 Avalonia 12 — Material.Avalonia Compatibility Update

**Status**: Draft
**Priority**: High
**Area**: UI Framework | Theming
**Related**: XIP0065 (Avalonia 12 upgrade)

---

## Summary

Material.Avalonia 3.7.4 (currently used by XerahS) predates Avalonia 12 and must be updated to a compatible version to avoid runtime style breaks. The latest Material.Avalonia is 3.15.0. This XIP covers the upgrade path, breaking changes in Material.Avalonia's API, and any necessary style adjustments in XerahS's `.axaml` theme files.

---

## Current State

```
Directory.Packages.props
  Material.Avalonia        → 3.7.4
  Material.Icons.Avalonia  → 2.1.10
```

`Material.Avalonia` provides Material Design 3 styling for Avalonia controls and is used throughout XerahS's theme layer (e.g., `Themes/MaterialOverrides.axaml`).

---

## Breaking Changes in Material.Avalonia 3.15.0

### 1. Control API Changes

Material.Avalonia 3.x made several breaking changes to custom controls:

| Old API | New API | Notes |
|---|---|---|
| `Material-icon` attached property (pre-3.0) | `Icon` property on `MaterialIcon` | Namespace change |
| Custom `ColorLoader` singleton | `IMaterialThemePalette` DI-injected | Requires DI registration change |
| `RippleEffect` behavior on `Button` | Replaced by built-in `PressableControl` ripple | May need `.axaml` style update |
| `Card` control `Outlined` property | `Card` always outlined; `FilledCard` for filled variant | Check all `Card` usages |

### 2. Icon Font Update

`Material.Icons.Avalonia` has a newer icon font. Some icon glyph identifiers may have changed between 2.1.10 and 3.0.1.

**Required action**: Search for any icon-related XAML warnings at startup and update glyph names accordingly.

### 3. Theme Resource Dictionary Reorganization

Material.Avalonia 3.x reorganized its internal resource dictionaries. Imports that directly reference internal paths (e.g., `avares://Material.Avalonia/Themes/Internal/SomeInternal.xaml`) may break. Check for any such hardcoded paths in XerahS's theme includes.

---

## Upgrade Steps

| # | Step | Notes |
|---|---|---|
| 1 | Update `Material.Avalonia` to `3.15.0` in `Directory.Packages.props` | Also update `Material.Icons.Avalonia` to `3.0.1` |
| 2 | Run `dotnet restore` and check for dependency conflicts | Material.Avalonia 3.15.0 requires Avalonia 12; confirms compatibility |
| 3 | Build and check for XAML warnings | Icon glyph renames and missing resources will surface here |
| 4 | Update any hardcoded Material.Avalonia internal resource paths | Likely not present, but audit `Themes/` directory |
| 5 | Verify all styled controls render correctly (buttons, cards, dialogs) | Focus on Material Design specific controls |
| 6 | Check icon rendering — all `material:Icon` usages | Update any renamed icon glyphs |

---

## Risk Assessment

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Icon glyph renamed, appears as tofu (missing character) | Low | Medium | Search for startup XAML warnings; fix per case |
| `Card` style change causes layout shift | Medium | Low | Check all `Card` usages; use `FilledCard` if needed |
| `RippleEffect` behavior change | Low | Low | Review `Button` styles in `Themes/MaterialOverrides.axaml` |
| Internal resource path break | Low | Low | Audit any `avares://Material.Avalonia/Themes/Internal/` references |

---

## Verification Checklist

After updating Material.Avalonia:

- [ ] `dotnet build` succeeds with no errors
- [ ] Application starts without XAML load exceptions
- [ ] All `Card` controls display correctly (outlined variant)
- [ ] All icon glyphs render (no tofu/missing character in icons)
- [ ] Button ripple effects work on mouse press
- [ ] Theme colors (primary, secondary, surface) match existing palette
- [ ] Dark/Light theme toggle works correctly

---

## Open Questions

1. **Style overrides**: Does XerahS have custom style overrides for Material.Avalonia controls in `Themes/MaterialOverrides.axaml` that might conflict with 3.15.0 changes?
2. **Icon audit strategy**: Should we write a test that enumerates all icon glyphs at startup and warns on unknown glyphs?
3. **Migration path**: Should we upgrade incrementally (3.7.4 → latest 3.x → 3.15.0) or jump directly to 3.15.0?

---

*Author: Claude (compatibility upgrade draft)*
