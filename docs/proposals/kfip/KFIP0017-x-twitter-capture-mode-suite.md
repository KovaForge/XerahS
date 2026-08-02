# KFIP0017: X/Twitter Capture Mode Suite — Scroll Capture, Video Clips & Smart GIF Conversion

**Status**: Draft  
**Priority**: P1  
**Area**: Capture Modes | Scroll Capture | Video Capture | X/Twitter | AfterCapture  
**Created**: 2026-08-02  
**Submitter**: Nadia (Research, KovaForge)  
**Co-Authors**: McoreD <195468996584275968@users.noreply.github.com>, vladislava-kova-kf  
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0014 (Screen Capture for X/Twitter — Power User Workflows), KFIP0015 (Screenshot Annotation Toolkit), KFIP0016 (Smart Capture Modes)

---

## Summary

X/Twitter users capture three content types that existing KFIPs treat as peripheral but that together represent the majority of real-world X/Twitter sharing workflows: **scroll-captured threads**, **extracted video clips**, and **screen recordings converted to GIFs**. Each of these is partially addressed — KFIP0005 mentions thread multi-select, KFIP0014 covers GIF encoding settings — but no KFIP provides a unified **Capture Mode Suite** that routes the user through the right capture mode based on detected content type and delivers X/Twitter-optimised output in a single intent-driven action.

This KFIP proposes a first-class **X/Twitter Capture Mode Suite** built on top of KFIP0016's Smart Capture Engine: it adds native scroll-capture for long threads and article pages, video clip extraction from X-hosted video and native screen recordings, and intelligent GIF conversion with X's 15 MB limit pre-baked into the encode pipeline. The Suite is accessible via a unified mode picker in the capture overlay and via KFIP0007 command palette entries.

---

## Motivation

### The Three Dominant X/Twitter Capture Workflows Are Under-Served

X/Twitter captures broadly fall into three categories:

| Capture Type | Description | Current Support | Gap |
|---|---|---|---|
| **Static screenshot** | Single tweet, UI state, code snippet | Well-served (KFIP0014, KFIP0016) | — |
| **Thread / article scroll capture** | Full thread (10–100+ tweets), news article, long-form post | KFIP0005 mentions "thread multi-select"; no native scroll-capture | Users stitch screenshots manually or use third-party tools (TwitterShots, Pikaso) |
| **Video clip / GIF** | Short screen recording or X video clip for reaction / meme | KFIP0014 covers GIF encode settings; no clip extraction from video | Users use screen recorders + video editors separately |
| **Full-page article capture** | Web article, blog post, documentation page | Generic scroll capture exists | No X/Twitter-specific output optimisation for articles |

### User Research Findings

**Thread scroll capture pain points:**

1. **Manual stitching is error-prone and time-consuming.** Users capturing a 20-tweet thread must take 4–5 screenshots, ensure overlap, and stitch them in an image editor. TwitterShots and Pikaso automate this via URL-based rendering, but require pasting a URL — breaking the native desktop capture workflow.

2. **X's thread display cuts off long images.** X truncates images longer than ~3:1 aspect ratio in-feed. Users who produce a single stitched thread image discover it is compressed to illegibility.

3. **Thread metadata (date, author, engagement) is not captured consistently.** A thread screenshot should show at least the first post's author and timestamp for context. Current workflows strip this or require manual annotation.

**Video clip / GIF pain points:**

4. **Screen recording → GIF is a multi-tool workflow.** User records a screen → opens video editor → trims clip → exports as GIF → opens XerahS upload workflow. No unified "record → clip → post" flow.

5. **X's 15 MB GIF limit is enforced only at upload.** Users produce a 25 MB GIF and only discover the limit when upload fails. The encode settings in KFIP0014 (800×800 max, 10 fps, Floyd-Steinberg dithering) are correct but not surfaced at capture time.

6. **Clip extraction from X video is not possible natively.** X hosts video on its CDN; users cannot extract a clip without a separate tool. Screen recording a playing X video is the workaround, but produces lower quality than a native clip would.

**Full-page article capture pain points:**

7. **X/Twitter users share articles alongside tweets.** A researcher capturing a news article for an X thread currently gets a generic scroll-capture that is not optimised for X's display constraints (aspect ratio, file size).

8. **Article text legibility after X re-encoding** is the same compression problem documented in KFIP0010 — articles with dense text are destroyed by X's JPEG pipeline.

### Evidence from the Market

- **TwitterShots, Pikaso, WebSniply**: URL-input tools that produce styled tweet/thread screenshots. Market validated demand for thread capture. But these are cloud services requiring URL input — not native desktop capture.
- **CleanShot X**: Scroll capture feature for Mac; users capture full pages and stitch automatically. Windows equivalent is absent from ShareX/XerahS.
- **ShareX**: Screen recording exists (FFmpeg-based) but has no X/Twitter-specific GIF encode pipeline. Users configure settings manually.
- **Peek, Katrack, xGif**: Standalone GIF creation tools. No integration with capture toolchain.
- **TweetPik, Carbon**: Specialised screenshot tools with X/Twitter styling (styled tweet frames, code syntax highlighting). But require URL input or manual paste, not native capture.

**The gap**: No tool combines native scroll-capture, video clip trimming, and smart GIF encoding in a single, X/Twitter-optimised capture suite accessible from a desktop capture overlay.

---

## Goals

- Provide native scroll-capture for X/Twitter threads and article pages with automatic stitching, with X/Twitter-optimised output (aspect ratio, file size, format)
- Provide a unified video clip capture mode that records a region, trims it, and encodes as GIF or MP4 with X/Twitter constraints pre-applied
- Surface the correct capture mode automatically via KFIP0016's Smart Capture Engine detection (scroll vs. static vs. video)
- Integrate with KFIP0007 command palette for mode switching: `> Capture: Scroll Thread`, `> Capture: Video Clip`, `> Capture: GIF`
- Reduce thread capture from multi-tool manual stitching to single action

## Non-Goals

- No URL-based rendering (URL → rendered screenshot); native capture only
- No X video CDN clip extraction (requires X API; not available on free tier)
- No AI-assisted clip selection (trim points are manual)
- No automatic video recording start (user-initiated only; no activity detection)
- No GIF-to-video hybrid format in v1

---

## Proposed Solution

### Architecture: Capture Mode Suite

The Capture Mode Suite (`ICaptureModeSuite`) is a dispatcher that runs after the Smart Capture Engine (KFIP0016) classifies the capture intent. It presents a mode picker when the detected content type is ambiguous, and can be invoked directly via overlay or command palette.

```
Capture Intent Triggered
        │
        ▼
Smart Capture Engine (KFIP0016)
  - Classifies: Static / Scroll-Page / Video-Recording
        │
        ├── [Static] → KFIP0016 static pipeline → annotate → upload
        │
        ├── [Scroll-Page detected] → Scroll Capture Mode
        │     - Auto-detect scrollable container (browser, document)
        │     - Stitch captures automatically
        │     - X/Twitter output optimisation applied
        │     → AfterCapture pipeline
        │
        └── [User or command palette invoked] → Video Clip / GIF Mode
              - Region record with timer
              - Trim UI (set start/end)
              - Encode: GIF (X) or MP4 (X video)
              - X/Twitter constraints pre-applied
              → AfterCapture pipeline
```

### 1. Scroll Capture Mode

#### 1.1 Detection

Scroll capture activates when:
- **Smart Capture Engine (KFIP0016)** detects `FullPageUI` type with `Confidence ≥ 0.75`, OR
- **Manual invocation**: user presses a dedicated hotkey or selects "Scroll Capture" from overlay/command palette, OR
- **URL hint**: `TweetCaptureDetector` (KFIP0003) detects a thread URL in the window title or clipboard

The user confirms or overrides the mode selection in the overlay before capture begins.

#### 1.2 Scroll Mechanism

The scroll capture engine (`IScrollCaptureEngine`) drives a virtual scroll of the target window:

```csharp
public interface IScrollCaptureEngine
{
    Task<SKBitmap> CaptureScrollingRegionAsync(
        WindowHandle targetWindow,
        ScrollCaptureOptions options,
        IProgress<ScrollCaptureProgress>? progress = null,
        CancellationToken ct = default);

    Task<SKBitmap> StitchCapturesAsync(
        IReadOnlyList<SKBitmap> captures,
        StitchOptions options,
        CancellationToken ct = default);
}

public record ScrollCaptureOptions
{
    public int OverlapPixels { get; init; } = 50;   // Overlap for stitching
    public int ScrollStepPixels { get; init; } = 600;
    public int MaxCaptures { get; init; } = 50;    // Safety cap; ~10000px scroll
    public bool DetectChangeOnly { get; init; } = true; // Capture only when frame changes
    public ImageBufferFormat BufferFormat { get; init; } = ImageBufferFormat.BGRA32;
}

public record ScrollCaptureProgress(int CapturesTaken, int TotalHeightPixels, int EstimatedTotalCaptures);
```

**Scroll algorithm:**
1. Capture the visible region of the target window
2. Programmatically scroll the window by `ScrollStepPixels`
3. Wait for render stabilise (detect no further DOM/render changes for 200ms via accessibility API or pixel-diff)
4. Capture next region with `OverlapPixels` overlap
5. Repeat until scroll reaches end or `MaxCaptures` is reached
6. Stitch captures using `StitchCapturesAsync` (median-cut blend for overlap regions to hide seams)

**Scroll end detection:**
- Browser windows: monitor `WM_VSCROLL` / `WM_MOUSEWHEEL` messages or use accessibility API (`IAccessible`)
- Document windows: use `WM_VSCROLL` position query; stop when position reaches `ScrollBarMax`
- Generic windows: pixel-diff between consecutive captures; stop when diff < threshold for 3 consecutive captures

#### 1.3 X/Twitter Thread Optimisation

When X/Twitter context is confirmed (via KFIP0003 `TweetCaptureDetector`):

| Output Property | Value | Rationale |
|---|---|---|
| Format | PNG (lossless) | Thread text must survive X's JPEG re-encoding |
| Stitch strategy | Vertical stack, no seam artefacts | Threads are naturally vertical |
| Aspect ratio | Full height, max 1200px wide | X truncates images > ~3:1; warn if result exceeds |
| File size pre-check | Warn if > 5 MB | X's hard limit |
| Author/date overlay | Optional annotation (toggle) | KFIP0015 annotation toolkit integration |
| Metadata strip | Auto-enabled (KFIP0009) | Privacy: strip GPS, device ID, timestamp |

**Smart crop:** If stitched image aspect ratio exceeds 1:3 (X's in-feed truncation threshold), offer a smart crop UI: let the user select which portion of the thread to keep, or suggest an automatic crop that preserves the thread root + most engaging reply.

#### 1.4 Article Page Capture

For non-X/Twitter article pages (news sites, blogs, documentation):

| Output Property | Value |
|---|---|
| Format | PNG for text-heavy; JPEG @ 90 for image-heavy (auto-detect) |
| Max dimensions | 1200px wide, full height |
| Scroll step | 800px (optimised for readable text density) |
| OCR trigger | Offer OCR-to-clipboard if text-heavy (KFIP0011 integration) |

### 2. Video Clip & GIF Capture Mode

#### 2.1 Capture Flow

```
User invokes Video Clip Mode (hotkey, overlay, or command palette)
        │
        ▼
[Region Selection — with GIF indicator badge]
        │
        ▼
[Recording starts on mouse release — ESC to stop]
  - Timer display: elapsed time (e.g., "0:03 / 0:30")
  - GIF size estimate updates live (KB/s based on content)
  - Max duration: 30 seconds (X GIF limit is 15 MB; estimate informs cap)
        │
        ▼
[Recording stops — Trim UI appears]
  - Set start frame (drag handle)
  - Set end frame (drag handle)
  - Play preview of trimmed clip
  - File size estimate for GIF and MP4 outputs
        │
        ├── If estimated GIF > 15 MB → suggest MP4 (X supports MP4 up to 512 MB)
        │
        ▼
[Encode]
  - GIF: 800×800 max, 10 fps, Floyd-Steinberg dithering, quality ladder
  - MP4: H.264, 15 fps, 2 Mbps, 720p max
        │
        ▼
[X/Twitter Pre-Check]
  - Confirm file size against platform limit
  - Show warning if approaching limit with option to reduce quality
        │
        ▼
[AfterCapture pipeline: annotate → strip metadata → upload]
```

#### 2.2 Encode Settings (X/Twitter Optimised)

**GIF settings:**

| Setting | Value | Rationale |
|---|---|---|
| Max dimensions | 800×800 | X crops GIFs to square in feed; 800px preserves detail |
| Frame rate | 10 fps | Reduces file size; adequate for screen content |
| Color depth | 128 colors | Balanced quality/size; Floyd-Steinberg dithering |
| Dithering | Floyd-Steinberg | Best gradient rendering for screen content |
| Max file size | 15 MB (X limit) | Hard cap; offer MP4 if exceeded |
| Quality ladder | Quality 90 → 80 → 64 colors → reduce fps to 8 | Step down until under 15 MB |

**MP4 settings (fallback):**

| Setting | Value |
|---|---|
| Codec | H.264 (libx264) |
| Frame rate | 15 fps |
| Bitrate | 2 Mbps |
| Max resolution | 1280×720 |
| Container | MP4 |
| Max file size | 512 MB (X limit for video) |

#### 2.3 Live File Size Estimation

During recording, a live KB/s estimate allows the UI to warn the user before they exceed X's GIF limit:

```csharp
public interface IGifSizeEstimator
{
    // Runs on a background thread during recording
    long EstimateFinalSizeBytes(int recordedFrames, long bytesSoFar, FrameEncodingSettings settings);
    // Returns estimated final GIF size; UI shows warning if > 15 MB
}
```

**Algorithm:** Sample the first 0.5 seconds of recording to measure bytes/frame, then extrapolate by total frame count and target settings. Update estimate every 0.5 seconds during recording.

### 3. Unified Mode Picker UI

When Smart Capture Engine detection confidence is below threshold or content type is ambiguous, the overlay shows a mode picker:

```
┌──────────────────────────────────────────────────────────┐
│  [Full Page / Thread detected — 87%]                     │
│                                                          │
│   [ 📸 Static ]   [ 📜 Scroll ]   [ 🎬 Video/GIF ]       │
│                                                          │
│   Press 1 / 2 / 3 to switch mode. Press Enter to confirm.│
└──────────────────────────────────────────────────────────┘
```

- Mode picker appears inline in the capture overlay (not a separate dialog)
- Keyboard shortcuts: `1` = static, `2` = scroll, `3` = video/GIF
- Selected mode badge is highlighted; confirmation proceeds with that mode
- Smart detection result is shown with confidence; user can override

### 4. Command Palette Entries (KFIP0007 Integration)

```
> Capture: Scroll Thread      — Invoke scroll capture for X/Twitter thread
> Capture: Scroll Page        — Invoke scroll capture for generic page
> Capture: Video Clip         — Record region as video clip
> Capture: GIF               — Record region as GIF (X/Twitter optimised)
> X/Twitter Capture Suite    — Open mode picker for X/Twitter capture
> GIF: Estimate Size         — Estimate GIF size for last recording
```

### 5. Smart Capture Engine Integration

The Smart Capture Engine (KFIP0016) is extended to detect scroll-capture candidates:

```csharp
// KFIP0016 interface extension
public record CaptureContentAnalysis
{
    // ... existing fields from KFIP0016
    public bool IsScrollCaptureCandidate { get; init; }
    public bool IsVideoRecordingCandidate { get; init; }
    public int? EstimatedScrollHeight { get; init; }  // pixels, if scroll candidate
    public string? DetectedPlatformContext { get; init; }  // "x-twitter", "web-article", etc.
}
```

**Detection signals for scroll-capture:**
- Page contains scrollable element (overflow: scroll/auto detected via accessibility API)
- Browser URL contains `x.com`, `twitter.com` + thread indicators (`/status/` in URL with multiple replies)
- Window title matches known article formats (site name in title, "—", date patterns)
- Pixel-diff between consecutive frames shows significant vertical movement on scroll

**Detection signals for video recording:**
- User has activated video/GIF mode from command palette
- Smart Capture Engine detects content type `FullPageUI` with no clear static region

---

## Implementation Phases

### Phase 1 — Scroll Capture Engine (target: sprint after KFIP0016 core)

- `IScrollCaptureEngine` interface and Win32 scroll automation
- Browser scroll end detection via accessibility API
- Two-capture stitch with seam blending
- Scroll progress indicator in overlay
- Thread detection via URL hint (KFIP0003 integration)
- Basic X/Twitter output optimisation (PNG, aspect ratio check, file size pre-check)
- Smart crop UI for oversized thread captures

### Phase 2 — Video Clip & GIF Mode (target: concurrent with Phase 1)

- Screen recording via FFmpeg (already in XerahS/ShareX codebase)
- Region selection with GIF badge
- Live file size estimator
- Trim UI: start/end frame handles with preview playback
- GIF encoder with quality ladder and X/Twitter limit enforcement
- MP4 fallback with H.264 encode
- Command palette entries for video/GIF modes

### Phase 3 — Unified Mode Picker & Smart Engine Integration (target: after Phase 1+2)

- Mode picker overlay integrated with KFIP0016 Smart Capture Engine
- `IsScrollCaptureCandidate` / `IsVideoRecordingCandidate` detection signals
- Keyboard shortcut mode switching
- Full integration with AfterCapture pipeline (annotate → strip metadata → upload)
- X/Twitter Capture Suite launcher from main overlay

### Phase 4 — Polish & Edge Cases (target: post-launch)

- Multi-monitor scroll capture support
- Firefox scroll detection (uses different accessibility API than Chromium/Edge)
- GIF color palette optimisation (median-cut pre-analysis before encode to pick best palette)
- Thread author/date annotation overlay (KFIP0015 integration)
- Per-user scroll step preference memory

---

## Technical Notes

### FFmpeg Integration for Recording

XerahS already uses FFmpeg for GIF encoding (via `Gifski` or direct `ffmpeg` invocation). The screen recording path should reuse this infrastructure:

```bash
# Screen recording via FFmpeg (region capture)
ffmpeg -f gdigrab -framerate 10 -offset_x {x} -offset_y {y} -video_size {w}x{h} -i desktop ...
```

**Windows-specific:** `gdigrab` captures the desktop; `dxgrpc` or `hwcodec` can be used for hardware-accelerated capture on discrete GPUs for smoother recording.

### Stitching Algorithm

The stitcher uses a **pixel-diff seam detection** approach for overlap blending:

1. For each pair of consecutive captures, find the vertical offset where content aligns (cross-correlation on the overlap region)
2. If alignment offset found: blend the overlap region using a linear gradient fade (50% each side) to hide the seam
3. If no alignment (jump cut detected): place captures back-to-back with a thin divider line (indicating scroll break)

### Scroll End Detection Edge Cases

| Scenario | Detection Method |
|---|---|
| Chromium/Edge browser | `IAccessible` from UI Automation API; `IScrollProvider` interface |
| Firefox | `IAccessible` fallback; pixel-diff fallback |
| Native Windows apps | `WM_VSCROLL` position query; pixel-diff fallback |
| Electron apps (Discord, Slack) | Pixel-diff only (no reliable accessibility API) |

Pixel-diff fallback: stop scrolling when 3 consecutive captures differ by < 0.5% of pixels changed.

---

## Success Metrics

| Metric | Baseline | Target |
|---|---|---|
| Thread capture time (manual stitching) | ~5 minutes | < 30 seconds |
| GIF capture-to-upload time | ~3 minutes (multi-tool) | < 45 seconds |
| Failed GIF uploads due to X size limit | ~30% (current, guess) | < 5% (live estimate prevents failures) |
| Scroll capture stitch accuracy | N/A (new feature) | > 90% seamless stitches |
| User mode-switch rate (auto-detect overridden) | N/A (new feature) | < 15% override rate |

---

## Open Questions

1. **Browser compatibility for scroll detection**: Firefox's accessibility API coverage is inconsistent. Should we require Firefox 115+ (ESR) with known-good accessibility, or rely on pixel-diff fallback only for Firefox?
2. **Scroll capture of X/Twitter's "Latest Tweets" UI**: X/Twitter's web UI is a SPA with infinite scroll; the "end" of a thread is ambiguous. Should we stop at the first scroll breakpoint (first tweet in thread), or attempt to find the thread end?
3. **GIF color palette pre-analysis**: Should we run a median-cut analysis on the first 5 frames before encoding begins, to pre-select an optimal 128-color palette? This adds ~200ms overhead but significantly improves GIF quality.
4. **Video clip vs. GIF mode selection UX**: Users may not know which format to choose. Should the UI recommend GIF or MP4 based on live content analysis (e.g., "this recording has smooth motion — MP4 recommended for better quality")?
5. **Thread capture with reply context**: For a thread, should we try to also capture top replies? This would require URL-based expansion (TwitterShots approach) which is out of scope for native capture — but the question is whether to even attempt it or clearly scope it out.

---

## Revision History

| Revision | Date | Author | Changes |
|---|---|---|---|
| 1.0.0 | 2026-08-02 | Nadia (Research, KovaForge) | Initial draft — research synthesis for X/Twitter Capture Mode Suite |
