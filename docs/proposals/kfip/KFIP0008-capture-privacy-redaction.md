# KFIP0008: Capture Privacy Redaction — Quick Redact and Smart PII Detection for X/Twitter Shares

**Status**: Proposed
**Priority**: P2
**Area**: Capture | Privacy | Image Annotation | AfterCapture
**Created**: 2026-05-12
**Related**: KFIP0003 (X/Twitter Context Detection), KFIP0005 (Social Sharing Workflows), KFIP0007 (Capture Command Palette)
**Owner**: KovaForge
**Co-Authors**: Nadia (analysis)

---

## Summary

X/Twitter screenshots are one of the most common capture use cases — and they routinely contain personal information the user doesn't intend to share: names in like/retweet lists, follower counts, profile bios, quoted text with handles, DM indicators, and incidental screen content. There's no structured way in XerahS to redact this before sharing. This KFIP adds a **Capture Privacy Redaction layer**: a privacy toolbar in the image editor with manual redact tools (blur, pixelate, black-box) and a smart PII detector that identifies and suggests redacting phone numbers, email addresses, and X handles in captured content, all scoped to the post-capture editing phase before the user commits to sharing.

---

## Problem Statement

### Privacy Leaks in X/Twitter Captures

When users screenshot X/Twitter content for sharing:

- **Profile context leakage**: Liking/retweet lists show follower counts, profile pictures, and handles the user hasn't consented to expose
- **Handle metadata**: Quote-tweets and replies include handles that link the capturer to specific accounts
- **Incidental personal data**: Following/followers sidebar, notification indicators, reply threads — content the user's post doesn't need
- **DM indicators**: Any screenshot taken near the DM tab risks capturing notification badges or conversation previews
- **Phone/email in bio**: Profile screenshots frequently include phone numbers or email addresses in bio fields

### Current Redaction Options Are Primitive

The existing image editor in ShareX.ImageEditor has basic drawing tools. Blur is available as a filter (TiltShiftImageEffect, LensBlurImageEffect, etc.), pixelate is not a first-class tool, and black-box redacting requires drawing a filled rectangle and hoping it's opaque enough. There is no tool that:

1. Understands X/Twitter UI layout to suggest redaction regions
2. Detects PII patterns (phone, email, handle) in the captured image
3. Provides a one-tap "redact all detected PII" action
4. Integrates into the AfterCapture pipeline so privacy protection is automatic, not opt-in

### Evidence

- PostCapture (postcapture.com) and similar Chrome extensions target "clean tweet screenshots" — but they operate on web-rendered content, not native captures. Users who capture native browser views still manually blur in third-party apps.
- BlurData (discuss.privacyguides.net), BlurShot (blurshot.io), uBlur (ublur.app) — all independent tools that gained traction in 2025–2026 for screenshot redaction. The demand is documented in user discussions, not just feature requests.
- KFIP0005's social sharing workflow (Step 8: "click attach image") has no privacy step — users attach whatever they captured, having either manually redacted beforehand or not at all.
- KFIP0007's command palette context detection could surface "x-tweet detected → privacy redact?" as a suggestion, but no redaction infrastructure exists to fulfill it.

---

## Goals

- Provide blur, pixelate, and solid black-box redaction tools as first-class toolbar buttons (not buried in filter menus)
- Implement smart PII detection on captured images: phone numbers, email addresses, X handles (@username patterns)
- Surface a "Redact Detected PII" one-click action with confirmation highlighting before applying
- Integrate as an AfterCapture task option so users can set "always run privacy redaction" as a default step
- Expose privacy tools in the image editor toolbar with clear iconography; keyboard shortcut `R` for redact mode
- Respect context from KFIP0003: when a tweet-view window is detected, pre-highlight the name/handle region as a suggested redact zone

## Non-Goals

- No automatic uploading or sharing (KFIP0005 handles that)
- No browser extension or DOM parsing (KFIP0003 handles URL/window detection only)
- No OCR-based text detection for redaction (KFIP0001 has DoOCR; this KFIP uses regex pattern matching on image regions, not full OCR — though a future integration could combine them)
- No facial blur (out of scope; different legal/complexity profile)
- No cloud-based redaction service — all processing is local

---

## Proposed Solution

### 1. Privacy Toolbar (Image Editor)

Add a dedicated privacy toolbar strip to the image editor, positioned below the existing annotation toolbar:

```
[ 🔲 Black Box ] [ ◐ Blur ] [ ▦ Pixelate ] [ ◎ Detect PII ] [ ✓ Apply ]
```

| Tool | Behavior | Keyboard |
|------|----------|----------|
| Black Box | Draws an opaque black rectangle; fill color configurable (black, white, custom) | `R` then `1` |
| Blur | Brush-based Gaussian blur with adjustable radius (default 15px); uses LensBlurImageEffect or a dedicated box blur | `R` then `2` |
| Pixelate | Brush-based pixelation with adjustable block size (default 8px); no existing ShareX filter, new implementation | `R` then `3` |
| Detect PII | Scans visible image area; highlights detected patterns with a pulsing magenta overlay; user confirms before applying | `R` then `D` |
| Apply | Commits all pending redaction marks as permanent pixel changes | `Enter` |

**Tool state machine:**
- Default editor mode: pan/zoom, select, draw annotations
- Redact mode active: cursor becomes crosshair; black box, blur, or pixelate brush follows cursor; right-click or `Esc` exits redact mode
- Detect PII runs asynchronously; results appear as semi-transparent magenta overlays with dashed bounding boxes
- "Apply" commits overlays as actual pixel changes; "Clear" (right-click during Detect results) dismisses without applying

### 2. Smart PII Detection

Pattern matching on captured image regions:

| Pattern | Regex | False Positive Risk |
|---------|-------|----------------------|
| Email address | `[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}` | Low; requires @ and TLD |
| Phone number | `\+?[0-9]{10,15}` (international formats) | Medium; crop/ratio context helps |
| X handle | `@[a-zA-Z0-9_]{1,15}` | Low; very specific format |
| URL fragments | `x\.com/[a-zA-Z0-9_]+` | Low |

**Implementation approach:**
- Run detection on the raw captured bitmap using SkiaSharp pixel sampling (no OCR dependency for v1)
- Resample image to a fixed width (e.g., 800px) for detection pass to keep performance reasonable
- Report bounding boxes in original image coordinate space
- Confidence: High for email and handles (low false positive rate); Medium for phone (may flag non-PII number sequences — user confirmation step mitigates)

**Scalability note:** For captured regions larger than ~4MP, detection runs on a downsampled proxy to avoid OOM. Full-resolution redaction is applied to the original on commit.

### 3. Context Integration (KFIP0003)

When `TweetRegionHint` from KFIP0003 returns a tweet-view detection at confidence > 0.7:

- Pre-highlight the author handle region as a suggested redact zone (magenta overlay, dashed border, "Suggested: author handle" label)
- Pre-highlight the like/retweet action bar (often shows avatar stack) as a suggested redact zone
- Detection runs automatically on capture; user sees "3 PII items detected — review?" toast notification

### 4. AfterCapture Integration

New `AfterCaptureTasks` flag:

```csharp
[Flags]
public enum AfterCaptureTasks
{
    None = 0,
    DoOCR = 1 << 17,       // KFIP0001
    DoPrivacyRedact = 1 << 18,  // KFIP0008 — NEW
    // ...existing flags...
}
```

When enabled:
1. Capture completes → image opens in editor
2. If `DoPrivacyRedact` is set, `Detect PII` runs automatically in the background
3. Toast: "Privacy scan complete. 3 items detected."
4. User can apply, dismiss, or manually add more redaction marks
5. If user closes editor without applying, a confirmation dialog asks "Discard redactions?"

### 5. Keyboard Shortcuts (Redact Mode)

| Key | Action |
|-----|--------|
| `R` | Enter redact mode (shows redact toolbar) |
| `1` | Select Black Box tool |
| `2` | Select Blur tool |
| `3` | Select Pixelate tool |
| `D` | Run Detect PII |
| `Enter` | Apply all redactions |
| `Esc` | Exit redact mode / cancel |
| `Right-click` | Cancel current redact stroke |

---

## Technical Design

### Architecture

```
src/desktop/core/XerahS.Core/
├── Services/
│   ├── PrivacyRedactionService.cs      // PII detection, bounding box generation
│   └── IPrivacyRedactionService.cs

src/ShareX.ImageEditor/
├── Core/
│   ├── RedactionTools/
│   │   ├── BlackBoxTool.cs
│   │   ├── BlurBrushTool.cs
│   │   ├── PixelateBrushTool.cs
│   │   └── RedactionToolManager.cs
│   └── PII/
│       ├── PIIPattern.cs               // Regex + bounding box
│       ├── PIIOverlay.cs               // Magenta highlight overlay
│       └── PIIOverlayRenderer.cs       // Draws overlays on image canvas

src/desktop/app/XerahS.UI/
├── ViewModels/
│   └── ImageEditorViewModel.cs         // Add redact mode state
├── Views/
│   ├── ImageEditorWindow.axaml         // Add privacy toolbar
│   └── Controls/
│       └── PrivacyToolbar.axaml
└── Services/
    └── AfterCaptureRedactionIntegration.cs  // AfterCapture task runner
```

### File Structure Changes

```
src/
├── ShareX.ImageEditor/
│   └── src/ShareX.ImageEditor/
│       └── Core/
│           └── RedactionTools/
│               ├── BlackBoxTool.cs          [NEW]
│               ├── BlurBrushTool.cs          [NEW — wrapper around existing blur]
│               ├── PixelateBrushTool.cs      [NEW]
│               └── RedactionToolManager.cs   [NEW]
│           └── PII/
│               ├── PIIPattern.cs             [NEW]
│               ├── PIIOverlay.cs            [NEW]
│               └── PIIOverlayRenderer.cs    [NEW]

src/desktop/core/XerahS.Core/
└── Services/
    ├── IPrivacyRedactionService.cs     [NEW]
    └── PrivacyRedactionService.cs      [NEW]

src/desktop/app/XerahS.UI/
├── Views/ImageEditorWindow.axaml       [MOD — add toolbar]
├── ViewModels/ImageEditorViewModel.cs  [MOD — redact mode]
├── Services/
│   └── AfterCaptureRedactionIntegration.cs  [NEW]
└── TaskSettingsViewModel.AfterCapture.cs  [MOD — add DoPrivacyRedact flag]
```

### Key Implementation Notes

**Blur brush**: Uses a stacked box blur (3-pass, 5px radius = perceived Gaussian) to avoid the complexity of the full LensBlurImageEffect pipeline. Faster and sufficient for screenshot redaction use cases.

**Pixelate brush**: New effect. For each brush stroke region, divide into N×N blocks, sample the average color, fill the block. N is configurable (default 8px). No existing ShareX filter does this.

**PII detection (PrivacyRedactionService)**: 
- Input: `SKBitmap` image, `Rectangle? regionOfInterest`
- Output: `IReadOnlyList<PIIMatch>` where each match has `BoundingBox`, `PatternType`, `Confidence`
- Algorithm: Resample to proxy resolution → apply regex to pixel brightness pattern matching (heuristic — no OCR). For email/handle patterns, look for sequences of light-on-dark pixels with the right spatial regularity. This is heuristic, not OCR-accurate, but handles the majority of cases for X/Twitter screenshots where text is rendered in known fonts and sizes.
- Threshold: Only surface matches with confidence > 0.6 as interactive overlays.

**Overlay commit**: When user clicks "Apply", `PIIOverlayRenderer` composites the confirmed overlays onto the working image as actual pixel changes. Undo stack is preserved (single undo level for the apply action).

---

## Acceptance Criteria

### Functional

- [ ] Privacy toolbar appears in image editor with Black Box, Blur, Pixelate, Detect PII, Apply buttons
- [ ] `R` enters redact mode; `Esc` exits; `1`/`2`/`3` select tools within redact mode
- [ ] Black box tool draws an opaque filled rectangle on mouse drag
- [ ] Blur tool applies visible Gaussian blur to dragged region (5px radius default)
- [ ] Pixelate tool applies visible pixelation to dragged region (8px block size default)
- [ ] Detect PII scans the image and shows magenta overlay on any detected email, phone, or handle pattern with confidence > 0.6
- [ ] "Apply" commits all overlays as permanent pixels
- [ ] "Clear" dismisses overlays without applying
- [ ] AfterCapture `DoPrivacyRedact` flag triggers automatic detection scan after capture
- [ ] When KFIP0003 tweet detection fires, author handle and avatar regions are pre-highlighted as suggested redaction zones

### Quality

- [ ] Detect PII does not produce false positives on non-text image regions at a rate that frustrates users (>10% false positive rate disqualifies the feature in its current form)
- [ ] Blur and pixelate brush operations complete within 500ms on 1080p captures on reference hardware
- [ ] Undo restores pre-redaction image state (single undo level)
- [ ] Privacy toolbar is keyboard-navigable (Tab/arrow keys) and screen-reader accessible

### Edge Cases

- [ ] PII detection on very small text (<12px font in original) gracefully returns no matches rather than producing garbage bounding boxes
- [ ] Detect PII on a dark-mode X/Twitter screenshot vs light-mode screenshot — both produce correct patterns
- [ ] User cancels redaction: confirmation dialog "Discard pending redactions? [Discard] [Keep Editing]"
- [ ] Very large image (>8MP): detection runs on 4MP proxy, full resolution redaction applied on commit
- [ ] No PII detected: toast "No personal information detected in this capture"

---

## Phased Implementation

### Phase 1: Manual Redaction Tools

- [ ] Add privacy toolbar to ImageEditorWindow.axaml
- [ ] `BlackBoxTool.cs` — simple filled rect on drag
- [ ] `BlurBrushTool.cs` — box blur implementation (3-pass, 5px default)
- [ ] `PixelateBrushTool.cs` — block averaging implementation
- [ ] `RedactionToolManager.cs` — tool state machine
- [ ] `R` / `1` / `2` / `3` / `Esc` key bindings in ImageEditorViewModel
- [ ] Tests: tool state transitions, blur output validity, pixelate block integrity

### Phase 2: PII Detection

- [ ] `PrivacyRedactionService.cs` — regex pattern matching on image pixel data (heuristic)
- [ ] `PIIOverlay.cs` / `PIIOverlayRenderer.cs` — overlay display
- [ ] "Apply" / "Clear" actions wired to overlay renderer
- [ ] Integration with KFIP0003 `TweetRegionHint` for pre-highlighting
- [ ] Tests: pattern match accuracy on synthetic X/Twitter screenshot images

### Phase 3: AfterCapture Pipeline

- [ ] `AfterCaptureRedactionIntegration.cs` — runs detection as an AfterCapture task
- [ ] `DoPrivacyRedact` flag in `AfterCaptureTasks` enum
- [ ] Checkbox in TaskSettingsPanel.axaml
- [ ] "Discard redactions?" confirmation dialog on editor close without apply
- [ ] Tests: AfterCapture pipeline integration, flag persistence

### Phase 4: Polish

- [ ] Configurable blur radius and pixelate block size (settings)
- [ ] Black box fill color option (black, white, custom)
- [ ] Tooltip overlays on PII matches showing pattern type ("Email", "Handle", "Phone")
- [ ] Screen reader labels for all toolbar controls

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| PII detection produces too many false positives on complex screenshots | User dismisses the feature and manually blurs everything | Start with high-confidence patterns only (email, handle); set threshold at 0.7; ship with user-visible "too many false positives?" feedback mechanism |
| Heuristic pixel-pattern detection fails on non-standard X/Twitter themes | Detection silently misses PII, user shares without realizing | KFIP0003 context pre-highlighting compensates for detection gaps; always provide manual tool as fallback |
| Blur brush on large regions causes UI freeze | User perception of instability | Async rendering on background thread; show progress indicator for regions > 0.5MP |
| PixelateBrushTool is net-new code | No existing ShareX filter to extend; higher test burden | Implement as pure pixel array manipulation (not a filter chain); easy to unit test independently |
| Users already use third-party blur tools (uBlur, etc.) | Displaces existing workflow only if detection accuracy exceeds manual effort | Phase 1 ships manual tools only; PII detection is additive; user can ignore it entirely |

---

## Open Questions

1. **Should auto-blur be an option?** Rather than just suggesting PII regions, a "auto-blur detected PII on capture" settings toggle would run detection + apply without the confirmation step. This is risky — false positives on auto-blur could hide content the user actually wanted visible. Recommend against for v1; keep it as "suggest + confirm."

2. **Should redaction be undoable after file save?** Currently redactions are permanent pixel changes. A "redact layer" approach (non-destructive editing) would allow undo after save, but adds significant complexity and doesn't match the existing ShareX editor model. Accept permanent redactions as the design decision.

3. **Should Detect PII use DoOCR (KFIP0001) as a backend?** Running DoOCR first to get text, then applying regex to the recognized text, would be far more accurate than pixel heuristics. However, this creates a dependency on KFIP0001 being implemented and on OCR results being reliable on small/low-contrast text in screenshots. Option: implement pixel heuristics as v1; add OCR backend behind a feature flag in a future phase.

4. **Should we integrate with KFIP0007 (Command Palette) for "redact mode" invocation?** Yes — when the user opens the palette while editing an image, a "Redact Mode" or "Privacy Scan" item should be available. Phase 2 work after Phase 1 tools exist.

---

## Success Metrics

- **Tool adoption**: >40% of users who open the image editor use at least one privacy tool within 30 days of release
- **False positive rate**: <10% of detected PII matches are false positives (measured via dismiss rate)
- **Time savings**: Users who enable `DoPrivacyRedact` AfterCapture flag spend <5 seconds on privacy cleanup vs. manual workflow time (measured via user survey at 60-day mark)
- **Detection accuracy**: Email and handle detection achieves >90% recall on standard X/Twitter screenshot content

---

## Related Work

- **KFIP0001 (AfterCapture OCR)**: No direct dependency, but OCR backend option for future PII detection improvement
- **KFIP0003 (X/Twitter Context Detection)**: Context signals power pre-highlighting; KFIP0008 builds on `TweetRegionHint` from KFIP0003
- **KFIP0005 (Social Sharing Workflows)**: Redaction precedes sharing; KFIP0008 is a dependency for a "privacy-aware" social sharing preset
- **KFIP0007 (Capture Command Palette)**: Redact mode invocation via palette; Phase 2+ integration
