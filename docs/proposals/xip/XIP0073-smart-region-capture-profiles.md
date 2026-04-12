# XIP0073: Smart Region Capture Profiles — Automatic UI Pattern Detection

**Status**: Draft
**Priority**: P1
**Area**: Region Capture | UX | Machine Learning | Screen Recording
**Created**: 2026-04-12
**Related**: XIP0072 (Screen Recording Bug Fixes), XIP0070 (User Research - Top Screen Capture Needs)
**Co-Authors**: Milena (research), TBD (implementation)

---

## Summary

Users spend an inordinate amount of time manually drawing capture regions for repetitive tasks — tweets, media players, chat windows, code snippets, and documentation panels. This XIP proposes **Smart Region Capture Profiles**: an intelligent system that automatically detects and suggests capture regions based on common UI patterns, reducing friction and saving time on every capture.

The system combines computer vision heuristics, window manager introspection, and learned user preferences to present one-click capture options for the most common capture targets.

---

## Problem Statement

### The Repetitive Drag Problem

Current region capture workflow:
1. User initiates capture (hotkey or UI)
2. User manually drags to define region
3. User adjusts edges for precision
4. Capture executes

For repetitive captures (e.g., documenting a series of tweets, capturing chat messages, recording a video player), steps 2-3 become tedious overhead. Users report:
- **"I capture tweets maybe 10 times a day — every time I'm drawing the same rough rectangle"**
- **"Chat windows are always the same size, but I still have to manually select each time"**
- **"I wish it just knew I wanted the video player region"**

### Current Workarounds and Their Limits

| Workaround | Limitation |
|------------|------------|
| Window capture mode | Captures entire window chrome (title bar, borders) — not the content region |
| Last region memory | Only works for identical positions; breaks on window moves or different monitors |
| Preset regions | Static; doesn't adapt to window position changes or different content layouts |
| Manual coordinate entry | Power-user only; defeats the purpose of visual selection |

### Why This Matters Now

- XIP0070 user research identified "faster region selection" as a top-3 requested feature
- XIP0072 fixed underlying capture pipeline bugs — the foundation is now stable for UX improvements
- Competitors (CleanShot X, Snagit) have basic smart detection; XerahS can differentiate with deeper pattern recognition

---

## Proposed Solution

### Core Concept: Detectable UI Patterns

Define a set of **detectable patterns** that represent common capture targets:

| Pattern | Description | Detection Method |
|---------|-------------|------------------|
| **Tweet/Post Card** | Social media content blocks (text + media + metadata) | DOM-like heuristics + aspect ratio + content density |
| **Video Player** | Embedded or standalone video regions | Aspect ratio (16:9, 4:3, 21:9) + motion detection + platform signatures |
| **Chat Message** | Individual or grouped chat bubbles | Platform-specific window classes + text density + bubble geometry |
| **Code Block** | Syntax-highlighted code regions | Monospace font detection + indentation patterns + scrollbars |
| **Image/Media** | Photos, diagrams, screenshots within a page | Large contiguous color regions + aspect ratio + border detection |
| **Modal/Dialog** | Popup windows and overlays | Z-order + centered positioning + dimmed background detection |
| **Documentation Panel** | API docs, README renderers | Heading structure + code block adjacency + scrollbar presence |

### Detection Pipeline

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Screen Buffer  │────▶│  Pattern Detector│────▶│  Scored Regions │
│   (DXGI/GL)     │     │  (Heuristics+ML) │     │  (Confidence)   │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                                                           │
                           ┌──────────────────┐           │
                           │  User Preferences│◀──────────┘
                           │  (Learned/Set)   │
                           └──────────────────┘
                                    │
                                    ▼
                           ┌──────────────────┐
                           │  Suggested Region│
                           │  Overlay (UI)    │
                           └──────────────────┘
```

### Phase 1: Heuristic-Based Detection (P1)

**Scope**: Rules-based detection without ML dependencies

**Implementation**:

1. **Window Manager Introspection**
   - Query window bounds and classes (Windows: `EnumWindows`, macOS: `AXUIElement`, Linux: `xwininfo`/`wlroots`)
   - Identify known app signatures (Chrome, Firefox, Slack, Discord, VS Code, etc.)
   - Map app-specific content regions (e.g., Chrome's viewport excluding dev tools)

2. **Visual Heuristics**
   - Aspect ratio analysis (16:9 → likely video, 1:1 or 4:5 → likely image/post)
   - Edge detection for content boundaries
   - Color histogram analysis (video = motion, static image = stable)
   - Text density detection (OCR-lite via connected component analysis)

3. **Platform-Specific Rules**
   - Twitter/X: Detect tweet cards by layout signature (avatar column + content column)
   - YouTube: Detect `#movie_player` equivalent regions
   - Slack/Discord: Detect message list containers
   - VS Code: Detect editor tabs and terminal panels

**Confidence Scoring**:
```csharp
public class DetectedRegion
{
    public Rect Bounds { get; set; }
    public DetectedPattern Pattern { get; set; }
    public float Confidence { get; set; } // 0.0 - 1.0
    public string SourceApp { get; set; }
    public Dictionary<string, float> FeatureScores { get; set; }
}
```

### Phase 2: Learned Preferences (P2)

**Scope**: Per-user learning from capture history

**Implementation**:

1. **Capture History Analysis**
   - Store anonymized capture metadata (region bounds, app, time, pattern type)
   - Identify recurring patterns: "user often captures 600×400 regions in Chrome at coordinates (X,Y)"
   - Predict next capture region based on current app context

2. **Adaptive Suggestions**
   - If user consistently adjusts a detected region, learn the offset
   - Prioritize recently-used patterns
   - Detect "capture sequences" (e.g., tweet thread documentation → suggest next tweet down)

3. **Privacy-First Design**
   - All learning is local-only
   - No cloud processing or telemetry
   - User can export/delete learned data

### Phase 3: ML-Enhanced Detection (P3)

**Scope**: On-device neural network for pattern recognition

**Implementation**:

1. **Model Architecture**
   - Lightweight CNN (MobileNet-style) for edge detection and object segmentation
   - Runs on CPU; GPU acceleration optional
   - Model size target: <10MB

2. **Training Data**
   - Synthetic: Rendered UI components across platforms
   - Curated: Public screenshots with annotated regions (CC0 licensed)
   - User-contributed: Opt-in anonymized captures

3. **Deployment**
   - Model updates via app updates (not auto-download for security)
   - Fallback to Phase 1 heuristics if model fails

---

## User Experience

### Capture Flow with Smart Regions

1. **User initiates capture** (hotkey/UI)
2. **Screen dims slightly** (existing behavior)
3. **Detected regions highlighted** with subtle borders:
   - Color-coded by pattern type (blue = video, green = tweet, purple = code)
   - Confidence indicator (solid = high, dashed = medium)
   - Keyboard shortcuts (1, 2, 3... for top suggestions)
4. **User options**:
   - Click a suggested region → immediate capture
   - Hover for preview → shows what will be captured
   - Drag custom region → fallback to manual mode
   - Press Escape → cancel

### Settings and Customization

```csharp
public class SmartCaptureSettings
{
    public bool EnableSmartDetection { get; set; } = true;
    public bool LearnFromCaptures { get; set; } = true;
    public float MinimumConfidence { get; set; } = 0.6f;
    public List<DetectedPattern> EnabledPatterns { get; set; }
    public Dictionary<DetectedPattern, Hotkey> PatternHotkeys { get; set; }
}
```

**Per-Pattern Toggles**:
- [x] Tweet/Post Cards
- [x] Video Players
- [x] Chat Messages
- [x] Code Blocks
- [x] Images/Media
- [x] Modals/Dialogs
- [x] Documentation Panels

**Advanced Options**:
- Confidence threshold slider
- Show/hide confidence indicators
- Custom pattern definitions (power users)

---

## Technical Implementation

### New Components

```
src/desktop/app/XerahS.RegionCapture/
├── SmartCapture/
│   ├── Detectors/
│   │   ├── IRegionDetector.cs
│   │   ├── HeuristicRegionDetector.cs
│   │   ├── WindowManagerDetector.cs
│   │   └── MlRegionDetector.cs (Phase 3)
│   ├── Models/
│   │   ├── DetectedRegion.cs
│   │   ├── DetectedPattern.cs
│   │   └── DetectionResult.cs
│   ├── Patterns/
│   │   ├── IPatternMatcher.cs
│   │   ├── TweetPatternMatcher.cs
│   │   ├── VideoPatternMatcher.cs
│   │   ├── ChatPatternMatcher.cs
│   │   └── CodePatternMatcher.cs
│   ├── Learning/
│   │   ├── ICaptureHistoryStore.cs
│   │   ├── LocalHistoryStore.cs
│   │   └── PreferenceLearner.cs
│   └── UI/
│       ├── SmartRegionOverlay.cs
│       ├── DetectedRegionHighlight.cs
│       └── ConfidenceIndicator.cs
```

### Platform-Specific Detection

**Windows**:
- `UI Automation` API for accessible element trees
- `Graphics.Capture` API for window thumbnails
- `DwmGetWindowAttribute` for chrome/client area distinction

**macOS**:
- `Accessibility` framework for element introspection
- `ScreenCaptureKit` for efficient frame capture
- `AXUIElement` for app-specific region queries

**Linux**:
- X11: `xwininfo`, `xprop`, `XGetWindowAttributes`
- Wayland: `zwlr_foreign_toplevel_manager` + custom protocols
- Fallback: Pure visual heuristics (no window manager introspection)

### Performance Considerations

| Concern | Mitigation |
|---------|------------|
| Detection latency | Run detection in background thread; cache results for 100ms |
| Memory usage | Process downsampled thumbnails (max 1080p) for analysis |
| Battery impact | Pause detection when capture UI not active |
| False positives | Conservative confidence thresholds; user feedback loop |

---

## Acceptance Criteria

### Phase 1 (Heuristic Detection)

- [ ] Tweet/post cards detected on Twitter/X with >80% accuracy
- [ ] Video players detected on YouTube, Vimeo, Twitch with >85% accuracy
- [ ] Chat messages detected on Slack, Discord with >75% accuracy
- [ ] Code blocks detected in VS Code, browser dev tools with >70% accuracy
- [ ] Detection completes in <200ms on mid-range hardware
- [ ] UI overlay renders smoothly at 60fps
- [ ] User can disable any pattern type individually
- [ ] Keyboard shortcuts (1-9) select top suggestions

### Phase 2 (Learned Preferences)

- [ ] System learns user-adjusted regions within 3 captures
- [ ] Recurring capture patterns suggested automatically
- [ ] Capture history stored locally with export/delete options
- [ ] No cloud processing or telemetry

### Phase 3 (ML Enhancement)

- [ ] On-device model runs inference in <100ms
- [ ] Model size <10MB
- [ ] Detection accuracy improves by >15% over heuristics
- [ ] Graceful fallback to heuristics if model unavailable

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| False positives frustrate users | High | Conservative thresholds; easy manual override; user feedback |
| Platform API changes break detection | Medium | Abstract platform layer; graceful degradation |
| Performance regression on older hardware | Medium | Downsample for analysis; disable on <4GB RAM systems |
| Privacy concerns about screen analysis | High | Local-only processing; clear privacy policy; no telemetry |
| ML model bias | Medium | Diverse training data; human review of edge cases |
| Accessibility: detection fails for atypical UIs | Medium | Always provide manual fallback; high-contrast highlight options |

---

## Future Extensions

1. **Multi-Region Capture**: Capture several detected regions in one action (e.g., tweet + replies)
2. **Smart Recording**: Auto-start/stop recording when video player enters/leaves detected region
3. **Content-Aware Naming**: Suggest filenames based on detected content (tweet author, video title)
4. **Integration with XIP0072**: Use stride-safe capture for all smart-detected regions
5. **Plugin API**: Allow third-party pattern matchers

---

## Related Work

- **CleanShot X**: Basic window detection; no content-aware patterns
- **Snagit**: "All-in-One" capture with some smart detection; heavy, enterprise-focused
- **ShareX**: Region capture with last-region memory; no pattern detection
- **XIP0070**: User research validating need for faster capture workflows
- **XIP0072**: Underlying capture pipeline fixes enabling this UX improvement
