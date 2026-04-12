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

---

## Critical Review

*Review conducted by Nadia Valeva, Analyst — KovaForge*

### Technical Feasibility: Overly Optimistic

The detection strategy looks reasonable on paper but glosses over some hard problems:

**1. DOM-like heuristics without DOM access**
The proposal mentions "DOM-like heuristics" for tweet detection, but region capture operates at the screen buffer level — there's no DOM. You're doing computer vision on pixels, not parsing markup. The heuristics table conflates what *should* be detectable with what *can* be detected from raw pixels. Detecting a "tweet card" by "avatar column + content column" requires either:
- Accessibility API integration (slow, permission-heavy, breaks on web apps that don't expose semantic structure)
- CV-based layout analysis (computationally expensive, fragile across themes/resolutions)

**2. The 200ms detection target is fantasy for Phase 1**
The acceptance criteria claim "detection completes in <200ms on mid-range hardware." For context: a single screen capture at 1080p is ~8MB of data. Running edge detection, aspect ratio analysis, text density detection, and platform-specific rule matching across that buffer in 200ms — while also querying window manager APIs — is aggressive. The mitigation ("downsampled thumbnails") helps, but downsampling + multiple CV passes + API calls + scoring logic won't hit 200ms consistently without GPU acceleration, which isn't mentioned for Phase 1.

**3. Platform abstraction is hand-waved**
The "Platform-Specific Detection" section lists three different approaches (Windows UI Automation, macOS Accessibility, Linux xwininfo) with no discussion of how they unify. These APIs have different capabilities, permissions, and failure modes. A Windows app might expose element trees; a macOS app might not; a Linux Wayland session exposes almost nothing. The proposal needs a concrete abstraction layer design, not a bullet list.

### Scope: Phase 1 Is Not One Sprint

The Phase 1 scope includes:
- Window manager introspection across 3 platforms
- 5+ visual heuristics (aspect ratio, edge detection, color histogram, text density, platform rules)
- Pattern matchers for tweets, videos, chat, code, images, modals, docs
- Confidence scoring system
- UI overlay with keyboard shortcuts
- Settings persistence

This is **not one sprint**. It's probably 4-6 sprints for a team of 2-3 engineers, assuming no research spikes. The acceptance criteria for Phase 1 alone (7 bullet points with accuracy thresholds) would take 2+ sprints to validate.

**Recommendation**: Split Phase 1. Sprint 1: Window detection + video player pattern only. Sprint 2+: Add patterns incrementally. Ship value early instead of boiling the ocean.

### Risks: Under-Covered

The risk table identifies the right categories but underestimates severity:

**False positives**: Listed as "High" impact with mitigation "conservative thresholds." The problem isn't threshold tuning — it's that users will see *any* false positive as a bug. If the system suggests a region that captures a UI element the user didn't want, trust erodes fast. The proposal needs a "first impression" strategy: maybe start with opt-in per pattern, or require high confidence (>0.9) for auto-suggest.

**Privacy concerns**: Listed as "High" with mitigation "local-only processing." This misses the real issue: users will ask *why* the app is analyzing their screen. Even local analysis feels invasive. The proposal needs explicit user consent flows and clear UI indicating when detection is active.

**Missing risk: Maintenance burden**
Platform detection rules for Twitter/X, YouTube, Slack, Discord, VS Code will break. These apps update frequently. The proposal doesn't address who maintains the pattern matchers or how updates are shipped. This is ongoing work, not a one-time implementation.

### Missing Acceptance Criteria

1. **Failure mode behavior**: What happens when detection fails? Does the UI show "no suggestions" or silently fall back to manual mode? The user needs to know the feature is working, not just absent.

2. **Accessibility**: How do screen reader users interact with detected regions? The overlay needs ARIA labels or equivalent.

3. **Multi-monitor**: Detection needs to work across monitors with different DPIs. Not mentioned.

4. **Performance degradation over time**: If the learning system accumulates history, does query performance degrade? Is there a retention policy?

5. **Accuracy measurement methodology**: "80% accuracy" is meaningless without a test dataset. How is accuracy defined? Who validates the ground truth?

### Problem Statement: Evidence Is Anecdotal

The problem statement cites user quotes:
- "I capture tweets maybe 10 times a day"
- "Chat windows are always the same size"
- "I wish it just knew I wanted the video player region"

These are valid pain points, but they're **anecdotes, not data**. The proposal references XIP0070 (user research) but doesn't quantify:
- What percentage of captures are repetitive?
- How much time is spent on region selection vs. other workflow steps?
- Do users actually want automatic detection, or would they prefer better last-region memory?

Without this baseline, there's no way to measure success. The acceptance criteria set accuracy targets (80%, 85%) but don't tie them to user outcomes. A system with 85% accuracy that users don't trust is worse than no system at all.

### Bottom Line

The vision is sound — smart region capture would differentiate XerahS. But the proposal needs to:
1. **Cut scope for Phase 1** (one pattern, one platform, prove the concept)
2. **Add concrete performance benchmarks** (measured on target hardware, not theoretical)
3. **Define the abstraction layer** for cross-platform detection
4. **Quantify the problem** with data from XIP0070
5. **Plan for maintenance** (who updates pattern matchers when Twitter changes their layout?)

Don't build a CV pipeline when a simpler heuristic might solve 80% of the problem. Start small, measure, iterate. The current proposal is a 6-month roadmap masquerading as a P1 sprint.
