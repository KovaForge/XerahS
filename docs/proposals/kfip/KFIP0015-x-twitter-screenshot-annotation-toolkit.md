# KFIP0015: X/Twitter Screenshot Annotation Toolkit

**Status**: Draft  
**Priority**: P1  
**Area**: Annotation | Region Capture | AfterCapture | X/Twitter | Social Sharing  
**Created**: 2026-07-19  
**Submitter**: Nadia (Research, KovaForge)  
**Co-Authors**: McoreD <195468996584275968@users.noreply.github.com>  
**Related**: KFIP0005 (Social Sharing Workflows), KFIP0009 (Share-Ready Enhancements), KFIP0014 (Screen Capture for X/Twitter — Power User Workflows)

---

## Summary

X/Twitter power users annotate screenshots to highlight key text, redact sensitive information, draw attention to specific UI elements, and add context before sharing. Existing screen capture tools offer generic annotation — rectangles, arrows, text — but none are tuned for X/Twitter's visual language: tweet columns, thread layouts, DM surfaces, profile cards, and the constrained square/4:5 canvas where annotations must be legible at small sizes.

This KFIP proposes an **X/Twitter Screenshot Annotation Toolkit**: a purpose-built overlay canvas and annotation layer for XerahS that activates automatically when the `x-twitter-screenshot` capture preset is selected. It provides X-aware smart shapes (tweet highlight box, thread bracket, handle callout), a privacy redaction layer with X-specific presets (username, handle, timestamp), one-tap styling tuned for X's dark/light themes, and an export path that feeds directly into the upload pipeline from KFIP0014.

This is Phase 2 of the X/Twitter capture story begun in KFIP0014, focused specifically on the annotation step that sits between region capture and upload.

---

## Problem Statement

Users sharing annotated screenshots on X/Twitter face three recurring annotation failures with existing tools:

1. **Generic shapes don't fit X's UI.** Drawing a rectangle around a tweet in a thread is straightforward, but existing tools force users to manually position a generic box over a tweet card, manually adjust corner radius, and manually match the dark/light theme. A tweet highlight box is structurally different from a generic rectangle — it has rounded corners matching X's card design, consistent padding, and theme awareness. Doing this by hand on every capture is slow and inconsistent.

2. **Privacy redaction for X-specific elements is missing.** When sharing screenshots that contain other users' content, researchers and journalists frequently need to redact: username, handle, timestamp, like/retweet counts, and profile images. Generic "blur rectangle" tools require manual placement and offer no guidance on what to redact for X screenshots. There is no preset for "X/Twitter identity elements."

3. **Annotations degrade badly on X's re-encoding pipeline.** Text labels and high-contrast shapes drawn directly on screenshots become illegible after X's JPEG re-encoding (documented in KFIP0010). Annotations need to be applied with anti-aliasing and contrast preservation tuned to survive X's recompression — a distinct requirement from annotation for print or local archival.

### Evidence

| Pain Point | Evidence | Severity |
|---|---|---|
| Tweet highlight box requires manual setup every time | Consistent request in ShareX/Reddit communities 2022–2026 | High — repetitive friction |
| No X-specific privacy redaction presets | GitHub discussions on KovaForge community board | Medium — researchers/journalists affected |
| Annotations become illegible after X re-encodes | KFIP0010 research; confirmed by ShareX issue #5862 | High — workflow failure |
| No dark/light theme detection for annotation styling | Feature requests on r/sharex | Medium — poor visual fidelity |
| Arrow/callout tools not tuned for small X canvas | Reddit r/XTwitterDesign threads | Medium — legibility at small sizes |

---

## Proposed Solution

### Core Concept

An **Annotation Toolkit** that activates within the XerahS capture overlay when `x-twitter-screenshot` preset is active. The toolkit provides:

- **Smart shapes** tuned to X's UI geometry (tweet highlight box, thread bracket, handle callout, DM bubble)
- **Privacy redaction layer** with X-specific presets (username, handle, timestamp, engagement counts, profile pic)
- **X theme detection** — captures the dominant background (dark/light/blue-tinted) and auto-styles annotation borders, text, and highlights accordingly
- **Anti-degradation rendering** — annotation rendering uses techniques tuned to survive X's JPEG pipeline (stroke anti-aliasing, contrast boosting, text protection via background fill)
- **Seamless handoff** to the KFIP0014 upload pipeline

### Design Principles

1. **Non-destructive by default.** Annotations are rendered as an overlay layer; the original capture is preserved in memory for re-editing until the user confirms the final export.
2. **Single-keyboard-shortcut activation.** Pressing a hotkey (configurable; default `Shift+A`) while in the X/Twitter capture overlay opens the annotation toolbar without leaving the overlay.
3. **Undo is always available.** `Ctrl+Z` undoes the last annotation action; the full annotation stack is preserved until confirmed.
4. **Theme-aware by default.** If X is in dark mode, annotation styles default to light; if X is in light mode, defaults to dark. Manual override always available.

---

## Detailed Specification

### 1. Annotation Toolbar

#### 1.1 Toolbar Layout

When `Shift+A` is pressed during an active X/Twitter capture overlay, a floating toolbar appears near the bottom of the selection region:

```
┌─────────────────────────────────────────────────────────────────┐
│  [Tweet] [Thread] [Handle] [Arrow] [Text] [Redact] │ [Undo] [✓] │
└─────────────────────────────────────────────────────────────────┘
```

- **Tweet** — Tweet highlight box (rounded rectangle matching X card design)
- **Thread** — Thread bracket with collapse indicator
- **Handle** — Handle/user callout (circle + tail pointing to username)
- **Arrow** — Directional arrow (tuned for small canvas legibility)
- **Text** — Text label with background fill
- **Redact** — Privacy redaction preset selector
- **Undo** — Undo last annotation
- **✓ (Confirm)** — Render annotations and pass to upload pipeline

#### 1.2 Tool Behaviours

**Tweet Highlight Box (`Tweet` tool)**
- User clicks on a tweet within the captured region
- A rounded rectangle (`border-radius: 12px` visual equivalent) is drawn around the tweet card with padding `8px` on all sides
- Auto-detects tweet background (white card on timeline, blue-tinted in quote tweets, semi-transparent in DMs)
- Border colour: auto-contrast (dark border on light backgrounds, white border on dark backgrounds)
- Border width: `2px` for visibility at small X display sizes
- Optional: user can drag corners to adjust; handles snap to tweet card edges

**Thread Bracket (`Thread` tool)**
- User clicks the first tweet of a thread and drags to the last tweet
- A vertical bracket with a small thread-collapse icon (≡) is drawn on the left side
- Highlights all tweets in the thread range with consistent styling
- Thread bracket styling: `2px` left border, light highlight fill at `10%` opacity

**Handle Callout (`Handle` tool)**
- User clicks a username or handle
- A small circle (`radius: 16px`) highlights the avatar area, with a tail pointing to the username text
- Auto-extracts the display name and handle from adjacent text
- Useful for pointing to "this person's tweet" without highlighting the whole card

**Arrow (`Arrow` tool)**
- Click-drag to draw a directional arrow
- Styles: filled head, `2px` shaft, `12px` head length
- Default colour: high-contrast red (`#E1306C`) — visible on both dark and light X themes
- Arrow scales appropriately for the small canvas: no arrow longer than `120px`

**Text Label (`Text` tool)**
- Click to place a text anchor
- Opens a small inline text entry field (max 100 characters)
- Background fill: semi-opaque dark or light depending on detected theme
- Font: system sans-serif at `11px` equivalent
- Text colour: auto-contrast (white on dark fill, dark on light fill)
- Padding: `4px 8px`

**Privacy Redaction (`Redact` tool)**
- Opens a sub-panel:
  ```
  [Username] [Handle] [Timestamp] [Likes/RTs] [Profile Pic] [All Identity]
  ```
- Each preset applies a redaction style:
  - **Username** — Black rectangle over display name
  - **Handle** — Black rectangle over `@handle`
  - **Timestamp** — Black rectangle over relative time ("2h", "Mar 14")
  - **Likes/RTs** — Black rectangle over engagement counts
  - **Profile Pic** — Black circle over avatar
  - **All Identity** — Applies all of the above to the selected tweet card
- Redaction uses solid black fill (`#000000`, `100%` opacity) — not blur — because blur is reversed by X's re-encoding pipeline (KFIP0010 finding)
- User clicks a tweet card to apply the selected redaction preset

### 2. Theme Detection

#### 2.1 Detection Logic

When the annotation toolbar opens, XerahS samples the pixels in the captured region to determine the dominant background tone:

```csharp
public enum XTheme
{
    Light,       // White/light gray background (#FFFFFF, #F7F9F9)
    Dark,        // Dark background (#000000, #15202B)
    BlueTinted,  // Quote tweet blue (#1DA1F2 tint)
    Transparent  // DM semi-transparent overlay
}

public XTheme DetectTheme(SKBitmap capture)
{
    // Sample 5 points across the image
    // Classify based on luminance histogram
    // Return dominant XTheme
}
```

#### 2.2 Style Mapping by Theme

| Annotation Element | Light Theme | Dark Theme | Blue-Tinted |
|---|---|---|---|
| Tweet box border | `#15202B` (dark) | `#FFFFFF` (white) | `#1DA1F2` (X blue) |
| Text label fill | `rgba(21,32,43,0.85)` | `rgba(255,255,255,0.9)` | `rgba(29,161,242,0.85)` |
| Arrow colour | `#E1306C` | `#E1306C` | `#E1306C` |
| Redaction | `#000000` | `#000000` | `#000000` |

### 3. Anti-Degradation Rendering for X's Re-encoding Pipeline

X/Twitter re-encodes all uploaded images as JPEG at quality ~85–90. Annotations are particularly vulnerable because:

- Thin strokes (1px) disappear or become jagged
- Text on transparent backgrounds gets halo artifacts
- High-contrast edges cause posterisation in gradients

The annotation rendering pipeline applies the following safeguards:

#### 3.1 Stroke Protection
- Minimum stroke width: `2px` for all annotation outlines
- Stroke anti-aliasing: use `SKPaint.IsAntialias = true` with `SKPaint.StrokeCap = StrokeCap.Round`
- High-contrast stroke edges get a `1px` background outline ("stroke behind") to preserve edge definition after JPEG compression

#### 3.2 Text Protection
- All text labels get a `2px` background fill behind them (same colour as the text label fill, fully opaque)
- This creates a "letterbox" effect that preserves text legibility through JPEG re-encoding
- Text is rendered at `12px` minimum; no smaller text is permitted in X/Twitter annotations

#### 3.3 Shape Protection
- Filled shapes use a minimum fill opacity of `15%` to ensure they remain visible after compression
- Shape edges use a `1px` border on all filled shapes to define boundaries after compression

### 4. Integration with KFIP0014 Upload Pipeline

#### 4.1 Export Flow

```
[Annotation confirmed]
        │
        ▼
[AnnotationRenderer]
  - Renders all annotation layers onto capture bitmap
  - Applies anti-degradation safeguards (Section 3)
  - Returns final SKBitmap
        │
        ▼
[Pass to XTwitterImagePreprocessor]
  - KFIP0014 pre-softening and quality ladder
  - File size check against 5 MB limit
        │
        ▼
[Upload via configured uploader]
  - Imgur / S3 / Pixelfox (KFIP0014 Section 2)
        │
        ▼
[URL copied to clipboard]
  - User pastes into X compose window
```

#### 4.2 Annotation Persistence

- If the user closes the overlay without confirming, annotations are discarded and the raw capture is kept
- If the user confirms annotations, the annotated bitmap is passed forward; the raw capture is stored in capture history for re-annotation
- The annotation stack (shape type, position, style) is stored in `TaskMetadata.Annotations` for potential re-editing

### 5. Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Shift+A` | Toggle annotation toolbar |
| `Escape` | Close toolbar (discard annotations) |
| `Ctrl+Z` | Undo last annotation |
| `Enter` | Confirm annotations and proceed to upload |
| `1` | Select Tweet tool |
| `2` | Select Thread tool |
| `3` | Select Handle tool |
| `4` | Select Arrow tool |
| `5` | Select Text tool |
| `6` | Select Redact tool |

---

## User Flows

### Flow A: Annotated Screenshot-to-Link (Full Pipeline)

```
User presses PrintScreen (X/Twitter preset active)
        │
        ▼
[Region Selection — 4:5 overlay, X/Twitter badge]
        │
        ▼
[User selects tweet area, releases mouse]
        │
        ▼
[Annotation Toolbar — Shift+A hint shown]
        │
        ├── User presses Shift+A
        │
        ▼
[Tweet tool selected, user clicks tweet card]
        │
        ▼
[Redact tool → "Handle" → click handle]
        │
        ▼
[Text tool → click → type "Source:"]
        │
        ▼
[Press Enter to confirm]
        │
        ▼
[Anti-degradation render → Pre-softening → 5MB check]
        │
        ▼
[Upload via Imgur → URL copied to clipboard]
        │
        ▼
[Notification: "Annotated screenshot uploaded. Link copied."]
```

### Flow B: Quick Privacy Redact (Minimal)

```
User captures DM screenshot
        │
        ▼
[Annotation Toolbar → Redact → "All Identity"]
        │
        ▼
[Click DM bubble]
        │
        ▼
[Username, handle, timestamp, avatar all redacted automatically]
        │
        ▼
[Confirm → Upload → Link copied]
```

---

## Alternatives Considered

### Alternative 1: General-Purpose Annotation Layer (No X/Twitter Specifics)

**Description:** Add generic annotation tools (rectangle, arrow, text) to XerahS without X-specific tooling.

**Why rejected:** Generic annotation is already available via external tools (ShareX's built-in editor, Snipping Tool, Lark). The value XerahS adds is specifically the X/Twitter domain knowledge — tweet card geometry, identity element presets, theme detection, and anti-degradation rendering. A generic layer would not solve the three core problems identified in Section 2.

### Alternative 2: AI-Generated Annotations

**Description:** Use a vision model to auto-detect tweets, extract context, and suggest annotations.

**Why rejected:** AI annotation introduces latency (network call), cost, and nondeterminism. Users annotating for research or journalism need precise, reproducible annotation placement. AI suggestions may misidentify elements, introduce hallucinations, or change interpretation of the captured content. A deterministic rule-based system is more appropriate for this use case. AI-assisted features (auto-captioning, smart text extraction) are tracked as Phase 3.

### Alternative 3: Post-Capture Annotation Only

**Description:** Keep annotation as a separate AfterCapture task, not in the capture overlay.

**Why rejected:** Context-switching out of the capture overlay to a separate editor breaks the capture flow and adds friction. Annotation is fastest when integrated into the selection moment — the user has the region in mind and can immediately click the tweet to annotate. Separation would reduce adoption among power users who want a one-hotkey annotated capture.

---

## Compatibility Notes

### KFIP Dependencies

| KFIP | Dependency | Required Before |
|---|---|---|
| KFIP0014 (Screen Capture for X/Twitter) | Capture preset, upload pipeline, URL copy | Phase 1 (this KFIP) |
| KFIP0010 (Compression-Resilient Capture) | Pre-softening pipeline, anti-degradation techniques | Phase 1 |
| KFIP0009 (Share-Ready Enhancements) | Annotation overlay integration points | Phase 1 |
| KFIP0005 (Social Sharing Workflows) | Social capture preset model | Phase 1 |

### Platform Compatibility

- **Windows 10/11**: Primary platform; full feature support
- **macOS**: Annotation toolbar available via ShareX compatibility layer (future)
- **SkiaSharp**: Used for all annotation rendering (cross-platform)

### Settings Schema

- New field `AnnotationSettings` in capture preset JSON
- Annotation toolbar shortcuts stored in `HotkeySettings`
- Annotation history (last 10 annotations) stored in capture history for undo/re-edit

---

## Revision History

| Revision | Date | Author | Changes |
|---|---|---|---|
| 1.0.0 | 2026-07-19 | Nadia (Research, KovaForge) | Initial draft |
