# XIP0072: Screen Recording Bug Fixes — AV1, HEVC Video Editor Launch, and Stride Safety

**Status**: Complete
**Version**: v0.22.257

**Priority**: P0 (stride) / P1 (HEVC) / P2 (AV1)
**Area**: Screen Recording | Video Encoding | Memory Safety | Video Editor
**Created**: 2026-04-12
**Related**: XIP0070 (User Research - Top Screen Capture Needs)
**Co-Authors**: Milena (research), Nadia (critical review)

---

## Summary

Three user-reported bugs in XerahS screen recording are dissected and addressed:

1. **Stride Safety (P0)**: A memory safety bug in the DXGI capture pipeline causes random red border artifacts on both screenshots and recordings — `dataBox.RowPitch` (GPU-aligned stride, e.g. 3712 bytes for 1920px) is ignored and `width*4` (7680) is hardcoded as stride, causing out-of-bounds memory access and padding bytes interpreted as pixel color values.

2. **HEVC Video Editor Launch (P1)**: HEVC recordings produce valid MP4 files that play in external players, but the integrated video editor fails to open them — the diagnosis that `VideoEditorLaunchPolicy` has a codec allowlist is incorrect; the real failure point is FFprobe codec detection and/or missing Windows HEVC Video Extensions for WebView2 playback.

3. **AV1 Screen Recording (P2)**: AV1 is listed in the `VideoCodec` enum but has no encoder implementation — selecting it fails silently or with an unhandled encoder exception. AV1 should either use Windows Media Foundation (hardware-accelerated when available) or fall back to FFmpeg.

---

## Bug 1 — Stride Safety: Red Border Artifacts on Screenshots and Recordings

### Problem Statement

Users report red borders appearing randomly on both screenshots and screen recordings. The artifacts are **random** in position and severity — the hallmark of a memory safety bug where misaligned padding bytes from a strided DXGI surface are copied into the destination buffer and later interpreted as pixel color values.

### Root Cause ✅ Verified

DXGI surfaces are allocated with GPU-aligned row pitches. For example, a 1920px-wide surface at 32bpp may have a row pitch of 3712 bytes (64-byte L1 cache line alignment) instead of the expected 7680 bytes (`1920 * 4`). The current copy code in `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs:433-447` hard-codes `sourceWidth * 4` as stride:

```csharp
// BUG: sourcePitch hardcodes width*4, ignoring actual DXGI dataBox.RowPitch
int srcPitch = (int)dataBox.RowPitch;
int sourcePitch = sourceWidth * 4; // e.g. 7680 for 1920px

unsafe
{
    for (int y = 0; y < sourceHeight; y++)
    {
        IntPtr srcRow = IntPtr.Add(dataBox.DataPointer, y * srcPitch);
        IntPtr destRow = IntPtr.Add(destPixels, y * sourcePitch);
        Buffer.MemoryCopy((void*)srcRow, (void*)destRow, sourcePitch, sourcePitch);
    }
}
```

When `srcPitch` (from DXGI `dataBox.RowPitch`, e.g. 3712) differs from `sourcePitch` (7680), the copy reads beyond the source row into padding bytes, which get interpreted as pixel color values (appearing as red borders at row edges).

**The `FrameData.Stride` contract is also undocumented** — the struct in `RecordingModels.cs` says only "Stride (bytes per row)" without clarifying whether it holds the actual GPU row pitch (with padding) or the tight pixel stride (`width * 4`). This ambiguity is the root cause of the bug propagating to downstream consumers.

### Affected Files

- `src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs` — primary bug location
- `src/platform/XerahS.Platform.Windows/Recording/MediaFoundationEncoder.cs` — uses `frame.Stride` for flip math but assumes tight stride
- `src/desktop/app/XerahS.RegionCapture/Frames/FrameData.cs` — stride contract undocumented
- `src/desktop/core/XerahS.Common/RegionCropper.cs` — sets cropped stride to tight `region.Width * bytesPerPixel`, assumes contiguous output
- `src/desktop/app/XerahS.ScreenCapture/GdiCaptureStrategy.cs` — uses `Math.Min(bitmapData.Stride, rowBytes)` which drops pixels silently

### Proposed Fix

**Step 1 — Fix `WindowsModernCaptureService` copy loop** (`src/platform/XerahS.Platform.Windows/WindowsModernCaptureService.cs`)

Use `srcPitch` only for row address calculation. Copy exactly `width * bytesPerPixel` bytes per row:

```csharp
// FIXED: srcPitch used only for row addressing, copy size is always tight pixel width
int bytesPerRow = sourceWidth * 4;
for (int y = 0; y < sourceHeight; y++)
{
    IntPtr srcRow = IntPtr.Add(dataBox.DataPointer, y * srcPitch);
    IntPtr destRow = IntPtr.Add(destPixels, y * bytesPerRow);
    Buffer.MemoryCopy((void*)srcRow, (void*)destRow, bytesPerRow, bytesPerRow);
}
```

**Step 2 — Fix `MediaFoundationEncoder.CopyFrame`** (`src/platform/XerahS.Platform.Windows/Recording/MediaFoundationEncoder.cs`)

The vertical flip path is correct (uses `frame.Stride` for addressing, `width * 4` for copy size). Verify the non-flip path handles non-contiguous stride:

```csharp
// Verify: uses srcStride for addressing, bytesPerRow for copy — correct pattern
int srcStride = frame.Stride;
int bytesPerRow = frame.Width * 4;
```

**Step 3 — Audit `GdiCaptureStrategy`** (`src/desktop/app/XerahS.ScreenCapture/GdiCaptureStrategy.cs`)

Uses `Math.Min(bitmapData.Stride, rowBytes)` — prevents overflow but silently drops pixels if source stride is larger. Document whether this is intentional behavior.

**Step 4 — Document `FrameData.Stride` contract** (`src/desktop/app/XerahS.RegionCapture/Frames/FrameData.cs`)

Clarify in the struct XML docs that `Stride` is the **actual source surface row pitch** (may include padding bytes), not a pixel-width-aligned value. All consumers must handle `Stride >= Width * BytesPerPixel`.

**Step 5 — Add stride validation asserts**

```csharp
Debug.Assert(frame.Stride >= frame.Width * 4, "Frame stride must be >= pixel data width");
Debug.Assert(frame.Stride % 4 == 0, "Frame stride must be 4-byte aligned");
```

### Testing

1. Enable `XERAHS_RECORDING_DUMP_RAW=1` and `XERAHS_RECORDING_DUMP_FIRST=1` — capture frames before and after copy loop; compare pixel data at row boundaries for corrupted padding bytes
2. Record full-screen HEVC/H264 at 1920×1080 and 2560×1440 — verify no red borders
3. Capture full-screen screenshots at mixed resolutions — verify no red borders
4. Window capture at various sizes — verify no red borders at window edges
5. **HDR monitors** — test with `DXGI_FORMAT_R10G10B10A2_UNORM` and `DXGI_FORMAT_R16G16B16A16_FLOAT` surfaces
6. **Rotated displays** — verify per-output stride is handled independently
7. **Multi-monitor mixed DPI** — each output may have different alignments

### Acceptance Criteria

- [ ] No red border artifacts on recordings at any resolution, codec, or capture mode
- [ ] No red border artifacts on screenshots
- [ ] `XERAHS_RECORDING_DUMP_RAW` frames show correct pixel data — no padding byte corruption at row boundaries
- [ ] `RegionCropper.CropFrame` correctly handles source stride != tight stride
- [ ] `FrameData.Stride` contract documented in code XML comments
- [ ] HDR/10-bit formats explicitly handled (reject or support — no silent corruption)
- [ ] Rotated displays and multi-monitor mixed DPI configurations tested

---

## Bug 2 — HEVC Recording Video Editor Fails to Open

### Problem Statement

A user records with HEVC codec. The recording completes and produces a valid MP4 that plays in VLC/mpv. When XerahS tries to open it in the integrated video editor, it fails — but the file is valid and opens when double-clicked directly.

### Root Cause ⚠️ XIP Had Wrong Target

**The original XIP incorrectly assumed `VideoEditorLaunchPolicy` had a codec allowlist.** `VideoEditorLaunchPolicy.cs` is a Linux Wayland policy struct — it has no codec allowlist or file validation logic.

**The real failure path is:**

1. **FFprobe codec detection** — `VideoEditorFfprobeResolver` runs `ffprobe` to extract video metadata. If the installed FFmpeg build lacks `libhevc` support, it cannot identify HEVC codec, causing silent failure or generic error when opening the editor.

2. **WebView2 codec dependency** — The integrated video editor uses WebView2's `<video>` element for playback. WebView2 relies on **Windows HEVC Video Extensions** (a paid Microsoft Store app) for HEVC hardware decoding. If the user doesn't have this installed, playback fails even with a valid FFmpeg-built file.

3. **The error message** — The "video editor doesn't exist" message likely comes from `VideoEditorRuntimeValidator` or is a user-reported paraphrase of a generic failure. The actual code path needs inspection.

### Affected Files

- `src/desktop/core/XerahS.Common/VideoEditorLaunchPolicy.cs` — NOT the bug location (was a misdiagnosis)
- `VideoEditorFfprobeResolver.cs` — likely failure point for HEVC detection
- `VideoEditorHost.cs` — validates native libraries, not codecs
- `AvaloniaUIService.cs` / `VideoEditorHost.cs` — video editor dialog launch

### Proposed Fix

**Step 1 — Inspect actual code path**

Before proposing fixes, the full video editor launch code path needs inspection to confirm:
- Does `VideoEditorFfprobeResolver` handle HEVC codec identification?
- Does the editor launch flow report specific errors or generic ones?
- Is the "video editor doesn't exist" message from user report or actual code?

**Step 2 — Add FFprobe codec capability detection**

Test FFprobe HEVC detection before opening editor:
```bash
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1 input.mp4
# Should output: codec_name=hevc
```

If FFprobe output is empty or unrecognized, check FFmpeg version and codec support:
```bash
ffprobe -version | head -3
ffmpeg -encoders 2>/dev/null | grep -i hevc
```

**Step 3 — Add Windows HEVC Video Extensions guidance**

The video editor uses WebView2 which depends on the Windows HEVC Video Extensions store app. Before attempting to open HEVC files:

1. Check if the extensions are installed (via `AppxPackage` or Store API)
2. If missing, show a descriptive error with a link to install:
   > "HEVC video playback requires the Windows HEVC Video Extensions. [Install from Microsoft Store]"

**Step 4 — Descriptive error messages**

Replace generic failures with specific, actionable messages:
- "FFmpeg not found — required for video metadata extraction"
- "FFmpeg version too old for HEVC decoding (minimum: ffmpeg 4.x with libhevc support)"
- "HEVC Video Extensions not installed — [Install from Microsoft Store]"
- "FFmpeg cannot decode this HEVC profile (Main 10, etc.)"

**Step 5 — Linux behavior**

Define what happens when a HEVC file is opened on Linux (no WebView2, no HEVC extensions). FFmpeg can decode HEVC if built with `libhevc`, but the embedded editor won't work — specify graceful fallback or error.

### Testing

1. HEVC recording + "Open in editor" — verify editor opens and plays the clip (on system with HEVC Video Extensions)
2. HEVC recording on system WITHOUT HEVC Video Extensions — verify clear install prompt, not generic error
3. FFmpeg < 4.x with HEVC file — verify descriptive error message
4. H264 recordings continue to open correctly (regression test)
5. Linux: define expected behavior for HEVC files

### Acceptance Criteria

- [ ] HEVC-encoded MP4 recordings open in XerahS video editor (on HEVC-capable systems)
- [ ] Systems without HEVC Video Extensions show a clear install prompt with Store link
- [ ] FFmpeg codec detection failures produce specific, actionable error messages
- [ ] H264 recordings continue to open correctly (no regression)
- [ ] Linux behavior for HEVC files is defined and documented
- [ ] `ffprobe -show_streams` correctly identifies HEVC on supported FFmpeg builds

---

## Bug 3 — AV1 Screen Recording Unimplemented

### Problem Statement

AV1 is declared in the `VideoCodec` enum but has no encoder implementation in `MediaFoundationEncoder`. Selecting AV1 fails silently or throws an unhandled encoder exception. This creates a poor first impression — users expect the codec to either work or fail gracefully.

**Note:** VP9 has the same problem — declared but unimplemented in `MediaFoundationEncoder`.

### Root Cause ✅ Confirmed

`MediaFoundationEncoder.CreateSinkWriter` (line 198) only sets `MFVideoFormat_H264`. No handling for AV1, HEVC, or VP9 codecs.

### Technical Background

- **AV1 encoding** requires Windows 10 2004+ (SDK 10.0.19041+) with `MFVideoFormat_AV1` FourCC (`av01`)
- Hardware acceleration available on Intel Arc, AMD RDNA 2+, NVIDIA RTX 40-series+
- Software fallback uses `libaom-av1` via FFmpeg
- **HEVC encoding** GUID needs verification against current Windows SDK `mfapi.h` — the XIP's HEVC GUID may be incorrect
- **VP9** has limited Media Foundation support; FFmpeg is the more reliable path

### Proposed Fix

**Phase 1 — Graceful Failure (P2, immediate)**

Make AV1 fail gracefully before attempting encoding:

1. **Add AV1 to `VideoCodec` switch with explicit `NotSupportedException`** in `MediaFoundationEncoder` — replace silent failures with clear errors
2. **Detect AV1 capability before selection** — check if AV1 encoder is available when user opens codec dropdown
3. **UI guard** — disable AV1 option in settings with tooltip when no encoder is available:
   ```
   AV1: Requires Windows 10 2004+ with AV1 codec, or FFmpeg with libaom-av1
   ```

**Phase 2 — AV1/HEVC Media Foundation Implementation (P3, separate XIP)**

This requires significant work beyond a single bug fix:

1. **Verify GUIDs against Windows SDK headers** — the XIP's HEVC GUID needs confirmation; AV1 GUID (`39326caf-d300-4d70-8c38-3aa4b33d1cc6`) needs the same
2. **Hardware encoder detection via `MFTEnumEx`** — COM interop to find hardware vs software encoders
3. **Bitrate configuration** — AV1/HEVC use different rate control modes; `VideoFormat.Bitrate` may need profile/level configuration
4. **FFmpeg fallback path** — confirm `FFmpegRecordingService` handles AV1 input and wire it as fallback
5. **VP9 handling** — same implementation needed; decide whether VP9 goes to FFmpeg exclusively

### Affected Files

- `src/platform/XerahS.Platform.Windows/Recording/MediaFoundationEncoder.cs` — encoder switch
- `src/desktop/app/XerahS.RegionCapture/ScreenRecording/RecordingEnums.cs` — `enum VideoCodec`
- `ScreenRecorderService.cs` — fallback routing
- Settings UI — codec dropdown with AV1 guard

### Testing

**Phase 1:**
1. Select AV1 on any system — clear error or graceful fallback
2. No silent encoder exceptions in logs

**Phase 2:**
1. AV1-available system: record 30s clip, verify `codec_name=av1` via `ffprobe -show_streams`
2. AV1-unavailable system: clear error or FFmpeg fallback
3. AV1 software encoding: performance warning displayed
4. FFmpeg fallback: verify AV1 is routed correctly when MF fails

### Acceptance Criteria

**Phase 1 (Graceful Failure):**
- [ ] AV1 selection produces clear error or FFmpeg fallback — no silent failures
- [ ] AV1 option disabled in UI with tooltip when no encoder available
- [ ] No unhandled encoder exceptions logged

**Phase 2 (Full Implementation):**
- [ ] AV1 GUID verified against Windows SDK `mfapi.h`
- [ ] HEVC GUID verified (XIP's GUID may be incorrect — needs confirmation)
- [ ] Hardware encoder detection via `MFTEnumEx` implemented and tested
- [ ] Software AV1 encoding shows performance warning
- [ ] FFmpeg fallback path confirmed to exist and functional
- [ ] VP9 handling decision documented (MF or FFmpeg-only)

---

## Architecture Interactions

These three bugs share the same video encoding subsystem:

```
ScreenRecorderService
  ├── CaptureSource (DXGI → FrameData with Stride)
  ├── RegionCropper (uses FrameData.Stride for cropping)
  └── MediaFoundationEncoder
        ├── H264 (working)
        ├── HEVC (encode path — needs XIP0073 or later)
        └── AV1  (encode path — needs Phase 2)
```

Bug 1 (stride) affects all three codecs because it corrupts pixel data before it reaches any encoder. Bug 2 and Bug 3 are independent of stride but should be validated after Bug 1 is fixed to avoid compounding failures.

---

## Risks

| Bug | Risk | Mitigation |
|-----|------|------------|
| Stride | HDR or rotated monitor captures have unusual stride patterns | Test on HDR monitors and rotated displays |
| HEVC | WebView2/HEVC extensions are Windows Store-dependent | Check for extensions before opening; show install prompt |
| AV1 | AV1/HEVC GUIDs from XIP may not match current SDK | Verify against `mfapi.h` before implementation |
| AV1 | Software AV1 encoding is very slow | Show performance warning; default to FFmpeg on weak hardware |

---

## Priority Summary

| Priority | Bug | Task | Reason |
|----------|-----|------|--------|
| **P0** | Stride | Fix DXGI copy loop, document FrameData.Stride | Memory corruption affecting screenshots AND recordings |
| **P1** | HEVC | Inspect actual error path, fix FFprobe + HEVC extensions | Works end-to-end but editor integration is broken |
| **P2** | AV1 Phase 1 | Graceful failure, UI guard | No more silent failures; immediate value |
| **P3** | AV1 Phase 2 | Full MF AV1 + HEVC encoding | Significant scope — separate from bug fix |
