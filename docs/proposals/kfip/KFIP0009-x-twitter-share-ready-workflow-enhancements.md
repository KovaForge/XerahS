# KFIP0009: X/Twitter Screen Capture Workflow — Share-Ready Enhancements

**Status**: Proposed
**Priority**: P2
**Area**: Region Capture | AfterCapture | Image Annotation | Social Media | Privacy
**Created**: 2026-05-17
**Related**: KFIP0001 (AfterCapture OCR), KFIP0002 (Smart Region Capture Profiles), KFIP0003 (X/Twitter Context Detection), KFIP0005 (Social Sharing Workflows), KFIP0007 (Capture Command Palette), KFIP0008 (Capture Privacy Redaction)
**Owner**: KovaForge
**Co-Authors**: Nadia (analysis)

---

## Summary

X/Twitter screen capture users face a fragmented post-capture gap between "I have a screenshot" and "this is ready to post." Existing KFIPs handle upload (KFIP0005), redaction (KFIP0008), context detection (KFIP0003), and smart region selection (KFIP0002), but no KFIP addresses the **share-preparation layer**: metadata stripping before upload, before/after comparison shots, thread multi-select capture, quick social annotation (pointer + caption), and one-tap format optimization that accounts for X's 5MB JPEG limit. This KFIP fills that gap with focused, X/Twitter-specific enhancements that plug into the existing AfterCapture pipeline and KFIP0007 command palette.

---

## Problem Statement

### The Post-Capture Gap

Even with KFIP0005's upload automation and KFIP0008's redaction tools, users still perform manual work before sharing:

| Step | What Users Do | Gap |
|------|--------------|-----|
| Metadata cleanup | Manually check EXIF in file properties | No automatic strip on social capture |
| Before/after comparison | Open two captures in editor, manually annotate | No comparison-shot workflow |
| Thread capture | Screenshot each tweet separately, paste into editor | No multi-select / multi-capture thread mode |
| Quick annotation | Add pointer or caption for context | Annotation tools exist (KFIP0008 redact tools) but no lightweight pointer/label tools |
| Format check | Discover image is 6MB after trying to upload | No pre-upload size estimate with inline guidance |

### Evidence

- Scanly.co (2025) documented that Twitter/X strips EXIF metadata on upload — but users don't know their screenshots contain GPS coordinates, device identifiers, and capture timestamps. The stripping happens server-side, not by user choice.
- PostCapture, TweetPik, and TwitterShots all target "clean share-ready screenshots" as a core value prop, indicating the market understands this need — but they operate on web-rendered content, not native captures.
- KFIP0005's social presets include file size limits (5MB for X) but surface the failure only after an upload attempt, not before.
- KFIP0002 Phase 2 mentions thread capture via URL-based server-side rendering, but native multi-tweet multi-select capture in the app itself is not specced.
- KFIP0007's command palette could surface "Capture Thread" as a context-aware suggestion when a thread is detected — but no thread selection mode exists to fulfill it.

### Scope Gap vs. Existing KFIPs

| What KFIP Covers | What KFIP Does NOT Cover |
|-----------------|--------------------------|
| KFIP0005: Upload + URL copy | Pre-upload metadata strip, format pre-check, before/after comparison |
| KFIP0008: Manual redaction (blur, black box) | Lightweight annotation (pointer, caption, highlight) for social use |
| KFIP0003: Tweet compose/tweet-view detection | Multi-tweet thread capture selection mode |
| KFIP0002: Smart region detection | Thread capture sequencing and stitching |
| KFIP0007: Command palette | Execution actions for thread capture, metadata strip, comparison shot |

---

## Goals

- Strip EXIF/XMP metadata automatically when a social preset or X/Twitter capture context is detected
- Provide a pre-upload size estimate with actionable guidance when X's 5MB limit is at risk
- Add a before/after comparison capture mode that captures two regions and composites them side-by-side
- Enable thread capture: multi-select multiple tweets in a single region capture session, then stitch or present as a set
- Add lightweight social annotation tools (pointer arrow, caption label, highlight) distinct from redaction tools
- Integrate all of the above into the command palette (KFIP0007) as context-aware suggestions

## Non-Goals

- No server-side or API-based screenshot rendering (KFIP0002 covers this via URL)
- No automatic posting to X/Twitter (user reviews and pastes manually)
- No full-featured image editor (existing ShareX editor + KFIP0008 redact tools)
- No video or GIF capture for X (out of scope)
- No DOM-based element detection for thread structure (native multi-select, not auto-detection)

---

## Proposed Solution

### 1. EXIF/XMP Metadata Strip — AfterCapture Integration

New `AfterCaptureTasks` flag:

```csharp
[Flags]
public enum AfterCaptureTasks
{
    // ...existing flags...
    StripMetadata = 1 << 19,  // NEW — remove EXIF/XMP before upload or save
}
```

**Behavior:**
- When `StripMetadata` is set, run `IImageMetadataService.StripAllAsync(SKBitmap)` before the AfterCapture pipeline continues
- Strip removes: GPS coordinates, device make/model, software version, capture timestamp, XMP data, and all other EXIF tags
- Output image is a clean bitmap with no metadata
- If saving locally, file is written without metadata (EXIF chunks not written to PNG/JPEG)
- If uploading (KFIP0005), metadata is stripped before encoding

**UX Triggers (auto-enable):**
- When X/Twitter social preset (`x-tweet`, `x-thread`) is active, `StripMetadata` is auto-enabled for that capture session
- Toast notification: "Metadata stripped" (dismissible, shown briefly)

**Settings:**
- Default: OFF for generic captures, ON automatically for social presets
- Toggle in Task Settings: "Always strip metadata for social captures" (default: checked)
- Per-preset override: advanced settings allow disabling strip for specific presets

### 2. Pre-Upload Size Estimate

When using a social preset (KFIP0005), after capture and before the upload step:

1. Encode a proxy version at the preset's quality setting (JPEG 85)
2. Measure the encoded byte size
3. If size > platform limit (X = 5MB):
   - Show inline guidance before upload attempt: "Image is 6.2MB. X requires under 5MB."
   - Offer inline actions: **[Reduce quality]** **[Crop / resize]** **[Upload anyway]**
4. If size ≤ platform limit: no message, proceed

**Implementation:**
- This is an extension of the `ISocialImageOptimizer` from KFIP0005's Phase 2
- Runs synchronously before `ShareToUploader` in the AfterCapture pipeline
- Does not re-encode the final image twice — the proxy encode is used as the final encode if size is acceptable

### 3. Before/After Comparison Capture Mode

New capture mode: **Comparison Shot**

**Interaction:**
1. User invokes "Comparison Shot" from command palette (KFIP0007) or via a configured hotkey
2. Screen dims; instruction overlay: "Capture BEFORE region — click or drag"
3. User captures first region → image saved as `before`
4. Screen dims again; instruction overlay: "Capture AFTER region — click or drag" (same screen coordinates suggested as a starting hint)
5. User captures second region → image saved as `after`
6. **Compositor** combines them into one image:
   - Side-by-side (default): `[ BEFORE ] | [ AFTER ]`
   - Top/bottom (if portrait): `[ BEFORE ]` / `[ AFTER ]`
   - Labels: "Before" and "After" as subtle text overlays (optional, configurable)
   - Separator: thin vertical or horizontal line (1px, configurable color)
7. Result opens in editor for final annotation or goes directly to AfterCapture pipeline

**Configuration options:**
- Layout: side-by-side (default) or top/bottom
- Label text: "Before / After" (customizable)
- Label style: none, subtle text, or numbered badge
- Include border: none, thin border, or shadow effect

**Keyboard shortcut:** `Ctrl+Alt+C` (configurable)

### 4. Thread Capture Mode — Multi-Select

**Interaction:**
1. User invokes "Capture Thread" from command palette (context-aware: KFIP0007 surfaces it when timeline is detected)
2. Screen dims; overlay shows multi-select mode indicator: "Thread Capture Mode — Select multiple regions"
3. User clicks/drags to capture first tweet
4. Overlay shows "1 selected — Click to add more, Enter to finish"
5. User continues capturing additional tweets
6. User presses `Enter` to finish selection
7. **Thread compositor** presents options:
   - **Vertical stitch**: combine all captures into a single long image (seamless, top-to-bottom)
   - **Grid**: arrange in a 2-column grid
   - **As individual**: keep as separate files, open all in editor
   - **Slide deck**: arrange as sequential slides (one tweet per slide)
8. Result opens in editor for annotation or goes to AfterCapture pipeline

**Smart stitching:**
- Vertical stitch uses a flat-color background fill for any gap between captures (not blending — intentional spacing)
- Each capture is treated independently; no attempt to align or normalize
- A thin separator line (1px, configurable) separates each tweet

**Limits:**
- Maximum 10 captures per thread session
- If user tries to capture the 11th, toast: "Thread limit reached (10 tweets). Finish or cancel."

**Context hint (KFIP0003 integration):**
- When `IsTimelineWindow` fires with confidence > 0.7, command palette surfaces "Capture Thread" as a context suggestion
- "Capture Thread" does NOT auto-scroll — it's a manual multi-capture workflow

### 5. Lightweight Social Annotation Tools

Distinct from KFIP0008's privacy redaction tools, these are for pointing out content in a social share:

| Tool | Behavior | Icon |
|------|----------|------|
| **Pointer Arrow** | Draws a single-direction arrow with configurable color and thickness | ➡️ |
| **Caption Label** | Places a text box with configurable background color, font size, max width | 💬 |
| **Highlight** | Semi-transparent rectangular highlight (30% opacity, configurable color) | 🖍️ |

**Implementation:**
- These live in the image editor toolbar alongside KFIP0008's redact tools
- New annotation category: "Social Tools"
- Arrow tool: click-drag to set direction; arrowhead auto-sizes to stroke width
- Caption tool: click to place, type to enter text, click outside to commit
- Highlight tool: click-drag to draw a semi-transparent rectangle

**Keyboard shortcuts (in editor):**
- `S` → toggle social annotation mode
- `S` then `1` → pointer arrow
- `S` then `2` → caption label
- `S` then `3` → highlight

**Styling options:**
- Arrow: color (white, black, red, X-blue), thickness (2/4/6px)
- Caption: background color (white, black, semi-transparent), text color, font size (12/16/20pt), max width (px)
- Highlight: color (yellow, blue, pink — 30% opacity), border radius (0 or 4px)

### 6. Command Palette Integration (KFIP0007)

New palette items surfaced contextually:

| Palette Item | Context Trigger | Action |
|-------------|----------------|--------|
| `📋 Strip Metadata` | Always available | Toggle metadata strip for next capture |
| `🔄 Comparison Shot` | Always available | Trigger comparison capture mode |
| `🧵 Capture Thread` | Timeline window detected (KFIP0003) | Trigger thread capture mode |
| `📐 X/Twitter — Clean (4:5)` | x.com detected | Capture with x-tweet preset + strip metadata + auto-upload |
| `⚠️ Image too large — reduce` | Pre-upload size > 5MB | Open size reduction UI |
| `📝 Add Pointer Arrow` | Image editor open | Enter social annotation mode, arrow selected |

These items are sourced from `SocialAnnotationProvider` (new provider, Phase 2) and `ContextualCaptureProvider` (new provider, Phase 2), integrated with KFIP0007 Phase 2+.

---

## Technical Design

### New Services

```csharp
// Metadata stripping
public interface IImageMetadataService
{
    Task<SKBitmap> StripAllAsync(SKBitmap source);  // Removes all EXIF/XMP
    bool HasMetadata(SKBitmap source);              // Quick check before stripping
}

// Comparison compositor
public interface IComparisonCompositor
{
    SKBitmap ComposeSideBySide(SKBitmap before, SKBitmap after, ComparisonOptions opts);
    SKBitmap ComposeTopBottom(SKBitmap before, SKBitmap after, ComparisonOptions opts);
}

// Thread compositor
public interface IThreadCompositor
{
    SKBitmap StitchVertical(IEnumerable<SKBitmap> captures, int separatorHeight = 2);
    SKBitmap ComposeGrid(IEnumerable<SKBitmap> captures, int columns = 2);
    SKBitmap ComposeSlides(IEnumerable<SKBitmap> captures, Size slideSize);
}
```

### New Models

```csharp
public enum ComparisonLayout { SideBySide, TopBottom }

public class ComparisonOptions
{
    public ComparisonLayout Layout { get; init; } = .SideBySide;
    public string BeforeLabel { get; init; } = "Before";
    public string AfterLabel { get; init; } = "After";
    public bool ShowLabels { get; init; } = true;
    public int SeparatorSize { get; init; } = 2;
    public string SeparatorColor { get; init; } = "#FFFFFF";
}

public class ThreadCaptureSession
{
    public List<SKBitmap> Captures { get; } = [];
    public int MaxCaptures { get; } = 10;
    public bool IsComplete { get; set; }
}

public enum ThreadLayout { VerticalStitch, Grid, Individual, Slides }

public class ThreadCompositorOptions
{
    public ThreadLayout Layout { get; init; } = .VerticalStitch;
    public int GridColumns { get; init; } = 2;
    public int SeparatorHeight { get; init; } = 4;
    public string SeparatorColor { get; init; } = "#1DA1F2";  // X blue
    public Size? SlideSize { get; init; }
}

// Social annotation tools
public enum SocialAnnotationType { PointerArrow, CaptionLabel, Highlight }

public class AnnotationStyle
{
    public string Color { get; init; } = "#FFFFFF";
    public int Thickness { get; init; } = 4;
    public int FontSize { get; init; } = 16;
    public int MaxWidth { get; init; } = 300;
}
```

### File Structure Changes

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── IImageMetadataService.cs    [NEW]
│   ├── ImageMetadataService.cs      [NEW]
│   ├── IComparisonCompositor.cs     [NEW]
│   ├── ComparisonCompositor.cs      [NEW]
│   ├── IThreadCompositor.cs         [NEW]
│   └── ThreadCompositor.cs          [NEW]

src/desktop/app/XerahS.UI/
├── ViewModels/
│   ├── ThreadCaptureViewModel.cs     [NEW — manages multi-select session]
│   └── ComparisonCaptureViewModel.cs [NEW]
├── Views/
│   ├── ThreadCaptureOverlay.axaml   [NEW — multi-select UI overlay]
│   └── ComparisonCaptureOverlay.axaml [NEW — before/after capture overlay]
└── Services/
    └── AfterCaptureMetadataIntegration.cs  [NEW — runs StripMetadata task]

src/ShareX.ImageEditor/
├── Core/
│   └── AnnotationTools/
│       ├── PointerArrowTool.cs      [NEW]
│       ├── CaptionLabelTool.cs      [NEW]
│       ├── HighlightTool.cs         [NEW]
│       └── SocialAnnotationManager.cs [NEW — orchestrates social tools separately from redact]
```

### AfterCapture Task Flag

```csharp
[Flags]
public enum AfterCaptureTasks
{
    // ...existing...
    StripMetadata = 1 << 19,  // NEW
    // Note: DoPrivacyRedact = 1 << 18 from KFIP0008
}
```

### Integration Points

| Component | Integration |
|---------|-------------|
| `CaptureJobProcessor` | Call `StripMetadata` after capture, before editor/preview |
| `ISocialImageOptimizer` (KFIP0005) | Pre-upload size check, reject-before-upload |
| `CaptureCommandPaletteService` (KFIP0007) | New providers for social/thread items |
| `TweetCaptureDetector` (KFIP0003) | Context signal for thread capture suggestion |
| `ImageEditorWindow` (KFIP0008 overlap) | Social annotation tools in editor toolbar |

---

## Acceptance Criteria

### Functional

- [ ] `StripMetadata` AfterCapture task removes all EXIF/XMP/GPS data from captured image
- [ ] Metadata strip is auto-enabled when X/Twitter social preset is active
- [ ] Pre-upload size estimate runs before `ShareToUploader`; guidance shown if > 5MB
- [ ] Comparison Shot mode captures two regions and composites them side-by-side (or top/bottom)
- [ ] Comparison output includes configurable "Before" / "After" labels
- [ ] Thread Capture mode allows selecting 2–10 regions before finishing
- [ ] Thread compositor offers vertical stitch, 2-column grid, individual, and slide deck options
- [ ] Vertical stitch renders a clean seam between captures with configurable separator
- [ ] Pointer arrow tool draws a single-direction arrow in configurable color/thickness
- [ ] Caption label tool places a text box with configurable background and font size
- [ ] Highlight tool draws a 30% opacity rectangle in configurable color
- [ ] Social annotation tools (`S` key) are accessible in the image editor
- [ ] Command palette surfaces thread capture suggestion when timeline context is detected
- [ ] All social tools respect the AfterCapture pipeline (can run before upload)

### Quality

- [ ] Metadata strip completes in <50ms on 1080p image
- [ ] Thread stitch of 10 × 1080p images completes in <3s
- [ ] Social annotation tools respond instantly (no perceptible lag on draw)
- [ ] Comparison output is pixel-perfect: no anti-aliasing artifacts on separator line
- [ ] Pre-upload size estimate byte count is within 5% of actual encoded file size

### Edge Cases

- [ ] User cancels comparison shot mid-session: no partial image saved
- [ ] Thread capture exceeds 10 tweets: toast shown, user must finish or cancel
- [ ] X/Twitter context detected but user has no social preset configured: palette suggests the preset setup
- [ ] Comparison shot with mismatched aspect ratios: compositor scales to fit tallest in each column (no stretch)
- [ ] Image editor closed during thread capture session: session is cancelled, no files saved
- [ ] Pre-upload size estimate on very small image (<50KB): estimate skipped, proceed directly

---

## Phased Implementation

### Phase 1: Metadata Strip + Pre-Upload Check

- [ ] `IImageMetadataService` with full EXIF/XMP strip
- [ ] `StripMetadata` flag in `AfterCaptureTasks`
- [ ] Auto-enable strip for X/Twitter social presets
- [ ] Pre-upload size estimate in `ISocialImageOptimizer` with inline guidance UI
- [ ] Toast notification on strip completion
- [ ] Tests: metadata removal correctness, strip perf, size estimate accuracy

### Phase 2: Comparison Shot

- [ ] `ComparisonCaptureViewModel` + overlay UI
- [ ] `IComparisonCompositor` with side-by-side and top/bottom layout
- [ ] Configurable labels and separator style
- [ ] Command palette entry for "Comparison Shot"
- [ ] Keyboard shortcut: `Ctrl+Alt+C`
- [ ] Tests: compositor output quality, mismatch scaling

### Phase 3: Thread Capture Mode

- [ ] `ThreadCaptureViewModel` + multi-select overlay UI
- [ ] `IThreadCompositor` with all four layout modes
- [ ] Session management (2–10 captures, cancel, finish)
- [ ] Command palette context suggestion when timeline detected (KFIP0003)
- [ ] Max-capture enforcement with toast guidance
- [ ] Tests: stitch correctness, grid layout, slide compositor

### Phase 4: Social Annotation Tools

- [ ] `PointerArrowTool.cs`, `CaptionLabelTool.cs`, `HighlightTool.cs`
- [ ] Social annotation toolbar strip in image editor
- [ ] `S` + `1/2/3` keyboard shortcuts
- [ ] Style configuration (color, thickness, font size)
- [ ] Tests: tool output rendering, keyboard shortcut binding

### Phase 5: Command Palette Integration

- [ ] `SocialAnnotationProvider` for palette items
- [ ] `ContextualCaptureProvider` for context-aware items
- [ ] KFIP0007 Phase 2 integration for all providers
- [ ] Tests: provider data correctness, palette item execution

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Metadata strip is redundant if user doesn't use social presets | Feature is ignored | Auto-enable strip for X/Twitter context even without preset; toast confirms action |
| Thread stitch produces visually misaligned captures | User distrusts the feature | Show a preview before committing stitch; allow user to adjust or re-capture individual tweets |
| Comparison shot mode adds perceived complexity | Users use it as a two-step workaround instead of learning it | Surfacing it via command palette (fuzzy "comparison") makes it discoverable without menu navigation |
| Pre-upload size estimate re-encodes the image twice | Performance regression | Proxy encode IS the final encode when size is acceptable — no second pass |
| Social annotation tools overlap KFIP0008 redact tools | Confusing toolbar or mode confusion | Separate "Privacy" and "Social" tool groups; keyboard shortcut `R` for redact (KFIP0008), `S` for social |

---

## Open Questions

1. **Should thread capture attempt auto-scroll for timeline threads?** This would require browser extension or accessibility API support not currently in scope. Current design is manual multi-capture. Auto-scroll belongs in a future KFIP.

2. **Should comparison shot support more than 2 captures?** "Before / During / After" is a valid use case (e.g., documenting UI changes over time). The compositor could accept N captures, but the use case is less common than 2-capture. Defer to Phase 2 if demand materializes.

3. **Should metadata strip apply to saved files as well as uploads?** Currently designed for uploads. Users who share screenshots via file (Discord attachment, email) might also want clean files. Option: make it a per-preset toggle, not just an upload-specific action. Recommend: apply to uploads only for v1; add file save option if requested.

4. **Should thread capture offer a "numbered" layout for documentation?** Academic/legal documentation of tweet threads benefits from sequential numbering (1, 2, 3...) on each capture. This is a caption variant (number badge) — could be handled by the caption tool. Don't add as a separate layout mode in v1.

5. **Should comparison shot auto-detect the "after" region as the same coordinates as the "before"?** For UI change documentation, the region is typically identical. Default the "after" capture region to the "before" bounds as a starting hint, with the user able to adjust. This reduces friction for the most common comparison use case.

---

## Related Work

- **KFIP0003**: Thread capture context suggestion depends on `IsTimelineWindow` detection
- **KFIP0005**: Pre-upload size check extends `ISocialImageOptimizer`; `ShareToUploader` depends on clean metadata
- **KFIP0007**: All social/thread capture modes are palette-invokable items; Phase 2 provider integration
- **KFIP0008**: Social annotation tools (pointer, caption, highlight) are distinct from redact tools but share the editor toolbar
- **KFIP0002**: Thread capture via URL is server-side; native multi-select thread capture (this KFIP) is a complement, not a replacement

---

## Success Metrics

- **Thread capture adoption**: >20% of users who capture X/Twitter content use thread capture when capturing 2+ tweets in a session (measured via capture session metadata)
- **Metadata strip coverage**: >80% of social preset uploads have metadata stripped (measured via strip flag presence in capture history)
- **Pre-upload check effectiveness**: <5% of social preset uploads fail due to file size (vs. current failure rate which is unmeasured but assumed high from KFIP0005 review)
- **Comparison shot usage**: >10% of users who open the image editor use comparison shot within 30 days
- **Social annotation tool usage**: >25% of users who enable social presets use at least one annotation tool