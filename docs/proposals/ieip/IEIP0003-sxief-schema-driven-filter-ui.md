# IEIP0003: SXIEF Schema-Driven Filter UI

## Status
- Status: Drafted on March 21, 2026.
- Type: Architecture and extensibility proposal for `ShareX.ImageEditor`.
- Scope: `ShareX.ImageEditor` filter catalog, filter parameter UI, and effect browser/dialog registration.

## Summary
`ShareX.ImageEditor` does not need 50+ plugin projects to make filters scalable. The better fix is to keep filter algorithms compiled in the same assembly, but replace the current handwritten per-filter UI and registration model with a schema-driven in-proc definition system.

This proposal introduces the concept of `SXIEF` as a filter-definition contract:
- one compiled filter algorithm
- one typed descriptor/definition
- one generic settings UI that renders controls from metadata

In other words:
- keep `Core/ImageEffects/Filters/*.cs` for the actual image-processing code
- stop writing one bespoke Avalonia dialog for every filter unless the filter genuinely needs it
- stop manually duplicating filter registration in the effect browser and dialog registry

Optional future work can serialize these definitions or presets to disk, but external file loading is not the primary fix for the current scaling problem.

## Motivation
- The current problem is not that there are too many filter algorithms.
- The current problem is that adding a filter usually means touching too many presentation-layer files:
  - filter class
  - dialog AXAML
  - dialog code-behind
  - dialog registry
  - effect browser inventory
- This is a poor scaling model for filters whose UI is mostly a handful of sliders, toggles, dropdowns, and color pickers.
- The repository already has dozens of filters, so the cost of manual UI/registration is now the dominant maintenance issue.

## Current State

### 1. Filter count has outgrown handwritten UI
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/ImageEffects/Filters/` currently contains 72 `.cs` files.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/Dialogs/Filters/` currently contains 66 filter dialog code-behind files.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Views/Dialogs/EffectDialogRegistry.cs` manually maps filter IDs to dialog factories.
- `ShareX.ImageEditor/src/ShareX.ImageEditor/Presentation/Controls/EffectBrowserPanel.axaml.cs` manually builds the filter browser inventory.

### 2. Most filter dialogs are structurally repetitive
Repository scan of `Presentation/Views/Dialogs/Filters/*.axaml.cs` shows:
- 64 dialogs use sliders
- 11 dialogs use checkboxes
- 4 dialogs use combo boxes
- 4 dialogs use color pickers
- 2 dialogs use numeric up/down
- 0 filter dialogs require file pickers or canvas-interaction UI

Representative examples:
- [`BlurDialog.axaml.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Views\Dialogs\Filters\BlurDialog.axaml.cs)
- [`DitheringDialog.axaml.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Views\Dialogs\Filters\DitheringDialog.axaml.cs)
- [`PaperStencilMaskDialog.axaml.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Views\Dialogs\Filters\PaperStencilMaskDialog.axaml.cs)
- [`RisoPrintDialog.axaml.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Views\Dialogs\Filters\RisoPrintDialog.axaml.cs)

### 3. The filter classes are already close to schema-friendly
Many filter classes expose public settable properties that map cleanly to generic controls:
- numeric scalars such as `Radius`, `Strength`, `Threshold`, `Density`, `Angle`
- booleans such as `Invert`, `AutoResize`, `OutlineOnly`
- enums such as `DitheringMethod`, `DitheringPalette`, `PixelSortDirection`, `PixelSortMetric`
- colors such as `Color`, `StencilColor`, `InkColorA`, `InkColorB`

Examples:
- [`BlurImageEffect.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Core\ImageEffects\Filters\BlurImageEffect.cs)
- [`PixelSortingImageEffect.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Core\ImageEffects\Filters\PixelSortingImageEffect.cs)
- [`PaperStencilMaskImageEffect.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Core\ImageEffects\Filters\PaperStencilMaskImageEffect.cs)
- [`RisoPrintImageEffect.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Core\ImageEffects\Filters\RisoPrintImageEffect.cs)

### 4. The preview/apply lifecycle is already generic
- Filter dialogs implement `IEffectDialog`.
- They mostly do the same thing:
  - read control values
  - construct a filter instance
  - raise preview/apply with `Func<SKBitmap, SKBitmap>`
- `MainViewModel.EffectPreview.cs` and `EditorView.EffectsHost.cs` already provide a generic preview/apply pipeline.

## Goals
- Keep filter algorithms in one project and one assembly.
- Eliminate most handwritten per-filter dialogs.
- Eliminate duplicate manual filter registration across the browser and dialog registry.
- Make adding a new filter primarily a matter of:
  - writing the algorithm
  - declaring a schema/descriptor
- Preserve the current live preview/apply/cancel workflow.
- Support filter-specific settings UI without creating one project per filter.

## Non-Goals
- No one-project-per-filter architecture.
- No arbitrary external code/plugin loading for filters.
- No requirement that every filter UI be generated entirely by reflection.
- No forced rewrite of every non-filter effect category in phase 1.
- No removal of escape hatches for genuinely bespoke filter editors.

## Recommendation
Yes, this is doable.

The right model is:
- in-proc pluggable filter definitions
- shared generic parameter editor
- typed escape hatch for exceptional cases

The wrong model is:
- 50+ `csproj`
- 50+ runtime-loaded assemblies
- external file descriptors as the first and only abstraction

## Proposal

### 1. Introduce `SXIEF` as a filter-definition contract
Define a filter-definition model inside `ShareX.ImageEditor`, conceptually:

```csharp
public sealed class FilterDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string? IconKey { get; init; }
    public Func<ImageEffect> CreateEffect { get; init; } = default!;
    public IReadOnlyList<FilterParameterDefinition> Parameters { get; init; } = [];
    public string? CustomEditorKey { get; init; }
}
```

`SXIEF` in this proposal means the definition shape, not necessarily a disk file.

The primary runtime source should be compiled definitions in the same assembly.

### 2. Add a schema for filter parameters
Introduce `FilterParameterDefinition` entries for common UI primitives:
- slider
- numeric input
- checkbox
- enum dropdown
- color picker
- text input if needed

Example:

```csharp
new FilterParameterDefinition
{
    PropertyName = nameof(BlurImageEffect.Radius),
    Label = "Radius",
    Control = FilterParameterControl.Slider,
    Min = 1,
    Max = 100,
    DefaultValue = 5
}
```

This should be explicit metadata, not pure reflection heuristics.

### 3. Build a generic filter settings dialog
Add a single generic dialog or panel that:
- takes a `FilterDefinition`
- instantiates the filter
- renders controls from the parameter schema
- updates the filter instance as values change
- pushes preview/apply through the existing `IEffectDialog` flow

This replaces most of the current per-filter dialogs.

### 4. Replace manual filter registration with a filter catalog
Add an `ImageEffectCatalog` or `FilterCatalog` inside `ShareX.ImageEditor`.

Responsibilities:
- expose all filter definitions
- group them for the browser
- provide lookup by ID
- drive favorites and future preset binding

Then move filter inventory out of:
- [`EffectBrowserPanel.axaml.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Controls\EffectBrowserPanel.axaml.cs)
- [`EffectDialogRegistry.cs`](C:\Users\liveu\source\repos\ShareX Team\XerahS\ShareX.ImageEditor\src\ShareX.ImageEditor\Presentation\Views\Dialogs\EffectDialogRegistry.cs)

Those files should consume the catalog, not define the catalog.

### 5. Keep filter implementations in the same project
Do not split filters into one project per effect.

Recommended structure:
- `Core/ImageEffects/Filters/*.cs`
  - algorithm implementations
- `Presentation/Filters/Definitions/*.cs`
  - descriptor metadata
- `Presentation/Views/Dialogs/GenericFilterDialog.*`
  - generic schema-driven editor
- optional `Presentation/Filters/Editors/*.cs`
  - rare bespoke editor implementations

This keeps deployment and build complexity low while still making the UI pluggable.

### 6. Support bespoke UIs only where they are actually needed
Most filters do not need bespoke editors.

Based on the current dialog scan, the vast majority can be expressed by the generic schema.

Allow an escape hatch like:
- `CustomEditorKey`
- `CreateCustomEditor`
- or an interface such as `ICustomFilterEditorFactory`

Use it only for outliers, not the default path.

### 7. Treat external `.sxief` files as optional later work
If you still want `sxief` as a file format, add it later as a serialized descriptor/preset layer over the in-proc catalog.

That means:
- phase 1 solves maintainability inside `ShareX.ImageEditor`
- phase 2 can externalize descriptors or presets if needed

This sequencing matters because external files do not solve the current duplication problem unless the in-proc schema/generic editor already exists.

## Implementation Plan

### Phase 1: Catalog and descriptor model
- Add `FilterDefinition` and `FilterParameterDefinition`.
- Add a central filter catalog for the `Filters` category.
- Register a small pilot set:
  - blur
  - gaussian blur
  - glow
  - vignette
  - dithering
  - lens blur

### Phase 2: Generic filter dialog
- Implement one generic settings dialog/panel for schema-driven filter editors.
- Route preview/apply through the existing `IEffectDialog` lifecycle.
- Replace the registry entries for the pilot filters with the generic dialog path.

### Phase 3: Browser integration
- Make the filter browser read from the catalog rather than hardcoded item declarations.
- Keep existing IDs stable.

### Phase 4: Migrate the simple majority
- Port filters that use only:
  - sliders
  - checkboxes
  - combo boxes
  - color pickers
  - numeric inputs
- Delete the corresponding bespoke dialog files as they become redundant.

### Phase 5: Outlier handling
- Keep or add bespoke editors only for filters that cannot be represented cleanly by the schema.
- Require justification for each bespoke editor to avoid falling back into the current pattern.

### Optional Phase 6: `sxief` file format
- Add a serialized descriptor or preset format only after the in-proc schema is stable.
- Restrict it to allowlisted built-in filter definitions.

## Verification
- Build:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`
  - `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Automated tests:
  - filter catalog exposes pilot filters with stable IDs
  - generic dialog binds schema values to effect instances
  - slider, checkbox, enum, color, and numeric parameter types all round-trip correctly
  - preview updates when parameter values change
  - effect browser renders pilot filters from the catalog
- Manual smoke scenarios:
  - open pilot filters from the browser
  - verify preview/apply/cancel behavior
  - verify defaults match prior handwritten dialogs
  - verify migrated filters still produce the same visual output

## Risks
- Pure reflection-based property editors will be too loose; explicit schema metadata is safer.
- Some filters may still need custom UI, so the generic editor must support an escape hatch from day one.
- Migrating IDs or browser names carelessly will break favorites and user expectations.
- The first generic dialog must be visually clean, or contributors will revert to bespoke dialogs out of convenience.

## Open Questions
- Should filter descriptors live next to the filter classes or in a separate `Definitions/` folder?
- Should schema metadata be declared in code, attributes, or both?
- Should the generic dialog support collapsible advanced settings from day one?
- Do we want `sxief` as a serialized descriptor format, a preset format, or both?

## Recommendation
Proceed, but keep it in-proc first.

The best immediate architecture is:
- one `ShareX.ImageEditor` project
- one filter catalog
- one generic schema-driven filter editor
- rare bespoke editors only when justified

That solves the real scaling problem without turning every filter into a separate plugin/package/deployment unit.
