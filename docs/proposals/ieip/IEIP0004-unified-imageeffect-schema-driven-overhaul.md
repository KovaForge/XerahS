# IEIP0004: Unified ImageEffect Schema-Driven Overhaul

## Status
- Status: Drafted on March 23, 2026.
- Type: Architecture proposal for `ShareX.ImageEditor`.
- Scope: Extending the IEIP0003 schema-driven contract to the remaining three `ImageEffectCategory` categories — Adjustments, Manipulations, and Drawings.
- Builds on: IEIP0003 (Filters) — the existing `FilterCatalog` / `FilterDefinition` / `SchemaDrivenFilterDialog` infrastructure is preserved and used as the reference pattern.

## Summary
IEIP0003 established a schema-driven definition contract for **Filters** — a `FilterCatalog` with `FilterDefinition` entries, a `SchemaDrivenFilterDialog` that renders controls from parameter metadata, and catalog-driven browser and dialog registration. This proposal extends the same contract pattern to the remaining three categories — **Adjustments**, **Manipulations**, and **Drawings** — so that all ImageEffects work under similar contracts.

The existing Filter implementation is not modified. Instead, three new parallel catalogs are introduced following the same architecture.

## Motivation

| Category | Effect classes | Bespoke dialog code-behinds | Schema-driven |
|---|---|---|---|
| Filters | 85 | ~5 manual + catalog | 66+ via `FilterCatalog` ✅ |
| Adjustments | 27 | 23 | 0 ❌ |
| Manipulations | 13 | 14 | 0 ❌ |
| Drawings | 12 | 11 | 0 ❌ |

The Filters category has proven the schema pattern works at scale. The remaining ~48 bespoke dialogs in Adjustments, Manipulations, and Drawings still follow the old manual pattern: handwritten AXAML dialogs, manual `EffectDialogRegistry._factories` entries, and manual `EffectBrowserPanel.InitializeEffects()` registration.

## Current Architecture (Post-IEIP0003)

The IEIP0003 infrastructure that we are extending:

| Component | File(s) | Role |
|---|---|---|
| `FilterDefinition` | `Presentation/Filters/FilterDefinition.cs` | Typed descriptor: ID, name, icon, params, factory |
| `FilterParameterDefinition` | `Presentation/Filters/` | Parameter types: slider, checkbox, enum, color, numeric, text |
| `FilterCatalog` | `Presentation/Filters/FilterCatalog.cs` + partials | Central registry with helper builders |
| `SchemaDrivenFilterDialog` | `Presentation/Views/Dialogs/Filters/` | Generic dialog that renders controls from parameter schema |
| `EffectDialogRegistry` | `Presentation/Views/Dialogs/` | Checks `FilterCatalog` first, falls back to `_factories` dict |
| `EffectBrowserPanel` | `Presentation/Controls/` | Calls `AddCatalogDrivenFilters()` for Filters; manual for other categories |

## Goals
- Create analogous catalogs for Adjustments, Manipulations, and Drawings following the Filter contract pattern.
- Eliminate the remaining ~48 bespoke dialog files where possible.
- Eliminate manual browser registration for all four categories.
- Eliminate manual factory entries in `EffectDialogRegistry._factories`.
- Preserve all existing effect IDs, favorites, recent lists, and aliases.
- Preserve the existing Filter infrastructure unchanged.
- Preserve the current live preview/apply/cancel workflow.

## Non-Goals
- No modifications to the existing `FilterCatalog`, `FilterDefinition`, or `SchemaDrivenFilterDialog` types.
- No changes to the `ImageEffect` base class or `ImageEffectCategory` enum.
- No changes to core algorithm implementations.
- No external plugin/assembly loading.

## Proposal

### 1. Create a shared `EffectDefinition` base

Extract the common shape shared across all categories into a base type that the existing `FilterDefinition` and the new category-specific definitions can align to:

```csharp
public class EffectDefinition
{
    public string Id { get; }
    public string Name { get; }
    public string BrowserLabel { get; }
    public string Icon { get; }
    public string Description { get; }
    public ImageEffectCategory Category { get; }
    public Func<ImageEffect> CreateEffect { get; }
    public IReadOnlyList<EffectParameterDefinition> Parameters { get; }
    public string? CustomEditorKey { get; }
}
```

> [!NOTE]
> The existing `FilterDefinition` uses `FilterParameterDefinition` and `FilterParameterState` types. The new categories should reuse the same parameter definition type hierarchy (slider, checkbox, enum, color, numeric, text) since these controls are category-agnostic. Whether to achieve this through inheritance, composition, or a shared base interface (`IEffectParameterDefinition`) is an implementation detail — the key requirement is that the parameter schema types are shared, not duplicated per category.

### 2. Create per-category catalogs

Following the `FilterCatalog` pattern, create:

```
Presentation/
├── Filters/                                    # ← UNCHANGED
│   ├── FilterCatalog.cs
│   ├── FilterCatalog.Definitions.cs
│   ├── FilterCatalog.Metadata.cs
│   └── FilterDefinition.cs
│
├── Adjustments/                                # ← NEW
│   ├── AdjustmentCatalog.cs
│   ├── AdjustmentCatalog.Definitions.cs
│   └── AdjustmentCatalog.Metadata.cs
│
├── Manipulations/                              # ← NEW
│   ├── ManipulationCatalog.cs
│   ├── ManipulationCatalog.Definitions.cs
│   └── ManipulationCatalog.Metadata.cs
│
└── Drawings/                                   # ← NEW
    ├── DrawingCatalog.cs
    ├── DrawingCatalog.Definitions.cs
    └── DrawingCatalog.Metadata.cs
```

Each catalog follows the same pattern as `FilterCatalog`:
- A static partial class with `BuildDefinitions()` and `BuildPresentationMetadata()`.
- Helper methods for typed parameter builders (`IntSlider<T>`, `FloatSlider<T>`, `BoolParameter<T>`, etc.) — likely shared from a common utility or base class.
- Definitions list and lookup by ID.

### 3. Generalize the generic dialog

Create a `SchemaDrivenEffectDialog` (or extend the existing `SchemaDrivenFilterDialog` to accept any `EffectDefinition`-shaped input) so that Adjustments, Manipulations, and Drawings can use the same generic parameter-rendering UI.

If the existing `SchemaDrivenFilterDialog` is already generic enough in its rendering logic, the simplest path is to make it accept the shared base type rather than `FilterDefinition` specifically.

### 4. Migrate Adjustments to catalog

The 27 Adjustment effects mostly expose simple numeric properties:

| Effect | Parameters | Controls |
|---|---|---|
| `BrightnessImageEffect` | Amount | Slider |
| `ContrastImageEffect` | Amount | Slider |
| `HueImageEffect` | Amount | Slider |
| `SaturationImageEffect` | Amount | Slider |
| `GammaImageEffect` | Amount | Slider |
| `AlphaImageEffect` | Amount | Slider |
| `ExposureImageEffect` | Amount | Slider |
| `ThresholdImageEffect` | Threshold | Slider |
| `PosterizeImageEffect` | Levels | Slider |
| `SolarizeImageEffect` | Threshold | Slider |
| `VibranceImageEffect` | Amount | Slider |
| `GrayscaleImageEffect` | Method | Enum |
| `SepiaImageEffect` | Intensity | Slider |
| `ColorizeImageEffect` | Hue, Saturation, Lightness | 3× Slider |
| `SelectiveColorImageEffect` | Channel + adjustments | Enum + Sliders |
| `ReplaceColorImageEffect` | Source, Target, Tolerance | 2× Color + Slider |
| `DuotoneGradientMapImageEffect` | Colors + Midpoint + Contrast | 2× Color + 2× Slider |
| `ShadowsHighlightsImageEffect` | Shadows, Highlights | 2× Slider |
| `TemperatureTintImageEffect` | Temperature, Tint | 2× Slider |
| `LevelsImageEffect` | InputBlack/White, Gamma, OutputBlack/White | 5× Slider |
| `ColorMatrixImageEffect` | 5×4 matrix | 20× Numeric |
| `FilmEmulationImageEffect` | Preset, Grain, Fade, Vignette | Enum + 3× Slider |
| `AutoContrastImageEffect` | ClipPercent, PreserveColor | Slider + Checkbox |
| `InvertImageEffect` | *(none)* | Parameterless |
| `BlackAndWhiteImageEffect` | *(none)* | Parameterless |
| `PolaroidImageEffect` | *(none)* | Parameterless |

**Parameterless effects** (Invert, Black & White, Polaroid) are currently applied instantly via direct events (`Raise(InvertRequested)`). They should be registered in the catalog with empty parameter lists. Whether they continue to apply instantly or gain a minimal dialog is an open question (see Open Questions below).

### 5. Migrate Manipulations to catalog

| Effect | Parameters | Controls | Schema-eligible? |
|---|---|---|---|
| `SkewImageEffect` | Horiz, Vert, AutoResize | 2× Slider + Checkbox | ✅ |
| `PinchBulgeImageEffect` | Amount, Radius, CenterX/Y | 4× Slider | ✅ |
| `TwirlImageEffect` | Angle, Radius, CenterX/Y | 4× Slider | ✅ |
| `RoundedCornersImageEffect` | Radius | Slider | ✅ |
| `ScaleImageEffect` | ScaleX, ScaleY | 2× Slider | ✅ |
| `FlipImageEffect` | Direction | Enum | ✅ |
| `RotateImageEffect` | Angle | Slider | ✅ |
| `Rotate3DImageEffect` | RotateX/Y/Z, etc. | Multiple Sliders | ✅ |
| `Rotate3DBoxImageEffect` | Multiple params | Multiple Sliders | ✅ |
| `AutoCropImageEffect` | Threshold | Slider | ✅ |
| `PerspectiveWarpImageEffect` | 4 corner points | *(bespoke — canvas)* | ❌ |
| `DisplacementMapImageEffect` | Scale + Filename | *(bespoke — file picker)* | ❌ |
| `ResizeImageEffect` | Width, Height, Maintain | *(bespoke — linked dims)* | ❌ |

> [!NOTE]
> `PerspectiveWarpImageEffect`, `DisplacementMapImageEffect`, and resize/crop dialogs require spatial canvas interaction, file pickers, or linked-dimension logic. These remain bespoke editors via `CustomEditorKey`.

### 6. Migrate Drawings to catalog

| Effect | Parameters | Schema-eligible? |
|---|---|---|
| `DrawBackgroundEffect` | Color | ✅ (Color picker) |
| `DrawCheckerboardEffect` | CellSize, Color1, Color2 | ✅ (Slider + 2× Color) |
| `WoodenFrameImageEffect` | Frame params | ✅ (Multiple Sliders) |
| `DrawBackgroundImageEffect` | Filename, Placement, Opacity | ❌ (file picker) |
| `DrawImageEffect` | Filename, Placement, Size, Opacity | ❌ (file picker + canvas) |
| `DrawLineEffect` | Start/End points, Color, Thickness | ❌ (spatial) |
| `DrawParticlesEffect` | FolderPath, Count, etc. | ❌ (folder picker) |
| `DrawShapeEffect` | Shape, Position, Size, Color | ❌ (spatial) |
| `DrawTextEffect` | Text, Font, Size, Color, Position | ❌ (text + font + spatial) |
| `TextWatermarkEffect` | Text, Font, Size, Colors | ❌ (complex) |

> [!IMPORTANT]
> Drawings have the highest ratio of bespoke-to-schema effects. Most require file/folder/font pickers or canvas-based spatial placement. All Drawing effects should still be registered in the catalog (for browser inventory and ID stability), but only 3 will use the generic dialog. The rest use `CustomEditorKey` escape hatches pointing to their existing bespoke editors.

### 7. Update `EffectDialogRegistry` and `EffectBrowserPanel`

Update `EffectDialogRegistry.TryCreate` to check all four catalogs:

```csharp
if (FilterCatalog.TryGetDefinition(effectId, out var filterDef))     { ... }
if (AdjustmentCatalog.TryGetDefinition(effectId, out var adjDef))    { ... }
if (ManipulationCatalog.TryGetDefinition(effectId, out var manipDef)){ ... }
if (DrawingCatalog.TryGetDefinition(effectId, out var drawDef))      { ... }
```

Update `EffectBrowserPanel.InitializeEffects()` to call `AddCatalogDrivenEffects()` per category, mirroring the existing `AddCatalogDrivenFilters()` pattern. Remove the manual `AddEffect` calls and direct event handlers that are replaced by catalog entries.

## Implementation Plan

### Phase 1: Shared parameter infrastructure
- Extract shared parameter definition types if they are currently tightly coupled to `FilterDefinition`. If the existing `FilterParameterDefinition` hierarchy is already usable by other categories, simply reference it.
- Create a shared `EffectDefinition` base or interface that new catalogs can implement alongside the existing `FilterDefinition`.

### Phase 2: Adjustment catalog + migration
- Create `AdjustmentCatalog` with definitions for all 27 Adjustment effects.
- Route eligible effects through the generic dialog.
- Remove bespoke Adjustment dialog files (23 files).
- Remove Adjustment entries from `EffectDialogRegistry._factories`.
- Update `EffectBrowserPanel` to read Adjustments from catalog.

### Phase 3: Manipulation catalog + migration
- Create `ManipulationCatalog` with definitions for all 13 Manipulation effects.
- Mark bespoke-required effects (`PerspectiveWarp`, `DisplacementMap`, resize/crop) with `CustomEditorKey`.
- Remove bespoke dialogs for schema-eligible effects (~9 files).
- Update `EffectBrowserPanel` and `EffectDialogRegistry`.

### Phase 4: Drawing catalog + migration
- Create `DrawingCatalog` with definitions for all 12 Drawing effects.
- Schema-drive the 3 eligible effects; mark the rest with `CustomEditorKey`.
- Remove bespoke dialogs for schema-eligible effects (~3 files).
- Update `EffectBrowserPanel` and `EffectDialogRegistry`.

### Phase 5: Browser consolidation + cleanup
- Rewrite `EffectBrowserPanel.InitializeEffects()` to use `AddCatalogDrivenEffects()` per category.
- Remove `Raise(*)` events for parameterless effects that move to catalog (if applicable).
- Delete emptied bespoke dialog `.axaml` and `.axaml.cs` files.
- Remove unused `using` directives and event declarations from `EffectBrowserPanel.axaml.cs`.

## Verification

- Build:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`
  - `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Automated tests:
  - Each new catalog exposes definitions with stable IDs matching existing browser/registry IDs.
  - Generic dialog binds parameter values to effect instances for all parameter types.
  - Parameterless effects render correctly (apply-only or with minimal dialog).
  - Preview updates when parameter values change.
  - Browser renders all categories from their respective catalogs.
- Manual smoke:
  - Open effects from each category in the browser and verify preview/apply/cancel.
  - Verify bespoke editors (PerspectiveWarp, DrawText, etc.) still function.
  - Verify favorites, recent lists, and search all work with catalog-driven IDs.

## Risks
- **Drawings bespoke ratio**: ~9 of 12 Drawing effects need bespoke editors. The catalog still provides value for browser registration and ID management, but fewer dialog files are eliminated.
- **Parameterless effects UX**: Moving Invert/BlackAndWhite/Polaroid from instant-apply events to catalog entries may change UX unless an `ApplyImmediately` flag is added.
- **Shared parameter types**: If `FilterParameterDefinition` is tightly coupled to `FilterDefinition`, decoupling requires refactoring. If it is already generic, this is a non-issue.

## Open Questions
1. Should parameterless effects open a minimal dialog or apply instantly? If instant, add an `ApplyImmediately` flag on the definition.
2. Should the parameter definition types be shared as-is or extracted into a common base to avoid cross-referencing `Presentation/Filters/` from other catalog namespaces?
3. Should a `FilePathEffectParameterDefinition` be added to unlock more Drawings/Manipulations, or deferred?
