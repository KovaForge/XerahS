# XIP0059 Recover Partial Recording on External Portal Session Termination

**Status**: Complete
**Version**: v0.22.257

**Created**: 2026-03-30  
**Updated**: 2026-03-30  
**Area**: Desktop | Core  
**Goal**: Gracefully recover partial recordings when the XDG ScreenCast portal session is terminated externally (e.g. via GNOME's screencast indicator).

---

## Overview

When recording on GNOME Wayland via the XDG ScreenCast portal, the user can stop the recording from GNOME's built-in screencast indicator (red dot in the top panel) instead of XerahS's own stop button. This kills the portal session externally, causing PipeWire to tear down the stream mid-recording. GStreamer's `pipewiresrc` then fails with `not-negotiated (-4)`, exits non-zero, and XerahS treats it as a fatal crash — even though GStreamer may have already flushed valid video data to the segment file.

**Root cause chain:**
```
GNOME indicator stops portal session
  → PipeWire tears down stream
    → pipewiresrc fails with not-negotiated (-4)
      → GStreamer exits non-zero
        → XerahS treats as fatal error
          → Partial recording discarded
```

**Fix:** Before treating a GStreamer non-zero exit as a fatal error, check if the segment output file exists with non-trivial size (>0 bytes). If so, treat it as an external stop (partial recording) rather than a crash, allowing the user to keep their video.

Additionally, fixed a debug file listing bug where `ScreenRecordingManager.StopRecordingCoreAsync` searched for `*.mp4` files even when the recording container was WebM/MKV.

## Implementation

### Phase 1: Part-File Recovery (Completed)

**Key Files:**
- `src/platform/XerahS.Platform.Linux/Recording/WaylandPortalRecordingService.cs`
- `src/desktop/core/XerahS.Core/Managers/ScreenRecordingManager.cs`

**Changes:**

1. Added `_gstreamerOutputPath` field to track the segment file GStreamer writes to.
2. Added `ExtractGStreamerOutputPath(args)` helper to parse the `filesink location="..."` path from GStreamer pipeline args.
3. In `RunGStreamerProcess`, after GStreamer exits non-zero and `!_stopRequested`, check if the segment file exists with >0 bytes. If so, return `false` (success) instead of triggering `HandleFatalError`.
4. Fixed hardcoded `"*.mp4"` glob pattern in `ScreenRecordingManager.StopRecordingCoreAsync` to use the actual file extension.

## Non-Negotiable Rules

1. Never silently discard a recording that contains valid data.
2. The normal XerahS stop flow (hotkey/tray button) must not be affected.
3. The fix must tolerate incomplete/truncated segment files — a short video is better than no video.

## Deliverables

1. ✅ Part-file recovery in `WaylandPortalRecordingService.RunGStreamerProcess`
2. ✅ `ExtractGStreamerOutputPath` helper (internal, testable)
3. ✅ Fixed debug file listing glob in `ScreenRecordingManager`

## Affected Components

- **XerahS.Platform.Linux**: `WaylandPortalRecordingService` — part-file recovery logic
- **XerahS.Core**: `ScreenRecordingManager` — debug file listing fix

## Post-v1 Improvements

- Subscribe to `org.freedesktop.portal.Session::Closed` D-Bus signal for proactive external stop detection.
- Send SIGINT to GStreamer on portal closure for a clean EOS flush.
- Investigate `persist_mode=2` limitations with GNOME's screencast indicator.
- Add automatic segment concatenation resilience for partial WebM files.
