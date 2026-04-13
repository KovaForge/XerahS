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

## Research Update - 2026-04-12

Online research against the official Avalonia 12 release post and breaking-change guide adds these Material-specific implications:

- validation is now handled at the base `Control` level, and validation automation exposure matters on Linux as well as Windows.
- `Window.WindowState` is now a direct property, so Material theme dictionaries must not set it through styles.
- themeable client window decorations replaced older title-bar plumbing; Material windows should not depend on removed `TitleBar`, `CaptionButtons`, `ChromeOverlayLayer`, or `ExtendClientAreaChromeHints` APIs.
- focus event args and focus traversal APIs changed; Material dialogs and flyouts need runtime keyboard testing, not just XAML load testing.
- invisible controls no longer tick style-triggered animations by default; custom Material ripple, progress, flyout, and transition behavior should be checked for deliberate hidden-state behavior.
- the AvaloniaCommunity `Material.Avalonia` GitHub releases page shows v3.14.1 as the latest published GitHub release at the time of research, while NuGet metadata lists `Material.Avalonia` 3.15.0 and 3.15.1. XerahS central package management currently pins 3.15.0. Outstanding action: decide whether to stay on 3.15.0 intentionally or bump to the current stable NuGet 3.15.1 after release-note/feed review.

The repo scan also changes the scope of this XIP. Active references to `Material.Avalonia` and `Material.Icons.Avalonia` are in `src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj`; active desktop XAML/theme files do not currently show `Material.Styles` or `MaterialTheme` includes. Desktop continues to use Fluent/Avalonia theming and Lucide icons unless a future change introduces a real desktop Material layer.

---

## Current State

XerahS centrally pins Material packages, but the active Avalonia Material scope found in the repo is the mobile-experimental Avalonia project:

```text
src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj
  Material.Avalonia
  Material.Icons.Avalonia
```

Desktop icon rendering continues to rely primarily on the bundled Lucide font, and active desktop theme files do not currently show `Material.Styles` or `MaterialTheme` includes. `Material.Icons.Avalonia` is not the main desktop icon path and should not drive desktop theming decisions.

That means this XIP is mainly about:

- Material styles and control behavior in the mobile-experimental Avalonia project
- any future Material-themed dialogs and windows that are intentionally added to desktop
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

### 6. Material scope must be proven before desktop work starts

The current repo scan does not find an active desktop Material theme layer. Outstanding action: before any desktop compatibility work is claimed under this XIP, identify the actual `Material.Avalonia` theme include or package reference in the desktop app. If none exists, keep this XIP scoped to mobile-experimental package compatibility plus central-package hygiene.

### 7. Material package version provenance must be verified

`Directory.Packages.props` pins `Material.Avalonia` to 3.15.0. NuGet metadata confirms that 3.15.0 exists and also lists 3.15.1, while the public GitHub releases page found during research still lists v3.14.1 as latest. Outstanding action: verify the release notes/source for the 3.15.x line and decide whether XerahS should remain on 3.15.0 or move to 3.15.1.

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
| Mobile-experimental Avalonia app | Package restore, startup, navigation, validation, and icon rendering | This is the currently confirmed Material.Avalonia consumer |
| Desktop Material scope proof | Confirm whether desktop actually includes Material themes before auditing them | Prevents this XIP from inventing desktop work that does not exist |
| Version provenance | Confirm why XerahS pins 3.15.0 when NuGet also lists 3.15.1 and GitHub releases lag at v3.14.1 | Avoids anchoring compatibility on an unreviewed package version |

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
| 9 | Prove whether any active desktop project consumes `Material.Avalonia`; if not, record desktop as out of scope | Prevents false desktop acceptance criteria |
| 10 | Verify `Material.Avalonia` 3.15.x feed provenance and release notes, then decide whether to stay on 3.15.0 or bump to 3.15.1 | Required before calling package alignment done |
| 11 | Run the mobile-experimental Avalonia Material startup/theme/icon smoke path if that project is in scope | Confirms the actual Material consumer works |
| 12 | Check Material styles for hidden animation/ripple behavior after Avalonia 12 invisible-animation changes | Aligns theme behavior with the new compositor model |

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
- [ ] Active desktop Material scope is proven, or desktop Material validation is explicitly marked out of scope
- [ ] `Material.Avalonia` 3.15.x provenance is verified against the configured package feed and release notes
- [ ] Mobile-experimental Avalonia Material startup/theme/icon smoke test is run when that project is in scope
- [ ] Material ripple/progress/flyout animations behave intentionally when controls are hidden

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
4. Is `Material.Avalonia` still intended for desktop, or is it now only a mobile-experimental dependency?
5. Should XerahS stay on `Material.Avalonia` 3.15.0, or move to the NuGet-listed 3.15.1 after release-note review?
6. Should mobile-experimental Avalonia keep using Material themes, or should it move closer to the Fluent desktop baseline after the Avalonia 12 upgrade?

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>
- Avalonia Docs, "Breaking changes in Avalonia 12": <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>
- AvaloniaCommunity Material.Avalonia GitHub releases: <https://github.com/AvaloniaCommunity/Material.Avalonia/releases>
- NuGet flat-container metadata for Material.Avalonia: <https://api.nuget.org/v3-flatcontainer/material.avalonia/index.json>

---

*Author: Claude draft, revised for Avalonia 12 adoption rather than version-only compatibility*
