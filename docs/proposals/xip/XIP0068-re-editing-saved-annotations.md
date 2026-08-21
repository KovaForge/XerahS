# XIP0068 Re-Editing Saved Annotated Screenshots

**Status**: Complete
**Version**: v0.22.257

**Author**: Milena Petrova (KovaForge Researcher)  
**Co-Authors**: Vladislava Kova (KovaForge COO)  
**Created**: 2026-04-09  
**Area**: Image Editor | Persistence | UX  
**Related**: XIP0023 (Annotation Toolbar Refactor), XIP0039 (ImageEditor Refactor Priorities), XIP0065 (Avalonia 12 Upgrade and Breaking Changes)  

---

## Summary

Introduce annotation preservation for saved screenshots so that annotated images can be fully re-edited later, matching Snagit's project-file workflow. When XerahS saves a capture with annotations, it will write a companion `.xann` sidecar file alongside the flat image. The sidecar must preserve both the clean editor source image and the vector annotation layer, because reopening a flattened PNG/JPEG and overlaying saved annotations would double-render every mark. Double-clicking the image in history or invoking "Edit annotations" will restore the full annotation layer instead of a blank canvas.

This proposal should explicitly use Avalonia 12 well instead of merely running on it. The re-edit workflow should take advantage of Avalonia 12's compiled-bindings-by-default posture, stronger dispatcher and focus APIs, Linux accessibility backend, and themeable client decorations so that the sidecar recovery flow is performant, accessible, and visually integrated with the rest of the upgraded app.

---

## Motivation

### The Problem

Today, XerahS saves annotated screenshots as flat raster images (PNG, JPG, etc.) - the annotation vectors are composited onto the pixels and then discarded. There is no way to re-open a saved screenshot and continue editing its annotations.

A XerahS user described this as missing Snagit's workflow:

> "In Snagit, after I save a screenshot, I can always go back and re-edit it because the annotations are stored separately from the rendered image. I wish XerahS had that."

### What Snagit Does

Snagit stores captures in `.snag` / `.snagx` files, which bundle the base image with vector annotation objects. The editor re-opens the bundle and restores the annotation stack. Users never lose editability.

XerahS currently composites annotations onto the bitmap at save time, then throws away the annotation data. The rendered image is a one-way door.

### Why It Matters

1. **Iteration**: Users annotate -> upload -> realize they missed something -> want to add more without re-capturing
2. **Collaboration**: A team member receives an annotated screenshot and wants to refine it further
3. **Template reuse**: Users want to re-apply annotation sets across multiple captures
4. **Error correction**: A mis-clicked annotation in a saved screenshot cannot currently be fixed without re-capturing

### What XerahS Already Has

The annotation system is close to serialization-ready, but the implementation must not blindly persist every live object property:

- `ShareX.ImageEditor.Core.Annotations.Base.Annotation` and all subtypes use `System.Text.Json` via `[JsonDerivedType]` discriminators on the base class
- `EditorCore.Annotations` exposes the live annotation list as `IReadOnlyList<Annotation>`
- Yoink (reference app) uses an identical `record`-based annotation model with JSON serialization

The hard work is mostly done, but the save/load pipeline must add a persistence boundary that skips transient caches (`EffectBitmap`, live selection state) and embeds payloads that are otherwise lost (`ImageAnnotation` bitmaps).
---

## Design

### Annotation Storage Format

**Option chosen: `.xann` sidecar file (JSON)**

| | `.xann` Sidecar | Embedded PNG tEXt | `.snagx` Container |
|---|---|---|---|
| Backwards compatible | Yes - no new format required | Yes - no new format required | No - new extension |
| Standard tooling | Yes - plain JSON | No - requires PNG library | No - requires zip + custom spec |
| Multi-file image support | Yes - PNG, JPG, WebP, BMP | No - PNG only | No - custom |
| File size overhead | Minimal (JSON gzipped) | Minimal | Larger (zip overhead) |
| Partial save on crash | Yes - safe (image + sidecar separate) | No - corrupts image | Warning - corrupts archive |
| Easy extract/reuse | Yes - sidecar is standalone | No - requires stripping metadata | No - requires unzip |
| **Decision** | **Yes - adopted** | Rejected | Rejected |

The `.xann` file is a gzipped JSON document alongside the image:

```
screenshot_2026-04-09_001.png     <- rendered image (unchanged)
screenshot_2026-04-09_001.xann    <- annotation project file (new)
```

**`.xann` schema (v1):**

```json
{
  "version": 1,
  "imagePath": "screenshot_2026-04-09_001.png",
  "imageHash": "sha256:abc123...",
  "canvasWidth": 1920,
  "canvasHeight": 1080,
  "createdAt": "2026-04-09T07:44:00Z",
  "modifiedAt": "2026-04-09T08:00:00Z",
  "sourceImagePngBase64": "iVBORw0KGgo...",
  "embeddedImages": {
    "550e8400-e29b-41d4-a716-446655440000": "iVBORw0KGgo..."
  },
  "annotations": [
    {
      "$type": "Arrow",
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "toolType": "Arrow",
      "strokeColor": "#ef4444",
      "strokeWidth": 4.0,
      "fillColor": "#00000000",
      "shadowEnabled": false,
      "startPoint": { "x": 100, "y": 200 },
      "endPoint": { "x": 300, "y": 400 },
      "zIndex": 0,
      "rotationAngle": 0.0
    }
  ]
}
```

The `sourceImagePngBase64` field is the non-destructive source canvas. It is not the flattened saved PNG/JPEG; it is the editor background before annotations are composited. The `imageHash` field allows validation that the `.xann` still corresponds to the current rendered image file, but hash mismatch must degrade gracefully instead of blocking re-edit outright. `embeddedImages` stores PNG-encoded pasted/sticker images keyed by `ImageAnnotation.Id`, because those bitmaps are otherwise only present in memory or burned into the flat render.

### Storing `.xann` Path in History

The existing `HistoryItem` model should expose a convenience property backed by `Tags`, not a new database column:

```csharp
// Convenience property on HistoryItem, persisted in Tags JSON.
public string? AnnotationSidecarPath
{
    get => GetTagValue(nameof(AnnotationSidecarPath));
    set => SetTagValue(nameof(AnnotationSidecarPath), value);
}
```

The current SQLite history schema already persists `Tags`, so this avoids a migration for v1. When a capture is saved with annotations, `AnnotationSidecarPath` is set to the `.xann` path. When the history item is loaded and the user requests re-editing, XerahS:
1. Loads the base image
2. If `AnnotationSidecarPath` exists and the `.xann` file is valid -> restores annotations
3. If not -> opens with no annotations (degrades gracefully)

### UX - Triggering Re-Edit

Three entry points:

| Trigger | Behavior |
|---|---|
| **Double-click / Enter** on history item | Opens editor with annotations restored (if sidecar exists) |
| **Right-click -> "Edit Annotations"** context menu | Same as above |
| **Hotkey** `Ctrl+Shift+E` on selected history item | Same as above |
| **Drag-and-drop** `.xann` file onto editor | Opens editor with annotations restored |

**Degraded mode**: If the `.xann` exists but references an image that has been moved or modified, show a dialog:
- "Annotation data found but image has changed. Load image without annotations, or reload the matching image file?"

### Backwards Compatibility

- **Old captures**: No `.xann` file -> editor opens with empty annotation layer (no crash, no data loss)
- **Non-annotated saves**: No `.xann` file is written. If a previously annotated capture is re-saved with all annotations removed, the existing sidecar is deleted and the history tag is cleared.
- **Image-only workflows**: Users who never use the editor are unaffected

### Avalonia 12 Enablement

This feature has a direct fit with Avalonia 12's April 7, 2026 release themes and should adopt the parts that materially improve the XerahS user journey.

#### 1. Compiled bindings for new re-edit UI

Avalonia 12 enables compiled bindings by default. All new XAML introduced by this XIP should preserve that benefit with explicit `x:DataType` usage:

- history badge / indicator that a `.xann` sidecar exists
- toolbar-level `Re-edit` action
- restored-annotations info banner
- degraded-mode recovery dialogs

The goal is that the history surface remains responsive even when many captures are loaded and sidecar availability is being resolved.

#### 2. Dispatcher-first restore pipeline

Avalonia 12 adds `Dispatcher.CurrentDispatcher`, `Dispatcher.FromThread`, `AvaloniaObject.Dispatcher`, `Dispatcher.Yield`, and better background-processing support. This XIP should use that model directly:

- gzip decompression, hash validation, and JSON parsing run off the UI thread
- editor rehydration returns to the UI thread through the dispatcher
- opening or previewing a history item should not freeze the history grid while sidecar work is happening

#### 3. Focus management and keyboard predictability

Avalonia 12's focus-management overhaul is directly relevant because this feature introduces a new editor-entry mode plus recovery dialogs:

- after `Ctrl+Shift+E`, focus should land on the restored editor canvas or the banner's primary action
- if recovery is cancelled, focus should return cleanly to the original history item
- context-menu and dialog flows should use Avalonia 12 focus APIs rather than ad hoc focus jumps

#### 4. Linux accessibility and automation metadata

Avalonia 12 ships the native AT-SPI2 Linux accessibility backend and broader automation support. Every new piece of UI in this XIP should include accessibility metadata:

- `Re-edit` toolbar action
- sidecar-present history badge
- recovery dialogs and their primary/secondary actions
- restored-annotations banner

This proposal should explicitly target screen-reader discoverability on Linux, not just visual discoverability on Windows.

#### 5. Themeable client decorations for recovery UX

Avalonia 12 introduces themeable client-side window decorations. Recovery dialogs opened by this workflow should use the standard Avalonia 12 decorations model and inherit XerahS theming, rather than relying on custom chrome workarounds.

#### 6. Explicit non-goals

Avalonia 12 also introduces page-based navigation controls. Those are valuable framework additions, but they are not a good fit for this XIP. Re-editing saved annotations should improve the existing history/editor workflow rather than trigger a shell-navigation rewrite.

### File Naming Convention

Sidecar files share the stem of the image:
```
{same-stem}.xann
```

Sidecars live in the same folder as the image by default. Users may configure an alternate annotation storage root in settings (e.g., a dedicated `.xann` folder for cleaner directories).
---

## Implementation Plan

### Phase 1 - Core Serialization (MVP)

**Goal**: Serialize annotations to `.xann` on save, deserialize on load.

| # | Deliverable | Description |
|---|---|---|
| 1 | `XannProjectFile` model | Root object for gzipped `.xann` JSON, including source image PNG, flat image hash, dimensions, annotations, and embedded pasted images |
| 2 | `XannProjectFileService` | `SaveAsync(...)` writes atomically; `LoadAsync(...)` validates and rehydrates annotation payloads |
| 3 | Editor session result | UI service returns the flattened image, clean source image, annotation snapshot, and editor task result |
| 4 | Save-with-annotations flow | After compositing, write/update `.xann` alongside image; skip/delete sidecar when annotation list is empty |
| 5 | Load-with-annotations flow | Check for `.xann`, deserialize if present, restore source image and annotations to `EditorCore` |
| 6 | HistoryItem extension | Add `AnnotationSidecarPath` and `HasEditableAnnotations` convenience properties backed by `Tags` |
| 7 | History re-edit command | `EditImage` in `HistoryViewModel` checks sidecar and restores annotations before opening the editor |
| 8 | Graceful degradation | Missing/corrupt sidecar -> opens image only, with non-fatal logging or a concise dialog when history claimed annotations existed |
| 9 | Async dispatcher handoff | Parse/hash-check off-thread, restore onto the UI thread through Avalonia dispatcher APIs |
| 10 | Compiled-binding requirement | New re-edit XAML uses `x:DataType` and compiled bindings instead of reflection bindings |

**Implementation notes**:
- Use `System.Text.Json` with the existing `[JsonDerivedType]` attributes on `Annotation`; annotate transient bitmap cache properties with `[JsonIgnore]`
- Use `System.IO.Compression.GZipStream` to keep `.xann` files small
- The `imageHash` field uses SHA-256 of the image file contents at save time
- Validate hash on load; warn or log if image was modified post-save, but still allow the embedded clean source image to restore annotations
- v1 scope covers the ImageEditor after-capture and standalone editor save flows. Region-capture overlay annotations are a separate vector-capture problem and should not be implied until that path returns annotation objects instead of only a composited layer.

### Phase 2 - UX Polish & Editor Integration

**Goal**: Full UX story for re-editing saved screenshots.

| # | Deliverable | Description |
|---|---|---|
| 1 | Context menu entry | "Edit Annotations" on history item right-click |
| 2 | Hotkey `Ctrl+Shift+E` | Re-edit selected history item |
| 3 | Drag `.xann` onto editor | Accept file drop, restore annotation layer |
| 4 | "Re-edit" badge in history | Visual indicator on items with preserved annotations |
| 5 | Settings option | Toggle annotation sidecar storage on/off |
| 6 | Configurable sidecar root | Setting to store `.xann` files in a dedicated folder |
| 7 | Accessibility metadata | Automation names/landmarks for badges, banner, and recovery dialogs |
| 8 | Focus restoration | Predictable keyboard focus for open/cancel/complete re-edit flows |

### Phase 3 - Advanced Features

| # | Deliverable | Description |
|---|---|---|
| 1 | Annotation templates | Save annotation sets as reusable `.xann` templates |
| 2 | Batch re-annotation | Apply same annotations to multiple images |
| 3 | Export annotation layer | Export `.xann` without image (for overlay workflows) |
| 4 | Import Snagit `.snag`/`.snagx` | Convert Snagit project files to `.xann` (stretch goal) |
---

## Alternatives Considered

### Embedded PNG tEXt / TIFF Tags

Store annotation JSON inside the image file's metadata chunks.  
**Rejected because**: Only works for PNG/TIFF; breaks standard image viewers that may strip metadata; harder to validate integrity; no good story for JPG/WebP.

### `.snagx`-style Zip Container

Bundle `image.png` + `annotations.json` inside a zip as `.xann`.
**Rejected because**: A zip with two entries is essentially the same as sidecar files, but if the image is modified even slightly, the whole archive is suspect. Sidecar files degrade more gracefully - the image is always valid standalone.

### Native `.xann` Binary Format

Use protobuf or MessagePack instead of JSON.  
**Rejected because**: JSON is human-debuggable, standard tooling works, and annotation files are small enough that performance is not a concern. `System.Text.Json` is already in the codebase.
---

## References

- **Snagit `.snagx` format**: TechSmith stores annotations as structured data inside a zip container. Reference: <https://github.com/Hacksore/snagit-file-extension>
- **Yoink annotation model**: `Ref/yoink/src/Yoink/Models/Annotation.cs` - similar `record`-based annotation types with JSON serialization
- **XerahS annotation base class**: `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Annotations/Base/Annotation.cs` - already uses `[JsonDerivedType]` for polymorphic JSON serialization
- **EditorCore annotations**: `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Editor/EditorCore.cs` - exposes `IReadOnlyList<Annotation>` for serialization
- **History re-edit**: `XerahS.UI/ViewModels/HistoryViewModel.cs` - `EditImage` command opens editor without annotation restoration (line 345)
- **XIP0023**: Annotation Toolbar Refactor - establishes the reusable toolbar infrastructure this feature depends on
- **XIP0039**: ImageEditor Refactor Priorities - context on the annotation system's current design
- **Avalonia 12 release notes**: Avalonia UI Blog, "Avalonia 12 - Ready for What's Next," published April 7, 2026 - source for compiled-bindings-by-default, dispatcher/focus improvements, Linux accessibility backend, and themeable client decorations: <https://avaloniaui.net/blog/avalonia-12/>
---

*Authors: Milena Petrova (KovaForge Researcher) + Vladislava Kova (KovaForge COO)*
---

## Critique

### Strengths

- **Format decision is correct.** `.xann` sidecar is the right call - crash-safe, image-format-agnostic, human-readable, and easy to migrate later.
- **Serialization path is mostly there.** `[JsonDerivedType]` discriminators on `Annotation` cover all 16 subtypes. `EditorCore.Annotations` exposes a clean `IReadOnlyList<Annotation>` for serialization.
- **Hash validation is smart.** Checking `imageHash` on load to detect post-save image edits is a genuine safeguard most competitors skip.
- **Graceful degradation is the right default.** No sidecar -> blank canvas, no crash. The risk of data loss is low.
- **Motivation is grounded in real user pain.** The Snagit comparison is accurate and gives the feature a clear bar.

### Weaknesses & Risks

#### 1. `HistoryItem` uses Newtonsoft.Json, not `System.Text.Json`

The `Annotation` class and all subtypes serialize via `[JsonDerivedType]` on `System.Text.Json`. But `HistoryItem` uses **Newtonsoft.Json** (`[JsonProperty]` attributes). This creates a mixed serialization environment. If `AnnotationSidecarPath` is added to `HistoryItem`, it will be serialized with Newtonsoft - fine for a string path. But any shared model that needs to serialize in both contexts (e.g., if `Annotation` is ever part of the history DB schema directly) will need careful handling. This is not a blocker but a **known maintenance complexity**.

#### 2. `ImageAnnotation` - pasted images do not survive the composite step
When the editor composites annotations onto the bitmap at save time, any `ImageAnnotation` (pasted image/sticker) is rasterized into the output. The pasted bitmap data is not stored in the image file - it's burned into the pixels. Loading a `.xann` for an image that had pasted content will restore the annotation object with its `ImageId` reference, but the actual bitmap data for that pasted image will be gone from the `.xann`. The `.xann` schema does not include an `embeddedImages` array. **This is a data loss risk for the most common multi-edit workflow after text annotations.**


**Mitigation required:** Either (a) add an `embeddedImages` section to the `.xann` schema storing base64 pasted bitmaps, or (b) document this as a known limitation and require that pasted images be re-imported on re-edit.

#### 3. Image moved/renamed after save - sidecar becomes orphaned
The degraded mode dialog says "Load image without annotations, or reload the matching image file?" But if the image has been moved, there is no "matching image file" - the old path is dead. The dialog assumes the image still exists at some discoverable location. **Orphaned sidecar files accumulate silently.**

**Mitigation required:** When loading an orphaned `.xann`, prompt the user to locate the original image file manually, then update `imagePath` in the sidecar.

#### 4. Re-save update cycle is undefined
What happens when a user re-edits an annotated screenshot and saves again? Does the existing `.xann` get overwritten? Does a new one get created? Is the `imageHash` updated? There is no version field in the schema and no explicit save-cycle protocol. **After N re-edits, the user ends up with N+1 `.xann` files or undefined behavior.**

**Mitigation required:** Define the update cycle: overwrite the `.xann` on every save-with-annotations. Add a `version` field for future migration.

#### 5. TextAnnotation content encoding
TextAnnotation stores text content - verify that the font, size, and rich text formatting (if any) are fully serialized by the `[JsonDerivedType]` path. FreehandAnnotation stores stroke point data - verify it doesn't exceed practical file sizes for long strokes.

#### 6. Phase 3 scope is unachievable in v1
Template systems, batch re-annotation, and Snagit import are all substantial features. Including them in the same document as the core MVP gives the wrong impression about effort. **Phase 3 should be a separate XIP.**

### Edge Cases Not Addressed

| Edge Case | Risk | Recommendation |
|---|---|---|
| Image deleted, `.xann` orphaned | Silent annotation loss | Detect orphaned sidecars via hash scan on startup; surface a notification |
| Image moved to new folder | Sidecar uses relative `imagePath` - may still resolve | If hash-match fails, prompt for new location |
| Same image saved multiple times | Multiple `.xann` files accumulate | One `.xann` per save, overwrite existing |
| User edits image externally (Photoshop, etc.) | Hash mismatch on re-edit | Degraded mode dialog is correct; clarify wording |
| Pinned/favorite captures | User expects annotations preserved forever | Ensure `AnnotationSidecarPath` survives history DB migration |
| Network/shared drives | `.xann` file may be inaccessible | Handle `IOException` gracefully; disable re-edit button if sidecar is unreachable |
| Large annotated captures | SHA-256 + GZip on every save adds latency | Profile on 4K captures with 50+ annotations; async the hash computation |

### Revised Implementation Plan

**Phase 1 is under-scoped.** The following deliverables are missing:

| # | Missing Deliverable | Why It Matters |
|---|---|---|
| 8 | `ImageAnnotation` embedded bitmap handling | Data loss risk - pasted images vanish on composite |
| 9 | Orphaned `.xann` detection + user prompt | Orphaned sidecars accumulate without recovery UX |
| 10 | Re-save overwrite protocol | Undefined update cycle produces unreliable sidecar state |
| 11 | `.xann` migration path for schema `version` field | v1 writes `version: 1` with no migration story for v2 |

**Phase 2 item 3 (skip empty `.xann`) should be in Phase 1**, not Phase 2 - writing empty sidecar files for every non-annotated save is a wasteful footgun.

### Decisions for v1

The critique choices are resolved for this implementation:

**1. Pasted image handling**: Add `embeddedImages` to `.xann` schema. This keeps `ImageAnnotation` fully re-editable and avoids a silent data-loss class.

**2. Sidecar storage**: Write sidecars alongside the image by default using `{same-stem}.xann`. A configurable root can be added later, but v1 should keep lookup deterministic and debuggable.

**3. Re-edit badge**: Add a history badge/context entry in v1 because the feature is invisible without a discoverability signal. A toolbar-level re-edit action can follow after the core path is validated.
---

*Critique: Nadia Valeva (KovaForge Analyst)*
---

## Design Review

*Review by Sofia Novak, KovaForge Designer - 2026-04-09*
---

### 1. Settings Placement

**Recommendation: Two-tier - global toggle in Image settings, sidecar root in Application settings.**

The XIP proposes a single "Toggle annotation sidecar storage on/off" in Phase 2. This undersells the feature and buries it too deep.

**Global toggle** (`Save annotations for re-editing`) belongs in **Settings -> Image**, next to existing image-quality and format controls. This is where users configure how captures are saved - it's the right mental model context. Default: **On**.

**Sidecar root** (`Store .xann files in:`) is an Application-level path setting, not per-workflow. Per-workflow placement creates confusing behavior when the same image is captured via different workflows - the sidecar path should be deterministic, not workflow-dependent.

**Per-capture override** (in the After-Capture window, when `AnnotateMedia` fires): optionally show a checkbox "Save annotations" checked by default, matching the global preference. This gives power users control without burying it in a settings pane they visit once.

**Avoid**: placing the toggle inside the annotation editor itself. The editor is where users go to *make* annotations, not to configure *persistence*. Context-switching to settings mid-editing breaks flow.
---

### 2. Trigger UX

**Recommendation: Double-click is correct primary trigger; add a discoverable "Re-edit" entry in the toolbar above the history grid.**

Double-click on a history item is the right primary trigger - it matches every file browser and image viewer convention. `Ctrl+Shift+E` as a hotkey is fine for power users.

**Problem with current XIP**: Right-click -> "Edit Annotations" is discoverable only if users already know the feature exists. A first-time user right-clicking a screenshot will scan the menu for "Edit" or "Annotate" and won't find either. The context menu is an *additional* entry point, not the primary discovery path.

**What the XIP is missing**: A toolbar-level "Re-edit" button (or icon) above the history grid, visible without any interaction. This is the discoverable entry point that makes the feature visible at first glance. It should:
- Show as enabled (with a badge) on items that have `.xann` sidecars
- Show as disabled with a tooltip ("No annotations saved for this capture") on items without
- Be a icon button with a tooltip label, not a text button that clutters the toolbar

**Right-click context menu** should list:
- `Open in Editor` (existing - flat render)
- `Edit Annotations` (new - restores annotations, shown only when sidecar exists)
- `Open File` / `Open Folder` / `Upload` (existing)


Hiding "Edit Annotations" behind a menu that only appears when the sidecar exists is a natural progressive disclosure pattern - users won't see it until it's relevant.
---

### 3. Visual Language

#### `.xann` File Icon

**Recommendation: Dedicated icon - a PNG file silhouette with a layered annotation mark overlay.**

Think of a document-stack icon where the top page has annotation marks (arrows, text lines) visible. This clearly separates `.xann` from plain PNG/JPG in file browsers without requiring users to understand the extension first.

Color: use the app's accent color (typically blue) for the overlay to match how XerahS brand elements appear elsewhere.


#### History View - Editable vs Flat

**Recommendation: Badge overlay on the thumbnail corner + subtle border treatment.**

Items with preserved annotations should show a small badge (layered-papers icon, or a small ribbon) on the top-right corner of their thumbnail in the history grid. This badge is:
- Visible at a glance in grid view
- Self-explanatory: "this capture has editability preserved"
- Not obtrusive - doesn't change the thumbnail image itself

Additionally, consider a left-border accent (1-2px) in the app's accent color on list-view rows that have editable annotations. Provides the same signal without a dedicated icon.


**What NOT to do**: Don't change the thumbnail itself to show annotation marks overlaid - thumbnails are small and already visually dense. The badge is cleaner.


#### Annotation Editor - First-Run on Re-Open

**Recommendation: Pre-loaded annotation layer with a dismissible "Restored from saved annotations" info banner.**

When re-opening a `.xann`-backed capture, the editor should:
1. Load the base image as the canvas background (unchanged)
2. Immediately restore all annotation objects from `.xann` into the live annotation layer
3. Show a small dismissible banner above the canvas: `"Annotations restored - you're editing the saved version"` with an info icon

This banner serves two purposes:
- **Orientation**: confirms to the user that they're in re-edit mode and not starting from scratch
- **Informs the mental model**: users know they're editing a saved project, not a flat image - their changes will overwrite the previous `.xann`

The banner auto-dismisses after 3 seconds or on first annotation interaction. Users who frequently re-edit will learn to ignore it; users who forgot this was a re-edit will be immediately oriented.

**Contrast with "blank slate"**: Opening a saved annotated image as a blank canvas with just the image as background is disorienting - users may not realize annotations were saved and present, and make changes that overwrite them without understanding the data model. The banner + pre-loaded layer is worth the small UI complexity.
---

### 4. Settings Scope

**Recommendation: Global toggle + sidecar root path + format preference.**


Three settings are warranted:

| Setting | Location | Default | Notes |
|---|---|---|---|
| `Save annotations for re-editing` | Settings -> Image | On | Master toggle. Off = behavior is identical to today (flat saves only) |
| `.xann storage location` | Settings -> Application | Same folder as image | Option: ` Alongside image` / `Custom folder` |
| `Default save format for annotated captures` | Settings -> Image | PNG + `.xann` | Option: `PNG + .xann` / `JPEG + .xann` / `WebP + .xann`. PNG is always recommended since JPEG recompression degrades annotation sharpness |

The format preference setting is low priority for Phase 1 but should be in the spec so it's not forgotten. JPEG users should understand that re-compression artifacts accumulate on every save-round-trip - the XIP's hash-validation approach will surface this as a degradation warning, but a format-setting explanation tooltip can preempt confusion.

**Skip**: per-workflow `.xann` toggle. Workflows are for capture/upload routing, not persistence configuration. Same image via two workflows should produce identical sidecar behavior.
---

### 5. Edge Case UI

#### No `.xann` Sidecar Found

**Current XIP**: "Annotations Not Available" - this is functional but cold.


**Refined UX**:
- If the history item has `AnnotationSidecarPath` set but the file doesn't exist: show a dialog "Annotations were saved for this capture, but the annotation file is missing. The image will open without annotations." with options: `[Open Without Annotations]` `[Cancel]`
- If the history item has no `AnnotationSidecarPath`: do nothing special - open in editor silently without annotations, consistent with current behavior. Don't show any dialog; this is the "old captures" graceful degradation path and users shouldn't be prompted for every pre-feature capture they open

#### Image Modified After Save (Hash Mismatch)

**Current XIP degraded mode dialog**: "Annotation data found but image has changed. Load image without annotations, or reload the matching image file?"

**Problem**: "reload the matching image file" implies the image still exists somewhere. If the image was deleted, this is confusing.

**Refined UX**:
- **Image moved/renamed**: `[Locate Image File]` button -> opens file picker -> user selects the new location -> hash is re-validated -> if match, annotations restore; if no match, show "Selected image doesn't match saved annotations. Open without annotations?"
- **Image edited externally**: `[Open Without Annotations]` primary, `[Cancel]` secondary. No "reload matching image" - edited images are fundamentally different and the workflow intent ("I want to tweak my annotation") no longer applies to an edited version

#### Orphaned Sidecar Detection

**Recommendation**: On application startup, scan for `.xann` files that reference images no longer present at the stored `imagePath`. Surface a one-time notification: `"N saved annotation(s) found without matching images. Open Settings -> History to review."` This prevents silent annotation accumulation and gives users a recovery path before they notice the feature isn't working for a specific capture.

---

### 6. Design Decisions Affecting Implementation Spec

| Decision | Implementation Impact |
|---|---|
| Global toggle in Image settings (not per-workflow) | `AnnotationSidecarPath` is written on every save where the toggle is on, regardless of workflow. No conditional logic by workflow. |
| `Same folder` default for sidecar root | Sidecar path = `{imageDirectory}/{imageStem}.xann`. Computing this is trivial at save time. Custom root requires `AnnotationSidecarPath` to store an absolute path (not relative) or the orphaned-sidecar scanner needs a lookup table. |
| Pre-loaded annotation layer on re-open | The `ShowEditorAsync` call in `HistoryViewModel.EditImage` must deserialize `.xann` and pass the annotation list to the editor session, not just the `SKBitmap`. The editor session must accept a pre-populated annotation layer on open. |
| Badge on history items with sidecar | The history view needs to check for `.xann` existence at render time (or cache the check). Doing this synchronously on every bind is expensive - consider an async badge loader or a pre-computed `HasEditableAnnotations` property on `HistoryItem` refreshed on history load. |
| Info banner on re-edit | The editor session needs a flag/parameter indicating "this is a re-edit of a saved project" vs "new capture" vs "open from file drop". The banner shows only in re-edit mode. |
| Format preference setting | The `SaveWithAnnotations` flow needs to consult the user's preferred annotated save format. Currently the XIP implies PNG-only (from the "rendered image unchanged" framing). This setting extends that to JPEG/WebP, which requires the same compositing pipeline but outputs to a different encoder. |
---

### 7. Summary of Design Decisions


| # | Decision | Rationale |
|---|---|---|
| D1 | Global toggle in Image settings | Where users configure save behavior; consistent mental model |
| D2 | Sidecar root as app-level path setting, defaulting to image folder | Simple default; custom root is an advanced option, not the common case |
| D3 | Per-capture override in After-Capture window | Power-user escape hatch without complicating settings hierarchy |
| D4 | "Re-edit" toolbar button above history grid as primary discoverable entry point | Makes the feature visible without requiring right-click exploration |
| D5 | Context menu "Edit Annotations" shown only when sidecar exists | Natural progressive disclosure; no empty-state clutter |
| D6 | Badge overlay on thumbnails for editable items | Immediate visual differentiation in grid view |
| D7 | Pre-loaded annotation layer + dismissible info banner on re-open | Orients users to the re-edit mental model; prevents accidental overwrites |
| D8 | Orphaned sidecar notification on startup | Prevents silent accumulation of broken annotation state |
| D9 | Three settings (toggle, root, format) | Enough depth for power users; not so many that casual users are overwhelmed |
---

*Design Review: Sofia Novak (KovaForge Designer)*
