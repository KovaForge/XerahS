# IEIP0006: Move Linux Wallpaper Prewarm to ImageEditor

## Status
- Status: Draft

## Motivation
Currently, the prewarming of Linux desktop wallpapers (converting unsupported formats like `.jxl` into cached `.png` files via thumbnailers or `ffmpeg`) is initiated by the host application (e.g., during `MainWindow.axaml.cs` initialization with `PreWarmDestinationSettingsAsync`). 
Because the `ShareX.ImageEditor` component relies directly on the desktop wallpaper for its background context, and because other hosts (like the original Windows ShareX) might also integrate this editor, the responsibility for pre-loading or prewarming the background image should be owned by the Image Editor itself rather than the host's main window. Moving this behavior will improve encapsulation and ensure the editor always has its background ready without relying on host-specific startup tasks.

## Goals
- Move the initiation of wallpaper prewarming/conversion out of `MainWindow.axaml.cs`.
- Introduce a mechanism within `ShareX.ImageEditor` (or its Avalonia Integration layer) to asynchronously request the desktop wallpaper during initialization.
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

## Verification
- Launch XerahS on a GNOME environment with a `.jxl` wallpaper.
- Verify that the image editor opens without a UI lag spike on its first invocation.
- Verify that the background conversion process (`ffmpeg` or `glycin-thumbnailer`) runs exactly once.
- Verify that the host application compiles and runs without regression.
