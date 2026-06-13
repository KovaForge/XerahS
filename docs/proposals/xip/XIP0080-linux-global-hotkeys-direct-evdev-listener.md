# XIP0080: Linux Global Hotkeys via Direct evdev Input Device Listening

**Status:** Draft  
**Authors:** McoreD, Aoife Brennan  
**Date:** 2026-06-14

## Summary

Replace the XDG Global Shortcuts Portal implementation for Linux global hotkeys with a direct evdev-based input device listener, modeled on the working approach in CrossMacro.

## Motivation

The current portal-based hotkey system is unreliable on Wayland (particularly GNOME). Known issues include:

- App ID mismatch causing instant rejection
- Race condition (binding attempted before window handle exists)
- Wrapper script vs symlink verification failures
- Silent retry crashes
- Incorrect Avalonia key mapping for PrintScreen

These make core capture hotkeys non-functional for many Linux users.

## Proposed Solution

Implement direct input device listening using evdev, following the `LinuxInputCapture` / `EvdevReader` pattern from CrossMacro.

### Key Elements

- Open `/dev/input/event*` devices directly and read raw `input_event` structs in a background loop
- Support daemon-backed mode (preferred) and direct device mode (fallback)
- Provide proper keycode mapping (fixes PrintScreen issue)
- Add `xerahs doctor` diagnostics for input permissions
- Ship udev rules + polkit policies in packaging

## Implementation Plan

1. Adapt `EvdevReader` and `LinuxInputCapture` from the CrossMacro reference
2. Create `LinuxEvdevGlobalHotkeyProvider` behind an abstraction
3. Wire into existing hotkey service with portal as legacy fallback
4. Update Linux packaging (deb, rpm, AppImage, Flatpak) with required rules
5. Deprecate XDG Portal path on Linux once stable

## Benefits

- Removes all current portal failure modes
- Reliable hotkeys on all major Wayland compositors
- No dependency on window handles or exact `.desktop` naming
- Proven pattern already running in production Avalonia software
- Foundation for future low-level input features

## Risks

- Permission model complexity → mitigated by diagnostics + automated setup
- Sandboxed distribution support → handled via daemon socket + polkit paths

## References

- CrossMacro (cloned at `Projects/KovaForge/CrossMacro`)
  - `src/CrossMacro.Platform.Linux/LinuxInputCapture.cs`
  - `src/CrossMacro.Infrastructure/Linux/Native/Evdev/EvdevReader.cs`
  - `docs/linux.md`
