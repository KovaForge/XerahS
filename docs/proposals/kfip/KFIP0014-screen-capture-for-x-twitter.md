# KFIP0014: Screen Capture for X/Twitter — Power User Workflows

**Status**: Draft  
**Priority**: P1  
**Area**: Region Capture | AfterCapture | X/Twitter | Image Optimisation | Uploader Pipeline  
**Created**: 2026-07-12  
**Submitter**: Nadia (Research, KovaForge)  
**Co-Authors**: McoreD, vladislava-kova-kf  
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0010 (Compression-Resilient Capture), KFIP0013 (Smart Thumbnail Generation)

---

## Summary

### Problem Statement

X/Twitter power users — developers, researchers, analysts, and content creators — capture screenshots dozens of times per day for sharing code snippets, data visualisations, tweets, UI states, and bug reports. The current screen capture → share pipeline on Windows/macOS is fragmented: each step (capture, annotate, compress, upload, copy link, paste) requires a separate tool or manual action, and X/Twitter's own media constraints (5 MB image limit, recompression pipeline, aspect ratio preferences) are invisible until upload fails or quality degrades.

Three pain points dominate user complaints on Reddit, GitHub, and X's developer forums:

1. **X/Twitter re-encodes all uploaded images**, silently degrading JPEG quality — often catastrophically for screenshots with text, fine lines, or UI elements. Users who upload PNGs or high-quality JPEGs see muddy text and banding gradients after X's servers touch them.
2. **Image size and aspect ratio constraints are enforced only at upload time**, causing failed uploads or sub-optimal in-feed display. X prefers 4:5 vertical images for timeline visibility and rejects anything over 5 MB with no pre-upload warning.
3. **The capture-to-share loop requires too many steps**: region capture → save → open browser → attach file → wait → copy URL → paste into compose. Power users doing 10+ captures per day want one hotkey that produces a shareable link.

### Proposed Solution

XerahS (the KovaForge ShareX fork) should implement a **first-class X/Twitter capture mode** that combines smart region detection, pre-upload image optimisation tuned to X's re-encoding pipeline, one-click upload to configurable destinations (Imgur, self-hosted S3, Pixelfox, or community uploaders), and automatic link formatting for paste into X's compose window. The feature builds on KFIP0005's social presets, KFIP0009's share-ready annotations, and KFIP0010's compression-resilient capture pipeline, but focuses specifically on the X/Twitter platform constraints that existing tools ignore.

### Expected Outcomes

- Failed uploads due to file size reduced to near-zero (pre-upload validation against X's 5 MB limit)
- Perceived image quality on X improved for screenshot content (pre-softening tuned to survive X's re-encoding)
- Capture-to-link time reduced from ~45 seconds (manual) to under 10 seconds (automated)
- X/Twitter-specific presets discoverable from the main capture UI, not buried in settings

---

## Motivation

### Why This Matters to XerahS Users

XerahS is a ShareX fork whose primary differentiator from the upstream is faster iteration on features that matter to the KovaForge community. The upstream ShareX has a loyal power-user base — over 50,000 GitHub stars — but its X/Twitter integration has not kept pace with X's API migrations and platform changes:

| Pain Point | Evidence | Severity |
|---|---|---|
| X recompresses screenshots, destroying text legibility | GitHub issues: ShareX #5862 (2021, still active); Reddit r/sharex threads (2022–2025) | High — core use case failure |
| No pre-upload file size check against X's 5 MB limit | Consistent complaint in ShareX issue tracker | High — failed uploads waste time |
| Aspect ratio not optimised for X feed display | X's own best-practice guides (2026) recommend 4:5 vertical | Medium — reduced engagement |
| X API v2 migration (June 2025) broke media upload workflows | X Developer Community announcements | High — third-party tool breakage |
| GIF capture and conversion friction for X | Reddit r/gamedev, r/developers communities | Medium — power users want native GIF |
| No clipboard→link workflow for screenshot sharing | ShareX Feature Requests GitHub | High — the #1 requested workflow |

XerahS is positioned to own this workflow if ShareX continues its slower release cadence. The KovaForge community has direct visibility into these complaints via the research pipeline (this KFIP is Phase 1 of the XerahS KFIP pipeline).

---

## Detailed Specification

### 1. X/Twitter Capture Mode

#### 1.1 Preset Definition

A new capture preset, `x-twitter-screenshot`, is added to the built-in social presets (extending KFIP0005's `SocialCapturePreset` model):

```csharp
public class XTwitterScreenshotPreset : SocialCapturePreset
{
    public override string Id => "x-twitter-screenshot";
    public override string Name => "X/Twitter Screenshot";
    public override string Platform => "X";
    public override AspectRatio TargetAspectRatio => AspectRatio.v4x5;       // X's preferred feed ratio
    public override int MaxWidth => 1200;
    public override int MaxHeight => 1500;
    public override long MaxFileSizeBytes => 5 * 1024 * 1024;              // 5 MB hard limit
    public override ImageFormat PreferredFormat => ImageFormat.Png;         // PNG survives recompression better
    public override int JpegQuality => 92;                                  // Start high; reduce if > 5 MB
    public override bool IncludeAltTextPrompt => true;
    public override bool PreSofteningEnabled => true;                       // New: compensate for X's JPEG pipeline
}
```

#### 1.2 Pre-Softening Pipeline (Compression-Resilient Capture)

X/Twitter re-encodes all images as JPEG (even uploads submitted as PNG go through a transcode step). Screenshots with text, UI lines, and flat-color regions are most visible to this: text becomes blurry, gradients band, and anti-aliased edges smear.

The pre-softening pipeline applies a light Gaussian blur (radius 0.3–0.5 px) and subtle sharpening pass tuned to survive a second JPEG encode at quality 85–90 without further degradation. This is conceptually aligned with KFIP0010's compression-resilient capture work.

```csharp
public interface IXTwitterImagePreprocessor
{
    Task<SKBitmap> PreprocessForXReencodingAsync(SKBitmap source, XTwitterScreenshotPreset preset,
        CancellationToken ct = default);
    // Applies: light blur → slight USM sharpen → resize to max 1200×1500 → encode as PNG or JPEG@92
    // If result > 5 MB: re-encode at JPEG@85, then @75, with size check at each step
}
```

**Size reduction ladder:**
1. PNG (lossless, preserves text)
2. JPEG @ 92 (if PNG > 5 MB)
3. JPEG @ 85 (if @ 92 > 5 MB)
4. JPEG @ 75 + aggressive resize to 900px wide (last resort; warn user)

#### 1.3 Region Capture with X-Specific Overlay

When `x-twitter-screenshot` preset is active, the region selection overlay shows:

- **Aspect ratio guide**: A 4:5 frame overlay with corner handles
- **Preset badge**: "X/Twitter" label in the top-left corner
- **Text hint** (first-use only): "X prefers 4:5 screenshots for full-height display in feed"
- **Smart hints**: If `TweetCaptureDetector` (KFIP0003) detects a tweet window, auto-suggest the preset

### 2. Upload Pipeline

#### 2.1 Supported Upload Destinations

| Uploader | Status | Notes |
|---|---|---|
| Imgur (anonymous) | Built-in | 50 uploads/hour; no auth needed |
| Imgur (OAuth) | Built-in | Higher rate limit; persistent album |
| S3 / S3-compatible | Built-in | User provides bucket/credentials |
| Pixelfox | Community (KFIP0004) | XerahS-native; recommended for KovaForge users |
| Custom URL | Built-in | User provides upload endpoint + field name |

#### 2.2 URL Format Options

Users choose how the link is delivered to clipboard:

| Format | Example output |
|---|---|
| Raw URL | `https://i.imgur.com/abc123.png` |
| Markdown image | `![X screenshot](https://i.imgur.com/abc123.png)` |
| HTML img tag | `<img src="https://i.imgur.com/abc123.png" alt="screenshot" />` |
| X compose ready | `https://i.imgur.com/abc123.png` (raw is default; X auto-embeds) |

#### 2.3 Upload Failure Handling

| Failure mode | Behaviour |
|---|---|
| File too large (X 5 MB limit) | Show: "Image is X MB. X requires under 5 MB. [Reduce quality] [Crop further] [Upload anyway]" |
| Network failure mid-upload | Retry up to 3 times with exponential backoff; if all fail, offer save-local escape hatch |
| Rate limit hit (Imgur) | Show: "Imgur rate limit reached. Try again in X minutes or switch uploader." |
| X API upload endpoint errors | Surface specific error code and developer guidance |

### 3. GIF Capture and Conversion for X

X supports GIF uploads up to 15 MB. A common power-user workflow is: capture a short screen recording → convert to GIF → upload and share.

#### 3.1 GIF Capture Flow

1. User initiates capture with `x-twitter-gif` preset (or long capture activates GIF mode automatically above a threshold)
2. Region selection with GIF indicator badge
3. Recording starts immediately on mouse release; ESC to stop
4. On stop: preview with frame count, estimated file size
5. Auto-convert: if estimated > 15 MB, offer MP4 as alternative (X supports MP4 up to 512 MB)
6. Upload via same pipeline as screenshots; link copied to clipboard

#### 3.2 GIF Encoding Settings

| Setting | Value | Rationale |
|---|---|---|
| Max dimensions | 800×800 | X GIF display is cropped to square in feed; 800px preserves detail |
| Frame rate | 10 fps | Reduces file size; adequate for screen content |
| Colour depth | 128 | Balanced quality/size |
| Dithering | Floyd-Steinberg | Better gradient rendering |
| Max file size | 15 MB (X limit) | Hard cap; warn if conversion exceeds |

### 4. X API v2 Media Upload Compatibility

X deprecated its v1.1 media upload endpoints on **9 June 2025** and requires migration to v2. XerahS's upload pipeline must be compatible with X API v2.

**Relevant constraints:**
- Media upload via `/2/media/upload` endpoint (chunked or simple)
- Rate limits: 500 req/15 min per user (Pro), 50 req/15 min (Free)
- Free tier does **not** support media upload attached to post creation via API — only standalone media upload is available, meaning users cannot post to X programmatically without a Pro subscription

**Implication for XerahS:** XerahS does NOT automate posting (user must paste the link into X's compose window manually). The upload pipeline only needs to produce a shareable URL. X API v2 media upload is **not required** for XerahS's current scope — image hosting via Imgur/S3/etc. is sufficient. If a future KFIP proposes X-native posting, X API v2 compliance with Pro subscription is required.

### 5. User Flows

#### 5.1 Standard Screenshot-to-Share Flow

```
User presses [PrintScreen] or custom hotkey
        │
        ▼
[Region Selection — X/Twitter preset active]
  - 4:5 overlay shown
  - "X/Twitter" badge displayed
  - Smart hints from TweetCaptureDetector (KFIP0003)
        │
        ▼
[User draws region, releases mouse]
        │
        ▼
[XTwitterImagePreprocessor]
  - Resize to max 1200×1500 (maintaining aspect)
  - Apply pre-softening for X re-encoding
  - Encode as PNG or JPEG (quality ladder)
  - File size check against 5 MB limit
        │
        ├── If > 5 MB → quality reduction loop
        │
        ▼
[Upload via configured destination]
  - Imgur / S3 / Pixelfox
        │
        ▼
[URL copied to clipboard]
  - Format: raw / Markdown / HTML (user-configured)
        │
        ▼
[Notification]
  "✅ Screenshot uploaded. Link copied.
   [View] [Share to X] [Dismiss]"
```

#### 5.2 GIF Capture Flow

```
User selects "GIF Capture" or uses dedicated hotkey
        │
        ▼
[Region + FPS selection overlay]
        │
        ▼
[Recording — ESC to stop]
        │
        ▼
[Preview: frame count, estimated GIF size]
        │
        ├── If > 15 MB → offer MP4 conversion
        │
        ▼
[GIF encoder runs with X-optimised settings]
        │
        ▼
[Upload via same pipeline]
        │
        ▼
[URL + format copied; GIF converted MP4 note if applicable]
```

---

## Alternatives Considered

### Alternative 1: Native X API v2 Posting

**Description:** Authenticate with X via OAuth and post directly with embedded media.

**Why rejected:** Free X API v2 does not support media upload + post creation in one call. Pro API costs ~$100/month. OAuth handling introduces account linkage risk (X suspends accounts for third-party app OAuth misuse). User must review posts anyway — clipboard + manual paste is more reliable and policy-safe. This aligns with KFIP0005's decision to avoid credential storage for social platforms.

### Alternative 2: Browser Extension for X-Native Capture

**Description:** A companion browser extension that captures directly within the X web interface.

**Why rejected:** Requires maintaining a separate codebase (browser extension). Platform support would be limited to Chrome/Edge/Firefox. X's web UI changes frequently and breaks extension integrations. Users on mobile (X app) cannot use a browser extension. The desktop capture tool approach is more general and benefits all X users regardless of how they access X.

### Alternative 3: Third-Party Screenshot-as-a-Service (Twittershots, Carbon, etc.)

**Description:** Delegate screenshot generation to an external API.

**Why rejected:** Privacy risk: screenshots may contain sensitive data (DMs, private tweets, financial information). External APIs change their terms, pricing, or shut down (Twittershots has changed hands multiple times). No guarantee of image quality preservation. XerahS should own the capture pipeline locally; third-party services are opt-in via uploader plugins.

---

## Compatibility Notes

### XerahS Version Compatibility

- **Minimum version:** XerahS 2.0 (develop branch, post-KFIP0005 merge)
- **Platform:** Windows 10/11 (primary), macOS via ShareX compatibility layer (future)
- **Dependencies:** SkiaSharp (image processing), Imgur API v3 client library, AWSSDK.S3 (S3 uploads)

### Settings Migration

- Existing capture presets are unaffected (new `x-twitter-screenshot` preset is additive)
- Existing uploaders continue to work (no breaking changes to `IUploader` interface)
- Settings file schema version incremented to 7; migration logic handles v6 → v7 upgrade

### X Platform Compatibility

| X Feature | XerahS Support | Notes |
|---|---|---|
| Static image (PNG/JPEG/GIF) | ✅ | Preprocessed and validated pre-upload |
| Animated GIF | ✅ | With size/quality ladder |
| Video (MP4) | 🔜 Phase 2 | Capture-to-MP4 workflow |
| Multi-image (up to 4) | 🔜 Phase 2 | Thread capture mode |
| X API v2 posting | ❌ | Out of scope; manual paste required |
| Alt text | ✅ | Prompt shown; stored in image metadata and clipboard if user adds text |

### Existing KFIP Dependencies

| KFIP | Dependency | Required Before |
|---|---|---|
| KFIP0005 (Social Presets) | `SocialCapturePreset` model, preset selector UI | Phase 1 |
| KFIP0003 (TweetCaptureDetector) | Context detection for auto-preset suggestion | Phase 1 |
| KFIP0004 (Plugin Registry) | Community uploader discovery | Phase 2 |
| KFIP0010 (Compression-Resilient) | Pre-softening pipeline implementation | Phase 1 |
| KFIP0013 (Smart Thumbnail) | Thumbnail text overlay for X-optimised output | Phase 2 |

---

## Revision History

| Revision | Date | Author | Changes |
|---|---|---|---|
| 1.0.0 | 2026-07-12 | Nadia (Research, KovaForge) | Initial draft — Phase 1 research synthesis |
