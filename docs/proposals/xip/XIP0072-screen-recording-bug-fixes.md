# XIP0072: Screen Recording Bug Fixes — AV1, HEVC Launch Policy, and Stride Safety

**Status**: Draft
**Priority**: P0 (stride) / P1 (HEVC) / P2 (AV1)
**Area**: Screen Recording | Video Encoding | Memory Safety | Video Editor
**Created**: 2026-04-12
**Related**: XIP0070 (User Research - Top Screen Capture Needs)

---

## Summary

Three user-reported bugs in XerahS screen recording are dissected and addressed:

1. **Stride Safety (P0)**: A memory safety bug in the DXGI capture pipeline causes random red border artifacts on both screenshots and recordings — `dataBox.RowPitch` (GPU-aligned stride, e.g. 3712 bytes for 1920px) is ignored and `width*4` (7680) is hardcoded as stride, causing out-of-bounds memory access and padding bytes interpreted as pixel color values.

2. **HEVC Video Editor Launch (P1)**: HEVC recordings produce valid MP4 files that play in external players, but the integrated video editor reports "video editor doesn't exist" — the `VideoEditorLaunchPolicy` incorrectly rejects HEVC MP4 files before the editor subprocess is spawned.

3. **AV1 Screen Recording (P2)**: AV1 is listed in the `VideoCodec` enum but has no runtime implementation — selecting it fails silently or with an unhandled encoder exception. AV1 should either use Windows Media Foundation (hardware-accelerated when available) or fall back to FFmpeg.

---

## Bug 1 — Stride Safety: Red Border Artifacts on Screenshots and Recordings

### Problem Statement

Users report red borders appearing randomly on both screenshots and screen recordings. The artifacts are **random** in position and severity — the hallmark of a memory safety bug where misaligned padding bytes from a strided DXGI surface are copied into the destination buffer and later interpreted as pixel color values.

### Root Cause

DXGI surfaces are allocated with GPU-aligned row pitches. For example, a 1920px-wide surface at 32bpp may have a row pitch of 3712 bytes (64-byte L1 cache line alignment) instead of the expected 7680 bytes (`1920 * 4`). The current copy code in `WindowsModernCaptureService.cs:536` hard-codes `sourceWidth * 4` as stride:

```csharp
// Unsafe — ignores DXGI dataBox.RowPitch
int sourcePitch = sourceWidth * 4; // e.g. 7680 for 1920px
...
Buffer.MemoryCopy((void*)srcRow, (void*)destRow, sourcePitch, sourcePitch);
```

When `srcPitch` (from DXGI `dataBox.RowPitch`, e.g. 3712) differs from `sourcePitch` (7680), the copy either reads beyond the source buffer or writes beyond the destination, corrupting memory or interpreting padding bytes as pixel color values (appearing as red borders at row edges).

The `FrameData.Stride` contract is also inconsistent — `RegionCropper` and `MediaFoundationEncoder.CopyFrame` assume stride == `width * 4`, but if the actual stride is larger, downstream consumers will misread pixel data.

### Proposed Fix

**Step 1 — Fix `WindowsModernCaptureService` copy loop**

Use `srcPitch` only to find each row's starting address. Always copy exactly `width * bytesPerPixel` bytes per row (the actual pixel data):

```csharp
// After: safe — srcPitch only used for row address calculation
for (int y = 0; y < sourceHeight; y++)
{
    IntPtr srcRow = IntPtr.Add(dataBox.DataPointer, y * srcPitch);
    IntPtr destRow = IntPtr.Add(destPixels, y * sourceWidth * 4);
    Buffer.MemoryCopy((void*)srcRow, (void*)destRow, sourceWidth * 4, sourceWidth * 4);
}
```

**Step 2 — Fix `MediaFoundationEncoder.CopyFrame`**

The encoder uses `frame.Stride` for vertical flip source row calculation but `frame.Width * 4` for copy size — if stride differs, this copies wrong amounts:

```csharp
// After: use actual stride for source addressing, fixed pixel width for copy size
int srcStride = frame.Stride;
int bytesPerRow = frame.Width * 4;

for (int y = 0; y < frame.Height; y++)
{
    byte* srcRow = srcBase + (height - 1 - y) * srcStride; // flip using actual stride
    byte* dstRow = dstBase + y * destStride;
    Buffer.MemoryCopy(srcRow, dstRow, destStride, bytesPerRow);
}
```

**Step 3 — Audit `GdiCaptureStrategy`**

`GdiCaptureStrategy.cs:160` uses `Math.Min(bitmapData.Stride, rowBytes)` — this prevents buffer overflow but silently drops pixels if source stride is larger. Verify this is intentional and document it.

**Step 4 — Document `FrameData.Stride` contract**

Add a comment to `FrameData` struct clarifying that `Stride` is the **actual source surface row pitch** (may include padding), not a pixel-width-aligned value. All consumers must handle non-contiguous rows.

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

### Acceptance Criteria

- [ ] No red border artifacts on recordings at any resolution, codec, or capture mode
- [ ] No red border artifacts on screenshots
- [ ] `XERAHS_RECORDING_DUMP_RAW` frames show correct pixel data — no padding byte corruption at row boundaries
- [ ] `RegionCropper.CropFrame` and `MediaFoundationEncoder.CopyFrame` correctly handle non-contiguous source stride

---

## Bug 2 — HEVC Recording "Video Editor Doesn't Exist"

### Problem Statement

A user records with HEVC codec. The recording completes and produces a valid MP4 that plays in VLC/mpv. When XerahS tries to open it in the integrated video editor, it reports "video editor doesn't exist" — but the file is valid and opens when double-clicked directly. The error is in the launch policy gate, not the file itself.

### Root Cause

The `VideoEditorLaunchPolicy` and/or `VideoEditorRuntimeValidator` checks one or more of:

1. **Codec allowlist**: The policy may block HEVC (`hev1`/`hvc1` FOURCC atoms) because it predates HEVC support wiring
2. **FFmpeg availability**: `VideoEditorFfprobeResolver` may not detect FFmpeg correctly for HEVC input, or FFmpeg version is too old for HEVC profiles
3. **File extension**: `.mp4` should be accepted but the MIME type validation may fail for HEVC MP4

The error message "video editor doesn't exist" is a generic rejection — it should instead report the specific validation failure (e.g., "FFmpeg version too old for HEVC" or "HEVC profile not supported").

### Proposed Fix

**Step 1 — Add diagnostic logging**

```csharp
TroubleshootingHelper.Log("VideoEditor", "LaunchPolicy",
    $"Validating: {filePath}, ext: {Path.GetExtension(filePath)}, " +
    $"codec: {detectedCodec}, ffmpegAvailable: {_ffmpegResolver.IsAvailable}");
```

**Step 2 — Fix codec allowlist in VideoEditorLaunchPolicy**

Ensure HEVC (`hev1`/`hvc1`) is in the accepted codec list. If MIME type validation fails for HEVC MP4, add a fallback that uses Media Foundation (`MF/source reader`) to probe the file's codec.

**Step 3 — Verify FFmpeg for HEVC detection**

Test FFprobe HEVC detection:
```bash
ffprobe -v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1 input.mp4
# Should output: codec_name=hevc
```

If FFprobe cannot identify HEVC (FFmpeg too old or HEVC support not compiled in), add a fallback using Media Foundation for codec probing.

**Step 4 — Descriptive error messages**

Replace the generic "video editor doesn't exist" with specific failures:
- "FFmpeg not found — required for video editing"
- "FFmpeg version too old for HEVC decoding (minimum: ffmpeg 4.x with libhevc)"
- "HEVC profile not supported by current FFmpeg build"

### Testing

1. Record with HEVC codec, click "Open in editor" — verify editor opens and plays the clip
2. Confirm same file plays in VLC as a baseline
3. If editor fails, error message should be specific and actionable
4. H264 recordings continue to open correctly (regression test)

### Acceptance Criteria

- [ ] HEVC-encoded MP4 recordings open in XerahS video editor
- [ ] Error messages are descriptive when the editor fails to open
- [ ] H264 recordings continue to open correctly (no regression)
- [ ] `ffprobe -show_streams` correctly identifies HEVC in test recordings

---

## Bug 3 — AV1 Screen Recording Unimplemented

### Problem Statement

AV1 is declared in the `VideoCodec` enum but has no encoder implementation in `MediaFoundationEncoder`. Selecting AV1 fails silently or throws an unhandled encoder exception. This creates a poor first impression — users expect the codec to either work or fail gracefully.

### Root Cause

`MediaFoundationEncoder.CreateSinkWriter` only supports H264:

```csharp
SetGuid(outputMediaType, MF_MT_SUBTYPE, MFVideoFormat_H264);
```

No AV1 GUID, no AV1 FourCC handling, no AV1 hardware detection, no FFmpeg fallback path for AV1.

### Technical Background

Windows 10 2004+ (SDK 10.0.19041+) supports AV1 via Media Foundation with the `MFVideoFormat_AV1` FourCC (`av01`). Hardware acceleration is available on Intel 10th Gen+, AMD RDNA 2+, and NVIDIA RTX 20-series+ GPUs. Software fallback uses `libaom-av1` via FFmpeg.

### Proposed Fix

**Step 1 — Add AV1 GUID and HEVC path to MediaFoundationEncoder**

```csharp
// GUID from mfapi.h — verify against current SDK
private static readonly Guid MFVideoFormat_AV1 = new("39326caf-d300-4d70-8c38-3aa4b33d1cc6");
private static readonly Guid MFVideoFormat_HEVC = new("4c552e48-ab84-4d6d-8298-1aee8f02c1f7"); // verify

// In CreateSinkWriter, switch on videoFormat.Codec:
switch (videoFormat.Codec)
{
    case VideoCodec.H264: SetGuid(outputMediaType, MF_MT_SUBTYPE, MFVideoFormat_H264); break;
    case VideoCodec.HEVC: SetGuid(outputMediaType, MF_MT_SUBTYPE, MFVideoFormat_HEVC); break;
    case VideoCodec.AV1:  SetGuid(outputMediaType, MF_MT_SUBTYPE, MFVideoFormat_AV1); break;
    case VideoCodec.VP9:  // Route to FFmpeg fallback (MF VP9 support is limited)
}
```

**Step 2 — Add AV1 hardware capability detection**

```csharp
private static bool IsAV1EncoderAvailable()
{
    // Use MFTEnum to check for AV1 codec presence on system
    // Return true if hardware or software AV1 encoder found
}
```

**Step 3 — Automatic FFmpeg fallback**

In `ScreenRecorderService.StartRecordingAsync`, catch `NotSupportedException` from encoder init when AV1 is selected and no encoder is available. If `FallbackServiceFactory` is available, delegate to `FFmpegRecordingService`. Show a toast that FFmpeg fallback is active.

**Step 4 — UI guard for AV1**

Disable AV1 option in settings UI with a tooltip when no AV1 encoder is available:
```
AV1: Requires Windows 10 2004+ with AV1 codec, or FFmpeg with libaom-av1
```

### Testing

1. AV1-available system: record 30s clip, verify output is `codec_name=av1` via `ffprobe -show_streams`
2. AV1-unavailable system: select AV1 → clear error or automatic FFmpeg fallback
3. FFmpeg fallback: with no AV1 support, verify FFmpeg is invoked with `libaom-av1`

### Acceptance Criteria

- [ ] AV1 selection on AV1-capable system produces valid AV1 MP4
- [ ] AV1 selection on non-AV1 system shows clear error or falls back to FFmpeg
- [ ] No silent failures for any codec selection
- [ ] `ffprobe -show_streams` confirms `codec_name = av1` in output

---

## Architecture Interactions

These three bugs share the same video encoding subsystem:

```
ScreenRecorderService
  ├── CaptureSource (DXGI → FrameData with Stride)
  ├── RegionCropper (uses FrameData.Stride for cropping)
  └── MediaFoundationEncoder
        ├── H264 (working)
        ├── HEVC (Bug 2: launch policy, Bug 3: encode path)
        └── AV1  (Bug 3: unimplemented)
```

Bug 1 (stride) affects all three codecs because it corrupts pixel data before it reaches any encoder. Bug 3 affects both HEVC and AV1 encoding paths in `MediaFoundationEncoder`. Fixing Bug 1 first is a prerequisite for validating Bug 2 and Bug 3 fixes.

---

## Risks

| Bug | Risk | Mitigation |
|-----|------|------------|
| Stride | Changing stride handling could affect HDR or rotated monitor captures | Test on rotated displays and HDR monitors |
| HEVC | HEVC patent licensing affects error message wording | Use factual, non-legal phrasing ("not supported" not "licensed") |
| AV1 | AV1 codec detection is driver-dependent; software encoding is slow | Provide clear hardware capability detection; use FFmpeg fallback; warn about performance |

---

## Priority Summary

| Priority | Bug | Task | Reason |
|----------|-----|------|--------|
| **P0** | Stride | Fix DXGI copy loop, MF encoder stride handling | Memory corruption affecting both screenshots AND recordings |
| **P1** | HEVC | Fix launch policy codec allowlist, FFmpeg HEVC detection | Feature works end-to-end but editor integration is broken |
| **P2** | AV1 | Implement AV1 encoder or FFmpeg fallback | Unimplemented codec; should fail gracefully |
