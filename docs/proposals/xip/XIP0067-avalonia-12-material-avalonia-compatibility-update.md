# XIP0067 Avalonia 12 - Material.Avalonia Compatibility Update

**Status**: Complete
**Version**: v0.22.257

**Priority**: High
**Area**: UI Framework | Theming
**Related**: XIP0065 (core upgrade)

---

## Summary

The correct Avalonia 12 compatibility outcome for XerahS was not to keep chasing a dormant Material layer. The active Avalonia app no longer depends on `Material.Avalonia` or `Material.Icons.Avalonia`, and the desktop app was already on Fluent plus Lucide rather than Material theming.

This XIP is therefore complete as a scope-correction and dependency-cleanup task:

- prove whether Material is an active runtime dependency
- remove dead Material package references from the only live Avalonia consumer that still carried them
- confirm desktop Material validation is out of scope because no desktop Material theme layer exists

That is the cleanest Avalonia 12-ready state for the current repository.

---

## Findings

### Desktop Material scope was not real

The desktop app does not include an active `Material.Avalonia` theme layer. Desktop theming continues to use Avalonia Fluent resources and Lucide-based iconography. There was no legitimate desktop Material migration task to perform.

### Mobile-experimental carried dead Material package references

Before this implementation, `src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj` referenced `Material.Avalonia` and `Material.Icons.Avalonia`, but the active app/theme code in scope did not require them. Keeping those packages created a false compatibility obligation for Avalonia 12.

### The right fix was removal, not version churn

Because the active consumer did not need the packages, the repository no longer has to choose between Material 3.15.0 and 3.15.1, nor prove compatibility for a theme stack that is not actually running.

---

## Implemented Work

### 1. Central package management no longer carries dead Material entries

`Directory.Packages.props` no longer pins:

- `Material.Avalonia`
- `Material.Icons.Avalonia`

### 2. The mobile Avalonia app no longer references dead Material packages

`src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj` no longer references either Material package.

### 3. Active mobile forms were brought up to Avalonia 12 accessibility expectations directly

Instead of relying on a removed or unused Material layer, the touched mobile Avalonia views now expose the Avalonia 12 behaviors they actually need:

- landmarks on major surfaces
- automation names on key inputs
- polite live regions for status/progress
- focus recovery to the first validation failure after scroll

Those changes landed in:

- `MobileUploadView.axaml`
- `MobileSettingsView.axaml`
- `MobileAmazonS3ConfigView.axaml`
- `MobileAmazonS3ConfigView.axaml.cs`
- `MobileCustomUploaderConfigView.axaml`
- `MobileCustomUploaderConfigView.axaml.cs`

---

## Scope Decision

XIP0067 is closed with the following repository rule:

- desktop Material compatibility is out of scope until a desktop project actually adds a Material theme include or package reference
- future Material adoption must start with a fresh package-provenance review and an explicit decision to make Material part of the active runtime stack

For the current codebase, removing the dead dependency is the compatibility update.

---

## Actionable Task Ledger

| # | Task | Outcome | Commit |
|---|---|---|---|
| 1 | Prove whether desktop Material theming is active | Completed: no active desktop Material layer found | `c6ece84c` |
| 2 | Remove dead Material package references from central package management and the mobile Avalonia app | Completed | `c6ece84c` |
| 3 | Replace dependency-level assumptions with direct Avalonia 12 accessibility/form hardening on active mobile views | Completed | `c6ece84c` |

---

## Verification

- `dotnet build src/mobile-experimental/XerahS.Mobile.Ava/XerahS.Mobile.Ava.csproj -f net10.0-android -m:1` succeeds
- no active desktop app project references `Material.Avalonia` or `Material.Icons.Avalonia`
- desktop theming remains Fluent/Lucide-based and unaffected by the removal

---

## Reference

- Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," April 7, 2026: <https://avaloniaui.net/blog/avalonia-12/>
- Avalonia Docs, "Breaking changes in Avalonia 12": <https://docs.avaloniaui.net/docs/avalonia12-breaking-changes>
