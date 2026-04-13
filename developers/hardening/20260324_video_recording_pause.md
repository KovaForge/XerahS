# Bug Report: Recording Pause/Resume Causes Restart + UI Flood + Application Crash

- **Date**: 2026-03-24
- **Reported by**: QA Tester (not developer)
- **Branch**: `develop` (HEAD: `dd19a5b0`)
- **App Version**: `0.20.17` (built locally from source)
- **Severity**: Critical — renders recording feature completely unusable

---

## Environment

| Property | Value |
|---|---|
| OS | EndeavourOS Linux (rolling) |
| Kernel | `6.18.13-zen1-1-zen` x86\_64 |
| Display Server | Wayland (`WAYLAND_DISPLAY=wayland-0`) |
| Session Type | `XDG_SESSION_TYPE=wayland` |
| Compositor | KDE Plasma (KWin) |
| GPU | NVIDIA GeForce RTX 4070 Ti SUPER |
| GPU Driver | NVIDIA proprietary `590.48.01` |
| OpenGL | 4.6.0 NVIDIA (via EGL/Wayland) |
| CPU | AMD Ryzen 9 5900X 12-Core |
| .NET Version | `10.0.103` |
| GStreamer | `1.28.1` |
| XDG\_RUNTIME\_DIR | `/run/user/1000` |

---

## Bug 1: Pause/Resume Re-Prompts for Region and Restarts Recording

### Description
When pausing a Region Capture recording and then resuming it, the application does not resume the paused recording. Instead, it re-prompts the user to select a capture region as if a brand new recording is being started. The recording then restarts from zero. After approximately 1 second, the UI becomes completely unusable — it fills with what appears to be a live debug trace/exception dump rendered directly in the interface, making the application impossible to interact with.

### Steps to Reproduce
1. Launch the application on a Wayland session (KDE Plasma).
2. Start a **Region Capture** recording. Select a region when prompted.
3. Let the recording run for a few seconds.
4. Click **Pause**.
5. Click **Resume**.
6. Observe: the region selection prompt appears again (recording restarts).
7. After ~1 second, the UI floods with debug/trace information and becomes unresponsive.

### Expected Behavior
- Clicking Resume should seamlessly continue the paused recording without re-prompting for a region.
- The UI should remain stable and interactive.

### Actual Behavior
- Region selection re-fires — recording is not resumed, it is restarted.
- After ~1 second the UI shows a visually corrupt debug trace dump and becomes completely unresponsive.
- The application ultimately crashes (see Bug 2 below).

---

## Bug 2: GStreamer MP4Mux Crash → Unhandled `TaskCanceledException`

### Root Cause Evidence

The following GStreamer error is emitted during the recording lifecycle:

```
ERROR: from element /GstPipeline:pipeline0/GstMP4Mux:mp4mux0: Could not multiplex stream.
Additional debug info:
../gstreamer/subprojects/gst-plugins-good/gst/isomp4/gstqtmux.c(5927): gst_qt_mux_add_buffer ():
/GstPipeline:pipeline0/GstMP4Mux:mp4mux0:
Buffer has no PTS.
```

**Analysis**: `GstMP4Mux` received a buffer with no Presentation Timestamp (PTS). This is almost certainly caused by the pause/resume logic breaking the GStreamer pipeline's timestamp continuity. When recording is "resumed" (actually restarted), the new pipeline segments are likely feeding raw buffers into the muxer without proper PTS offset adjustment, causing the mux to fail.

This GStreamer pipeline failure then propagates up the stack and tears down the DBus connection, which in turn throws on the Avalonia UI thread:

### Full Unhandled Exception Stack Trace

```
Unhandled exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.
   at Avalonia.Threading.DispatcherOperation.Wait(TimeSpan timeout)
   at Avalonia.Threading.DispatcherOperation.Wait()
   at Avalonia.Threading.Dispatcher.InvokeImpl(DispatcherOperation operation, CancellationToken cancellationToken, TimeSpan timeout)
   at Avalonia.Threading.Dispatcher.Send(SendOrPostCallback action, Object arg, Nullable`1 priority)
   at Avalonia.Threading.AvaloniaSynchronizationContext.Send(SendOrPostCallback d, Object state)
   at Tmds.DBus.Protocol.DBusConnection.Observer.Emit(Exception exception)
   at Tmds.DBus.Protocol.DBusConnection.Observer.Dispose(Exception exception, Boolean removeObserver)
   at Tmds.DBus.Protocol.DBusConnection.Dispose()
   at Tmds.DBus.Protocol.Connection.Disconnect(Exception disconnectReason, DBusConnection trigger)
   at Tmds.DBus.Protocol.DBusConnection.HandleMessages(Exception exception, Message message)
   at System.Threading.Tasks.Task.<>c.<ThrowAsync>b__124_1(Object state)
   at System.Threading.ThreadPoolWorkQueue.Dispatch()
   at System.Threading.PortableThreadPool.WorkerThread.WorkerThreadStart()
   at System.Threading.Thread.StartCallback()
```

> **Note**: Exit code was `0` despite this being an unhandled exception — the crash is likely being swallowed somewhere upstream, which also explains why the UI debug flood appears rather than a clean error dialog.

### Stack Trace Analysis
The chain is: **GStreamer MP4Mux fails** → **Recording pipeline tears down** → **DBus portal session disconnects** → `Tmds.DBus.Protocol.DBusConnection.Dispose()` calls `Observer.Emit(exception)` → which tries to `Send()` on the Avalonia UI dispatcher → but the dispatcher is already in a cancelled state → throws `TaskCanceledException` as an unhandled exception that terminates the thread.

---

## Suspected Root Cause

The pause/resume implementation likely:
1. **Stops and re-creates the GStreamer pipeline** instead of sending a `PAUSE` state change to the existing pipeline. This causes the region selection to fire again (as if a new recording is being initiated).
2. The newly created pipeline either lacks proper PTS initialization or the re-created pipeline feeds into the same MP4Mux without resetting the timestamp context, causing `Buffer has no PTS`.
3. The pipeline failure is not caught cleanly, leading to a cascading DBus → Dispatcher thread crash.

---

## Relevant Wayland Globals (from `WAYLAND_DEBUG=1`)

The KWin compositor advertised the following relevant protocols — all correctly available:

| Protocol | Version |
|---|---|
| `xdg_wm_base` | 6 |
| `zwp_linux_dmabuf_v1` | 5 |
| `wp_linux_drm_syncobj_manager_v1` | 1 |
| `zwlr_layer_shell_v1` | 5 |
| `org_kde_plasma_shell` | 8 |
| `wp_fractional_scale_manager_v1` | 1 |
| `zxdg_decoration_manager_v1` | 1 |

No Wayland protocol errors were observed prior to the GStreamer failure — the crash is not a Wayland-layer issue; it is internal to the GStreamer pipeline management code.

---

## Files Likely Involved

Based on codebase inspection, the following are the most probable locations for the bug:

- **`WaylandPortalHotkeyService.cs`** — `src/platform/XerahS.Platform.Linux/Services/` — may be re-triggering portal session flow on resume
- **Recording pipeline manager** — GStreamer pipeline state machine; look for `Pause()` / `Resume()` implementations in the Linux platform layer
- **DBus connection lifecycle** — `Tmds.DBus` `DBusConnection.Dispose()` is being called aggressively; the Avalonia dispatcher sync context should not be used from `Dispose()` paths

---

## Suggested Fix Direction

1. **Pause/Resume**: Implement GStreamer pipeline `GST_STATE_PAUSED` → `GST_STATE_PLAYING` state transitions instead of stopping and restarting the pipeline. Do not re-fire region selection on resume.
2. **PTS Fix**: Ensure PTS values are continuous across pause/resume boundaries. If the pipeline is torn down and recreated, the new segment's PTS must be offset to continue from where the previous segment left off.
3. **DBus Crash**: Wrap `DBusConnection.Observer.Emit()` / `Dispose()` calls in a `try/catch` that gracefully handles the case where the Avalonia dispatcher is shut down, to prevent the unhandled exception from crashing the thread.
