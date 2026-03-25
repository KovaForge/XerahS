# Bug Report: Application Crash on Recording Stop Button

- **Date**: 2026-03-24
- **Reported by**: QA Tester (not developer)
- **Branch**: `develop` (HEAD: `dd19a5b0`)
- **App Version**: `0.20.17` (built locally from source)
- **Severity**: Critical — application always crashes when stopping a recording

---

## Environment

| Property | Value |
|---|---|
| OS | EndeavourOS Linux (rolling) |
| Kernel | `6.18.13-zen1-1-zen` x86\_64 |
| Display Server | Wayland (`WAYLAND_DISPLAY=wayland-0`) |
| Session Type | `XDG_SESSION_TYPE=wayland` |
| Compositor | KDE Plasma (KWin) — identified by `org_kde_plasma_shell`, `org_kde_kwin_*` globals |
| GPU | NVIDIA GeForce RTX 4070 Ti SUPER |
| GPU Driver | NVIDIA proprietary `590.48.01` |
| OpenGL | 4.6.0 NVIDIA (EGL/Wayland) |
| CPU | AMD Ryzen 9 5900X 12-Core |
| .NET Version | `10.0.103` |
| GStreamer | `1.28.1` |
| WebKit | `webkitgtk-2.50.5` (WebKit2GTK) |
| XDG\_RUNTIME\_DIR | `/run/user/1000` |

---

## Description

When a Region Capture recording is stopped using the **Stop button**, the recording file is successfully saved to disk. A new window briefly appears (the Video Editor). Almost immediately after, the application crashes entirely. The Abort button does **not** cause this crash — the crash is specific to the Stop button flow (which triggers the Video Editor window).

---

## Steps to Reproduce

1. Launch the application on a Wayland/KDE Plasma session with an NVIDIA GPU.
2. Start a Region Capture recording. Select a region when prompted.
3. Let the recording run for a few seconds.
4. Click the **Stop** button (not Abort).
5. Observe: the recording is saved, a window briefly flashes on screen, then the entire application crashes.

### Contrast (working case)
- Clicking **Abort** does not crash the application.

---

## Root Cause Analysis — Two Concurrent Failures

### Cause 1: Wayland DRM Syncobj Protocol Error

At the point of crash, the Wayland trace shows the Video Editor's Photino window (`wl_surface#39`) being created and committing its first frame. The compositor then immediately returns a **fatal protocol error**:

```
[2542904.789]  -> wl_surface#39.commit()
[2542905.132] wl_display#1.error(wp_linux_drm_syncobj_surface_v1#44, 4,
              "explicit sync is used, but no acquire point is set")
Gdk-Message: 04:03:52.228: Error 71 (Protocol error) dispatching to Wayland display.
```

**What this means**: The application requested `wp_linux_drm_syncobj_manager_v1` (explicit GPU sync) for the Video Editor surface, but then committed the surface to the compositor **without setting an acquire sync point** on the `wp_linux_drm_syncobj_surface_v1`. KWin's compositor treats this as a fatal protocol violation (error code `4`) and tears down the connection.

**Why this happens on NVIDIA**: The NVIDIA driver enables `wp_linux_drm_syncobj_manager_v1` (DMA-BUF explicit synchronization) which KWin then negotiates. Photino.NET (or the underlying GTK/WebKit layer) attempts to use explicit sync but fails to set the acquire point before committing the buffer — likely a Photino.NET + NVIDIA Wayland explicit sync compatibility issue.

**Why Stop triggers it but Abort does not**: The Stop button opens the Video Editor window (Photino.NET/WebKit2GTK). Abort skips the Video Editor entirely. The crash is in Video Editor window initialization, not in the recording pipeline itself.

---

### Cause 2: WebKit Internal Crash (Network Process)

Following the Wayland protocol error, the WebKit layer used by Photino.NET also reports an internal crash:

```
ERROR: WebKit encountered an internal error. This is a WebKit bug.
/usr/src/debug/webkit2gtk-4.1/webkitgtk-2.50.5/Source/WebKit/WebProcess/
  Network/WebLoaderStrategy.cpp(716) :
  void WebKit::WebLoaderStrategy::networkProcessCrashed()
```

This error fires **twice**, indicating the WebKit network process responsible for loading the Video Editor's HTML frontend (`frontend/dist/index.html`) crashes when the Wayland connection is torn down beneath it. The double-fire suggests both the main WebProcess and a secondary renderer are affected.

---

## Wayland Protocol Context (from `WAYLAND_DEBUG=1`)

The sequence leading to the crash:

```
# Video Editor window surface is created and initial buffer committed:
[2542898.481]  -> wl_shm#7.create_pool(new id wl_shm_pool#47, fd 360, 2160000)
[2542898.490]  -> wl_shm_pool#47.create_buffer(new id wl_buffer#36, 0, 900, 600, 3600, 0)
[2542901.445]  -> wp_linux_drm_syncobj_manager_v1#34.get_surface(
                       new id wp_linux_drm_syncobj_surface_v1#44, wl_surface#39)
# ↑ Explicit sync surface object created — acquire point is NEVER set before commit ↓
[2542904.789]  -> wl_surface#39.commit()
# ↓ Compositor returns fatal protocol error:
[2542905.132] wl_display#1.error(wp_linux_drm_syncobj_surface_v1#44, 4,
                   "explicit sync is used, but no acquire point is set")
```

**Available Wayland protocols** (all correctly advertised by KWin):

| Protocol | Version |
|---|---|
| `xdg_wm_base` | 6 |
| `zwp_linux_dmabuf_v1` | 5 |
| `wp_linux_drm_syncobj_manager_v1` | 1 |
| `zwlr_layer_shell_v1` | 5 |
| `wp_fractional_scale_manager_v1` | 1 |
| `zxdg_decoration_manager_v1` | 1 |

---

## Exit Code

```
Exit code: 0
```

Despite two fatal errors (Wayland protocol violation + WebKit crash), the process exits with code `0`. This means the crash is not being caught as an unhandled exception at the .NET level — it is a native-level abort from the Wayland/GTK layer that the managed runtime sees as a clean exit. This also explains why no .NET stack trace is available for this crash.

---

## Files / Components Likely Involved

| File / Component | Reason |
|---|---|
| `VideoEditorHost.cs` / `VideoEditorSession.Run()` | Creates the PhotinoWindow (and thus the Wayland surface) |
| Photino.NET native library | Opts into `wp_linux_drm_syncobj_manager_v1` but does not set acquire point before committing |
| `AvaloniaUIService.ShowVideoEditorAsync()` | Calls `VideoEditorHost.ShowEditorDialog()` on Stop |
| WebKit2GTK `2.50.5` | Secondary crash from network process dying after Wayland teardown |

---

## Suggested Fix Direction

### Option A — Disable explicit sync for Photino on NVIDIA/Wayland (short-term)
Set the environment variable `GDK_DEBUG=no-explicit-sync` (or equivalent) before creating the Photino window on Linux to prevent GTK/GDK from negotiating `wp_linux_drm_syncobj_manager_v1`. This avoids the protocol entirely until Photino.NET gains proper explicit sync support.

```csharp
// In VideoEditorSession.Run(), before new PhotinoWindow():
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    Environment.SetEnvironmentVariable("GDK_DEBUG", "no-explicit-sync");
```

### Option B — Update Photino.NET (long-term)
The proper fix requires Photino.NET to correctly set the `wp_linux_drm_syncobj_surface_v1` acquire sync point before each `wl_surface.commit()`. This is a Photino.NET upstream bug that should be reported to the Photino.NET project, referencing this crash and the `wp_linux_drm_syncobj_manager_v1` protocol.

### Option C — Catch the Wayland fatal error gracefully
Wrap the `VideoEditorSession.Run()` native teardown in a way that catches the GDK `Error 71 (Protocol error)` signal/exception and displays a user-friendly error dialog instead of silently crashing the entire application.
