# XIP0068 Re-Editing Saved Annotated Screenshots

**Status**: Draft  
**Author**: Milena Petrova (KovaForge Researcher)  
**Co-Authors**: Vladislava Kova (KovaForge COO)  
**Created**: 2026-04-09  
**Area**: Image Editor | Persistence | UX  
**Related**: XIP0023 (Annotation Toolbar Refactor), XIP0039 (ImageEditor Refactor Priorities)  

---

## Summary

Introduce annotation preservation for saved screenshots so that annotated images can be fully re-edited later, matching Snagit's project-file workflow. When XerahS saves a capture with annotations, it will write a companion `.xera` sidecar file alongside the flat image. Double-clicking the image in history or invoking "Edit annotations" will restore the full annotation layer instead of a blank canvas.

---

## Motivation

### The Problem

Today, XerahS saves annotated screenshots as flat raster images (PNG, JPG, etc.) — the annotation vectors are composited onto the pixels and then discarded. There is no way to re-open a saved screenshot and continue editing its annotations.

A XerahS user described this as missing Snagit's workflow:

> "In Snagit, after I save a screenshot, I can always go back and re-edit it because the annotations are stored separately from the rendered image. I wish XerahS had that."

### What Snagit Does

Snagit stores captures in `.snag` / `.snagx` files, which bundle the base image with vector annotation objects. The editor re-opens the bundle and restores the annotation stack. Users never lose editability.

XerahS currently composites annotations onto the bitmap at save time, then throws away the annotation data. The rendered image is a one-way door.

### Why It Matters

1. **Iteration**: Users annotate → upload → realize they missed something → want to add more without re-capturing
2. **Collaboration**: A team member receives an annotated screenshot and wants to refine it further
3. **Template reuse**: Users want to re-apply annotation sets across multiple captures
4. **Error correction**: A mis-clicked annotation in a saved screenshot cannot currently be fixed without re-capturing

### What XerahS Already Has

The annotation system is already designed for serialization:

- `ShareX.ImageEditor.Core.Annotations.Base.Annotation` and all subtypes use `System.Text.Json` via `[JsonDerivedType]` discriminators on the base class
- `EditorCore.Annotations` exposes the live annotation list as `IReadOnlyList<Annotation>`
- Yoink (reference app) uses an identical `record`-based annotation model with JSON serialization

The hard work is done — the serialization path exists. What's missing is wiring it into the save/load pipeline.

---

## Design

### Annotation Storage Format

**Option chosen: `.xera` sidecar file (JSON)**

| | `.xera` Sidecar | Embedded PNG tEXt | `.snagx` Container |
|---|---|---|---|
| Backwards compatible | ✅ No new format required | ✅ No new format required | ❌ New extension |
| Standard tooling | ✅ Plain JSON | ❌ Requires PNG library | ❌ Requires zip + custom spec |
| Multi-file image support | ✅ PNG, JPG, WebP, BMP | ❌ PNG only | ❌ Custom |
| File size overhead | Minimal (JSON gzipped) | Minimal | Larger (zip overhead) |
| Partial save on crash | ✅ Safe (image + sidecar separate) | ❌ Corrupts image | ⚠️ Corrupts archive |
| Easy extract/reuse | ✅ Sidecar is standalone | ❌ Requires stripping metadata | ❌ Requires unzip |
| **Decision** | **✅ Adopted** | Rejected | Rejected |

The `.xera` file is a gzipped JSON document alongside the image:

```
screenshot_2026-04-09_001.png     ← rendered image (unchanged)
screenshot_2026-04-09_001.xera    ← annotation project file (new)
```

**`.xera` schema (draft v1):**

```json
{
  "version": 1,
  "imagePath": "screenshot_2026-04-09_001.png",
  "imageHash": "sha256:abc123...",
  "canvasWidth": 1920,
  "canvasHeight": 1080,
  "createdAt": "2026-04-09T07:44:00Z",
  "modifiedAt": "2026-04-09T08:00:00Z",
  "annotations": [
    {
      "typeDiscriminator": "Arrow",
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

The `imageHash` field allows validation that the `.xera` matches the current image file (protects against accidental image edits breaking annotation alignment).

### Storing `.xera` Path in History

The existing `HistoryItem` model must be extended:

```csharp
// New field on HistoryItem
public string? AnnotationSidecarPath { get; set; }
```

When a capture is saved with annotations, `AnnotationSidecarPath` is set to the `.xera` path. When the history item is loaded and the user requests re-editing, XerahS:
1. Loads the base image
2. If `AnnotationSidecarPath` exists and the `.xera` file is valid → restores annotations
3. If not → opens with no annotations (degrades gracefully)

### UX — Triggering Re-Edit

Three entry points:

| Trigger | Behavior |
|---|---|
| **Double-click / Enter** on history item | Opens editor with annotations restored (if sidecar exists) |
| **Right-click → "Edit Annotations"** context menu | Same as above |
| **Hotkey** `Ctrl+Shift+E` on selected history item | Same as above |
| **Drag-and-drop** `.xera` file onto editor | Opens editor with annotations restored |

**Degraded mode**: If the `.xera` exists but references an image that has been moved or modified, show a dialog:
- "Annotation data found but image has changed. Load image without annotations, or reload the matching image file?"

### Backwards Compatibility

- **Old captures**: No `.xera` file → editor opens with empty annotation layer (no crash, no data loss)
- **Non-annotated saves**: Even new captures without annotations will produce an empty `.xera` file (or we can skip writing it when annotation list is empty — TBD per-phase)
- **Image-only workflows**: Users who never use the editor are unaffected

### File Naming Convention

Sidecar files share the stem of the image:
```
{same-stem}.xera
```

Sidecars live in the same folder as the image by default. Users may configure an alternate annotation storage root in settings (e.g., a dedicated `.xera` folder for cleaner directories).

---

## Implementation Plan

### Phase 1 — Core Serialization (MVP)

**Goal**: Serialize annotations to `.xera` on save, deserialize on load.

| # | Deliverable | Description |
|---|---|---|
| 1 | `XeraProjectFile` model | Root object for `.xera` JSON, matches schema above |
| 2 | `AnnotationSerializer` class | `Serialize(EditorCore, stream)`, `Deserialize(stream) → List<Annotation>` |
| 3 | `SaveWithAnnotations` flow | After compositing, write `.xera` alongside image |
| 4 | `LoadWithAnnotations` flow | Check for `.xera`, deserialize if present, restore to `EditorCore` |
| 5 | HistoryItem extension | Add `AnnotationSidecarPath` to history DB schema |
| 6 | History re-edit command | `EditImage` in `HistoryViewModel` checks sidecar and restores annotations |
| 7 | Graceful degradation | Missing/corrupt sidecar → opens image only, no error |

**Implementation notes**:
- Use `System.Text.Json` with the existing `[JsonDerivedType]` attributes on `Annotation`
- Use `System.IO.Compression.GZipStream` to keep `.xera` files small
- The `imageHash` field uses SHA-256 of the image file contents at save time
- Validate hash on load; warn if image was modified post-save

### Phase 2 — UX Polish & Editor Integration

**Goal**: Full UX story for re-editing saved screenshots.

| # | Deliverable | Description |
|---|---|---|
| 1 | Context menu entry | "Edit Annotations" on history item right-click |
| 2 | Hotkey `Ctrl+Shift+E` | Re-edit selected history item |
| 3 | Empty annotation check | Skip writing `.xera` if no annotations were added (optional, perf) |
| 4 | Drag `.xera` onto editor | Accept file drop, restore annotation layer |
| 5 | "Re-edit" badge in history | Visual indicator on items with preserved annotations |
| 6 | Settings option | Toggle annotation sidecar storage on/off |
| 7 | Configurable sidecar root | Setting to store `.xera` files in a dedicated folder |

### Phase 3 — Advanced Features

| # | Deliverable | Description |
|---|---|---|
| 1 | Annotation templates | Save annotation sets as reusable `.xera` templates |
| 2 | Batch re-annotation | Apply same annotations to multiple images |
| 3 | Export annotation layer | Export `.xera` without image (for overlay workflows) |
| 4 | Import Snagit `.snag`/`.snagx` | Convert Snagit project files to `.xera` (stretch goal) |

---

## Alternatives Considered

### Embedded PNG tEXt / TIFF Tags

Store annotation JSON inside the image file's metadata chunks.  
**Rejected because**: Only works for PNG/TIFF; breaks standard image viewers that may strip metadata; harder to validate integrity; no good story for JPG/WebP.

### `.snagx`-style Zip Container

Bundle `image.png` + `annotations.json` inside a zip as `.xera`.  
**Rejected because**: A zip with two entries is essentially the same as sidecar files, but if the image is modified even slightly, the whole archive is suspect. Sidecar files degrade more gracefully — the image is always valid standalone.

### Native `.xera` Binary Format

Use protobuf or MessagePack instead of JSON.  
**Rejected because**: JSON is human-debuggable, standard tooling works, and annotation files are small enough that performance is not a concern. `System.Text.Json` is already in the codebase.

---

## References

- **Snagit `.snagx` format**: TechSmith stores annotations as structured data inside a zip container. Reference: <https://github.com/Hacksore/snagit-file-extension>
- **Yoink annotation model**: `Ref/yoink/src/Yoink/Models/Annotation.cs` — similar `record`-based annotation types with JSON serialization
- **XerahS annotation base class**: `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Annotations/Base/Annotation.cs` — already uses `[JsonDerivedType]` for polymorphic JSON serialization
- **EditorCore annotations**: `ShareX.ImageEditor/src/ShareX.ImageEditor/Core/Editor/EditorCore.cs` — exposes `IReadOnlyList<Annotation>` for serialization
- **History re-edit**: `XerahS.UI/ViewModels/HistoryViewModel.cs` — `EditImage` command opens editor without annotation restoration (line 345)
- **XIP0023**: Annotation Toolbar Refactor — establishes the reusable toolbar infrastructure this feature depends on
- **XIP0039**: ImageEditor Refactor Priorities — context on the annotation system's current design

---

*Authors: Milena Petrova (KovaForge Researcher) + Vladislava Kova (KovaForge COO)*
