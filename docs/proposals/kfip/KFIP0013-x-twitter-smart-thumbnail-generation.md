# KFIP0013: X/Twitter Smart Thumbnail Generation

**Status**: Proposal  
**Priority**: P2  
**Area**: Region Capture | AfterCapture | Image Annotation | Social Media | Creator Tools  
**Created**: 2026-07-04  
**Related**: KFIP0001 (AfterCapture OCR), KFIP0005 (Social Sharing Workflows), KFIP0009 (X/Twitter Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0011 (OCR-to-Clipboard)  
**Owner**: KovaForge  
**Co-Authors**: Nadia (research)

---

## Summary

X/Twitter content creators — educators, analysts, developers, and journalists — frequently share screenshots as the primary visual medium for their posts. But the workflow from "I have a great screenshot" to "this is a compelling, thumbnailable visual that stops the scroll" requires manual effort: cropping to an engaging aspect ratio, positioning the most important content in the frame, overlaying text or headlines, and applying platform-specific optimisation. This KFIP proposes a **Smart Thumbnail Generator** that transforms any X/Twitter screenshot into a ready-to-share, creator-branded thumbnail in a single action — combining smart content-aware cropping, headline text overlay, platform optimisation, and optional creator branding within the existing AfterCapture pipeline.

---

## Problem Statement

### The Thumbnail Gap

X/Twitter rewards visual content. A well-designed screenshot thumbnail — clean crop, visible headline, branded frame — outperforms an uncropped, unreadable full-screen capture. Yet every creator builds thumbnails manually:

| Step | Manual Effort | Frequency |
|------|--------------|-----------|
| Crop to 16:9 or 4:5 | Open editor, crop manually | Every capture |
| Position subject | Guess based on visual weight | Every capture |
| Add headline text | Type into editor, position, style | ~50% of captures |
| Apply creator frame | Add border/logo/handle overlay | ~20% of captures |
| Optimise for X | Check file size, re-compress | Every social capture |
| Export + save | Save, find file, attach to post | Every capture |

**User research signal from X (via KovaForge research, 2026-06):** Creators describe spending "5–10 minutes making a screenshot look good enough to post" when the underlying content is already captured in seconds. The friction is not in getting the screenshot — it's in making it thumbnailable.

### Existing KFIP Coverage Gap

| KFIP | What It Covers | What It Does NOT Cover |
|------|--------------|----------------------|
| KFIP0005 | Social presets, upload, URL copy | Thumbnail composition, text overlay, smart crop |
| KFIP0009 | Metadata strip, thread capture, social annotation (pointer, caption, highlight) | Automated smart crop to subject, headline text extraction, creator branding, scroll-stop visual composition |
| KFIP0011 | OCR text extraction to clipboard | OCR used as clipboard output, not as input to thumbnail text overlay |
| KFIP0010 | Compression-resilient capture | Compression quality, not composition or text overlay |

KFIP0009's social annotation tools (pointer arrow, caption label, highlight) are manual, single-use tools. They require the user to decide what to annotate and where. This KFIP proposes an *automated* pipeline that uses the screenshot's own content — via OCR from KFIP0001 — to generate the thumbnail without manual annotation steps.

---

## Goals

- Transform any captured screenshot into an X/Twitter-ready thumbnail in one automated step
- Automatically detect and crop to the most visually important region (subject-aware cropping)
- Extract headline or prominent text from the screenshot (via KFIP0001 OCR) and overlay it on the thumbnail
- Apply platform-specific optimisation: 16:9 (X feed thumbnail), 4:5 (X full-post display), 1:1 (profile/grid)
- Support optional creator branding: username handle overlay, colour border, logo badge
- Plug into the AfterCapture pipeline as a new `GenerateSmartThumbnail` task
- Work entirely offline; no cloud API dependency for core thumbnail generation

## Non-Goals

- No AI-generated artwork or illustration (uses existing screenshot content only)
- No direct posting to X/Twitter via API (user reviews and pastes manually)
- No video or GIF thumbnail support in v1
- No full image editor replacement (crop, text, frame — not Photoshop-tier)
- No cross-platform template system (X/Twitter only for v1; other platforms deferred)

---

## Proposed Solution

### 1. Smart Cropping — Subject-Aware Framing

Not all screenshots have the subject in the centre. UI elements, browser chrome, floating panels, and secondary windows all compete for visual space. The generator analyses the captured image to find the "subject region" and crops to it.

**Crop modes:**

| Mode | Behaviour | Best For |
|------|-----------|----------|
| `AutoSubject` | Detects primary content region via visual weight and edge density | General screenshots, tweets, articles |
| `CenterFrame` | Crops to centre region at target aspect ratio | Flat UI, symmetrical content |
| `TopHeavy` | Crops top 60% of captured region | Tweets with long threads, tall content |
| `TextRegion` | Uses OCR bounding box to frame text content | Document screenshots, code snippets |
| `Manual` | User drags crop region in preview step | Power users who want control |

**Subject detection algorithm (no ML required):**
1. Convert to grayscale; compute edge density via Sobel filter
2. Divide image into a 3×3 grid; score each cell by edge density + contrast variance
3. Select the highest-scoring cell(s); expand to target aspect ratio
4. If selected region is <30% of original image area, fall back to `CenterFrame`
5. Return bounding box: `{ X, Y, Width, Height }`

**Aspect ratio targets (X/Twitter-specific):**

| Target | Dimensions | Use Case |
|--------|-----------|----------|
| `XFeedThumbnail` | 1200 × 675 (16:9) | X feed preview, link cards |
| `XFullPost` | 1200 × 1500 (4:5) | Full-post image display |
| `XGrid` | 1200 × 1200 (1:1) | Profile grid, carousel |

### 2. Headline Text Extraction and Overlay

Many screenshots contain readable text — a tweet headline, a code error, a data heading — that makes a compelling thumbnail when surfaced large. This step extracts text via KFIP0001 OCR and places it as a styled overlay.

**Text extraction pipeline:**
1. Run OCR on the captured image (reuse `IOcrService.RecognizeAsync` from KFIP0001)
2. Identify the "headline" text — longest line, largest detected font size, or highest confidence block
3. If no text detected, skip text overlay (no empty text boxes)
4. Render headline onto a semi-transparent text band positioned at the bottom of the crop region

**Text overlay styles:**

| Style | Appearance | Use Case |
|-------|-----------|----------|
| `BottomBar` | Dark gradient band across bottom 25%, white text centred | General use, maximum readability |
| `TopBanner` | Dark gradient band across top 20%, white text centred | When subject is bottom-weighted |
| `Floating` | Text placed over image with subtle drop shadow, no band | Short text, high-contrast images |
| `Bracket` | Text framed by decorative brackets `[ Headline Text ]` | Minimal aesthetic |
| `None` | No text overlay | User prefers image-only |

**Typography defaults:**
- Font: system sans-serif (SF Pro on macOS, Segoe UI on Windows)
- Size: auto-scaled to fit overlay band width at readable size (min 24pt, max 72pt)
- Colour: white (#FFFFFF) with 90% opacity on gradient band
- Alignment: centre

### 3. Creator Branding Overlays

Frequent X/Twitter content creators want brand consistency across their screenshots.

**Branding options:**

| Element | Behaviour | Customisation |
|---------|-----------|--------------|
| **Handle badge** | Small pill in corner: `@username` | Handle text, background colour, position (TL/TR/BL/BR) |
| **Colour border** | 4px border around entire thumbnail in creator's brand colour | Hex colour, border width |
| **Logo badge** | Small logo/watermark in corner | Image file (PNG with transparency), position, opacity |
| **Date stamp** | Subtle date text in corner: `Jul 2026` | Format, position, colour |

**Handle badge:**
- Rendered as a rounded-rectangle pill, semi-transparent background
- Default: bottom-right corner, white text on dark background
- Creator sets their `@handle` once in Task Settings; applies to all thumbnails

**Logo badge:**
- User provides a PNG logo (≤64×64px recommended)
- Stored in settings; applied as a fixed-size overlay
- Default: top-right corner, 80% opacity

### 4. Platform Optimisation

After composition, the thumbnail is encoded for X/Twitter:

- JPEG output at quality 85 (configurable: 75–95)
- Pixel dimensions capped at 1200px on the longest edge
- If encoded size > 5MB, reduce quality in 5-point steps until ≤ 5MB
- Preserve PNG output option if user has `preferPng` flag set
- Embed extracted alt text into image metadata (EXIF XMP) for accessibility

### 5. AfterCapture Pipeline Integration

New `AfterCaptureTasks` flag:

```csharp
[Flags]
public enum AfterCaptureTasks
{
    // ...existing flags...
    GenerateSmartThumbnail = 1 << 20,  // NEW — run smart crop + text + branding + optimise
}
```

**Pipeline order:**
```
CaptureRaw
    │
    ▼
StripMetadata  [KFIP0009 — if social preset active]
    │
    ▼
SmartCrop      [NEW — subject-aware crop to target aspect ratio]
    │
    ▼
ExtractText    [KFIP0001 OCR — identify headline text]
    │
    ▼
ComposeOverlay [NEW — render text band + branding elements]
    │
    ▼
OptimiseEncode [NEW — JPEG/PNG encode per platform constraints]
    │
    ▼
SaveAndCopy    [existing: save to file, copy path/URL]
```

**Preview step:**
- After composition but before final save, show a preview modal
- User can: **[Accept]** **[Adjust Crop]** **[Change Text Style]** **[Remove Branding]** **[Cancel]**
- "Adjust Crop" enters a lightweight crop editor (pre-filled with smart crop suggestion)
- "Change Text Style" shows a style picker (BottomBar, TopBanner, Floating, Bracket, None)
- Settings accessible: gear icon opens Task Settings focused on Thumbnail panel

---

## User Stories

### Story 1: Educator Sharing a Code Snippet
**Actor:** Sarah, developer educator on X  
**Trigger:** She captures a terminal window showing an error message  
**Flow:** Region capture → `GenerateSmartThumbnail` → Auto-crop to error region → OCR extracts error headline → `BottomBar` text overlay renders headline → Handle badge added → JPEG optimised → Preview shown → Accept → File saved, path copied  
**Outcome:** A clean, branded thumbnail ready to paste into her educational thread.

### Story 2: Analyst Sharing a Data Screenshot
**Actor:** Marcus, financial analyst  
**Trigger:** He screenshots a Bloomberg chart and wants to share it on X  
**Flow:** Region capture → `AutoSubject` crop to chart area → No readable text found → `Floating` style with no text → Colour border in brand blue → Logo badge → Preview → Accept  
**Outcome:** A professional thumbnail matching his brand guidelines, no manual editing.

### Story 3: Journalist Documenting a Thread
**Actor:** Priya, investigative journalist  
**Trigger:** She captures a series of tweets for documentation  
**Flow:** Thread capture mode (KFIP0009) → Captures 4 tweets → Vertical stitch → `GenerateSmartThumbnail` on stitched image → `TopHeavy` crop focuses on most important tweet → Handle badge + date stamp → Preview → Accept  
**Outcome:** A clean thread thumbnail for her archive, not for posting.

### Story 4: Power User Batch Processing
**Actor:** Daniel, ShareX power user  
**Trigger:** He has 10 screenshots from a recording session he wants to turn into thumbnails  
**Flow:** Selects all 10 files → Invokes "Generate Thumbnails" from command palette (KFIP0007) → Batch process with same settings → Progress indicator → All 10 saved to output folder with `*-thumb.jpg` suffix → Notification lists all output paths  
**Outcome:** 10 thumbnails in <30 seconds; no individual editing required.

---

## Technical Design

### New Services

```csharp
// Subject-aware cropping
public interface ISmartCropService
{
    Task<Rectangle> DetectSubjectRegionAsync(SKBitmap source, CropMode mode,
        TargetAspectRatio target, CancellationToken ct = default);
    SKBitmap CropToRegion(SKBitmap source, Rectangle region);
}

public enum CropMode { AutoSubject, CenterFrame, TopHeavy, TextRegion, Manual }

// Text extraction and overlay composition
public interface IThumbnailComposerService
{
    Task<SKBitmap> ComposeAsync(ThumbnailCompositionRequest request,
        CancellationToken ct = default);
}

public class ThumbnailCompositionRequest
{
    public required SKBitmap Source { get; init; }
    public required ThumbnailPreset Preset { get; init; }
    public string? ExtractedHeadline { get; init; }
    public CropMode CropMode { get; init; } = .AutoSubject;
    public ThumbnailTextStyle TextStyle { get; init; } = .BottomBar;
    public CreatorBranding? Branding { get; init; }
}

public enum ThumbnailTextStyle { BottomBar, TopBanner, Floating, Bracket, None }

// Thumbnail generation task
public interface IThumbnailGeneratorService
{
    Task<ThumbnailResult> GenerateAsync(ThumbnailRequest request,
        CancellationToken ct = default);
}

// Preset management
public class ThumbnailPreset
{
    public string Id { get; init; } = "";              // "x-feed", "x-fullpost", "x-grid"
    public string Platform { get; init; } = "X";
    public TargetAspectRatio AspectRatio { get; init; }
    public CropMode DefaultCropMode { get; init; } = .AutoSubject;
    public ThumbnailTextStyle DefaultTextStyle { get; init; } = .BottomBar;
    public int MaxDimensionPx { get; init; } = 1200;
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
    public int JpegQuality { get; init; } = 85;
}

public class ThumbnailResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public SKBitmap? OutputBitmap { get; init; }
    public long FileSizeBytes { get; init; }
    public string? AltText { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### Creator Branding Model

```csharp
public class CreatorBranding
{
    public bool Enabled { get; init; }

    // Handle badge
    public string? HandleText { get; init; }           // "@username"
    public string HandleBadgePosition { get; init; } = "BR";
    public string HandleBadgeBgColor { get; init; } = "#1DA1F2";

    // Colour border
    public bool ShowBorder { get; init; }
    public string BorderColor { get; init; } = "#1DA1F2";
    public int BorderWidth { get; init; } = 4;

    // Logo
    public string? LogoPath { get; init; }              // PNG file path
    public string LogoPosition { get; init; } = "TR";
    public int LogoOpacity { get; init; } = 80;

    // Date stamp
    public bool ShowDateStamp { get; init; }
    public string DateFormat { get; init; } = "MMM yyyy";
    public string DateStampPosition { get; init; } = "BL";
}
```

### Data Flow

```
RegionCapture → SKBitmap(raw)
      │
      ▼
ISmartCropService.DetectSubjectRegionAsync()
      │  Returns Rectangle cropRegion
      ▼
SKBitmap cropped = source.CropToRegion(cropRegion)
      │
      ▼
IOcrService.RecognizeAsync(cropped) → OcrResult
      │  Headline text extracted from OcrResult
      ▼
ThumbnailCompositionRequest { cropped, preset, headline, style, branding }
      │
      ▼
IThumbnailComposerService.ComposeAsync()
      │  Renders text band, handle badge, border, logo, date
      ▼
SKBitmap composed
      │
      ▼
SKEncodedImageInfo { Format = Jpeg, Quality = preset.JpegQuality }
      │  Encode → measure bytes
      │  If > 5MB: re-encode at lower quality
      ▼
ThumbnailResult { OutputBitmap, FileSizeBytes, AltText }
```

### File Structure Changes

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── ISmartCropService.cs       [NEW]
│   ├── SmartCropService.cs         [NEW]
│   ├── IThumbnailComposerService.cs [NEW]
│   ├── ThumbnailComposerService.cs  [NEW]
│   └── IThumbnailGeneratorService.cs [NEW]
│       └── ThumbnailGeneratorService.cs [NEW]

src/desktop/core/XerahS.Core/Models/
├── ThumbnailPreset.cs              [NEW]
├── CreatorBranding.cs              [NEW]
├── ThumbnailCompositionRequest.cs   [NEW]
└── ThumbnailResult.cs              [NEW]

src/desktop/app/XerahS.UI/
├── ViewModels/
│   ├── ThumbnailPreviewViewModel.cs [NEW — preview dialog logic]
│   └── ThumbnailSettingsViewModel.cs [NEW]
├── Views/
│   ├── ThumbnailPreviewDialog.axaml [NEW]
│   └── ThumbnailSettingsPanel.axaml [NEW — settings panel for branding/crop defaults]
└── Services/
    └── AfterCaptureThumbnailIntegration.cs [NEW — wires GenerateSmartThumbnail to pipeline]
```

### Integration Points

| Component | Integration |
|-----------|-------------|
| `CaptureJobProcessor` | New `GenerateSmartThumbnail` flag triggers `PerformSmartThumbnailAsync` |
| `IOcrService` (KFIP0001) | OCR used for headline text extraction; reused, not duplicated |
| `ISocialImageOptimizer` (KFIP0005) | Thumbnail optimisation extends platform constraint logic |
| `IImageMetadataService` (KFIP0009) | Alt text from headline stored in XMP metadata after composition |
| `CaptureCommandPaletteService` (KFIP0007) | "Generate Thumbnail" surfaced as context-aware palette item |
| `IComparisonCompositor` (KFIP0009) | Comparison shot output can be passed to `ThumbnailGeneratorService` |

### Settings UI

New section in Task Settings: **Smart Thumbnails**

- **Default preset**: X Feed / X Full Post / X Grid / Custom
- **Default crop mode**: Auto / Centre / Top-heavy / Text region
- **Default text style**: Bottom bar / Top banner / Floating / Bracket / None
- **Creator handle**: `@yourhandle` (applies to all thumbnails)
- **Brand border**: colour picker, width (2/4/6px)
- **Logo**: file picker for PNG logo, position selector, opacity slider
- **Date stamp**: toggle, format picker, position
- **JPEG quality**: slider (75–95)
- **Auto-generate alt text**: toggle (default: on — uses OCR headline)

---

## Alternatives Considered

### Alternative A: Web-Based Thumbnail Generator (Canva, Crello)
**Rejected:** Requires browser, internet connection, and account. Creator workflow is interrupted. XerahS thumbnails are generated locally with zero latency and no dependency on third-party tools.

### Alternative B: ML-Based Subject Detection (TensorFlow, ONNX)
**Rejected for v1:** Adds significant binary size (~200MB model), GPU/CPU overhead, and cross-platform complexity. The edge-density + grid-scoring approach in this KFIP achieves good results for UI screenshots without any ML dependency. ML subject detection can be revisited in Phase 2 if demand exists.

### Alternative C: Manual Crop + External Editor
**Rejected:** This is the status quo this KFIP explicitly tries to eliminate. Creators already do this manually; the goal is to automate it.

### Alternative D: AI-Generated Headlines (LLM)
**Rejected:** Using OCR to extract existing visible text from the screenshot is deterministic and accurate. LLM-generated headlines would hallucinate, require API calls, introduce cost and latency, and may not reflect what the screenshot actually shows.

---

## Compatibility Notes

- **Backwards compatibility:** Adding `GenerateSmartThumbnail` as bit 20 does not affect existing flags; all existing AfterCapture tasks continue to work unchanged.
- **Cross-KFIP dependency safety:** The feature depends on KFIP0001 (OCR) for text extraction and KFIP0009 (metadata strip) for social preset integration. These KFIPs are already proposed; this KFIP depends on their Phase 1 implementation.
- **macOS/Linux:** Smart crop algorithm uses only SkiaSharp primitives; fully cross-platform. No Windows-specific APIs.
- **Performance:** Subject detection + OCR + composition should complete in <3 seconds for a 1920×1080 screenshot on a mid-range machine. Optimisation loop (JPEG quality reduction) adds at most 1 additional encode pass.

---

## Acceptance Criteria

### Functional

- [ ] `GenerateSmartThumbnail` flag triggers the full thumbnail pipeline
- [ ] `AutoSubject` crop mode correctly identifies the most visually dense region in a UI screenshot
- [ ] OCR extracts readable headline text from a screenshot containing standard UI text at 16pt or larger
- [ ] `BottomBar` text overlay renders centred white text on a dark gradient band at the bottom 25% of the image
- [ ] Handle badge renders `@username` in a styled pill at the configured corner
- [ ] Colour border renders as a 4px frame around the final thumbnail
- [ ] JPEG output at 1200px longest edge is ≤ 5MB when quality is at 85 for typical screenshots
- [ ] If JPEG > 5MB at quality 85, re-encoding at lower quality reduces file size
- [ ] Preview dialog shows composed thumbnail before save; user can adjust or cancel
- [ ] Alt text (headline text) is written to image XMP metadata on save

### Quality

- [ ] Smart crop correctly ignores browser chrome (address bar, tabs) when screenshot contains a tweet
- [ ] Text overlay font size scales correctly for headlines between 20 and 200 characters
- [ ] Creator branding elements do not overlap the text overlay band
- [ ] Thumbnail generation adds <3s latency to the AfterCapture pipeline for a 1080p capture
- [ ] Batch processing 10 thumbnails sequentially completes in <30 seconds

### Edge Cases

- [ ] Screenshot with no readable text: text overlay skipped, thumbnail saved without it
- [ ] Screenshot where subject region is < 30% of image: falls back to `CenterFrame`
- [ ] Very wide or very tall screenshots: smart crop handles extreme aspect ratios gracefully
- [ ] User provides a logo PNG > 128px: resized to max 128px before overlay
- [ ] JPEG quality reduction reaches 50% and file is still > 5MB: saves as PNG instead, notifies user
- [ ] Batch processing interrupted: partial results are saved, error list reported at end

---

## Phased Implementation

### Phase 1: Core Smart Crop and Text Overlay

- [ ] `ISmartCropService` with `AutoSubject`, `CenterFrame`, `TopHeavy`, `TextRegion` crop modes
- [ ] `IThumbnailComposerService` with `BottomBar`, `TopBanner`, `Floating`, `Bracket`, `None` text styles
- [ ] OCR headline extraction via KFIP0001 `IOcrService`
- [ ] Preview dialog with Accept / Adjust Crop / Cancel actions
- [ ] JPEG optimisation with quality reduction loop
- [ ] Built-in presets: `x-feed` (16:9), `x-fullpost` (4:5), `x-grid` (1:1)
- [ ] Settings: preset selection, crop mode, text style defaults
- [ ] Tests: crop correctness, text overlay rendering, file size compliance

### Phase 2: Creator Branding

- [ ] Handle badge (pill overlay with `@username`)
- [ ] Colour border (configurable hex colour, width)
- [ ] Logo badge (PNG overlay, position, opacity)
- [ ] Date stamp (configurable format and position)
- [ ] All branding elements integrated into composer
- [ ] Settings UI for branding options
- [ ] Tests: branding overlay positions, logo opacity, border rendering

### Phase 3: Batch Processing and Command Palette

- [ ] Multi-file selection and batch thumbnail generation
- [ ] Progress indicator for batch operations
- [ ] `CaptureCommandPaletteService` integration (KFIP0007)
- [ ] "Generate Thumbnail" palette item with preset sub-menu
- [ ] Tests: batch ordering, partial failure handling, progress reporting

### Phase 4: Advanced Crop and Polish

- [ ] Manual crop mode with draggable crop region in preview
- [ ] Per-preset default crop mode and text style settings
- [ ] Alt text field in preview for user editing
- [ ] Keyboard shortcut: `Ctrl+T` to trigger smart thumbnail on last capture
- [ ] Tests: manual crop accuracy, shortcut binding, alt text editing

---

## Open Questions

1. **Should smart crop support portrait screenshots (e.g., phone captures)?** The algorithm works for any aspect ratio, but the `TopHeavy` crop mode is UI-biased. Portrait screenshots from phone captures (X posts, Stories) may need a `BottomHeavy` mode. Recommend: add `BottomHeavy` in Phase 4 if phone-capture use cases are reported.

2. **Should the headline extraction use LLM to choose the "best" text block among multiple candidates?** v1 uses the longest/highest-confidence block. If a screenshot contains multiple text regions (e.g., sidebar + main content), the current heuristic may pick the wrong one. Consider: a "preferred region" hint from the user via crop selection. LLM re-ranking is Phase 5 material.

3. **Should batch processing be parallel or sequential?** Sequential is safer (avoids memory pressure from multiple large bitmaps in memory). Parallel with a max-concurrency limit (e.g., 2 at a time) could speed up batch operations. Recommendation: sequential for v1, add optional parallel in Phase 3 with a settings toggle.

4. **Should we support exporting thumbnail as PNG for lossless use?** Yes — add a toggle in settings: "Prefer PNG for high-quality output." PNG output ignores the 5MB X/Twitter limit (user is responsible for manual compression if posting).

5. **Should the handle badge pull from the user's X/Twitter handle if connected?** Not in scope for v1 — no X API integration. User sets handle manually in Task Settings. Future integration with X OAuth could auto-populate this field.

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Smart crop misidentifies subject on complex screenshots | Thumbnail has wrong focal point | Provide "Adjust Crop" in preview; Phase 4 adds manual crop mode |
| OCR fails on low-resolution or blurred screenshots | No text overlay, thumbnail is image-only | Skip text overlay gracefully when OCR confidence < 30% |
| JPEG quality reduction loop still exceeds 5MB | Saves PNG instead, user may not notice | Notify user explicitly: "Saved as PNG — X limit exceeded" |
| Branding overlay obscures important screenshot content | Creator badge or border hides part of the subject | Text band and branding positioned at edges; provide "None" options for all |
| Batch processing memory pressure on large screenshots | App slowdown or OOM on low-memory machines | Sequential processing; release bitmap memory after each thumbnail |

---

## Success Metrics

- **Thumbnail generation adoption**: >30% of users who use social presets generate at least one smart thumbnail within 30 days
- **Text overlay hit rate**: >60% of smart thumbnails include an extracted text overlay (OCR success)
- **Creator branding adoption**: >20% of thumbnail users configure at least one branding element within 60 days
- **Time savings**: Median time from capture to saved thumbnail < 10 seconds (vs. estimated 5–10 minutes manual)
- **File size compliance**: <3% of smart thumbnails exceed X's 5MB limit after generation
- **Batch usage**: >15% of thumbnail users process multiple screenshots in a single session

---

## Related Work

- **KFIP0001**: OCR via `IOcrService` is reused for headline text extraction
- **KFIP0005**: Social presets configure thumbnail generation defaults; `ISocialImageOptimizer` underpins optimisation
- **KFIP0007**: Command palette integration for thumbnail generation invocation
- **KFIP0009**: Metadata strip (`StripMetadata`) runs before thumbnail generation; social annotation tools are complementary manual tools
- **KFIP0010**: Compression pipeline reused for JPEG quality reduction loop
- **KFIP0011**: OCR output (clipboard) is conceptually related but outputs to clipboard; this KFIP uses OCR output as thumbnail text input

---

## References

- X/Twitter media specs (2026): 16:9 at 1200×675, 4:5 at 1200×1500, 1:1 at 1200×1200, max 5MB per image
- SkiaSharp `SKBitmap` crop and encode APIs
- KFIP0001 `IOcrService` interface and implementation
- Creator workflow pain points identified via KovaForge user research (2026-06)
