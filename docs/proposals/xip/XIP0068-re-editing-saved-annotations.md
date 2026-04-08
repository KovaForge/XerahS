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

---

## Critique

### Strengths

- **Format decision is correct.** `.xera` sidecar is the right call — crash-safe, image-format-agnostic, human-readable, and easy to migrate later.
- **Serialization path is mostly there.** `[JsonDerivedType]` discriminators on `Annotation` cover all 16 subtypes. `EditorCore.Annotations` exposes a clean `IReadOnlyList<Annotation>` for serialization.
- **Hash validation is smart.** Checking `imageHash` on load to detect post-save image edits is a genuine safeguard most competitors skip.
- **Graceful degradation is the right default.** No sidecar → blank canvas, no crash. The risk of data loss is low.
- **Motivation is grounded in real user pain.** The Snagit comparison is accurate and gives the feature a clear bar.

### Weaknesses & Risks

#### 1. `HistoryItem` uses Newtonsoft.Json, not `System.Text.Json`

The `Annotation` class and all subtypes serialize via `[JsonDerivedType]` on `System.Text.Json`. But `HistoryItem` uses **Newtonsoft.Json** (`[JsonProperty]` attributes). This creates a mixed serialization environment. If `AnnotationSidecarPath` is added to `HistoryItem`, it will be serialized with Newtonsoft — fine for a string path. But any shared model that needs to serialize in both contexts (e.g., if `Annotation` is ever part of the history DB schema directly) will need careful handling. This is not a blocker but a **known maintenance complexity**.

#### 2. `ImageAnnotation` — pasted images do not survive the composite step
When the editor composites annotations onto the bitmap at save time, any `ImageAnnotation` (pasted image/sticker) is rasterized into the output. The pasted bitmap data is not stored in the image file — it's burned into the pixels. Loading a `.xera` for an image that had pasted content will restore the annotation object with its `ImageId` reference, but the actual bitmap data for that pasted image will be gone from the `.xera`. The `.xera` schema does not include an `embeddedImages` array. **This is a data loss risk for the most common multi-edit workflow after text annotations.**


**Mitigation required:** Either (a) add an `embeddedImages` section to the `.xera` schema storing base64 pasted bitmaps, or (b) document this as a known limitation and require that pasted images be re-imported on re-edit.

#### 3. Image moved/renamed after save — sidecar becomes orphaned
The degraded mode dialog says "Load image without annotations, or reload the matching image file?" But if the image has been moved, there is no "matching image file" — the old path is dead. The dialog assumes the image still exists at some discoverable location. **Orphaned sidecar files accumulate silently.**

**Mitigation required:** When loading an orphaned `.xera`, prompt the user to locate the original image file manually, then update `imagePath` in the sidecar.

#### 4. Re-save update cycle is undefined
What happens when a user re-edits an annotated screenshot and saves again? Does the existing `.xera` get overwritten? Does a new one get created? Is the `imageHash` updated? There is no version field in the schema and no explicit save-cycle protocol. **After N re-edits, the user ends up with N+1 `.xera` files or undefined behavior.**

**Mitigation required:** Define the update cycle: overwrite the `.xera` on every save-with-annotations. Add a `version` field for future migration.

#### 5. TextAnnotation content encoding
TextAnnotation stores text content — verify that the font, size, and rich text formatting (if any) are fully serialized by the `[JsonDerivedType]` path. FreehandAnnotation stores stroke point data — verify it doesn't exceed practical file sizes for long strokes.

#### 6. Phase 3 scope is unachievable in v1
Template systems, batch re-annotation, and Snagit import are all substantial features. Including them in the same document as the core MVP gives the wrong impression about effort. **Phase 3 should be a separate XIP.**

### Edge Cases Not Addressed

| Edge Case | Risk | Recommendation |
|---|---|---|
| Image deleted, `.xera` orphaned | Silent annotation loss | Detect orphaned sidecars via hash scan on startup; surface a notification |
| Image moved to new folder | Sidecar uses relative `imagePath` — may still resolve | If hash-match fails, prompt for new location |
| Same image saved multiple times | Multiple `.xera` files accumulate | One `.xera` per save, overwrite existing |
| User edits image externally (Photoshop, etc.) | Hash mismatch on re-edit | Degraded mode dialog is correct; clarify wording |
| Pinned/favorite captures | User expects annotations preserved forever | Ensure `AnnotationSidecarPath` survives history DB migration |
| Network/shared drives | `.xera` file may be inaccessible | Handle `IOException` gracefully; disable re-edit button if sidecar is unreachable |
| Large annotated captures | SHA-256 + GZip on every save adds latency | Profile on 4K captures with 50+ annotations; async the hash computation |

### Revised Implementation Plan

**Phase 1 is under-scoped.** The following deliverables are missing:

| # | Missing Deliverable | Why It Matters |
|---|---|---|
| 8 | `ImageAnnotation` embedded bitmap handling | Data loss risk — pasted images vanish on composite |
| 9 | Orphaned `.xera` detection + user prompt | Orphaned sidecars accumulate without recovery UX |
| 10 | Re-save overwrite protocol | Undefined update cycle produces unreliable sidecar state |
| 11 | `.xera` migration path for schema `version` field | v1 writes `version: 1` with no migration story for v2 |

**Phase 2 item 3 (skip empty `.xera`) should be in Phase 1**, not Phase 2 — writing empty sidecar files for every non-annotated save is a wasteful footgun.

### Decision Required

Before any code is written, the CEO must decide:

**1. Pasted image handling — which approach?**
- **Option A**: Add `embeddedImages` array to `.xera` schema (base64). Enables full re-editability for `ImageAnnotation`. Increases `.xera` file size.
- **Option B**: Document as known limitation. Users re-import pasted images on re-edit. Simpler to ship.
- **Option C**: Always composite pasted images to pixels, never store in `.xera`. Users who paste external photos as annotations must re-paste on re-edit.

**2. Sidecar storage — same directory or configured root?**
- **Option A**: Always alongside the image (`same-stem.xera` in same folder). Simplest. Breaks down when users move images to organized subfolders.
- **Option B**: Central `.xera` root in settings. Clean directories. But sidecar no longer shares the image's directory context, making orphaned-sidecar detection harder.
- **Option C**: Both — configurable root that defaults to same directory.

**3. Re-edit badge — visual indicator on history items with preserved annotations?**
- **Option A**: Yes, add badge in Phase 1. Worth the UI work to make the feature discoverable.
- **Option B**: No, defer to Phase 2. Ship core serialization first.
- **Decision affects**: Phase 1 deliverable 6 vs Phase 2 deliverable 5.

---

*Critique: Nadia Valeva (KovaForge Analyst)*
