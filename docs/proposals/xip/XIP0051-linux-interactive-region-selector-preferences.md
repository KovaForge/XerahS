# XIP0051 Linux Interactive Region Selector Preferences

**Status**: Complete
**Version**: v0.22.257

**Priority**: High
**Affected platform**: Linux
**Related**: XIP0016, XIP0046, XIP0047

---

## Problem Statement

XerahS currently exposes Linux capture choice mostly through a single `UseModernCapture` boolean. That setting is too coarse for the real Linux capture landscape:

- Some systems need the XerahS overlay crosshair to avoid broken or slow portal flows.
- Some systems have a working desktop-native selector and should keep that modern experience.
- Some systems expose multiple viable selectors at once, but users cannot express a preference.
- The same machine can behave differently depending on session protocol, desktop environment, portal backend, and selector provider availability.

This XIP proposes a Linux-specific interactive region selector preference system so users can choose how XerahS behaves for region-based screenshots and recordings, while still preserving automatic fallbacks.

The goal is not to expose every Linux implementation detail directly to end users. The goal is to provide a clear user-facing control that maps to the underlying stack intelligently.

---

## Background Reading

This section exists partly as future reading material for understanding Linux capture behavior.

### Linux capture stack terms

#### Session protocol

The session protocol describes how the desktop session communicates with applications for display and input.

- `X11`
- `Wayland`

This is one of the most important decision points because some selectors only work on one protocol family.

#### Desktop environment / window manager

This is the userΓÇÖs desktop shell or environment.

Examples:

- `GNOME`
- `KDE Plasma`
- `Cinnamon`
- `MATE`
- `XFCE`
- `LXQt`
- `Sway`
- `Hyprland`

This matters because some desktops expose their own region-selection APIs or tools.

#### Compositor

The compositor is the display/input composition layer. It is especially relevant on Wayland.

Examples:

- Mutter
- KWin
- wlroots-based compositors such as Sway
- Hyprland

This matters because some capture methods are compositor-specific, especially on Wayland.

#### Portal backend

The portal backend is the implementation behind XDG Desktop Portal interfaces such as Screenshot and ScreenCast.

Examples:

- `xdg-desktop-portal-gnome`
- `xdg-desktop-portal-kde`
- `xdg-desktop-portal-gtk`
- `xdg-desktop-portal-xapp`
- `xdg-desktop-portal-wlr`
- `xdg-desktop-portal-hyprland`
- `xdg-desktop-portal-lxqt`

This matters because the same XDG portal API can behave differently depending on which backend answers the request.

#### Selector provider

This is the actual mechanism used for interactive region selection.

Examples:

- XerahS overlay crosshair
- portal dialog
- GNOME native selector
- KDE native selector
- `slurp`

This is the concept users care about most directly, and it is the right level for a user-facing preference.

---

## Current State

Today, the main user-facing control is `UseModernCapture`, exposed in:

- `src/desktop/core/XerahS.Core/Models/TaskSettings.cs`
- `src/desktop/app/XerahS.UI/Views/ApplicationSettingsView.axaml`
- `src/desktop/app/XerahS.UI/Views/TaskSettingsPanel.axaml`

Internally, Linux capture already distinguishes between several providers and environment characteristics:

- session type
- desktop environment
- screenshot portal availability
- desktop-native D-Bus selectors
- wlroots selector tools
- XerahS overlay fallback

Recent Linux work also restored safe X11 overlay fallback behavior and introduced capability-aware ordering for capable X11 systems. That means the runtime has become smarter, but the settings model is still simpler than the runtime.

---

## UX Direction

The user-facing abstraction should be:

**Interactive region selector**

not:

- compositor implementation details
- D-Bus interface names
- low-level portal backend internals

The UI should let the user choose the interactive region selection experience, while XerahS handles the low-level mapping.

### Proposed UI card

Title:

`Linux Region Selector`

Description:

`Choose which tool to use for interactive region selection (screenshots and recordings).`

Primary control:

- `Region selector: [Automatic (recommended)]`

Diagnostics text below:

- `Current session: X11 / Cinnamon`
- `Available selectors: XerahS overlay, Desktop native selector, Portal dialog`
- `Automatic will prefer: Desktop native selector`

### Recommended option labels

- `Automatic (recommended)`
- `XerahS overlay crosshair`
- `Desktop native selector`
- `Portal dialog`
- `slurp (wlroots)`

Advanced provider-specific entries such as `GNOME selector` and `KDE selector` should be deferred unless the generic labels prove insufficient.

### Availability rules

The dropdown should only show:

- `Automatic`
- options that are genuinely usable on the current system
- `XerahS overlay crosshair` when overlay fallback is supported

Unavailable items should not appear in the first implementation.

---

## Goals

- Give Linux users direct control over interactive region-selection behavior.
- Preserve `Automatic` as the safe default.
- Keep successful current behavior intact on systems that already work well.
- Make logs and diagnostics clear about preferred selector versus actual selector used.
- Reduce support friction by surfacing environment and selector availability in the UI.

## Non-Goals

- Replace all Linux capture and recording backend settings in one step.
- Expose raw portal backend names as end-user settings.
- Let users manually choose every provider in every fallback stage in v1.
- Remove `UseModernCapture` immediately.

---

## Proposed Data Model

Add a Linux-specific preference enum for interactive region selection.

### Proposed enum

```csharp
public enum LinuxInteractiveRegionSelectorPreference
{
    Automatic = 0,
    XerahSOverlay = 1,
    DesktopNative = 2,
    PortalDialog = 3,
    Slurp = 4
}
```

### Proposed settings field

Add to `TaskSettingsCapture`:

```csharp
public LinuxInteractiveRegionSelectorPreference LinuxRegionSelectorPreference { get; set; }
    = LinuxInteractiveRegionSelectorPreference.Automatic;
```

### Why `TaskSettingsCapture`

This keeps the setting close to existing capture behavior and preserves future per-workflow flexibility.

It also matches the fact that different workflows may want different behavior later, even if the first UI surfaces it as a global default.

### Compatibility plan

- Existing configs default to `Automatic`.
- `UseModernCapture` remains intact.
- The new preference is additive.

---

## Proposed Runtime Abstraction

The runtime should distinguish between:

- **preference**
- **capability**
- **actual provider used**

### New concepts

#### Preference

What the user asked for:

- automatic
- overlay
- desktop native
- portal
- slurp

#### Capability snapshot

What the current Linux session can support:

- session protocol
- desktop environment
- compositor
- portal backend summary
- available selector providers
- overlay availability

#### Resolution result

What XerahS decides to try first and what fallback policy applies.

Example:

```csharp
public sealed record LinuxInteractiveRegionSelectorResolution(
    LinuxInteractiveRegionSelectorPreference Preference,
    string PreferredProviderId,
    bool AllowFallback,
    IReadOnlyList<string> CandidateProviders,
    string Reason);
```

The exact type is flexible; the important part is separating user preference from provider selection.

---

## Automatic Behavior

`Automatic` should remain the default and should continue to use environment-aware logic.

### Recommended automatic strategy

#### Wayland

- Prefer desktop-native or compositor-native selector when clearly available
- Otherwise use portal
- Otherwise use `slurp` when appropriate
- Overlay is typically unavailable or undesirable on pure Wayland

#### X11

- Prefer desktop-native selector when reliable
- Prefer desktop-matched portal on capable modern X11 sessions
- Fall back to XerahS overlay crosshair when modern selection is unsupported or unreliable

The important rule is:

`Automatic` should keep prioritizing what is most likely to work well on the current environment, not what is newest in the abstract.

---

## User-Selected Behavior

The first implementation should treat the userΓÇÖs choice as a **preferred first provider**, not an absolute requirement.

### Recommended v1 behavior

- Try the selected provider first
- If unavailable or failing, fall back using safe automatic rules
- Log both the requested provider and the actual provider used

### Why soft preference first

This reduces the risk that a user selects a provider that looks right in the dropdown but fails in practice on a quirky system.

Strict mode can come later if needed.

### Possible future strict mode

Later, XerahS may support:

- `Automatic`
- `Prefer selected tool, allow fallback`
- `Use only selected tool`

Strict mode should not be part of v1.

---

## Placement

Add a Linux-only advanced capture section in settings.

Potential locations:

1. Application settings advanced capture section
2. Task settings capture section
3. Both, with application default and per-task override later

### Recommendation

Start with a Linux-only section in application settings and optionally mirror it into task settings later if real demand appears.

This keeps the first rollout simpler and avoids immediately creating two layers of precedence logic.

## Diagnostics block

The UI should show simple read-only diagnostics:

- `Current session`
- `Desktop`
- `Portal backend summary`
- `Available selectors`
- `Automatic will prefer`

This serves both support and user education.

### Example

```text
Current session: X11 / Cinnamon
Portal backend: xapp
Available selectors: XerahS overlay, Desktop native selector, Portal dialog
Automatic will prefer: Desktop native selector
```

---

## Implementation Plan

### Phase 1 ΓÇö Model and diagnostics

Add:

- `LinuxInteractiveRegionSelectorPreference` enum
- new capture setting
- Linux capability snapshot object for UI display
- logging for:
  - current environment
  - available selectors
  - chosen preference
  - actual selector used

Deliverables:

- config serialization
- view model plumbing
- runtime diagnostics object
- no behavior change yet except richer logging

### Phase 2 ΓÇö UI

Add a Linux-only settings card:

- selector dropdown
- current session text
- available selectors text
- automatic preference explanation

Deliverables:

- Linux-only UI visibility
- bindings and descriptions
- localized/clear labels

### Phase 3 ΓÇö Preference-aware selector resolution

Teach Linux region capture orchestration to:

- map user preference to preferred provider
- keep safe fallback behavior
- use existing capability detection where possible
- report preferred provider and final provider in logs

Deliverables:

- preference-based first-provider routing for screenshots
- preference-based first-provider routing for recording region selection
- no regression to existing automatic flows

### Phase 4 ΓÇö Harden and refine

Add:

- more detailed diagnostics
- CLI verification command output for selector environment
- better support text for why a provider is unavailable
- migration guidance for users currently toggling only `UseModernCapture`

### Phase 5 ΓÇö Optional future work

Potential extensions:

- per-workflow override
- strict mode
- provider-specific advanced view
- record-only selector preference if necessary
- manual portal-backend diagnostics page

---

## Provider Mapping Guidance

This table expresses the intended user-facing mapping.

| User-facing choice | Likely internal mapping |
|---|---|
| `Automatic` | Environment-aware resolver |
| `XerahS overlay crosshair` | Overlay selector + legacy-compatible rect capture |
| `Desktop native selector` | GNOME D-Bus, KDE D-Bus, or future desktop-native selector |
| `Portal dialog` | XDG Screenshot portal interactive flow |
| `slurp (wlroots)` | wlroots CLI selection path |

This mapping should stay internal. The user should not need to know provider IDs such as `gnome-dbus` or `portal`.

---

## Recording Scope

This preference should apply to:

- region screenshots
- region-based recording selection

It should **not** automatically become the setting for:

- full-screen screenshot backend
- window screenshot backend
- FFmpeg recording backend selection
- ScreenCast recording backend selection

Those are related but separate concerns.

This scope limit is important to avoid repeating the ambiguity currently associated with `UseModernCapture`.

---

## Relationship to `UseModernCapture`

`UseModernCapture` should remain during the transition.

### Short-term role

- keep current broad behavior toggles intact
- continue serving non-Linux and non-region logic
- remain the compatibility switch while the new selector preference is introduced

### Long-term direction

Once the interactive region selector preference is proven stable, XerahS can reconsider whether the Linux meaning of `UseModernCapture` should be:

- narrowed
- relabeled
- or eventually superseded for region-selection scenarios

This XIP does not require deciding that immediately.

---

## Risks

### Risk 1 ΓÇö UI complexity

Linux capture is already conceptually dense. Too many exposed options would confuse users.

Mitigation:

- keep the first UI compact
- hide unavailable options
- prefer friendly labels

### Risk 2 ΓÇö False confidence from detection

A provider may appear available but still misbehave at runtime.

Mitigation:

- soft preference, not strict requirement, in v1
- log preferred provider and actual provider
- retain automatic fallback

### Risk 3 ΓÇö Regressing currently working systems

Changing provider ordering can reintroduce old Linux failures.

Mitigation:

- keep `Automatic` behavior conservative
- isolate preference logic from existing fallback safety rules
- expand tests around capability and ordering

### Risk 4 ΓÇö Settings duplication

Application-level and task-level settings can create unclear precedence.

Mitigation:

- begin with one level only
- document precedence before adding a second level

---

## Testing Plan

### Unit tests

Add coverage for:

- capability snapshot generation
- option visibility
- preference-to-provider resolution
- fallback when chosen provider is unavailable
- automatic ordering per session type

### Manual test matrix

Test at minimum:

- GNOME Wayland
- KDE Wayland
- Sway or Hyprland
- KDE X11
- Cinnamon X11
- XFCE X11
- old or portal-flaky X11 environment

For each:

- screenshot region selection
- recording region selection
- automatic mode
- explicit provider preference
- failure fallback behavior

### Logging verification

Confirm logs show:

- session type
- desktop
- portal backend summary
- available selectors
- requested selector preference
- actual selector used
- fallback reason when applicable

---

## Open Questions

1. Should the first UI live in application settings only, or also in task settings?
2. Should `Desktop native selector` and `Portal dialog` be hidden when they resolve to the same practical experience on a given desktop?
3. Should X11 overlay always remain visible as an explicit option on any X11 system, even when modern providers are available?
4. Should recording region selection always reuse the screenshot selector preference, or eventually gain its own override?

---

## Recommendation

Proceed with:

1. a Linux-only interactive region selector preference
2. `Automatic` as default
3. soft-preference behavior with fallback
4. a small diagnostics block in settings
5. no immediate removal of `UseModernCapture`

This delivers meaningful user control without forcing users to understand portal backends, D-Bus APIs, or compositor internals.

---

## Summary

Linux capture behavior varies because it sits on top of several layers:

- session protocol
- desktop environment or window manager
- compositor
- portal backend
- selector provider

XerahS already contains logic for many of these distinctions internally. XIP0051 proposes bringing that sophistication to the user experience in a focused, understandable way by introducing a Linux-specific interactive region selector preference system backed by diagnostics and safe fallbacks.