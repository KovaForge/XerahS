# XIP0073: Smart Region Capture Profiles & Social Media Screenshot Automation

**Status**: Open
**Version**: v0.22.257

**Priority**: P1
**Area**: Region Capture | UX | Machine Learning | Screen Recording | Social Media | Automation
**Created**: 2026-04-12
**Related**: XIP0072 (Screen Recording Bug Fixes), XIP0070 (User Research — Top Screen Capture Needs), XIP0071 (XerahS Spotlight Assistant)
**Co-Authors**: Milena (research), Nadia (analysis), TBD (implementation)
**Consolidated**: 2026-04-12 — Merged Smart Region Capture Profiles (original XIP0073) with Social Media Screenshot Automation (originally duplicate XIP0073) into single XIP0073

---

## Summary

Users spend excessive time manually drawing capture regions for repetitive tasks — tweets, media players, chat windows, code snippets, and documentation panels. Meanwhile, content creators, journalists, and social media managers need professional-looking tweet and thread captures that current generic screen capture tools don't provide.

This XIP proposes **Smart Region Capture Profiles**: an intelligent system that automatically detects and suggests capture regions based on common UI patterns, **unified with first-class social media screenshot capabilities** — enabling tweet-aware capture, thread auto-stitch, and styled export — reducing friction and saving time on every capture.

The system combines computer vision heuristics, window manager introspection, learned user preferences, and platform-specific detection to present one-click capture options for the most common capture targets, including social media content.

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

### Social Media Capture — The Fragmented Workflow Problem

Content creators and social media managers currently rely on a fragmented toolchain:

| Pain Point | Current Workaround | Friction |
|------------|-------------------|----------|
| Thread capture requires scrolling + stitching | Manual multi-screenshot + image editor | High |
| Clean tweet styling (no UI chrome) | Web tools or manual editing | Medium |
| Consistent branding across captures | Manual template application | High |
| API/automation for bulk capture | Paid third-party services | Cost + Complexity |
| Capturing deleted/archived tweets | None — content lost | Critical for journalists |

### Why This Matters Now

- XIP0070 user research identified "faster region selection" as a top-3 requested feature
- XIP0072 fixed underlying capture pipeline bugs — the foundation is now stable for UX improvements
- No desktop screen capture tool offers tweet-aware capture with professional styling and thread support
- Competitors (CleanShot X, Snagit) have basic smart detection; XerahS can differentiate with deeper pattern recognition

---

## Proposed Solution

### Core Concept: Detectable UI Patterns

Define a set of **detectable patterns** that represent common capture targets, including social media content:

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
│  Screen Buffer  │────▶│  Pattern Detector │────▶│  Scored Regions │
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

---

## Implementation Phases

### Phase 1: Heuristic-Based Detection + Tweet/Social Capture MVP

**Scope**: Rules-based detection without ML dependencies; URL-based tweet capture with basic styling

#### Smart Detection (Core)

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

4. **Pattern Matchers**
   ```csharp
   public interface IPatternMatcher
   {
       DetectedRegion? Match(CaptureFrame frame, Rect? hintRegion);
       float Confidence { get; }
   }

   public class TweetPatternMatcher : IPatternMatcher { /* ... */ }
   public class VideoPatternMatcher : IPatternMatcher { /* ... */ }
   public class ChatPatternMatcher : IPatternMatcher { /* ... */ }
   public class CodePatternMatcher : IPatternMatcher { /* ... */ }
   ```

#### Social Capture (MVP)

- [ ] Add `TweetCaptureService` with oEmbed fetching
- [ ] Implement HTML-to-image rendering (using existing SkiaSharp infrastructure)
- [ ] Create 3 built-in templates: Minimal, Dark Mode, Documentation
- [ ] Add "Capture from URL" action to AfterCapture tasks
- [ ] Spotlight Assistant integration: `capture tweet <url>` command
- [ ] `TweetCaptureDetector` pattern: detect when user is capturing X/Twitter content and offer smart actions

**Acceptance Criteria (Phase 1)**:
- Tweet/post cards detected on Twitter/X with >80% accuracy
- Video players detected on YouTube, Vimeo, Twitch with >85% accuracy
- Chat messages detected on Slack, Discord with >75% accuracy
- Code blocks detected in VS Code, browser dev tools with >70% accuracy
- Detection completes in <200ms on mid-range hardware (downsampled analysis)
- User can paste X/Twitter URL, get styled screenshot in under 5 seconds
- Templates apply correctly and are previewable before export
- Keyboard shortcuts (1-9) select top suggestions

### Phase 2: Learned Preferences + Browser Integration / Social Capture

**Scope**: Per-user learning from capture history; extension-based active browsing capture; styled export

#### Learned Preferences

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

#### Browser Integration / Social Capture

- [ ] Develop Chrome/Firefox extension (minimal, open source)
- [ ] Extension-XerahS IPC via native messaging or localhost
- [ ] Smart detection: extension signals when tweet is in view
- [ ] Thread auto-scroll and stitch
- [ ] Quoted tweet expansion
- [ ] Tweet styling engine with template-based rendering

**Acceptance Criteria (Phase 2)**:
- System learns user-adjusted regions within 3 captures
- Extension adds "Capture with XerahS" button to tweet actions
- Thread capture produces scrollable long image or paginated PDF
- Works without URL paste (captures what user is viewing)

### Phase 3: ML-Enhanced Detection + Advanced Styling / Automation

**Scope**: On-device neural network for pattern recognition; custom templates; batch capture; API

#### ML-Enhanced Detection

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

#### Advanced Styling / Automation

- [ ] Template editor UI (colors, fonts, backgrounds)
- [ ] Batch capture: paste multiple URLs, get ZIP of styled images
- [ ] MCP tool: `xerahs.capture_tweet(url, template_id)`
- [ ] Platform presets: Instagram, LinkedIn, Pinterest dimensions
- [ ] Auto-watermark with username/date

**Acceptance Criteria (Phase 3)**:
- On-device model runs inference in <100ms
- Model size <10MB
- Detection accuracy improves by >15% over heuristics
- Graceful fallback to heuristics if model unavailable
- Users can create and save custom templates
- Batch capture handles 50+ URLs efficiently

### Phase 4: Platform Expansion (Social Media)

**Scope**: Bluesky, TikTok, YouTube Shorts

- [ ] Abstract `ISocialContentProvider` interface
- [ ] Bluesky adapter (AT Protocol)
- [ ] TikTok/YouTube Shorts frame capture
- [ ] Unified "Social Capture" UI

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

### Scenario 1: Quick Tweet Capture

User browses X/Twitter in Chrome, sees a tweet worth capturing.

**With XIP0073**:
1. Activate region capture over tweet
2. XerahS detects X/Twitter domain + tweet structure
3. Overlay shows "Capture Tweet" smart action
4. One click: clean, styled tweet image in clipboard

### Scenario 2: Thread Capture

User wants to capture a 15-tweet thread for a blog post.

**With XIP0073**:
1. User clicks "Capture Thread" from smart actions
2. XerahS scrolls and stitches automatically
3. Preview shows full thread as scrollable image
4. Export options: long PNG, paginated PDF, or individual tweets

### Scenario 3: Styled Export for Branding

Social media manager needs consistent tweet visuals for Instagram.

**With XIP0073**:
1. Configure "Brand Template" once (colors, fonts, background, watermark)
2. Capture any tweet → automatic styling applied
3. Export dimensions optimized for target platform (Instagram square, Stories vertical, etc.)

### Scenario 4: Spotlight Assistant Integration

User wants to capture without touching mouse.

```
User: "capture tweet https://x.com/user/status/1234567890"
Assistant: "Capturing... Done. Styled with default template. Copy to clipboard?"
```

---

## Settings and Customization

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
├── SocialCapture/
│   ├── TweetCaptureService.cs
│   ├── TweetDetector.cs (TweetCaptureDetector pattern)
│   ├── ThreadCaptureEngine.cs
│   ├── TweetTemplate.cs
│   └── SocialCaptureOverlay.cs
```

### Social Media Architecture

#### Tweet Detection Engine

Lightweight detection when capture region intersects with known tweet DOM structures:

```csharp
public interface ITweetDetector
{
    bool IsTweetVisible(Rect captureRegion);
    TweetMetadata? ExtractTweetData(Rect captureRegion);
}

public record TweetMetadata(
    string TweetId,
    string AuthorHandle,
    string AuthorDisplayName,
    string Text,
    DateTime PostedAt,
    int LikeCount,
    int RetweetCount,
    int ReplyCount,
    IReadOnlyList<string> MediaUrls,
    string? ParentTweetId
);
```

**Detection Methods** (fallback chain):
1. **Browser Extension Bridge**: Chrome/Firefox extension signals tweet boundaries to XerahS
2. **Window Title Analysis**: Detect X/Twitter from window title patterns
3. **OCR Heuristics**: Look for "@username" patterns and engagement metrics in capture region
4. **Manual Trigger**: User explicitly selects "Tweet Capture Mode"

#### Thread Capture Engine

**Strategy A: Browser Automation** (requires extension)
- Extension scrolls thread, captures each tweet
- XerahS receives image set, stitches vertically
- Handles nested replies, media, quoted tweets

**Strategy B: URL-Based Server-Side** (no extension)
- User provides tweet URL
- XerahS fetches tweet data via oEmbed or API
- Renders clean HTML locally, captures to image
- No browser extension required

#### Styling Engine

```csharp
public class TweetTemplate
{
    public string Id { get; init; }
    public string Name { get; init; }
    public BackgroundStyle Background { get; init; }
    public TweetChromeStyle Chrome { get; init; }
    public DimensionPreset OutputSize { get; init; }
    public WatermarkOptions? Watermark { get; init; }
}

public record BackgroundStyle(
    BackgroundType Type,
    string PrimaryColor,
    string? SecondaryColor,
    string? PatternName,
    float? Padding,
    float? CornerRadius
);
```

**Built-in Templates**:

| Template | Font | Background | Accent |
|----------|------|------------|--------|
| Dark Mode | Inter / SF Pro | #15202B | #1DA1F2 |
| Light Mode | Inter / SF Pro | #FFFFFF | #1DA1F2 |
| Code Theme | JetBrains Mono | #0D1117 | #58A6FF |
| Documentation | Inter / SF Pro | #FFFFFF | #1DA1F2 |

#### Export Pipeline

```
Tweet Capture
    ├── Raw Capture (from browser or URL render)
    ├── Styling Engine (apply template)
    ├── Annotation Layer (optional: arrows, highlights)
    ├── Export
    │     ├── To Clipboard (default)
    │     ├── To File (PNG/JPG/PDF)
    │     ├── To Upload Destination (Imgur, S3, etc.)
    │     └── To Editor (for further annotation)
    └── History Entry (with source URL metadata)
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
- Fallback: Pure visual heuristics

### Performance Considerations

| Concern | Mitigation |
|---------|------------|
| Detection latency | Run detection in background thread; cache results for 100ms |
| Memory usage | Process downsampled thumbnails (max 1080p) for analysis |
| Battery impact | Pause detection when capture UI not active |
| False positives | Conservative confidence thresholds; user feedback loop |

### Privacy & Ethics (Social Capture)

- Default watermark includes capture timestamp and source URL
- Documentation template always shows original URL (attribution)
- No automatic public sharing — user must explicitly upload
- oEmbed cached aggressively; X API v2 rate limits respected
- No bulk scraping without user consent

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| False positives frustrate users | High | Conservative thresholds; easy manual override; user feedback loop |
| Platform API changes break detection | Medium | Abstract platform layer; graceful degradation |
| Performance regression on older hardware | Medium | Downsample for analysis; disable on <4GB RAM systems |
| Privacy concerns about screen analysis | High | Local-only processing; clear privacy policy; no telemetry |
| ML model bias | Medium | Diverse training data; human review of edge cases |
| X/Twitter blocks oEmbed or changes DOM | High | Maintain multiple detection strategies; community extension adapts faster than core app |
| Browser extension rejected from stores | Medium | Provide sideload instructions; enterprise users can self-host |
| Rendering fidelity complaints | Medium | Start with documentation template (accuracy over beauty); iterate on visual templates |
| Legal concerns around content capture | High | Clear ToS guidance; watermarking for attribution; no automated bulk collection |
| DOM-based element detection is fragile | High | Twitter/X frequently updates DOM; maintenance burden explicitly planned as recurring engineering cost |

---

## Future Extensions

1. **Multi-Region Capture**: Capture several detected regions in one action (e.g., tweet + replies)
2. **Smart Recording**: Auto-start/stop recording when video player enters/leaves detected region
3. **Content-Aware Naming**: Suggest filenames based on detected content (tweet author, video title)
4. **Integration with XIP0072**: Use stride-safe capture for all smart-detected regions
5. **Plugin API**: Allow third-party pattern matchers
6. **Accessibility**: Ensure detection works for atypical UIs; always provide manual fallback

---

## Related Work

- **CleanShot X**: Basic window detection; no content-aware patterns
- **Snagit**: "All-in-One" capture with some smart detection; heavy, enterprise-focused
- **ShareX**: Region capture with last-region memory; no pattern detection
- **TwitterShots**: API-first tweet screenshot service
- **PostCapture**: Web-based tweet/thread capture with styling
- **XIP0070**: User research validating need for faster capture workflows
- **XIP0071**: Spotlight Assistant provides natural language interface (`capture tweet <url>`)
- **XIP0072**: Underlying capture pipeline fixes enabling this UX improvement

---

## Critical Review

*Review conducted by Nadia Valeva, Analyst — KovaForge*

### Technical Feasibility: Overly Optimistic

**1. DOM-like heuristics without DOM access**
The proposal mentions "DOM-like heuristics" for tweet detection, but region capture operates at the screen buffer level — there's no DOM. You're doing computer vision on pixels, not parsing markup. Detecting a "tweet card" by "avatar column + content column" requires either:
- Accessibility API integration (slow, permission-heavy, breaks on web apps)
- CV-based layout analysis (computationally expensive, fragile across themes/resolutions)

The maintenance burden of keeping DOM selectors updated must be explicitly planned as recurring engineering cost, not a one-time fix.

**2. The 200ms detection target is aggressive for Phase 1**
A single screen capture at 1080p is ~8MB of data. Running edge detection, aspect ratio analysis, text density detection, and API calls + scoring logic in 200ms requires GPU acceleration not currently planned for Phase 1. Mitigation via downsampled thumbnails helps but doesn't fully solve it.

**3. Platform abstraction is under-specified**
Windows UI Automation, macOS Accessibility, and Linux Wayland have fundamentally different capabilities. Abstract platform layer design must be concrete before implementation, not a bullet list.

### Scope: Phase 1 Is Not One Sprint

The Phase 1 scope includes window manager introspection, 5+ visual heuristics, pattern matchers for tweets/videos/chat/code/images/modals/docs, confidence scoring, UI overlay with keyboard shortcuts, and settings persistence. This is 4-6 sprints for 2-3 engineers. **Recommendation**: Split Phase 1. Sprint 1: Window detection + video player pattern only. Sprint 2+: Add patterns incrementally.

### Missing Acceptance Criteria

- **Failure mode behavior**: What happens when detection fails? Silent fallback to manual or explicit "no suggestions" state?
- **Accessibility**: How do screen reader users interact with detected regions?
- **Multi-monitor**: Detection must work across monitors with different DPIs
- **Thread depth**: A tweet with 50 replies might be 3000px tall. Is there a capture limit?
- **Video tweets**: Does capture show video thumbnail or video player?
- **Rate limiting**: What happens when X/Twitter rate-limits oEmbed?

### Evidence Quality

User quotes in the problem statement are anecdotal. The proposal references XIP0070 (user research) but doesn't quantify what percentage of captures are repetitive or how much time is spent on region selection. Without this baseline, there's no way to measure success.

### Risks: Under-Covered

**Maintenance burden** (not in original risk table): Platform detection rules for Twitter/X, YouTube, Slack, Discord, VS Code will break with app updates. The proposal doesn't address who maintains pattern matchers or how updates are shipped.

**Legal/ToS risk**: Twitter's ToS prohibits automated scraping. Feature documentation must include explicit compliance guidance.

---

## Design Feedback

### UX Flow Assessment

**First-time flow:**
- The suggested overlay appearing on region capture activation is good — low friction, no forced commitment
- Problem: the XIP doesn't show how the user discovers existing profiles. Is there a settings page? A history? Users won't know they have saved profiles unless there's a discoverable management UI.

**Returning user flow:**
- "Top 3 suggested regions" is a good constraint — less choice paralysis
- Missing: what happens if the user wants a slightly different region than suggested? Manual override should be obvious and one-action.

### Suggestion Overlay

**Appearance:**
- Border color: neutral but visible — blue/cyan (#00D4FF) for "suggestion" state, not red or green
- Labels: short, uppercase — "TWEET BOX", "CODE", "CHAT" — readable at small sizes
- Corner markers: small L-shaped brackets (crop marks) rather than full border. Less intrusive.
- Animation: no animation on suggestion overlay appear/dismiss. Only animate on selection (brief flash confirmation).
- **Tweet-specific**: When on x.com, use X's brand blue (#1DA1F2) as border highlight

**Positioning:**
- Numbered badges (1, 2, 3) at top-left of each region
- Hover over badge → profile name tooltip
- Click → capture. No confirmation dialog.

### Hotkey Strategy

**F1-F12 is the wrong default.** Most power users have these bound to other tools (Raycast, Alfred, etc.).

Better approach:
- `Ctrl+Shift+R` as default "Region Capture with Profiles" hotkey
- `Ctrl+Shift+1-9` for quick capture of specific numbered profiles
- For social capture: `Ctrl+Shift+T` (Twitter) — intuitive, won't conflict with most apps
- Avoid bare function keys; require a modifier

### Profile Naming

Users think in terms of targets, not abstractions:
- Instead of: "Profile: Discord Message Pane" → "Capture: Discord Chat"
- Instead of: "Profile: Twitter Tweet Box" → "Capture: Tweet"

### Accessibility

**Keyboard users:**
- Escape dismisses suggestion overlay
- Arrow keys navigate between suggestions
- Enter selects, Tab moves to manual selection mode

**Screen reader:**
- Overlay announces: "3 capture regions detected. Press 1, 2, or 3 to capture, or Tab to select manually."
- Individual regions: "Region 1: Tweet Box, Ctrl+Shift+1 to capture"

**Reduced motion:**
- If user has `prefers-reduced-motion: reduce`, no animations on overlay appear/dismiss

### Profile Manager UI

Accessible via Ctrl+P during capture or via Settings → Profiles:
- List view with: profile name, target app/icon, hotkey, Edit/Delete buttons
- Search bar at top
- Import/Export at bottom (JSON)

---

## References

- [TwitterShots](https://twittershots.com/) — API-first tweet screenshot service
- [PostCapture](https://postcapture.com/) — Web-based tweet/thread capture with styling
- [Pikaso](https://pikaso.me/) — Twitter bot + extension for styled screenshots
- [Screenshot Guru](https://screenshot.guru/) — Chrome extension for tweet capture
- [X oEmbed Documentation](https://developer.twitter.com/en/docs/twitter-for-websites/oembed-api)
- [Bluesky AT Protocol](https://atproto.com/) — For future Bluesky support
