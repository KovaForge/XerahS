# KFIP0010: X/Twitter Compression-Resilient Capture and Format Optimization

**Status**: Proposed
**Priority**: P1
**Area**: Region Capture | AfterCapture | Image Encoding | Social Media | Accessibility
**Created**: 2026-06-07
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0007 (Capture Command Palette), KFIP0008 (Capture Privacy Redaction), KFIP0009 (X/Twitter Share-Ready Workflow)
**Owner**: KovaForge
**Co-Authors**: Nadia (analysis)

---

## Summary

X/Twitter's aggressive image compression destroys text-heavy screenshots, causing blurry text, color banding, and generation loss on re-shares. While KFIP0009 addressed pre-upload size checks and metadata stripping, no KFIP tackles the root cause: screenshots are not encoded for X's compression pipeline. This KFIP proposes a compression-aware capture pipeline that selects optimal formats (PNG, WEBP), applies content-aware encoding presets, provides a pre-upload compression preview, detects generation loss when re-capturing compressed content, and suggests alt text for accessibility — all integrated into the existing AfterCapture pipeline and KFIP0007 command palette.

---

## Problem Statement

### The Compression Destruction Pipeline

| Stage | What Happens | User Pain |
|-------|-------------|-----------|
| Capture | Screenshot saved as high-quality JPEG | First lossy encode, often suboptimal for target platform |
| AfterCapture | No format or quality optimization | Suboptimal encoding for X's specific compression engine |
| Upload to X | X re-encodes aggressively (JPEG ~75, 2048px max) | Double compression; text becomes blurry and illegible |
| Re-share | User screenshots the tweet containing the image | Triple compression; fine details destroyed |
| Alt text | User forgets or skips | Accessibility failure; screen-reader users excluded |

### Evidence

- **Current user reports (2026)** confirm X/Twitter applies strong lossy compression, especially for text-heavy images, causing "blurry text" and "color banding." Users describe the compression algorithm as overly destructive to sharp text and fine details.
- **Common user workarounds** include: resizing before upload, converting to PNG or WEBP, uploading via x.com instead of the mobile app, or hosting externally and linking. These are manual, inconsistent, and require platform knowledge.
- **Repeated sharing/reposting** adds another layer of compression, making text progressively blurrier with each generation.
- **Alt text on X** is critical for accessibility (screen readers, low-vision users) but is frequently skipped for screenshots because it requires manual effort and creative description. X supports up to 1,000 characters of alt text per image, but adoption for screenshots remains low.
- KFIP0009 introduced a pre-upload size estimate but did not address the quality degradation that happens *within* the size limit. A 4.9MB JPEG can still be compressed into unreadability.

### Scope Gap vs. Existing KFIPs

| Existing KFIP | What It Covers | What This KFIP Adds |
|--------------|----------------|---------------------|
| KFIP0005 | Upload automation, social presets, URL copy | Format-aware encoding *before* upload; platform-specific encoder profiles |
| KFIP0007 | Command palette for capture modes | "Preview Compression," "Suggest Alt Text" actions |
| KFIP0008 | Privacy redaction (blur, black box) | Quality-aware output that preserves readability after redaction |
| KFIP0009 | Metadata strip, size check, comparison shot, thread capture | Compression resilience, generation-loss protection, alt text generation |

---

## Goals

- Automatically select the optimal image format for X based on captured image content (text vs. photo vs. mixed)
- Apply content-aware encoding presets that preemptively minimize damage from X's re-compression pipeline
- Show a pre-upload compression preview so users can see how X will degrade their image before committing
- Detect and warn about generation loss when capturing a screenshot that already contains a compressed image (e.g., a tweet with an embedded photo)
- Generate or suggest alt text for screenshots to improve accessibility and reduce friction
- Integrate all of the above into the AfterCapture task pipeline and the command palette (KFIP0007)

## Non-Goals

- No full AI image description (basic OCR + UI heuristics for alt text only; LLM integration deferred)
- No server-side or API-based screenshot rendering (KFIP0002 covers this)
- No automatic posting to X/Twitter (user reviews and pastes manually)
- No video or GIF optimization (out of scope)
- No DOM-based element detection for thread structure (manual capture workflows)

---

## Proposed Solution

### 1. Smart Format Selector

Analyze the captured image to choose the upload format that best survives X's compression:

| Content Classification | Recommended Format | Rationale |
|-------------------------|-------------------|-----------|
| Text-heavy (UI, code, terminal, documents) | PNG (8-bit if <256 colors, else 24-bit) | Lossless; text edges survive aggressive re-compression |
| Photo/gradient-heavy (photos, game screenshots, gradients) | JPEG 90, 4:2:0 subsampling | Acceptable quality loss; smaller file size |
| Mixed content (web pages with images + text) | WEBP (quality 85, method 4) | Best compression-to-quality tradeoff |
| Contains previously compressed JPEG artifacts | PNG (if text is present) | Prevent generation loss from stacking compression |

**Content Analysis Algorithm:**
- `IImageContentAnalyzer` runs a lightweight edge-detection and color-diversity pass on the captured `SKBitmap`
- **Text-heavy signal**: >40% of image area contains high-contrast edges (Canny edge detection) + low color diversity (<256 dominant colors)
- **Photo-heavy signal**: >60% smooth gradients + high color diversity (>10,000 distinct colors)
- **Mixed**: Neither threshold met; default to WEBP
- **Compressed-artifact signal**: 8×8 DCT blockiness detected + chroma bleeding in flat regions (Generation Loss Detector, see §4)

**User Override:**
- Always available in AfterCapture task settings and per social preset
- If SmartFormatSelect is disabled, fallback to preset's existing format setting

### 2. Platform Encoding Presets

Per-platform encoder settings that preemptively optimize for the target platform's display and compression behavior:

**X/Twitter Preset (`x-twitter`):**

| Parameter | Text-Heavy (PNG) | Photo (JPEG) | Mixed (WEBP) |
|-----------|-----------------|--------------|--------------|
| Format | PNG | JPEG | WEBP |
| Quality / Depth | 8-bit palette (if possible) or 24-bit RGB | Quality 90 | Quality 85 |
| Chroma subsampling | N/A (lossless) | 4:2:0 | 4:2:0 |
| Max dimension | 2048 px (X's display limit) | 2048 px | 2048 px |
| Metadata | Strip all (KFIP0009) | Strip all | Strip all |
| Color profile | sRGB | sRGB | sRGB |

**Rationale for 2048px max dimension:**
X resizes images server-side for display. By pre-sizing to X's known display limit, we prevent X's server from applying its own (lower-quality) Lanczos or bilinear resize. This is a key factor in preserving text sharpness.

**Fallback logic:**
- If PNG output exceeds X's 5MB limit (KFIP0009 size check), automatically re-encode as WEBP with quality stepped down by 5 until under limit or quality floor (70) is reached
- If still over limit, show inline guidance: "Image too large even for WEBP. Consider crop or external host."

### 3. Pre-Upload Compression Preview

Before upload, provide a split-pane preview that simulates X's compression pipeline:

**UI:**
- Left pane: optimized capture (what user will upload)
- Right pane: simulated X compression (what followers will actually see)
- Quality metrics below panes:
  - **Text readability score**: OCR confidence on simulated image vs. original (%)
  - **File size**: original optimized size vs. X's served size estimate
  - **Format used**: PNG / JPEG / WEBP
- If readability score drops below 80%, highlight in amber with suggestion: "Text may become blurry. Try PNG format."
- Action buttons: **[Upload anyway]** **[Switch to PNG]** **[Cancel]**

**Implementation:**
- `ICompressionSimulator` applies an X-equivalent compression to a disposable copy:
  - JPEG quality 75, 4:2:0 subsampling, 2048px max dimension, sRGB
  - This approximates X's known server-side processing
- Runs asynchronously in a background task; preview shown when ready
- For PNG inputs, simulation also runs through X's PNG→JPEG conversion path (as X converts PNG to JPEG for serving)

**Trigger:**
- Optional: invoked from command palette (`👁️ Preview Compression`)
- Auto-show: when SmartFormatSelect classifies as text-heavy and user has enabled "Auto-preview for text captures" (default: off in v1)

### 4. Generation Loss Detection

When capturing a region that contains an already-compressed image (e.g., screenshot of a tweet that itself contains a photo):

**Detection Algorithm:**
- Analyze captured region for JPEG compression artifacts:
  - 8×8 DCT block boundary detection via frequency analysis
  - Chroma subsampling bleed in flat-color regions
  - Quantization artifact score (0–100); threshold >30 flags as "previously compressed"
- `IGenerationLossDetector.HasArtifacts(SKBitmap)` returns true/false with confidence score

**UX:**
- If detected and image contains text-heavy regions (per `IImageContentAnalyzer`):
  - Overlay toast: "⚠️ This screenshot contains a compressed image. Re-sharing will reduce quality."
  - Inline actions: **[Capture as PNG]** **[Continue with current format]** **[Open preview]**
- If detected but image is photo-only: silent — generation loss is acceptable for photos

**Use case:**
User is composing a thread about a visual bug. They screenshot a tweet that contains a photo of the bug. Without this warning, the resulting screenshot gets JPEG-encoded by XerahS, then re-JPEG-encoded by X. The bug details become illegible. PNG capture preserves the embedded photo's existing quality.

### 5. Alt Text Suggestion

Generate accessible descriptions for screenshots to improve X/Twitter sharing inclusivity:

**Implementation:**
- `IAltTextGenerator` extracts visible text and UI context from the capture:
  - Runs existing OCR pipeline (KFIP0001) to extract all readable text
  - Detects UI context: browser address bar (URL), application title bar, terminal prompt, code editor
  - Heuristic templates:
    - If URL contains `x.com` or `twitter.com`: "Screenshot of X/Twitter showing [first 100 chars of visible text]"
    - If code detected: "Code snippet showing [language hint] [first 80 chars]"
    - If terminal: "Terminal output showing [first 80 chars]"
    - If generic UI: "Screenshot of [app name] showing [first 100 chars of text]"
- Presents suggestion in AfterCapture dialog in an editable text field
- One-click actions: **[Use suggestion]** **[Write my own]** **[Skip for now]**
- If user selects "Use suggestion," alt text is copied to clipboard alongside the image file path for easy paste into X's ALT field

**Scope:**
- v1: OCR + heuristics only
- Future: local lightweight vision model for richer descriptions (deferred to future KFIP)

**Privacy:**
- Alt text generation runs locally; no text or image data leaves the device

### 6. AfterCapture Pipeline Integration

New `AfterCaptureTasks` flags:

```csharp
[Flags]
public enum AfterCaptureTasks
{
    // ...existing flags...
    // KFIP0009: StripMetadata = 1 << 19
    SmartFormatSelect   = 1 << 20,  // NEW — analyze and select optimal format
    CompressForPlatform = 1 << 21,  // NEW — apply platform-specific encoding preset
    GenerateAltText     = 1 << 22,  // NEW — suggest accessible description
}
```

**Pipeline execution order:**
1. Capture → raw `SKBitmap`
2. `StripMetadata` (KFIP0009)
3. `DoPrivacyRedact` (KFIP0008) — if enabled
4. `SmartFormatSelect` → determines format (PNG / JPEG / WEBP)
5. `CompressForPlatform` → encodes with platform preset (X/Twitter)
6. `PreUploadSizeCheck` (KFIP0009) → verify under 5MB, offer guidance if over
7. `GenerateAltText` → suggest description
8. `ShareToUploader` (KFIP0005) → copy to clipboard / open upload dialog

**Auto-enable rules:**
- When X/Twitter social preset is active, `SmartFormatSelect` + `CompressForPlatform` are auto-enabled
- `GenerateAltText` is auto-enabled when X/Twitter preset is active and user has checked "Suggest alt text for social captures" (default: checked)

### 7. Command Palette Integration (KFIP0007)

New palette items:

| Palette Item | Context Trigger | Action |
|-------------|----------------|--------|
| `🎨 Optimize for X` | Image in AfterCapture / editor | Runs SmartFormatSelect + CompressForPlatform on current capture |
| `👁️ Preview Compression` | Always available when capture pending | Opens compression preview window |
| `📝 Suggest Alt Text` | Capture contains detectable text | Generates and shows alt text suggestion |
| `⚠️ Check Generation Loss` | Capture contains image region | Runs generation-loss analysis and shows result |
| `🖼️ Force PNG` | Always available | Overrides SmartFormatSelect to PNG for next save/upload |

These items are sourced from `CompressionAwareProvider` (new provider, Phase 2), integrated with KFIP0007 Phase 2+.

---

## Technical Design

### New Services

```csharp
// Content analysis
public interface IImageContentAnalyzer
{
    ImageContentType Analyze(SKBitmap image);
}

public enum ImageContentType { TextHeavy, PhotoHeavy, Mixed, Precompressed }

// Platform encoding
public interface IPlatformEncoder
{
    byte[] Encode(SKBitmap image, PlatformPreset preset, ImageContentType contentType);
}

public class PlatformPreset
{
    public string PlatformId { get; init; } = "x-twitter";
    public int MaxDimension { get; init; } = 2048;
    public int SizeLimitBytes { get; init; } = 5 * 1024 * 1024; // 5MB
    public Dictionary<ImageContentType, EncoderSettings> Settings { get; init; } = [];
}

public class EncoderSettings
{
    public string Format { get; init; } = "png"; // png, jpeg, webp
    public int Quality { get; init; } = 90;       // for lossy formats
    public string? ChromaSubsampling { get; init; } // "4:2:0", "4:2:2", "4:4:4"
}

// Compression preview
public interface ICompressionSimulator
{
    Task<CompressionPreview> SimulateAsync(SKBitmap source, PlatformTarget target);
}

public class CompressionPreview
{
    public SKBitmap SimulatedImage { get; init; } = null!;
    public double OriginalReadabilityScore { get; init; }
    public double SimulatedReadabilityScore { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long SimulatedSizeBytes { get; init; }
}

// Generation loss detection
public interface IGenerationLossDetector
{
    GenerationLossResult Analyze(SKBitmap image);
}

public class GenerationLossResult
{
    public bool HasArtifacts { get; init; }
    public double Confidence { get; init; } // 0.0–1.0
    public bool ContainsText { get; init; }
}

// Alt text generation
public interface IAltTextGenerator
{
    Task<AltTextSuggestion> GenerateAsync(SKBitmap image);
}

public class AltTextSuggestion
{
    public string Text { get; init; } = "";
    public double Confidence { get; init; }
    public string Source { get; init; } = "ocr"; // ocr, heuristic, user
}
```

### New Models / Configuration

```csharp
public class CompressionAwareOptions
{
    public bool AutoEnableForSocialPresets { get; init; } = true;
    public bool AutoPreviewForTextHeavy { get; init; } = false; // v1 default off
    public bool SuggestAltText { get; init; } = true;
    public int ReadabilityWarningThreshold { get; init; } = 80; // percent
    public int GenerationLossThreshold { get; init; } = 30; // artifact score
    public int PngSizeFallbackQualityFloor { get; init; } = 70; // WEBP quality floor
}
```

### File Structure Changes

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── IImageContentAnalyzer.cs          [NEW]
│   ├── ImageContentAnalyzer.cs            [NEW]
│   ├── IPlatformEncoder.cs                [NEW]
│   ├── PlatformEncoder.cs                 [NEW]
│   ├── ICompressionSimulator.cs           [NEW]
│   ├── CompressionSimulator.cs            [NEW]
│   ├── IGenerationLossDetector.cs         [NEW]
│   ├── GenerationLossDetector.cs          [NEW]
│   ├── IAltTextGenerator.cs               [NEW]
│   └── AltTextGenerator.cs                [NEW]

src/desktop/app/XerahS.UI/
├── ViewModels/
│   ├── CompressionPreviewViewModel.cs       [NEW]
│   └── AltTextSuggestionViewModel.cs      [NEW]
├── Views/
│   ├── CompressionPreviewWindow.axaml     [NEW — split-pane preview]
│   └── AltTextSuggestionOverlay.axaml     [NEW — editable suggestion UI]
└── Services/
    └── CompressionAwarePipeline.cs        [NEW — orchestrates tasks 20-22]

src/ShareX.ImageEditor/
└── Core/
    └── CompressionAwareToolbarItems.cs     [NEW — editor toolbar buttons]
```

### Integration Points

| Component | Integration |
|-----------|-------------|
| `CaptureJobProcessor` | Inserts SmartFormatSelect + CompressForPlatform after redaction, before size check |
| `ISocialImageOptimizer` (KFIP0005) | Consumes platform-encoded output; size check runs on encoded bytes |
| `CaptureCommandPaletteService` (KFIP0007) | `CompressionAwareProvider` contributes palette items |
| `IImageMetadataService` (KFIP0009) | StripMetadata runs before format selection |
| `ImageEditorWindow` | "Optimize for X" toolbar button + alt text suggestion panel |
| `OCRService` (KFIP0001) | Reused for readability scoring and alt text extraction |

---

## Acceptance Criteria

### Functional

- [ ] `SmartFormatSelect` correctly classifies text-heavy vs. photo-heavy screenshots (>90% accuracy on an internal test set of 100 captures)
- [ ] `PlatformEncoder` produces PNG for text-heavy, JPEG for photos, WEBP for mixed — verified by file signature
- [ ] Pre-upload compression preview shows X-equivalent compressed image within 2 seconds for 1080p captures
- [ ] Readability score difference between original and simulated is calculated and displayed accurately
- [ ] `GenerationLossDetector` flags images with visible JPEG artifacts (tested on known compressed images)
- [ ] `AltTextGenerator` produces a readable suggestion for 80% of screenshots containing detectable text
- [ ] All new AfterCapture tasks integrate into the pipeline without breaking existing tasks (KFIP0001–KFIP0009)

### Quality

- [ ] Text-heavy screenshots uploaded to X remain readable after X compression (measured via OCR confidence >90% on the served image vs. <70% without optimization)
- [ ] Photo screenshots file size <2MB after platform optimization
- [ ] Compression preview generation completes in <2s for 1080p images on a modern desktop
- [ ] Alt text generation completes in <1s for 1080p images
- [ ] Generation loss detection runs in <500ms for 1080p images
- [ ] No perceptible UI lag during AfterCapture pipeline execution

### Edge Cases

- [ ] Very small images (<100px on longest side) bypass analysis and default to PNG
- [ ] Images with no detectable text skip alt text suggestion gracefully with "No text detected" message
- [ ] User format override persists per preset and survives app restart
- [ ] Compression preview falls back to original image display if simulation fails
- [ ] PNG output >5MB triggers WEBP fallback; if still >5MB, inline guidance is shown (no crash)
- [ ] Generation loss detection on a pure photo (no text) does not show a warning
- [ ] Alt text suggestion field supports up to 1,000 characters (matching X's limit)
- [ ] Pipeline cancellation mid-execution leaves no temporary files

---

## Phased Implementation

### Phase 1: Smart Format Selector + Platform Encoder

- [ ] `IImageContentAnalyzer` with edge detection + color diversity heuristics
- [ ] `PlatformEncoder` with X/Twitter presets (PNG, JPEG, WEBP branches)
- [ ] `AfterCaptureTasks.SmartFormatSelect` and `CompressForPlatform` flags
- [ ] Auto-enable for X/Twitter social presets
- [ ] Settings UI: thresholds, per-preset overrides, PNG fallback rules
- [ ] Tests: classification accuracy, encoder output correctness, performance benchmarks

### Phase 2: Compression Preview

- [ ] `ICompressionSimulator` with X-equivalent JPEG re-encode
- [ ] `CompressionPreviewWindow` split-pane UI (Avalonia)
- [ ] Readability score calculation (OCR confidence comparison)
- [ ] Command palette integration: `👁️ Preview Compression`
- [ ] Tests: preview accuracy, performance, fallback behavior

### Phase 3: Generation Loss Detection

- [ ] `IGenerationLossDetector` with DCT artifact + chroma bleed detection
- [ ] Warning toast overlay UI
- [ ] Integration with SmartFormatSelect: auto-promote to PNG when loss + text detected
- [ ] Command palette integration: `⚠️ Check Generation Loss`
- [ ] Tests: detection accuracy on synthetic and real compressed images

### Phase 4: Alt Text Generation

- [ ] `IAltTextGenerator` with OCR + heuristic templates
- [ ] Editable suggestion overlay UI
- [ ] Clipboard integration: copy suggestion alongside image path
- [ ] Command palette integration: `📝 Suggest Alt Text`
- [ ] Tests: suggestion relevance, performance, edge cases (no text, foreign language)

### Phase 5: Command Palette + Polish

- [ ] `CompressionAwareProvider` for KFIP0007 palette integration
- [ ] Additional platform presets (LinkedIn, Discord, Bluesky) using same encoder framework
- [ ] User-configurable sensitivity thresholds
- [ ] Telemetry: format adoption rates, readability retention, alt text usage
- [ ] Documentation: user guide for compression-aware captures

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Content analysis adds perceptible latency to AfterCapture pipeline | Medium | Run async where possible; cache edge-detection results; skip analysis for small images; target <200ms total |
| PNG files exceed X's 5MB limit, causing upload failure | Low | WEBP fallback with quality stepping; inline guidance before upload attempt (KFIP0009 size check integration) |
| Alt text heuristics produce irrelevant or noisy suggestions | Low | Always editable, never automatic; low-confidence suggestions show "Review recommended" badge |
| Generation loss false positives annoy users | Medium | Tunable threshold (default 30); only warn when text is present; photo-only captures silently pass |
| Users confused by new format auto-selection (expected JPEG, got PNG) | Low | Toast shows selected format and reason: "PNG selected for sharp text"; user can override in settings |
| Compression preview UI adds clutter | Low | Default off in v1; invoked via palette or auto-shown only for text-heavy with user opt-in |

---

## Open Questions

1. **Should we support X's occasional 4K image upload mode?** X periodically allows higher-resolution uploads. This could be a per-preset toggle that raises `MaxDimension` to 4096px for specific capture types. Defer to Phase 5 unless user demand is clear.

2. **Should alt text generation use a local vision model instead of OCR + heuristics?** A small local VLMs (e.g., Phi-3-vision, Moondream) could produce richer descriptions. For v1, OCR + heuristics keeps the feature lightweight and offline-only. A future KFIP can upgrade to VLM if descriptions prove too limited.

3. **Should the compression preview be mandatory for all social captures?** Making it mandatory could train users to think about quality but would slow the workflow. Current design: optional (palette-invoked) with auto-show opt-in for text-heavy captures. Revisit after Phase 2 telemetry.

4. **Should generation loss detection apply to screenshots of video content?** Videos use different compression (H.264/H.265) than JPEG. The DCT artifact detector may not fire reliably. For v1, scope is JPEG-artifact detection only; video screenshot warnings deferred.

5. **Should we add a "re-compression counter" metadata tag?** Track how many times an image has been through lossy compression (1 = original, 2 = screenshot of compressed, etc.). Could be stored in XerahS capture history for user awareness. Interesting but not critical for v1.

---

## Related Work

- **KFIP0001**: OCR pipeline reused for readability scoring and alt text extraction
- **KFIP0005**: `ISocialImageOptimizer` and upload pipeline consume platform-encoded output; size check runs after encoding
- **KFIP0007**: Compression-aware actions (`Preview Compression`, `Suggest Alt Text`) are palette-invokable items
- **KFIP0008**: Redaction tools operate on raw `SKBitmap` before SmartFormatSelect runs; output quality must remain high
- **KFIP0009**: Metadata strip and size check run immediately before this KFIP's format selection; comparison shot and thread capture outputs benefit from compression-aware encoding

---

## Success Metrics

- **Format optimization adoption**: >60% of X-targeted captures use the SmartFormatSelect-optimized format (telemetry via capture history)
- **Text readability retention**: >90% OCR confidence on X-served images when SmartFormatSelect chose PNG, vs. <70% baseline without optimization (measured via simulated compression)
- **Pre-upload preview usage**: >20% of users who capture text-heavy content open the compression preview within 30 days
- **Alt text usage**: >30% of screenshots shared to X include alt text when GenerateAltText is enabled (measured by user acceptance of suggestions)
- **Generation loss warnings acted upon**: >50% of users who see a generation-loss warning switch to PNG or open the preview
- **Upload failure rate**: <2% of X-targeted uploads fail due to file size or quality rejection (vs. unmeasured but assumed higher baseline)