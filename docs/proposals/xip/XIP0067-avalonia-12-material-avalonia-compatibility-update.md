# XIP0067 Avalonia 12 - Material.Avalonia Compatibility Update

**Status**: Draft
**Priority**: High
**Area**: UI Framework | Theming
**Related**: XIP0065 (Avalonia 12 upgrade)

---

## Summary

Material.Avalonia compatibility for Avalonia 12 should be treated as more than a package-version requirement. The framework release changed the baseline expectations for validation, focus management, accessibility, client-side decorations, and binding performance. This XIP updates XerahS's Material layer so it behaves like an Avalonia 12 application rather than an Avalonia 11 theme stack carried forward.

The immediate package alignment is already straightforward:

```text
Directory.Packages.props
  Material.Avalonia        -> 3.15.0
  Material.Icons.Avalonia  -> 3.0.1
```

But the real goal is to ensure that Material-themed surfaces in XerahS:

- respect Avalonia 12 validation behavior
- cooperate with Avalonia 12 focus handling
- expose accessibility metadata cleanly
- integrate with themeable client-side decorations
- keep benefiting from compiled bindings in touched views

---

## Current State

XerahS uses Material.Avalonia for desktop theming, while desktop icon rendering continues to rely primarily on the bundled Lucide font. `Material.Icons.Avalonia` is not the main desktop icon path and should not drive desktop theming decisions.

That means this XIP is mainly about:

- Material styles and control behavior
- Material-themed dialogs and windows
- validation, focus, accessibility, and shell integration under Avalonia 12

not about replacing Lucide usage with Material icons.

---

## Avalonia 12 Behaviors the Material Layer Must Adopt

### 1. Validation is now broader and more important

The Avalonia 12 release notes call out validation behavior moving to the base `Control` class and specifically mention automation support for validation errors.

For Material-themed XerahS surfaces, that means:

- validation visuals must still render correctly after the upgrade
- validation states must remain readable in both light and dark themes
- dialogs and forms should not style validation only on a narrow subset of input controls
- accessibility metadata for validation must not be lost under custom Material styling

### 2. Focus handling is strong enough to rely on

Avalonia 12 overhauled focus management. Material dialogs, flyouts, menus, and form surfaces should align with that instead of preserving delayed-focus or click-first workarounds.

Focus-sensitive Material scenarios in XerahS include:

- settings dialogs
- onboarding flows
- confirmation dialogs
- any Material-styled forms with validation and primary/secondary actions

### 3. Client decorations should be part of the theme story

Avalonia 12 introduces themeable client-side window decorations. Material-themed windows and dialogs should inherit and complement that system instead of fighting it with legacy title-bar assumptions.

This matters most for:

- custom dialogs
- shell windows with branded chrome
- any future window-level Material polish work

### 4. Accessibility is now a cross-platform bar, not optional polish

Avalonia 12 adds native Linux accessibility and automation landmarks. Material-styled XerahS surfaces should include usable automation names, landmarks, and validation-error exposure so the theme layer does not become the point where accessibility regresses.

### 5. Compiled bindings should remain intact in Material views

Avalonia 12 enables compiled bindings by default. If a Material-themed view or template is touched during compatibility work, the change should preserve or improve binding clarity with explicit `x:DataType` where practical rather than backsliding into loose reflection bindings.

---

## Compatibility and Audit Areas

| Area | What To Check | Why It Matters |
|---|---|---|
| Material-themed dialogs | Focus landing, keyboard traversal, validation visuals, automation names | Avalonia 12 focus and validation behavior changed |
| Window chrome | Material styles do not clash with Avalonia 12 client decorations | Avoid title-bar regressions and mismatched chrome |
| Theme resources and overrides | Custom overrides still bind correctly and load cleanly | Prevent XAML load or styling regressions |
| Validation styling | Error states are visible, accessible, and consistent | Align with new `Control`-level validation behavior |
| Material-heavy views | Explicit compiled bindings on touched hot paths | Preserve Avalonia 12 binding-performance benefits |
| Mobile-experimental Material icons | Build and glyph rendering only where the package is actually used | Keep scope honest; desktop Lucide usage remains separate |

---

## Implementation Steps

| # | Step | Notes |
|---|---|---|
| 1 | Keep `Material.Avalonia` aligned with Avalonia 12-compatible versions | Package-level prerequisite |
| 2 | Audit custom Material theme overrides and includes | Catch style-load regressions early |
| 3 | Re-check validation visuals on Material-themed forms and dialogs | Must reflect Avalonia 12 validation behavior |
| 4 | Re-check keyboard focus and default-action behavior on Material dialogs | Must align with Avalonia 12 focus handling |
| 5 | Review Material-themed windows against Avalonia 12 client decorations | Avoid custom chrome conflicts |
| 6 | Ensure automation names, landmarks, and validation-error exposure survive custom styling | Linux accessibility is now a first-class requirement |
| 7 | Preserve compiled-binding posture in any touched Material views | Avoid performance regressions from sloppy bindings |
| 8 | Verify mobile-experimental `Material.Icons.Avalonia` usage only if that project is in scope | Keep desktop and mobile concerns separate |

---

## Verification Checklist

- [ ] `dotnet build` succeeds with no errors
- [ ] Application starts without XAML load exceptions
- [ ] Material-themed dialogs render correctly
- [ ] Validation states remain visible and accessible
- [ ] Keyboard focus is predictable through Material dialogs and forms
- [ ] Window chrome and title-bar behavior remain correct under Avalonia 12 decorations
- [ ] Theme colors remain consistent in both light and dark modes
- [ ] Desktop Lucide icon rendering remains unaffected
- [ ] Mobile-experimental Material icons only gate this XIP if that project is part of the validation run

---

## Non-Goals

- Replacing Lucide-based desktop icons with Material icons
- Turning this XIP into a full navigation rewrite just because Avalonia 12 added page controls
- Re-styling the entire application when the actual need is compatibility plus Avalonia 12 alignment

---

## Open Questions

1. Which Material-themed dialogs in XerahS still rely on older focus workarounds?
2. Which custom theme overrides are the highest risk for Avalonia 12 validation or decoration regressions?
3. Is any Material icon verification beyond the mobile-experimental project actually needed for desktop acceptance?

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>

---

*Author: Claude draft, revised for Avalonia 12 adoption rather than version-only compatibility*
