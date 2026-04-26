# KFIP0005: Screen Capture Workflow Enhancements for Social Sharing

**Status**: Proposed  
**Priority**: P1  
**Area**: Region Capture | AfterCapture | Social Media | UX | Upload  
**Created**: 2026-04-26  
**Related**: KFIP0002 (Smart Region Capture Profiles), KFIP0003 (X/Twitter Context Detection Hardening), KFIP0004 (Community Plugin Registry), XIP0070 (User Research — Top Screen Capture Needs)  
**Owner**: KovaForge  
**Co-Authors**: Milena (research), TBD (implementation)

---

## Summary

Users capturing content for social media — particularly X/Twitter — face a fragmented workflow: capture, annotate, resize for platform constraints, upload, then manually copy the share link. This KFIP proposes a unified **Social Capture Workflow** that streamlines the entire pipeline from screen capture to shareable post, with specific optimizations for X/Twitter's format requirements and user behaviors.

Building on KFIP0003's context detection and KFIP0004's plugin system, this proposal adds: (1) platform-optimized capture presets, (2) one-click "Capture → Upload → Copy Link" automation, (3) smart resize/aspect ratio handling for social platforms, and (4) integration with the plugin registry for community uploader discovery.

---

## Problem Statement

### The Social Capture Workflow is Too Many Steps

Current workflow for sharing a screenshot on X/Twitter:

1. Trigger region capture (hotkey)
2. Manually draw region around content
3. Open image editor (if annotation needed)
4. Save/export to file
5. Open X/Twitter
6. Click compose
7. Attach image
8. Wait for upload
9. Type post content
10. Submit

**Pain points identified from user research (XIP0070):**
- "I capture tweets maybe 10 times a day — every time I'm drawing the same rough rectangle"
- Users want "capture → auto-upload → get link in clipboard" as a single action
- ShareX's automated workflows "reduced bug documentation time by 60%" — but social sharing lacks equivalent streamlining
- No built-in awareness of platform constraints (X's 4:5 aspect ratio preference, file size limits, format optimization)

### Platform-Specific Friction

| Platform Constraint | Current Behavior | User Impact |
|---------------------|------------------|-------------|
| X/Twitter 4:5 optimal aspect ratio | No guidance; users crop manually | Suboptimal display in feed |
| X/Twitter 5MB image limit | No warning until upload fails | Failed uploads, frustration |
| X/Twitter converts PNG→JPEG | No pre-optimization option | Larger files, slower uploads |
| Multiple images in thread | Manual capture of each | Time-consuming for thread documentation |
| Alt text for accessibility | Separate step, often forgotten | Poor accessibility, missed engagement |

### Discovery Gap for Uploaders

Users don't know what uploaders are available or how to install them. The community plugin registry (KFIP0004) solves distribution, but users still need to discover relevant uploaders at the moment of capture intent.

---

## Goals

- Reduce social sharing workflow from 10+ steps to 3 steps: capture → review → share
- Provide platform-optimized capture presets (X/Twitter, LinkedIn, Discord, etc.)
- Enable true "capture and copy link" automation with configurable upload destinations
- Integrate uploader discovery into the capture workflow
- Respect platform constraints (aspect ratios, file sizes, formats) automatically
- Maintain privacy-first architecture — local processing, optional cloud, user-controlled

## Non-Goals

- No automatic posting to social platforms (user must review and submit)
- No browser extension required for basic functionality
- No AI-generated post content (user writes their own text)
- No real-time collaboration or shared workspaces
- No video editing or thread stitching in v1 (follow-up to KFIP0002 Phase 2)

---

## Proposed Solution

### 1. Social Capture Presets

Add predefined capture profiles optimized for social platforms:

```csharp
public class SocialCapturePreset
{
    public string Id { get; init; }                    // "x-tweet", "x-tweet-image", "linkedin-post"
    public string Name { get; init; }                  // "X/Twitter Tweet"
    public string Platform { get; init; }              // "X", "LinkedIn", "Discord"
    public AspectRatio TargetAspectRatio { get; init; } // 4:5, 16:9, 1:1, etc.
    public int MaxWidth { get; init; }                 // 1200 for X
    public int MaxHeight { get; init; }                // 1200 for X
    public long MaxFileSizeBytes { get; init; }        // 5MB for X images
    public ImageFormat PreferredFormat { get; init; }  // JPEG for photos, PNG for UI
    public int JpegQuality { get; init; }              // 85 for balance
    public bool IncludeAltTextPrompt { get; init; }    // true for accessibility
}
```

**Built-in presets:**

| Preset | Aspect Ratio | Max Size | Format | Use Case |
|--------|--------------|----------|--------|----------|
| `x-tweet` | 4:5 (vertical) | 1200×1500 | JPEG/PNG | Single tweet screenshot |
| `x-tweet-image` | 4:5 or 16:9 | 5MB | JPEG | Photos/media for X |
| `x-thread` | 4:5 | 1200×1500 | PNG | Thread documentation |
| `linkedin-post` | 1.91:1 | 1200×627 | JPEG | LinkedIn article images |
| `discord-attachment` | Any | 8MB | PNG | Discord sharing |

### 2. Smart Resize and Format Optimization

After capture, automatically optimize for the selected preset:

```csharp
public interface ISocialImageOptimizer
{
    Task<SKBitmap> OptimizeAsync(SKBitmap source, SocialCapturePreset preset);
    Task<byte[]> EncodeAsync(SKBitmap optimized, SocialCapturePreset preset);
}
```

**Optimization pipeline:**
1. Resize to max dimensions (maintaining aspect ratio)
2. Crop to target aspect ratio if specified (center-weighted, user-adjustable)
3. Encode to preferred format with quality settings
4. If result exceeds file size limit, re-encode with reduced quality
5. Return optimized image + metadata (original vs. final dimensions, file size)

### 3. One-Click "Capture → Upload → Copy Link"

New AfterCapture task: `ShareToUploader`

```csharp
[Flags]
public enum AfterCaptureTasks : long
{
    // ... existing flags
    ShareToUploader = 1 << 18,  // New: Upload and copy URL to clipboard
}
```

**Workflow:**
```
User initiates capture with social preset
        │
        ▼
Region capture (with smart detection hints from KFIP0003)
        │
        ▼
Image optimizer applies preset constraints
        │
        ▼
AfterCapture: ShareToUploader triggered
        │
        ▼
Upload to configured uploader (Imgur, S3, Pixelfox, etc.)
        │
        ▼
URL copied to clipboard with optional notification
        │
        ▼
User pastes into social platform compose window
```

**Configuration per preset:**
- Default uploader (can override global default)
- Auto-upload vs. confirm-first
- URL format preference (direct, page with preview, shortened)
- Copy format: raw URL, Markdown `![alt](url)`, HTML `<img>`, or custom template

### 4. Uploader Discovery Integration

When user selects a preset without a configured uploader, or when the configured uploader is unavailable:

1. Check installed uploaders for compatibility with preset's platform
2. If none found, query community plugin registry (KFIP0004) for relevant uploaders
3. Show subtle suggestion: "Pixelfox uploader available for X/Twitter sharing. Install?"
4. One-click install via existing plugin installer flow

### 5. Alt Text Reminder

For presets with `IncludeAltTextPrompt = true`:

- After capture, brief notification/toast: "Add alt text for accessibility?"
- Click opens annotation editor with alt text field
- Alt text stored in image metadata (EXIF/XMP) and passed to uploader if supported

---

## UX Design

### Capture Flow

```
[Hotkey] → [Region Selection with Preset Overlay]
              │
              ├─ Show preset name in corner ("X/Twitter Tweet")
              ├─ Show aspect ratio guide (4:5 overlay)
              ├─ Smart hints from TweetCaptureDetector (KFIP0003)
              └─ [Click/Drag] → Capture
                    │
                    ▼
              [Quick Preview + Actions]
                    │
                    ├─ [Upload & Copy Link] → Immediate upload
                    ├─ [Annotate] → Open editor
                    ├─ [Save] → Local file only
                    └─ [Change Preset] → Switch to different preset
```

### Task Settings Integration

New tab: **Social Sharing**

- Default preset selection
- Per-preset uploader configuration
- Auto-upload toggle
- URL format preferences
- Alt text reminder toggle

### Notification Design

**Success:**
> ✅ Uploaded to Imgur. Link copied to clipboard.
> [View] [Share to X] [Dismiss]

**With alt text missing:**
> ⚠️ Uploaded to Pixelfox. Link copied.
> Consider adding alt text for accessibility.
> [Add Alt Text] [Dismiss]

---

## Technical Design

### Data Model

```csharp
// New: Social capture preset configuration
public class SocialCapturePresetConfig
{
    public string PresetId { get; set; }
    public string? DefaultUploaderId { get; set; }
    public bool AutoUpload { get; set; }
    public SocialUrlFormat UrlFormat { get; set; }
    public string? CustomUrlTemplate { get; set; }
}

// Extend TaskSettings
public partial class TaskSettings
{
    public SocialCapturePreset? DefaultSocialPreset { get; set; }
    public List<SocialCapturePresetConfig> SocialPresetConfigs { get; set; } = [];
}
```

### Services

```csharp
// Preset management
public interface ISocialCapturePresetService
{
    IReadOnlyList<SocialCapturePreset> GetBuiltInPresets();
    SocialCapturePreset? GetPreset(string presetId);
    Task<SocialCapturePresetConfig> GetConfigAsync(string presetId);
    Task SaveConfigAsync(SocialCapturePresetConfig config);
}

// Image optimization
public interface ISocialImageOptimizer
{
    Task<OptimizedImage> OptimizeAsync(SKBitmap source, SocialCapturePreset preset, 
        CancellationToken ct = default);
}

// Upload coordinator
public interface ISocialShareService
{
    Task<ShareResult> ShareAsync(ShareRequest request, CancellationToken ct = default);
    Task<bool> IsUploaderAvailableAsync(string uploaderId);
    Task<IReadOnlyList<CommunityPluginIndexEntry>> DiscoverUploadersAsync(string platform);
}

public class ShareRequest
{
    public required SKBitmap Image { get; init; }
    public required SocialCapturePreset Preset { get; init; }
    public string? AltText { get; init; }
    public string? PreferredUploaderId { get; init; }
}

public class ShareResult
{
    public bool Success { get; init; }
    public string? Url { get; init; }
    public string? ErrorMessage { get; init; }
    public long FileSizeBytes { get; init; }
    public ImageFormat FinalFormat { get; init; }
}
```

### Integration Points

| Component | Integration |
|-----------|-------------|
| `TweetCaptureDetector` (KFIP0003) | Auto-suggest `x-tweet` preset when tweet context detected |
| `PluginIndexService` (KFIP0004) | Discover relevant uploaders for selected preset |
| `CaptureJobProcessor` | New `PerformSocialShareAsync` method |
| `RegionCaptureOverlay` | Display preset indicator and aspect ratio guide |
| `AnnotationEditor` | Alt text input field when social preset active |
| `TaskSettingsPanel` | New Social Sharing tab |

---

## Security & Privacy Considerations

### Data Handling

- **Local-first**: All image optimization happens locally
- **Explicit upload**: No automatic upload without user action or explicit auto-upload consent
- **URL history**: Optional local log of shared URLs (disabled by default)
- **Metadata stripping**: Option to remove EXIF GPS/data before upload

### Uploader Security

- Only HTTPS uploaders accepted for built-in presets
- Community uploaders verified via registry checksum (KFIP0004)
- User warned when using unverified custom uploader

### Platform Compliance

- Respect X/Twitter Terms of Service (no automation of posting)
- No credential storage for social platforms
- No analytics or tracking of shared content

---

## Acceptance Criteria

### Functional

- [ ] User can select a social preset before or during capture
- [ ] Preset applies correct aspect ratio guide during region selection
- [ ] Image is automatically resized/optimized per preset constraints
- [ ] `ShareToUploader` AfterCapture task uploads and copies URL
- [ ] URL is copied in configured format (raw, Markdown, HTML)
- [ ] Notification confirms upload success with link preview
- [ ] Alt text reminder appears for accessibility-enabled presets
- [ ] Uploader discovery suggests relevant plugins from registry
- [ ] Works with at least 3 uploaders: Imgur, S3, and one community plugin

### Quality

- [ ] Optimization completes in <2s for 1920×1080 source image
- [ ] File size stays within platform limits (e.g., <5MB for X)
- [ ] Visual quality acceptable at default JPEG 85 quality
- [ ] No duplicate uploads if user retries
- [ ] Graceful fallback if uploader fails (save locally, notify user)

### Security

- [ ] HTTP uploaders rejected for social presets
- [ ] User confirmation required before installing discovered plugin
- [ ] No social platform credentials stored or requested
- [ ] Metadata stripping option works (GPS data removed)

---

## Phased Implementation

### Phase 1: Core Presets and Optimization

- [ ] Define `SocialCapturePreset` model and built-in presets
- [ ] Implement `ISocialImageOptimizer` with resize/format logic
- [ ] Add preset selection to region capture overlay
- [ ] Add aspect ratio guide visualization
- [ ] Tests: optimization quality, file size limits, format conversion

### Phase 2: Share Workflow

- [ ] Add `ShareToUploader` AfterCapture flag
- [ ] Implement `ISocialShareService`
- [ ] Add URL format configuration
- [ ] Implement notification/toast system for share results
- [ ] Tests: upload flow, error handling, URL formatting

### Phase 3: Integration and Discovery

- [ ] Integrate with `TweetCaptureDetector` for auto-preset suggestion
- [ ] Integrate with `PluginIndexService` for uploader discovery
- [ ] Add alt text reminder flow
- [ ] Add Task Settings UI for social sharing preferences
- [ ] Tests: discovery flow, plugin installation trigger

### Phase 4: Polish and Documentation

- [ ] User documentation for social capture workflows
- [ ] Preset customization guide
- [ ] Community uploader development guide
- [ ] Performance benchmarking

---

## Open Questions

1. **Should we support video presets?** X/Twitter supports video up to 2:20 and 512MB. This could be a follow-up to KFIP0002's video work.

2. **Should presets be user-customizable or fixed?** Recommendation: built-in presets are fixed (consistent behavior), but users can create custom presets with their own constraints.

3. **How to handle multiple images?** Thread documentation often needs 2-4 screenshots. Future work could add "capture sequence" mode that queues multiple captures before upload.

4. **Should we integrate with X's API for draft posts?** No — OAuth complexity, rate limits, and policy risk outweigh convenience. Clipboard + manual paste is reliable and policy-safe.

---

## Critical Review

*Reviewed by Nadia (Analyst) — 2026-04-26*

### Risks & Overreach

**1. Scope Creep in "Social" Definition**
The proposal ambitiously lists X/Twitter, LinkedIn, and Discord presets. Each platform updates constraints unpredictably. X's 4:5 "optimal" ratio is guidance, not API-enforced — it changes. Recommendation: Ship with X/Twitter only initially. Prove the model before platform sprawl.

**2. Auto-Upload Defaults Are Dangerous**
The "Auto-upload vs. confirm-first" toggle sounds safe, but discoverability matters. Users will enable auto-upload, forget, and accidentally share sensitive screenshots. The privacy section mentions "explicit consent" but doesn't mandate confirmation UI for first-time auto-upload enable. Add: hard requirement for a "test upload to verify destination" step before auto-upload can be enabled.

**3. The 2-Second Optimization Target Is Unverified**
> "Optimization completes in <2s for 1920×1080 source image"

On what hardware? SkiaSharp resize + JPEG encode at quality 85 on a 4K source can take 3-5s on older machines. This target needs benchmarking on minimum spec hardware, not just dev machines. Risk: users abandon the flow if it feels sluggish.

### Missing Edge Cases

**4. Network Failure Mid-Upload**
No mention of partial upload handling. If a 4MB image upload fails at 90%, does the user retry from scratch? Implement chunked upload or at minimum, resume-aware retry logic. Current design implies atomic upload — unacceptable for flaky connections.

**5. Uploader Rate Limits**
Imgur's free tier is 50 uploads/hour. The proposal treats uploaders as infinite-capacity black boxes. Add: per-uploader rate limit tracking and graceful degradation (queue for later, fallback uploader, or local save with notification).

**6. Alt Text Storage Is Vague**
> "Alt text stored in image metadata (EXIF/XMP)"

Most social platforms strip EXIF on upload. If alt text is only in metadata, it's lost. The proposal needs explicit alt text handling per uploader — some support it in API (Imgur), others don't. Don't pretend metadata is a solution.

### Build/Test Implications

**7. Image Optimization Needs Visual Regression Testing**
Resize + re-encode at quality 85 is lossy. Two SkiaSharp versions can produce different outputs. Add: perceptual hash comparison tests to ensure optimization doesn't drift unexpectedly across builds.

**8. Preset Validation Requires Live Platform Testing**
File size limits (5MB for X) aren't static. If X changes to 4MB, built-in presets become wrong. Consider: a lightweight config update mechanism or at minimum, documented manual verification cadence (monthly?).

### Acceptance Criteria Tightening

**9. "Notification confirms upload success" Is Insufficient**
Current AC doesn't specify failure notification behavior. Add explicit AC: *"Failed uploads show actionable error message with: failure reason, retry button, and 'save locally instead' option."*

**10. Missing Accessibility AC**
The proposal emphasizes alt text for output images but ignores accessibility of the feature itself. Add: keyboard-only capture → upload → copy link flow must be possible (screen reader announcements at each step).

### Implementation Sequencing Issues

**11. Phase 2 Depends On Unfinished Work**
`ShareToUploader` requires the plugin registry (KFIP0004) for uploader discovery. KFIP0004 is still in development. Phase 2 cannot start until KFIP0004 Phase 1 is stable. Current timeline implies parallel work — risky.

**12. TweetCaptureDetector Integration Is Underestimated**
KFIP0003's context detection suggests presets, but this creates a decision tree: user selected preset vs. auto-suggested preset vs. previous preset. The interaction model isn't specified. Recommendation: prototype the conflict resolution UI before committing to Phase 3 timeline.

### Bottom Line

The workflow is sound. The ambition is not. Cut platform support to X/Twitter only for v1, add hard safeguards around auto-upload, and resolve the KFIP0004 dependency before promising Phase 2 delivery. The 2-second target needs data, not hope.

---

## Success Metrics

- Time from capture to shareable link: target <10 seconds (vs. 60+ seconds current)
- User satisfaction: qualitative feedback from beta testers
- Adoption: % of captures using social presets vs. generic capture
- Error rate: <2% failed uploads or size constraint violations

---

## Related Work

- **KFIP0002**: Smart Region Capture Profiles — social presets extend this with platform-specific optimization
- **KFIP0003**: X/Twitter Context Detection — auto-suggests appropriate preset based on detected context
- **KFIP0004**: Community Plugin Registry — enables discovery of platform-specific uploaders
- **XIP0070**: User Research validates the need for streamlined social sharing workflows
