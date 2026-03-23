# IEIP0004: Unified ImageEffect Schema-Driven Overhaul

## Status
- Status: Implemented on March 23, 2026.
- Type: Architecture proposal for `ShareX.ImageEditor`.
- Scope: All four `ImageEffectCategory` categories — Adjustments, Manipulations, Drawings, and Filters — under a single consistent contract.
- Builds on: IEIP0003 established the schema-driven pattern for Filters. This proposal promotes that pattern into shared infrastructure used by all four categories equally.

## Summary
IEIP0003 introduced a schema-driven definition contract for Filters. This proposal promotes that contract into shared, category-agnostic infrastructure so that **all four** ImageEffect categories — Adjustments, Manipulations, Drawings, and Filters — use the same definition types, the same parameter schema, and the same generic dialog.

Filters do **not** remain a solo implementation. The existing `FilterCatalog` / `FilterDefinition` / `SchemaDrivenFilterDialog` types are refactored into shared types that all categories consume equally. Each category gets its own catalog partial, but they all build on the same foundation.

## Motivation

| Category | Effect classes | Bespoke dialog files | Schema-driven |
|---|---|---|---|
| Filters | 85 | ~5 manual + catalog | 66+ via `FilterCatalog` ✅ |
| Adjustments | 27 | 23 | 0 ❌ |
| Manipulations | 13 | 14 | 0 ❌ |
| Drawings | 12 | 11 | 0 ❌ |

The Filters category proved the schema pattern works. The remaining ~48 bespoke dialogs follow the old manual pattern. This proposal eliminates the divergence by lifting the Filter infrastructure into shared types and extending it to all categories.

## Proposal

### 1. Extract shared parameter and definition types

Move the existing parameter definition types out of `Presentation/Filters/` into a shared `Presentation/Effects/` namespace. All four categories reference the same types:

| Current (Filters-only) | New (shared) |
|---|---|
| `FilterParameterDefinition` | `EffectParameterDefinition` |
| `SliderFilterParameterDefinition` | `SliderParameterDefinition` |
| `CheckboxFilterParameterDefinition` | `CheckboxParameterDefinition` |
| `EnumFilterParameterDefinition` | `EnumParameterDefinition` |
| `ColorFilterParameterDefinition` | `ColorParameterDefinition` |
| `NumericFilterParameterDefinition` | `NumericParameterDefinition` |
| `TextFilterParameterDefinition` | `TextParameterDefinition` |
| `FilterParameterState` | `EffectParameterState` |
| `FilterOptionDefinition` | `EffectOptionDefinition` |

Add a new shared parameter type:

| New type | Purpose |
|---|---|
| `FilePathParameterDefinition` | File picker (text field + browse button) |

The `FilePathParameterDefinition` renders a text input with a browse button. It stores a file path string and supports optional file-type filters (e.g., `"Image files|*.png;*.jpg;*.bmp"`). This unlocks `DrawBackgroundImageEffect`, `DrawImageEffect`, and `DisplacementMapImageEffect` for the generic dialog.

### 2. Create shared `EffectDefinition`

A common definition type used by all four categories:

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
    public bool ApplyImmediately { get; }
}
```

The `ApplyImmediately` flag controls parameterless effects:
- When `true`, the browser invokes the effect directly without opening a dialog.
- Used for Invert, Black & White, Polaroid, Edge Detect, Emboss, Mean Removal, Smooth.
- These effects are still registered in the catalog for consistent ID management, favorites, and search.

The existing `FilterDefinition` is replaced by `EffectDefinition`. Since Filters are the only category that previously had a definition type, this is a clean rename + extension (adding `Category`, `CustomEditorKey`, `ApplyImmediately`).

### 3. Rename the generic dialog

| Current | New |
|---|---|
| `SchemaDrivenFilterDialog` | `SchemaDrivenEffectDialog` |

Accepts `EffectDefinition` (the shared type) so it serves all four categories identically.

### 4. Create a unified `ImageEffectCatalog`

A single `ImageEffectCatalog` (partial class) replaces the existing `FilterCatalog` with per-category definition files:

```
Presentation/Effects/
├── EffectDefinition.cs                          # Shared definition type
├── EffectParameterDefinition.cs                 # Shared parameter types (8 types)
├── ImageEffectCatalog.cs                        # Core: lookup, helpers, parameter builders
├── ImageEffectCatalog.Adjustments.cs            # 27 Adjustment definitions
├── ImageEffectCatalog.Drawings.cs               # 12 Drawing definitions
├── ImageEffectCatalog.Filters.cs                # 85 Filter definitions (migrated from FilterCatalog.Definitions.cs)
├── ImageEffectCatalog.Manipulations.cs          # 13 Manipulation definitions
└── ImageEffectCatalog.Metadata.cs               # Merged browser labels, icons, descriptions for all categories
```

The `ImageEffectCatalog.cs` core file contains:
- `Definitions` property (all definitions across all categories).
- `GetByCategory(ImageEffectCategory)` method.
- `TryGetDefinition(string id, out EffectDefinition?)` method.
- Shared helper builders: `IntSlider<T>`, `FloatSlider<T>`, `BoolParameter<T>`, `EnumParameter<T>`, `ColorParameter<T>`, `IntNumeric<T>`, `DoubleNumeric<T>`, `TextParameter<T>`, `FilePathParameter<T>`.

The existing `FilterCatalog.Definitions.cs` content moves into `ImageEffectCatalog.Filters.cs`. The existing `FilterCatalog.Metadata.cs` merges into `ImageEffectCatalog.Metadata.cs`. The `Presentation/Filters/` directory is removed.

### 5. Adjustment definitions

All 27 Adjustments are schema-eligible:

| Effect | Controls |
|---|---|
| `BrightnessImageEffect` | Slider |
| `ContrastImageEffect` | Slider |
| `HueImageEffect` | Slider |
| `SaturationImageEffect` | Slider |
| `GammaImageEffect` | Slider |
| `AlphaImageEffect` | Slider |
| `ExposureImageEffect` | Slider |
| `ThresholdImageEffect` | Slider |
| `PosterizeImageEffect` | Slider |
| `SolarizeImageEffect` | Slider |
| `VibranceImageEffect` | Slider |
| `SepiaImageEffect` | Slider |
| `GrayscaleImageEffect` | Enum |
| `ColorizeImageEffect` | 3× Slider |
| `ShadowsHighlightsImageEffect` | 2× Slider |
| `TemperatureTintImageEffect` | 2× Slider |
| `LevelsImageEffect` | 5× Slider |
| `SelectiveColorImageEffect` | Enum + Sliders |
| `ReplaceColorImageEffect` | 2× Color + Slider |
| `DuotoneGradientMapImageEffect` | 2× Color + 2× Slider |
| `ColorMatrixImageEffect` | 20× Numeric |
| `FilmEmulationImageEffect` | Enum + 3× Slider |
| `AutoContrastImageEffect` | Slider + Checkbox |
| `InvertImageEffect` | Parameterless — `ApplyImmediately = true` |
| `BlackAndWhiteImageEffect` | Parameterless — `ApplyImmediately = true` |
| `PolaroidImageEffect` | Parameterless — `ApplyImmediately = true` |

**23 bespoke Adjustment dialog files deleted.**

### 6. Manipulation definitions

| Effect | Controls | Schema? |
|---|---|---|
| `SkewImageEffect` | 2× Slider + Checkbox | ✅ |
| `PinchBulgeImageEffect` | 4× Slider | ✅ |
| `TwirlImageEffect` | 4× Slider | ✅ |
| `RoundedCornersImageEffect` | Slider | ✅ |
| `ScaleImageEffect` | 2× Slider | ✅ |
| `FlipImageEffect` | Enum | ✅ |
| `RotateImageEffect` | Slider | ✅ |
| `Rotate3DImageEffect` | Multiple Sliders | ✅ |
| `Rotate3DBoxImageEffect` | Multiple Sliders | ✅ |
| `AutoCropImageEffect` | Slider | ✅ |
| `DisplacementMapImageEffect` | Slider + FilePath | ✅ (with `FilePathParameterDefinition`) |
| `PerspectiveWarpImageEffect` | 4 corner points | ❌ bespoke (canvas interaction) |
| `ResizeImageEffect` | Width, Height, Maintain | ❌ bespoke (linked dimensions) |

**~10 bespoke Manipulation dialog files deleted.** 3 remain bespoke via `CustomEditorKey`.

### 7. Drawing definitions

| Effect | Controls | Schema? |
|---|---|---|
| `DrawBackgroundEffect` | Color | ✅ |
| `DrawCheckerboardEffect` | Slider + 2× Color | ✅ |
| `WoodenFrameImageEffect` | Multiple Sliders | ✅ |
| `DrawBackgroundImageEffect` | FilePath + Enum + Slider | ✅ (with `FilePathParameterDefinition`) |
| `DrawImageEffect` | FilePath + Enum + Slider | ✅ (with `FilePathParameterDefinition`) |
| `DrawLineEffect` | Start/End, Color, Thickness | ❌ bespoke (spatial) |
| `DrawParticlesEffect` | FolderPath, Count | ❌ bespoke (folder picker) |
| `DrawShapeEffect` | Shape, Position, Size, Color | ❌ bespoke (spatial) |
| `DrawTextEffect` | Text, Font, Size, Color, Position | ❌ bespoke (text + font + spatial) |
| `TextWatermarkEffect` | Text, Font, Size, Colors | ❌ bespoke (complex) |

**~5 bespoke Drawing dialog files deleted.** 5 remain bespoke via `CustomEditorKey`.

### 8. Update `EffectDialogRegistry`

Simplified to a single catalog lookup:

```csharp
public static bool TryCreate(string effectId, out UserControl? dialog)
{
    if (!ImageEffectCatalog.TryGetDefinition(effectId, out var definition) || definition == null)
    {
        dialog = null;
        return false;
    }

    if (!string.IsNullOrEmpty(definition.CustomEditorKey))
    {
        dialog = CreateBespokeEditor(definition.CustomEditorKey);
        return dialog != null;
    }

    dialog = new SchemaDrivenEffectDialog(definition);
    return true;
}
```

The `_factories` dictionary is eliminated entirely. Bespoke editors are resolved through `CustomEditorKey` → a small switch/dictionary in `CreateBespokeEditor()`.

### 9. Update `EffectBrowserPanel`

Replace `InitializeEffects()` with catalog-driven construction:

```csharp
private void InitializeEffects()
{
    Categories.Add(_recentCategory);
    Categories.Add(_favoritesCategory);

    foreach (ImageEffectCategory categoryEnum in Enum.GetValues<ImageEffectCategory>())
    {
        var category = new EffectCategory(categoryEnum.ToString());

        foreach (var definition in ImageEffectCatalog.GetByCategory(categoryEnum))
        {
            if (definition.ApplyImmediately)
            {
                category.AddEffect(definition.BrowserLabel, definition.Icon,
                    definition.Description, () => ApplyImmediate(definition), definition.Id);
            }
            else
            {
                category.AddEffect(definition.BrowserLabel, definition.Icon,
                    definition.Description, () => RaiseDialog(definition.Id), definition.Id);
            }
        }

        Categories.Add(category);
    }
}
```

All 7 `Raise(*)` event handlers (InvertRequested, etc.) and the `AddCatalogDrivenFilters()` method are removed. Every effect in every category is now registered identically.

## Target File Layout (Post-IEIP0004)

```
Presentation/
├── Effects/                                    # ← NEW shared infrastructure
│   ├── EffectDefinition.cs
│   ├── EffectParameterDefinition.cs            # 8 parameter types (incl. FilePath)
│   ├── ImageEffectCatalog.cs                   # Core + helpers
│   ├── ImageEffectCatalog.Adjustments.cs
│   ├── ImageEffectCatalog.Drawings.cs
│   ├── ImageEffectCatalog.Filters.cs           # Migrated from FilterCatalog.Definitions.cs
│   ├── ImageEffectCatalog.Manipulations.cs
│   └── ImageEffectCatalog.Metadata.cs          # All categories
├── Controls/
│   └── EffectBrowserPanel.axaml.cs             # Catalog-driven, no manual entries
└── Views/Dialogs/
    ├── EffectDialogRegistry.cs                 # Catalog-only lookup
    ├── SchemaDrivenEffectDialog.axaml[.cs]     # Generic dialog for all categories
    └── Bespoke/                                # ~8 remaining bespoke editors
        ├── PerspectiveWarpDialog.axaml[.cs]
        ├── ResizeImageDialog.axaml[.cs]
        ├── ResizeCanvasDialog.axaml[.cs]
        ├── CropImageDialog.axaml[.cs]
        ├── DrawLineDialog.axaml[.cs]
        ├── DrawParticlesDialog.axaml[.cs]
        ├── DrawShapeDialog.axaml[.cs]
        ├── DrawTextDialog.axaml[.cs]
        └── TextWatermarkDialog.axaml[.cs]
```

The `Presentation/Filters/` directory is removed entirely.

## Implementation Plan

### Phase 1: Shared infrastructure
- Create `Presentation/Effects/` directory.
- Extract and rename parameter types from `FilterParameterDefinition` → `EffectParameterDefinition` hierarchy.
- Add `FilePathParameterDefinition`.
- Create `EffectDefinition` (with `Category`, `CustomEditorKey`, `ApplyImmediately`).
- Create `ImageEffectCatalog.cs` with core lookup + shared helper builders.
- Migrate `FilterCatalog.Definitions.cs` → `ImageEffectCatalog.Filters.cs`.
- Migrate `FilterCatalog.Metadata.cs` → `ImageEffectCatalog.Metadata.cs` (Filter entries).
- Rename `SchemaDrivenFilterDialog` → `SchemaDrivenEffectDialog`, accept `EffectDefinition`.
- Add `FilePathParameterDefinition` rendering to the generic dialog.
- Remove `Presentation/Filters/` directory.
- Build to verify no regressions.

### Phase 2: Adjustment catalog + migration
- Add `ImageEffectCatalog.Adjustments.cs` with all 27 definitions.
- Add Adjustment metadata to `ImageEffectCatalog.Metadata.cs`.
- Delete 23 bespoke Adjustment dialog files.
- Remove Adjustment entries from `EffectDialogRegistry._factories`.
- Build + verify.

### Phase 3: Manipulation catalog + migration
- Add `ImageEffectCatalog.Manipulations.cs` with all 13 definitions.
- Mark PerspectiveWarp, Resize, ResizeCanvas, Crop with `CustomEditorKey`.
- Add Manipulation metadata to `ImageEffectCatalog.Metadata.cs`.
- Delete ~10 bespoke Manipulation dialog files (keep bespoke editors).
- Remove Manipulation entries from `EffectDialogRegistry._factories`.
- Build + verify.

### Phase 4: Drawing catalog + migration
- Add `ImageEffectCatalog.Drawings.cs` with all 12 definitions.
- Mark DrawLine, DrawParticles, DrawShape, DrawText, TextWatermark with `CustomEditorKey`.
- Add Drawing metadata to `ImageEffectCatalog.Metadata.cs`.
- Delete ~5 bespoke Drawing dialog files (keep bespoke editors).
- Remove Drawing entries from `EffectDialogRegistry._factories`.
- Build + verify.

### Phase 5: Browser + registry consolidation
- Rewrite `EffectBrowserPanel.InitializeEffects()` to catalog-driven loop.
- Add `ApplyImmediate()` method for parameterless effects.
- Remove all `Raise(*)` events and handlers.
- Remove `AddCatalogDrivenFilters()`.
- Simplify `EffectDialogRegistry` to catalog-only lookup.
- Move remaining bespoke dialogs to `Views/Dialogs/Bespoke/`.
- Delete emptied dialog subdirectories.
- Build + final verification.

## Verification

- Build:
  - `ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj`
  - `src/desktop/app/XerahS.UI/XerahS.UI.csproj`
  - `tests/XerahS.Tests/XerahS.Tests.csproj`
- Automated tests:
  - `ImageEffectCatalog.Definitions` contains entries from all 4 categories with stable IDs.
  - `GetByCategory()` returns correct subsets.
  - Generic dialog renders all 8 parameter types (slider, checkbox, enum, color, numeric, text, file path).
  - `ApplyImmediately` effects execute without opening a dialog.
  - Preview updates when parameter values change.
  - Browser renders all categories from the catalog.
  - File path parameter opens a file picker and stores the selected path.
- Manual smoke:
  - Open effects from each category in the browser, verify preview/apply/cancel.
  - Verify parameterless effects (Invert, B&W, Polaroid) apply instantly.
  - Verify bespoke editors (PerspectiveWarp, DrawText, etc.) still function.
  - Verify favorites, recent lists, and search work across all categories.

## Risks
- **Phase 1 rename scope**: Renaming `FilterParameterDefinition` → `EffectParameterDefinition` and `FilterCatalog` → `ImageEffectCatalog` touches many existing Filter definitions. This must be done atomically with build verification.
- **File path parameter UX**: `FilePathParameterDefinition` introduces a native file picker dependency. Must integrate with the existing `IViewDialogService` or platform file picker abstraction.
- **Bespoke editor routing**: The `CustomEditorKey` → bespoke editor mapping must cover all 8–9 remaining bespoke dialogs cleanly.
- **ID stability**: All effect IDs must remain identical to preserve favorites, recents, and aliases.

## Implementation Outcome (March 23, 2026)

IEIP0004 has now been implemented in `ShareX.ImageEditor` and verified with a successful project build.

### Phase completion

- Phase 1 (shared schema infrastructure): Completed.
- Phase 2 (adjustment migration): Completed.
- Phase 3 (manipulation migration): Completed.
- Phase 4 (drawing migration): Completed.
- Phase 5 (browser/registry consolidation): Completed.

### Commits delivered

- `6f27900`: shared schema infrastructure, unified catalog, filter migration, schema dialog rename, file-path parameter support.
- `6bef4d9`: removed obsolete bespoke adjustment dialogs after catalog migration.
- `70e16f3`: removed obsolete bespoke manipulation/drawing dialogs after catalog migration.
- `f14289e`: browser and registry switched to unified catalog dispatch with immediate-apply handling.

### Benefits achieved

- One unified effect contract now powers all categories (`EffectDefinition`, shared parameter/state types, and `ImageEffectCatalog` partials).
- Large reduction in bespoke dialog maintenance surface through schema-driven rendering.
- Generic dialog now supports file-path parameters with integrated browse picker support.
- `EffectDialogRegistry` now resolves by catalog definition + `CustomEditorKey` instead of a large per-effect factory table.
- Effect browser category population is fully catalog-driven, improving consistency for search, favorites, and recent effects.

### Notes on final shape

- `SelectiveColor` remains bespoke (`CustomEditorKey`) because its multi-range state model is materially more complex than single-parameter schema controls.
- Metadata now has a safe fallback for unmapped effect IDs so newly cataloged entries remain discoverable even before explicit metadata copywriting is added.
