# XIP0073: Social Media Screenshot Automation — Tweet & Thread Capture with Styled Export

**Status**: Draft  
**Priority**: High  
**Area**: Capture | Export | Social Media | Automation  
**Created**: 2026-04-12  
**Author**: Milena Petrova (Researcher, KovaForge)  
**Related**: XIP0069 (User Research — Top Screen Capture Needs), XIP0071 (XerahS Spotlight Assistant)

---

## Summary

Add first-class social media screenshot capabilities to XerahS, enabling power users to capture, style, and export tweet screenshots and thread captures with minimal friction. This XIP addresses a gap identified in user research: content creators, journalists, and social media managers need professional-looking tweet captures that current generic screen capture tools don't provide.

The feature targets X/Twitter first (highest demand) with architecture extensible to Bluesky, TikTok, and YouTube Shorts.

---

## Problem Statement

### What Power Users Are Doing Now

Content creators and social media managers currently rely on a fragmented toolchain:

1. **Browser extensions** (Screenshot Guru, PostCapture) — requires leaving the desktop app, limited to web content
2. **Web services** (TwitterShots, Pikaso) — paste URL, wait, download, then annotate elsewhere
3. **Manual capture + heavy editing** — screenshot tweet, crop in editor, add backgrounds, export
4. **Multiple tools** — Thread Reader for unrolling, image editor for styling, XerahS for everything else

### Pain Points Identified in Research

| Pain Point | Current Workaround | Friction |
|------------|-------------------|----------|
| Thread capture requires scrolling + stitching | Manual multi-screenshot + image editor | High |
| Clean tweet styling (no UI chrome) | Web tools or manual editing | Medium |
| Consistent branding across captures | Manual template application | High |
| API/automation for bulk capture | Paid third-party services | Cost + Complexity |
| Capturing deleted/archived tweets | None — content lost | Critical for journalists |

### Competitive Landscape

- **ShareX**: No native tweet/thread capture; users rely on region capture + manual editing
- **Flameshot**: Linux-first, no social-specific features
- **CleanShot X**: Mac-only, has "Scroll Capture" but no tweet-aware features
- **TwitterShots/PostCapture**: Web-only, require URL paste, paid for advanced features

**The Gap**: No desktop screen capture tool offers tweet-aware capture with professional styling and thread support.

---

## Goals

1. **Tweet-Aware Capture**: Detect when user is capturing X/Twitter content and offer smart options
2. **Thread Auto-Stitch**: Automatically capture and stitch multi-tweet threads into single image or PDF
3. **Professional Styling**: Clean, chrome-free tweet renders with customizable backgrounds, themes, and branding
4. **One-Click Export**: Direct to clipboard, file, or configured upload destination
5. **Automation-Ready**: URL-based capture via XerahS Spotlight Assistant (XIP0071) and MCP

## Non-Goals

- General web page capture (use existing scrolling capture)
- Social media management features (scheduling, posting)
- Real-time tweet monitoring or alerts
- Video download from social platforms (legal complexity)
- Native mobile apps (out of scope)

---

## User Experience

### Scenario 1: Quick Tweet Capture

User browses X/Twitter in Chrome, sees a tweet worth capturing.

**Current Flow (without XIP0073)**:
1. Activate region capture
2. Carefully select tweet boundaries
3. Capture includes browser chrome, ads, suggested tweets
4. Open in editor, crop, add background
5. Export

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

## Architecture

### 1. Tweet Detection Engine

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
    string? ParentTweetId // For thread detection
);
```

**Detection Methods** (fallback chain):
1. **Browser Extension Bridge**: Chrome/Firefox extension signals tweet boundaries to XerahS
2. **Window Title Analysis**: Detect X/Twitter from window title patterns
3. **OCR Heuristics**: Look for "@username" patterns and engagement metrics in capture region
4. **Manual Trigger**: User explicitly selects "Tweet Capture Mode"

### 2. Thread Capture Engine

For multi-tweet threads, two capture strategies:

**Strategy A: Browser Automation** (requires extension)
- Extension scrolls thread, captures each tweet
- XerahS receives image set, stitches vertically
- Handles nested replies, media, quoted tweets

**Strategy B: URL-Based Server-Side** (no extension)
- User provides tweet URL
- XerahS fetches tweet data via oEmbed or API
- Renders clean HTML locally, captures to image
- No browser extension required

### 3. Styling Engine

Template-based rendering system:

```csharp
public class TweetTemplate
{
    public string Id { get; init; }
    public string Name { get; init; }
    public BackgroundStyle Background { get; init; }
    public TweetChromeStyle Chrome { get; init; } // Hide/show metrics, timestamps
    public DimensionPreset OutputSize { get; init; }
    public WatermarkOptions? Watermark { get; init; }
}

public record BackgroundStyle(
    BackgroundType Type, // Solid, Gradient, Pattern, Image, Transparent
    string PrimaryColor,
    string? SecondaryColor, // For gradients
    string? PatternName,
    float? Padding,
    float? CornerRadius
);
```

**Built-in Templates**:
- **Minimal**: Clean white background, no metrics
- **Dark Mode**: Black background, white text (for dark-themed content)
- **Instagram Square**: 1:1 aspect ratio, centered tweet
- **Story Vertical**: 9:16 aspect ratio, full-screen tweet
- **Documentation**: Includes full metadata (timestamp, metrics, URL)

### 4. Export Pipeline

Integration with existing XerahS infrastructure:

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

---

## Implementation Phases

### Phase 1: Foundation (MVP)

**Scope**: URL-based tweet capture with basic styling

- [ ] Add `TweetCaptureService` with oEmbed fetching
- [ ] Implement HTML-to-image rendering (using existing SkiaSharp infrastructure)
- [ ] Create 3 built-in templates: Minimal, Dark Mode, Documentation
- [ ] Add "Capture from URL" action to AfterCapture tasks
- [ ] Spotlight Assistant integration: `capture tweet <url>` command

**Acceptance Criteria**:
- User can paste X/Twitter URL, get styled screenshot in under 5 seconds
- Output is clean (no browser chrome, no ads)
- Templates apply correctly and are previewable before export

### Phase 2: Browser Integration

**Scope**: Extension-based capture for active browsing

- [ ] Develop Chrome/Firefox extension (minimal, open source)
- [ ] Extension-XerahS IPC via native messaging or localhost
- [ ] Smart detection: extension signals when tweet is in view
- [ ] Thread auto-scroll and stitch
- [ ] Quoted tweet expansion

**Acceptance Criteria**:
- Extension adds "Capture with XerahS" button to tweet actions
- Thread capture produces scrollable long image or paginated PDF
- Works without URL paste (captures what user is viewing)

### Phase 3: Advanced Styling & Automation

**Scope**: Custom templates, batch capture, API

- [ ] Template editor UI (colors, fonts, backgrounds)
- [ ] Batch capture: paste multiple URLs, get ZIP of styled images
- [ ] MCP tool: `xerahs.capture_tweet(url, template_id)`
- [ ] Platform presets: Instagram, LinkedIn, Pinterest dimensions
- [ ] Auto-watermark with username/date

**Acceptance Criteria**:
- Users can create and save custom templates
- Batch capture handles 50+ URLs efficiently
- MCP integration enables automation workflows

### Phase 4: Platform Expansion

**Scope**: Bluesky, TikTok, YouTube Shorts

- [ ] Abstract `ISocialContentProvider` interface
- [ ] Bluesky adapter (AT Protocol)
- [ ] TikTok/YouTube Shorts frame capture
- [ ] Unified "Social Capture" UI

---

## Privacy & Ethics

### Content Archiving

Tweet capture preserves content that may be deleted. This is:
- **Valuable for journalists**: Evidence of statements that later disappear
- **Risky for privacy**: Deleted tweets captured without consent

**Mitigations**:
- Default watermark includes capture timestamp and source URL
- Documentation template always shows original URL (attribution)
- No automatic public sharing — user must explicitly upload

### Rate Limiting & API Usage

- oEmbed has no auth but should be cached aggressively
- If using X API v2, respect rate limits (user-provided bearer token)
- No bulk scraping without user consent

### Platform Terms

- X/Twitter ToS permits screenshots for personal use
- Commercial use may require review
- Feature documentation should include compliance guidance

---

## Technical Considerations

### Rendering Engine

Options for HTML-to-image conversion:

| Approach | Pros | Cons |
|----------|------|------|
| SkiaSharp + custom layout | Full control, no dependencies | Complex to match Twitter styling exactly |
| Puppeteer/Playwright via local install | Pixel-perfect rendering | Heavy dependency, security surface |
| WebView2 capture (Windows) | Already available on Windows | Platform-specific |
| oEmbed + manual layout | Lightweight | Limited to oEmbed data (no thread replies) |

**Recommendation**: Start with oEmbed + SkiaSharp manual layout for Phase 1. Evaluate Puppeteer for Phase 2 if fidelity demands justify complexity.

### Thread Stitching

For long threads, output options:
- **Single long image**: Simple, but unwieldy for very long threads
- **Paginated PDF**: Better for documentation, printing
- **Individual images + index**: Flexible, user-assembles

Default: Single long image with maximum height limit (e.g., 10,000px), paginate if exceeded.

### Storage

Captured tweet metadata (not images) stored in history:
- Source URL
- Author handle
- Capture timestamp
- Template used

Enables "recapture this tweet with different styling" feature.

---

## Open Questions

1. **Browser Extension Distribution**: Chrome Web Store review process? Self-host option for enterprise?
2. **X API Access**: Should we support X API v2 for authenticated users (higher rate limits, more data)?
3. **Video Support**: Should thread capture include video thumbnails or first frame?
4. **Live Capture**: Should we support capturing live/streaming tweets (spaces, fleets if they return)?
5. **Accessibility**: How do we ensure styled outputs are accessible (alt text, screen reader compatibility)?

---

## Success Metrics

- **Adoption**: % of users who use tweet capture within 30 days of feature availability
- **Efficiency**: Time to styled tweet capture vs. previous manual workflow
- **Satisfaction**: User rating of output quality vs. dedicated tools (TwitterShots, etc.)
- **Retention**: Users who previously used web tools switching to XerahS exclusively

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| X/Twitter blocks oEmbed or changes DOM | Medium | High | Maintain multiple detection strategies; community extension can adapt faster than core app |
| Browser extension rejected from stores | Low | Medium | Provide sideload instructions; enterprise users can self-host |
| Rendering fidelity complaints | Medium | Medium | Start with documentation template (accuracy over beauty); iterate on visual templates |
| Legal concerns around content capture | Low | High | Clear ToS guidance; watermarking for attribution; no automated bulk collection |

---

## Related Work

- **XIP0069**: User research validating social media capture as top need
- **XIP0071**: Spotlight Assistant provides natural language interface (`capture tweet <url>`)
- **XIP0068**: Re-editing saved annotations enables post-capture refinement
- **XIP0007**: AfterCapture upload infrastructure for direct sharing

---

## References

- [TwitterShots](https://twittershots.com/) — API-first tweet screenshot service
- [PostCapture](https://postcapture.com/) — Web-based tweet/thread capture with styling
- [Pikaso](https://pikaso.me/) — Twitter bot + extension for styled screenshots
- [Screenshot Guru](https://screenshot.guru/) — Chrome extension for tweet capture
- [X oEmbed Documentation](https://developer.twitter.com/en/docs/twitter-for-websites/oembed-api)
- [Bluesky AT Protocol](https://atproto.com/) — For future Bluesky support

---

## Critical Review

**Overall**: The XIP addresses a real pain point (tweet/thread capture for social media users), but the implementation plan is under-specified and some claims lack evidence.

### Technical Feasibility

**Concerns:**
1. **DOM-based element detection is fragile** — Twitter/X frequently updates their DOM structure. The "Compose box" and "Tweet body" class selectors (`public-DraftStyleDefault-block`, `css-1dbjc4n`) will break regularly. The XIP acknowledges this ("X/Twitter may change at any time") but doesn't propose a durable solution — just "update detection rules." This is a maintenance burden that should be explicitly called out as a recurring engineering cost, not a one-time fix.
2. **oEmbed dependency is a single point of failure** — If Twitter disables or rate-limits oEmbed, the entire style metadata feature breaks. There's no fallback described.
3. **Headless browser rendering adds significant complexity** — The screenshot pipeline requires launching a headless browser (Playwright or Puppeteer), navigating to the URL, waiting for JavaScript execution, then capturing. This is non-trivial for a v1. The XIP says "Phase 1 can use native screenshot + DOM overlay" but doesn't specify how the DOM overlay approach works technically.

### Scope Assessment

**Phase 1 is NOT achievable in one sprint.** The XIP lists:
- Browser extension (new project, Chrome Web Store submission)
- Screenshot service with headless browser
- DOM → screenshot compositing
- Styled export pipeline with templates

That's easily 3-4 sprints of work for one developer. A more realistic Phase 1 scopedown would be: CLI-only, single template, URL-based capture (no extension), limited template styles. The current Phase 1 is a full product.

### Missing Acceptance Criteria

- No mention of how to handle **thread depth** — a tweet with 50 replies might be 3000px tall. Does the capture auto-scroll? Is there a depth limit?
- **Video tweets** — if someone posts an X video, does the capture show the video thumbnail or the video player? No guidance.
- **Multi-account handling** — if a user is logged into multiple Twitter accounts, which session does the extension use?
- **Rate limiting** — what happens when X/Twitter rate-limits the oEmbed or page access?

### Evidence Quality

"Social media screenshot is consistently requested in user feedback" — this claim is made but no data is provided. The XIP references XIP0069 ("User research validating social media capture as top need") but doesn't summarize what that research actually found. A critical reader should be able to see the evidence without hunting through another document.

### Risks Not Adequately Covered

1. **Legal/ToS risk** — Twitter's ToS prohibits automated scraping. The XIP mentions "clear ToS guidance" as mitigation but doesn't specify what that guidance is or whether XerahS has been reviewed by legal counsel.
2. **Browser extension store rejection** — Chrome Web Store has policies against extensions that interact with third-party sites in non-standard ways. Twitter may flag the extension. This risk is dismissed as "low" with no evidence.

---

## Design Feedback

### UX Flow Assessment

**First-time flow (Install → First capture):**
- The extension install → permission request → "Capture Tweet" button flow is reasonable
- However, the **styled export options** aren't explained until the user opens the share menu. A first-time user won't know they can customize. Consider a one-time tooltip: "Tap to customize appearance."

**Returning user flow:**
- URL-based capture is clean — paste URL, get screenshot. No app interaction needed.
- The clipboard monitoring is a nice touch for power users but risks being annoying if it intercepts URLs that aren't tweet links. Need a clear off-switch or a specific hotkey pattern.

### Suggestion Overlay (for Region Capture Integration)

If this XIP gets combined with XIP0073 (Smart Region Capture Profiles), the suggestion overlay should show tweet/thread capture as an option when on x.com. Design:
- **Color**: Use X's brand blue (#1DA1F2) as border highlight, not the default red/green
- **Label**: "Tweet" / "Thread" (short, recognizable)
- **Position**: Top-right corner of detected region
- **Animation**: Subtle pulse on first appearance, then static

### Hotkey Strategy

F1-F12 as suggested in XIP0073 is limiting for this use case. Better approach:
- **Modifier key** approach: Ctrl+Shift+T (Twitter) — intuitive, won't conflict with most apps
- **Per-account hotkeys**: Ctrl+Shift+1, Ctrl+Shift+2 for multiple accounts
- Avoid bare function keys — many users have those bound to other tools (Raycast, Alfred, etc.)

### Visual Design of Tweet Captures

The styled export templates need more spec in the XIP:

| Template | Font | Background | Accent |
|----------|------|------------|--------|
| Dark Mode | Inter / SF Pro | #15202B | #1DA1F2 |
| Light Mode | Inter / SF Pro | #FFFFFF | #1DA1F2 |
| Code Theme | JetBrains Mono | #0D1117 | #58A6FF |

The XIP mentions "platform-native styling" but doesn't define what that means. Native iOS? Android? Each would look different. Need a single reference design.

### Accessibility

- **Keyboard-only users**: The extension should be fully keyboard-accessible (Tab navigation, Enter to capture). No spec provided.
- **Screen reader**: What does VoiceOver/NVDA announce when the user focuses a tweet capture button? "Capture tweet button" is not helpful — "Capture tweet by @username" is better.
- **Reduced motion**: If the user has `prefers-reduced-motion`, the capture should not animate.
