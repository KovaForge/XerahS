# IEIP0006: Move Linux Wallpaper Prewarm to ImageEditor

## Status
- Status: Implemented on March 29, 2026.

## Motivation
Historically, the prewarming of Linux desktop wallpapers (converting unsupported formats like `.jxl` into cached `.png` files via thumbnailers or `ffmpeg`) was initiated by host application startup code rather than by the editor itself.
Because the `ShareX.ImageEditor` component relies directly on the desktop wallpaper for its background context, and because other hosts (like the original Windows ShareX or the standalone `ShareX.ImageEditor.Loader`) might also integrate this editor, the responsibility for pre-loading or prewarming the background image should be owned by the Image Editor itself rather than the host applications. Moving this behavior will improve encapsulation, simplify both `XerahS` and `Loader` codebases, and ensure the editor always has its background ready without relying on host-specific startup tasks.

## Goals
- Move the initiation of wallpaper prewarming/conversion out of `MainWindow.axaml.cs` and other host startup sequences.
- Introduce a mechanism within `ShareX.ImageEditor` (or its Avalonia Integration layer) to asynchronously request the desktop wallpaper during initialization.
- Simplify all host applications (including `ShareX.ImageEditor.Loader`) by centralizing the wallpaper initialization logic.
- Maintain the concurrent locking mechanism (e.g., `WallpaperConversionLocks`) in the platform services to guarantee that the expensive conversion operation only runs exactly once per wallpaper.

## Non-Goals
- Modifying the underlying Linux wallpaper conversion logic, paths, or dependencies (e.g., `LinuxDesktopWallpaperProvider` itself will remain in platform services).
- Changing how wallpapers are fetched on Windows or macOS.

## Proposed Changes
1. **Extend `IDesktopWallpaperService` (Optional)**:
   - If necessary, expose a `PrewarmAsync()` or `PrepareAsync()` method on the wallpaper service contract (`EditorDesktopWallpaperAdapter`), enabling the editor to signal the platform layer to begin processing.
   
2. **Editor Initialization**:
   - Update `ShareX.ImageEditor`'s startup or view-model initialization to dispatch a background task that requests the desktop wallpaper. By simply requesting the wallpaper early in an async context, the underlying platform service will hit the `TryConvertWallpaper` lock and perform the conversion before the UI actually needs to render it.

3. **Cleanup Host Code**:
   - Remove any specific wallpaper prewarming ties from `MainWindow.axaml.cs` so XerahS doesn't need to manually optimize the editor's dependencies.
   - Remove or simplify redundant wallpaper logic from `ShareX.ImageEditor.Loader` and other integration hosts, reducing the required boilerplate.

## Verification
- Launch XerahS on a GNOME environment with a `.jxl` wallpaper.
- Verify that the image editor opens without a UI lag spike on its first invocation.
- Verify that the background conversion process (`ffmpeg` or `glycin-thumbnailer`) runs exactly once.
- Verify that the host application compiles and runs without regression.

## Implementation Notes
- `ShareX.ImageEditor` now starts wallpaper prewarming during `MainViewModel` initialization instead of waiting for the settings panel to open.
- Default wallpaper-service registration for standalone hosts was centralized in `EditorServices.EnsureDefaultDesktopWallpaperService()`, allowing `ShareX.ImageEditor.Loader` to drop its duplicate Windows wallpaper resolver.
- XerahS continues to provide its own platform-aware adapter (`EditorDesktopWallpaperAdapter`), so Linux `.jxl` conversion still flows through `LinuxDesktopWallpaperProvider` and its existing conversion lock.

## Alternatives Considered: Native C# JXL Decoding
An alternative to pre-converting the wallpaper via `ffmpeg` or `glycin-thumbnailer` is decoding `.jxl` natively within the Avalonia/C# application. However, this is currently not viable:
- **SkiaSharp**: While SkiaSharp exposes the `SKEncodedImageFormat.Jpegxl` enum, the official pre-compiled native binaries distributed via NuGet do not include the `libjxl` dependency (due to its size and experimental nature). Enabling it would require maintaining a custom compiled fork of Skia/SkiaSharp, which contradicts the project's requirement to stay on the standard SkiaSharp 2.88.9 release.
- **Alternative .NET Libraries**: Tools like `Magick.NET`, `NetVips`, `PhotoSauce.NativeCodecs.Libjxl`, and `jxl.Net` provide JXL support by wrapping the native `libjxl` binary. However, introducing a heavy dependency like ImageMagick or libvips solely to decode a Linux desktop background adds unacceptable bloat to the application. 
Given these constraints, the out-of-process background conversion (via pre-installed OS thumbnailers or `ffmpeg`) combined with proper prewarming remains the most lightweight and reliable approach.
