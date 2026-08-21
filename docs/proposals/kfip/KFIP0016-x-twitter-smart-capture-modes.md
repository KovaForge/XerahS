# KFIP0016: X/Twitter Smart Capture Modes

**Status**: Draft  
**Priority**: P1  
**Area**: Region Capture | AfterCapture | X/Twitter | Auto-Detection | Intelligent Capture  
**Created**: 2026-07-26  
**Submitter**: Nadia (Research, KovaForge)  
**Co-Authors**: McoreD <195468996584275968@users.noreply.github.com>, vladislava-kova-kf  
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0011 (OCR-to-Clipboard), KFIP0013 (Smart Thumbnail Generation), KFIP0014 (Screen Capture for X/Twitter — Power User Workflows), KFIP0015 (Screenshot Annotation Toolkit)

---

## Summary

X/Twitter power users capture many different types of content — code snippets, data visualisations, tweet screenshots, full web pages, and plain-text posts — yet every capture type currently requires the user to manually select the right preset, annotation tools, format, and upload destination. Existing KFIPs handle individual concerns well (KFIP0009's metadata strip, KFIP0010's compression resilience, KFIP0014's X-specific preset, KFIP0015's annotation toolkit), but no KFIP provides the **intelligent orchestration layer** that auto-detects what is being captured and routes it through the optimal pipeline with X/Twitter-specific optimisations applied automatically.

This KFIP proposes **Smart Capture Modes**: a unified auto-detection engine that classifies captured content into one of four types (Code Snippet, Image/Photo, Text/Article, Full-Page), selects the optimal capture and post-processing pipeline for each, and applies X/Twitter-specific optimisations (compression pre-compensation, aspect ratio guidance, auto-hashtag suggestions, metadata handling) without requiring the user to know which preset or tool to select. It is the meta-layer that makes all existing X/Twitter KFIP work composable under a single, frictionless capture intent.

---

## Problem Statement

### The Capture-Type Tax

Users performing X/Twitter-ready captures today pay a cognitive and mechanical tax on every capture:

| What is captured | What the user must manually do | Friction |
|-----------------|-------------------------------|---------|
| Code snippet (terminal, IDE, docs) | Select region, pick format (PNG), apply anti-aliasing fix, upload | High — format mismatch kills legibility after X re-encodes |
| Data visualisation (chart, graph) | Select region, pick aspect ratio, check file size, annotate key data point | Medium — no guidance on what X prefers |
| Tweet screenshot | Activate X/Twitter preset, annotate, strip metadata, check size, upload | Medium — preset exists but must be manually selected |
| Full article or thread | Select region, decide whether to scroll-capture, handle multi-image | High — no intelligent fall-back for full-page vs. region |
| Plain text screenshot | Capture, run OCR, copy to clipboard, paste into alt text field | Low-medium — KFIP0011 partially solves this but requires manual trigger |

The root problem is that **the system does not know what kind of content is being captured**, so the user must always decide. This creates friction even for repeat workflows — a developer sharing terminal output ten times per day must manually invoke the correct preset each time, or accept degraded quality.

### Evidence

**Market signal from competing tools:**
- TwitterShots, TweetPik, Pikaso, and WebSniply all provide **type-specific** screenshot experiences: URL input yields styled tweet screenshots, thread URLs yield multi-tweet PDFs, and code URLs yield syntax-highlighted images. The market has validated that **content-type awareness improves output quality and reduces steps**.
- WebSniply explicitly markets "social media post extraction" as a separate workflow from "full-page scrolling" — acknowledging that different content types need different capture pipelines.
- Savvyshot identifies "plain screenshots don't stand out in crowded social media feeds" as a core content-creator pain point, and addresses it with annotation overlays and background formatting applied post-capture — but without any type-awareness at the point of capture.

**X/Twitter-specific compression and metadata evidence:**
- KFIP0010 documented that X re-encodes all images through a JPEG pipeline that destroys text legibility — particularly damaging for code snippets and data visualisations.
- KFIP0009 documented that X strips EXIF metadata server-side, but users do not know their screenshots contain GPS, device ID, and timestamp data before upload.
- KFIP0014 documented that X's preferred aspect ratio (4:5 vertical) and 5MB file limit are enforced only at upload time, causing failed uploads and suboptimal feed display.

**Existing KFIP coverage gap — the orchestration layer is missing:**

| Existing KFIP | What It Does | What It Does NOT Do |
|---|---|---|
| KFIP0005 | Social presets, upload automation | Auto-detect which preset applies |
| KFIP0009 | Metadata strip, thread multi-select, size check | Route capture based on type before these tasks run |
| KFIP0010 | Format selection, compression pre-softening | Detect content type to select format strategy |
| KFIP0011 | OCR-to-clipboard for alt text | Trigger automatically based on detected text content |
| KFIP0013 | Smart thumbnail generation | Auto-enable when visual content is detected |
| KFIP0014 | X-specific capture mode preset | Auto-activate when X/Twitter context is detected |
| KFIP0015 | Annotation toolkit | Offer relevant tools based on detected capture type |

No existing KFIP provides the **type detection → pipeline routing → X/Twitter optimisation** orchestration that composes all of the above into a single, zero-configuration capture intent.

---

## Goals

- Automatically detect the content type of any region capture and route it through the optimal processing pipeline — no manual preset selection required for common capture types
- Apply X/Twitter-specific optimisations (compression pre-compensation, aspect ratio guidance, metadata handling, auto-hashtag suggestions) **after** type detection, without requiring user configuration
- Provide a Smart Capture Mode indicator in the overlay UI showing detected type, confidence, and which pipeline will run
- Reduce capture-to-share time for X/Twitter from ~45 seconds (multi-tool manual) to under 10 seconds (intelligent auto-route)
- Enable intelligent fallback: when X/Twitter context is detected but no specific preset is active, the Smart Capture engine routes the capture through the most appropriate existing pipeline
- Support manual override at every detection decision point — the user always controls the outcome

## Non-Goals

- No AI-generated content, summarisation, or rewriting (OCR + heuristics only)
- No server-side or API-based screenshot rendering (native capture only; URL-based rendering is KFIP0002's scope)
- No automatic posting to X/Twitter (user reviews and pastes manually)
- No training or deployment of custom ML models (lightweight heuristic classifiers only)
- No video or GIF capture in v1
- No cross-platform generic smart capture (X/Twitter-specific in v1; other platforms deferred to Phase 2)

---

## Proposed Solution

### Core Concept

A **Smart Capture Engine** (`ISmartCaptureEngine`) runs as a lightweight post-capture step before the AfterCapture pipeline begins. It analyses the captured `SKBitmap` (and optionally the source URL or window title if available) to classify the content into one of four types, then sets pipeline flags and configuration values that downstream AfterCapture tasks consume.

```
Region Capture → Smart Capture Engine → Pipeline Flags → AfterCapture Tasks → Upload/Save
                        ↓
              [Type Detected + Confidence]
              [Auto-Hashtag Suggestions]
              [Aspect Ratio Recommendation]
              [Format Override]
```

### 1. Content Type Classification

The engine classifies captures into four primary types:

| Type | Detection Signals | Default Format | X/Twitter Pipeline |
|------|-----------------|---------------|-------------------|
| **Code Snippet** | Monospace font detection, high-contrast edge density, line-number patterns, dark background with bright text | PNG (lossless, preserves text edges) | KFIP0010 text-heavy pipeline + no pre-softening (lossless is better for code) |
| **Image / Photo** | High color diversity (>10,000 distinct colors), low edge density relative to area, photographic gradient patterns | JPEG @ 90 or WEBP @ 85 | KFIP0010 photo pipeline + compression pre-softening |
| **Text / Article** | Dense text regions detected by OCR, low color diversity, structured layout (paragraphs, columns) | PNG (text legibility) | KFIP0011 OCR-to-clipboard auto-triggered + KFIP0009 metadata strip |
| **Full-Page / UI** | Browser chrome signals (URL bar pixels, scrollbar presence, menu regions), mixed content (text + image + UI elements) | PNG or WEBP depending on content mix | KFIP0009 thread/multi-capture guidance + KFIP0013 smart crop if thumbnail requested |

**Detection Algorithm (heuristic, no ML required):**

```csharp
public enum CaptureContentType
{
    Unknown,
    CodeSnippet,
    ImagePhoto,
    TextArticle,
    FullPageUI,
}

public class CaptureContentAnalysis
{
    public CaptureContentType Type { get; init; }
    public float Confidence { get; init; }  // 0.0 – 1.0
    public string RecommendedAspectRatio { get; init; }
    public string RecommendedFormat { get; init; }
    public string[] AutoHashtagSuggestions { get; init; }
    public string[] CompressionWarnings { get; init; }
}

public interface ISmartCaptureEngine
{
    Task<CaptureContentAnalysis> AnalyseAsync(SKBitmap bitmap,
        CaptureSourceMetadata? sourceMetadata = null,
        CancellationToken ct = default);
}
```

**Detection heuristics (all run on the `SKBitmap` in-memory; no network required):**

1. **Pre-check**: Sample the four corners (each 10% of width/height) — if uniform color (browser chrome or OS chrome), flag as `FullPageUI` with medium confidence.
2. **Edge density pass**: Sobel filter; compute edge density histogram. High density + low color diversity → `CodeSnippet`. Low density + high color diversity → `ImagePhoto`.
3. **OCR pass**: Run lightweight OCR (reuse `IOcrService` from KFIP0001). If >30% of image area is classified as text blocks → `TextArticle`. If monospace font dominates → `CodeSnippet`.
4. **Color palette analysis**: Extract dominant colors via median-cut. Fewer than 32 dominant colors + high edge density → `CodeSnippet`. More than 1,000 dominant colors + smooth gradients → `ImagePhoto`.
5. **Composite score**: Each pass produces a probability per type. Weighted average → highest score wins. Confidence = gap between top score and second score.

**Source metadata hints** (when available):
- Window/app title contains "Code", "Terminal", "VS Code", "Visual Studio", "Xshell" → boost `CodeSnippet` confidence
- URL contains `github.com`, `stackoverflow.com`, `docs.` → boost `CodeSnippet` confidence
- URL contains `x.com`, `twitter.com` → boost `FullPageUI` confidence and pass to KFIP0014/X-specific pipeline

### 2. Pipeline Routing

After analysis, `ISmartCaptureEngine` sets session-level pipeline flags that the AfterCapture pipeline reads:

```csharp
public class SmartCaptureSession
{
    public CaptureContentType DetectedType { get; init; }
    public float Confidence { get; init; }
    public IReadOnlyList<string> ActiveAfterCaptureTasks { get; init; }  // populated by engine
    public IReadOnlyDictionary<string, object> PipelineConfig { get; init; }  // type-specific settings
    public string[] AutoHashtagSuggestions { get; init; }
}
```

**Routing table:**

| Detected Type | Confidence ≥ 0.80 | Confidence < 0.80 |
|---|---|---|
| `CodeSnippet` | Auto-enable: `DoOCR`, `CopyOcrTextToClipboard` (KFIP0011), `StripMetadata` (KFIP0009), PNG format forced, skip pre-softening | Show type badge + format indicator; apply pipeline but allow override |
| `ImagePhoto` | Auto-enable: `CompressForSocial` (KFIP0010), JPEG/WEBP format, aspect ratio 4:5 recommended, pre-softening active | Show type badge; suggest format based on palette analysis |
| `TextArticle` | Auto-enable: `DoOCR`, `CopyOcrTextToClipboard` (KFIP0011), `StripMetadata` (KFIP0009), PNG format | Show type badge; offer OCR clipboard copy |
| `FullPageUI` | Auto-enable: `StripMetadata` (KFIP0009), suggest scroll-capture if chrome detected, route to KFIP0014 X/Twitter pipeline if X context confirmed | Show type badge + "Try scroll capture?" prompt |

### 3. X/Twitter-Specific Optimisations

When X/Twitter context is confirmed (URL hint, app window title, or user has `x-twitter-screenshot` preset active), the engine applies X-specific overlays on top of the type-based pipeline:

#### 3.1 Compression Pre-Compensation (KFIP0010 integration)

- `CodeSnippet`: PNG forced (lossless; no pre-softening needed — PNG survives X's JPEG pipeline better than pre-softened JPEG for sharp text)
- `ImagePhoto`: JPEG @ 90 with pre-softening (radius 0.3 Gaussian + USM sharpen); if > 5MB after encode, step down to JPEG @ 85, then @ 80
- `TextArticle`: PNG forced; if > 5MB, offer WEBP fallback with quality 85
- `FullPageUI`: PNG or WEBP (mixed); auto-check against 5MB limit; warn before upload if exceeded

#### 3.2 Aspect Ratio Guidance

| Content Type | X Optimal Ratio | Rationale |
|---|---|---|
| `CodeSnippet` | 16:9 (1200×675) | Wide format shows full snippet; fits X's link card preview |
| `ImagePhoto` | 4:5 (1200×1500) | X's preferred feed display ratio; maximum vertical space |
| `TextArticle` | 4:5 or 16:9 | Depends on text layout; OCR bounding box informs decision |
| `FullPageUI` | 4:5 or full-height | X/Twitter display; thread capture often benefits from full-height |

**UI:** A small badge in the capture overlay shows the recommended aspect ratio and format with a one-click **[Apply X Optimal]** button.

#### 3.3 Auto-Hashtag Suggestions

Based on detected type and optionally OCR text, suggest 1–3 hashtags:

| Context | Suggested Hashtags |
|---|---|
| Code snippet from GitHub | `#CodeSnippet` `#Developer` |
| Data visualisation / chart | `#DataViz` `#Analytics` |
| Tweet screenshot | `#Twitter` `#X` |
| Article / text screenshot | `#Reading` `#Content` |
| Thread capture | `#Thread` `#MustRead` |

User can accept, edit, or dismiss. Suggestions are pre-copied to clipboard alongside the image or upload link.

#### 3.4 Metadata Handling

- All X/Twitter captures: `StripMetadata` (KFIP0009) auto-enabled with no user action required
- Toast notification: "Metadata stripped for X/Twitter" (brief, dismissible)
- Privacy-preserving by default — no GPS, device, or timestamp data in uploaded files

### 4. Overlay UI — Smart Capture Indicator

When the Smart Capture Engine runs, a non-intrusive indicator appears in the capture overlay:

```
┌─────────────────────────────────────────────────────┐
│ [Code Snippet · 94%]  📐 16:9  PNG  💾 #CodeSnippet │
│                            [Apply X Optimal]         │
└─────────────────────────────────────────────────────┘
```

- Shows detected type + confidence percentage
- Shows recommended aspect ratio and format
- Shows top auto-hashtag suggestion
- **[Apply X Optimal]** button applies all X-specific optimisations in one click
- Clicking the type badge opens a dropdown to manually override the type

### 5. Command Palette Integration (KFIP0007)

The command palette (KFIP0007) is extended with Smart Capture Mode entries:

```
> Smart Capture        — Run auto-detection on current region
> Smart Capture: Code  — Force code snippet pipeline
> Smart Capture: Image — Force image/photo pipeline
> Smart Capture: Text  — Force text/article pipeline
> Smart Capture: Page  — Force full-page pipeline
> X/Twitter Optimise   — Apply X/Twitter optimisations to last capture
> Preview Compression  — Simulate X re-encoding on last capture (KFIP0010)
```

---

## Success Metrics

| Metric | Baseline (2026) | Target |
|---|---|---|
| Capture-to-share time for X/Twitter | ~45 seconds (multi-tool manual) | < 10 seconds (Smart Capture auto-route) |
| Failed uploads due to file size | ~15% (X's 5MB limit surprise) | < 2% (pre-check prevents failures) |
| User manual preset selection rate | ~80% (users manually pick preset) | < 20% (auto-detection handles most cases) |
| OCR alt-text draft generation | Manual (user must trigger) | Auto for TextArticle type captures |
| Code snippet legibility post-X-reencode | Low (blurry text, common complaint) | High (PNG lossless routing eliminates double-JPEG) |
| Smart Capture type detection accuracy | N/A (new feature) | > 85% accuracy at confidence ≥ 0.80 in production |

---

## Implementation Phases

### Phase 1 — Core Engine (target: KFIP cycle after KFIP0014 implementation)

- `ISmartCaptureEngine` interface and heuristic classifier
- Four-type classification (CodeSnippet, ImagePhoto, TextArticle, FullPageUI)
- Session-level pipeline flag injection into AfterCapture pipeline
- Overlay type badge and confidence indicator
- Format and aspect ratio recommendation display

### Phase 2 — X/Twitter Optimisation Integration (target: next KFIP cycle)

- X/Twitter context detection (URL hint, window title)
- Compression pre-compensation routing (KFIP0010 integration)
- Metadata auto-strip on X/Twitter captures (KFIP0009 integration)
- Auto-hashtag suggestion engine
- **[Apply X Optimal]** one-click action

### Phase 3 — Command Palette and Automation (target: concurrent with Phase 2)

- KFIP0007 command palette entries for Smart Capture modes
- Manual override UX for all detection decisions
- Production accuracy telemetry (无声 logging of detection → override events)

### Phase 4 — Threshold Refinement (post-launch)

- Analyse override telemetry to improve detection heuristics
- Add `CodeSnippet` sub-types (terminal, IDE, documentation)
- Consider lightweight model for edge cases if heuristic accuracy < 80%
- Phase 2 expansion to other social platforms (LinkedIn, Discord, Threads)

---

## Open Questions

1. **Detection accuracy at low confidence**: Should the engine abstain (show "Unknown") or make a best guess at confidence below 0.50? Best-guess with visible uncertainty seems better for UX than silent failure.
2. **Auto-hashtag relevance**: Current suggestions are generic placeholders. Should hashtag suggestions be personalised per-user's posting history (local, on-device analysis only)?
3. **Performance budget**: Full OCR pass in the detection algorithm adds latency. Should the OCR pass be deferred to AfterCapture (KFIP0011) rather than co-located in detection?
4. **Override telemetry**: Should override events be logged locally to improve future detection? This must be purely local — no external telemetry.
