# XIP0073: Smart Region Capture Profiles

**Status**: Draft
**Priority**: P2
**Area**: Region Capture | UX | Screen Capture
**Created**: 2026-04-12
**Co-Authors**: Milena (research)

---

## Summary

XerahS lacks smart, reusable region capture templates. Users who repeatedly capture the same UI elements — tweet composer, media viewer, chat windows, code blocks — manually draw regions every time. Smart Region Capture Profiles would auto-detect common UI patterns and suggest capture regions, reducing friction for repetitive workflows.

---

## Problem Statement

### User Pain

Power users of screen capture tools capture the same regions dozens of times per day:
- Tweet compose box (Twitter/X web app)
- Media/image viewer panels
- Chat message windows (Discord, Slack)
- Code blocks in GitHub PRs
- Terminal windows (specific pane layouts)

Each capture requires drawing a region manually. Over a week, this is hundreds of redundant interactions.

### Current Workarounds

1. Fixed region shortcuts — users assign hotkeys to fixed pixel coordinates, but these break across DPI changes, window resizes, and multi-monitor setups
2. Bookmark regions — save coordinates as named bookmarks, but still requires manual selection
3. OCR-based tools — some tools auto-detect content boundaries, but none integrate natively with XerahS

### Market Gap

- **ShareX** (upstream): No smart region detection; only fixed bookmark regions
- **Snagit**: Has named capture templates but requires manual setup, no auto-detection
- **Xnip** (macOS): No smart detection
- **Lightshot**: No smart detection
- **Flameshot**: No smart detection

None of the popular tools auto-detect common capture targets based on window context.

---

## Proposed Solution

### Feature: Capture Profile System

**Three layers:**

1. **Pattern Recognizer Service** — analyzes window metadata and visible UI controls to detect common capture targets
2. **Profile Store** — persists named capture profiles with auto-generated region specs
3. **Suggestion UI** — non-intrusive overlay showing detected targets when region capture is invoked

### Architecture

```
CaptureProfileService
  ├── PatternRecognizer (Win32/Accessibility API for window control detection)
  │     ├── DetectTweetComposer() — Twitter/X web tweet box
  │     ├── DetectMediaViewer() — image/video preview panels
  │     ├── DetectChatWindow() — Discord/Slack message panes
  │     └── DetectCodeBlock() — GitHub/source code render areas
  ├── ProfileStore (JSON + SQLite, user profiles + curated defaults)
  │     ├── UserCreatedProfiles
  │     └── CuratedDefaultProfiles (shipped with XerahS)
  └── SuggestionEngine
        ├── OverlayPopover (shown during active region selection)
        └── QuickCaptureHotkeys (F1-F12 mapping to top detected regions)
```

### Pattern Recognition Strategy

**Layer 1 — Window Class + Title matching:**
- Match `Chrome_WidgetWin_1` + title patterns for Twitter, Discord, Slack, VS Code
- Low compute cost, high accuracy for known apps

**Layer 2 — Accessibility/UI Automation:**
- Use Windows `AccessibleObjectFromWindow` to enumerate child windows
- Identify content panes by class (`Chrome_RenderWidgetHostHWND`, `AfxFrameOrView`, `TBrowserChild`)
- More accurate but requires accessibility permissions

**Layer 3 — Visual Boundary Detection:**
- Screenshot + edge detection to find content boundaries
- Used as fallback when Layer 1 and 2 don't match known patterns
- Optional: ML model trained on common UI layouts (future XIP)

### Default Curated Profiles (Shipped)

| Profile Name | Target | Detection Logic |
|-------------|--------|-----------------|
| Twitter Tweet Box | `https://x.com/*/compose/*` | URL + element class |
| Discord Message | Discord app | Window class + message pane bounds |
| GitHub Code Block | github.com | URL path contains `/blob/` or `/pull/` |
| VS Code Editor | Code.exe/Code | Window class + editor pane |
| Chrome Media Viewer | Chrome + video/image element | Tab title patterns |

### Profile Storage Format

```json
{
  "id": "twitter-tweet-box",
  "name": "Twitter Tweet Box",
  "version": 1,
  "created": "2026-04-12",
  "detection": {
    "type": "url+element",
    "urlPattern": "https://x.com/*/compose/*",
    "elementClass": "public-DraftStyleDefault-block",
    "padding": { "top": 8, "bottom": 8, "left": 16, "right": 16 }
  },
  "capture": {
    "type": "relative",
    "anchor": "element",
    "includeDecoration": false
  },
  "hotkey": "F1"
}
```

### User Experience

**First-time flow:**
1. User activates region capture (global hotkey or tray menu)
2. XerahS shows live preview with detected regions highlighted
3. User clicks a highlighted region OR continues manual selection
4. Profile is auto-saved with one-click "Save as Profile" option

**Returning user flow:**
1. User activates region capture
2. Top 3 suggested regions shown as floating overlay with names
3. Single-click to capture OR press hotkey OR manual selection

---

## Implementation Phases

### Phase 1 — Profile Store + Basic Detection (P2)

- [ ] `CaptureProfileService` with SQLite store
- [ ] Hardcoded curated profiles for Twitter, Discord, GitHub, VS Code
- [ ] Window class + title matching as primary detection
- [ ] Suggestion overlay with clickable regions

### Phase 2 — UI Automation Detection (P3)

- [ ] Accessibility API integration for child window enumeration
- [ ] More accurate content pane detection
- [ ] Permission handling for accessibility APIs

### Phase 3 — Visual Boundary Detection (P4)

- [ ] Edge detection fallback for unknown apps
- [ ] User training: capture → "Teach this region" flow
- [ ] Profile export/import

### Phase 4 — ML-Enhanced Detection (Future)

- [ ] Lightweight ML model for common UI pattern recognition
- [ ] User-contributed profile sharing (via XerahS社区)

---

## Affected Files

- `src/desktop/app/XerahS.RegionCapture/` — new `CaptureProfileService`, `ProfileStore`
- `src/desktop/core/XerahS.Common/` — pattern matching utilities
- `src/desktop/app/XerahS.UI/` — suggestion overlay, profile manager UI
- `src/platform/XerahS.Platform.Windows/` — Win32/Accessibility API wrappers
- `docs/proposals/xip/XIP0073-smart-region-capture-profiles.md` — this document

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| Accessibility API blocked by permissions | Medium | Medium | Graceful fallback to window class matching |
| Profiles break across app updates | Medium | Low | Versioned profiles; app update detection |
| Overlapping regions on complex layouts | Medium | Low | User can dismiss suggestions; manual override always available |
| Performance impact on region capture activation | Low | Low | Detection runs async; suggestion overlay is lazy-loaded |
| Privacy concerns (screen content analysis) | Low | High | All processing local; no telemetry; clear privacy UI |

---

## Acceptance Criteria

- [ ] User can activate region capture and see up to 3 suggested regions
- [ ] Clicking a suggested region captures it correctly
- [ ] Curated profiles for Twitter, Discord, GitHub, VS Code are included
- [ ] User can save a custom profile from any capture
- [ ] Profiles persist across app restarts
- [ ] Profiles survive window resize within reasonable bounds (±10% size change)
- [ ] Performance: suggestion overlay appears within 500ms of region capture activation
- [ ] Accessibility API fallback works when window class matching fails
- [ ] User can delete or edit saved profiles

---

## Open Questions

1. **Cross-platform**: macOS/Linux equivalent for accessibility APIs? macOS has `NSAccessibility`, Linux has AT-SPI. Scope Phase 2 to Windows first.
2. **Profile conflicts**: Two overlapping profiles detected simultaneously — show both or pick best match?
3. **Hotkey conflicts**: F1-F12 may conflict with user-defined shortcuts. Validate on profile creation.
4. **Privacy**: Accessibility API reads UI tree. Any PII concerns? (All local, no telemetry — document this explicitly)
