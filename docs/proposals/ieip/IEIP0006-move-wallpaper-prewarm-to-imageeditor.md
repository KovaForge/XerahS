# IEIP0006: Move Wallpaper Prewarm to ImageEditor

## Status
- Status: Complete on March 30, 2026.

## Motivation
Historically, the editor wallpaper path mixed shared editor behavior with host-specific implementations. That left Linux wallpaper conversion/prewarm tied to host integration details and duplicated wallpaper-resolution code across hosts.

Because `ShareX.ImageEditor` depends on desktop wallpaper metadata for its canvas background, the default wallpaper lookup and prewarm behavior should live in the shared editor host layer rather than in XerahS-specific abstractions. That way XerahS, `ShareX.ImageEditor.App`, and third-party hosts all benefit from the same behavior by using the same registration entry point.

## Goals
- Move wallpaper prewarm initiation into `ShareX.ImageEditor`.
- Keep the default Windows, Linux, and macOS wallpaper services in `ShareX.ImageEditor.Hosting`.
- Make prewarm explicit so Windows and macOS can remain no-op while Linux can pre-convert unsupported wallpaper formats.
- Let host applications opt into the shared behavior through `EditorServices.EnsureDefaultDesktopWallpaperService()`.

## Non-Goals
- Replacing the external Linux conversion strategy (`ffmpeg`, `glycin-thumbnailer`, `gdk-pixbuf-thumbnailer`) with a new managed decoder.
- Introducing a native AppKit binding layer for macOS wallpaper lookup.
- Removing the ability for a host to provide a custom wallpaper service override when it has a good reason to do so.

## Implemented Design
1. **Shared wallpaper contract**
   - `IDesktopWallpaperService` now exposes `RequiresDesktopWallpaperPrewarm` and `PrewarmDesktopWallpaper()`, allowing the editor to distinguish "wallpaper lookup is supported" from "this platform benefits from background preparation."

2. **Editor-owned startup**
   - `ShareX.ImageEditor` starts wallpaper prewarm during `MainViewModel` initialization through `EditorServices.StartDesktopWallpaperPrewarm(...)`, instead of waiting for a host-specific settings-panel action.

3. **Shared default host services**
   - `EditorServices.EnsureDefaultDesktopWallpaperService()` now selects built-in wallpaper services from `ShareX.ImageEditor.Hosting`:
   - Windows: direct wallpaper-path lookup, no prewarm required.
   - Linux: wallpaper lookup plus `.jxl` conversion/cache prewarm.
   - macOS: wallpaper lookup with no prewarm required.

4. **Host simplification**
   - `ShareX.ImageEditor.App` and XerahS now both use the shared default registration path.
   - Third-party hosts can get the same behavior by calling `EditorServices.EnsureDefaultDesktopWallpaperService()` or going through `AvaloniaIntegration.Initialize()`.

## Verification
- `dotnet build ShareX.ImageEditor/src/ShareX.ImageEditor/ShareX.ImageEditor.csproj -c Debug -m:1 -nr:false`
- `dotnet build ShareX.ImageEditor/src/ShareX.ImageEditor.App/ShareX.ImageEditor.App.csproj -c Debug -m:1 -nr:false`
- `dotnet build src/desktop/XerahS.sln -c Debug -m:1 -nr:false -p:BuildWebUI=false`
- `dotnet test tests/XerahS.Tests/XerahS.Tests.csproj -c Debug -f net10.0-windows10.0.26100.0 --filter EditorWallpaperPrewarmTests -m:1 -nr:false -p:BuildWebUI=false`

## Implementation Notes
- The deleted `ShareX.ImageEditor.Loader/DesktopWallpaperService.cs` was only a duplicated Windows resolver. Its role is now covered by the shared hosting services in `ShareX.ImageEditor`.
- Linux prewarm is implemented in `ShareX.ImageEditor.Hosting.LinuxDesktopWallpaperService`, so the editor no longer depends on XerahS platform abstractions for wallpaper preparation.
- Windows intentionally does not prewarm because wallpaper lookup is already a direct file-path read.
- macOS intentionally does not prewarm. Apple exposes wallpaper URL and presentation metadata through `NSWorkspace`, and the current default service keeps lookup shell-based until a future AppKit binding is justified.
- XerahS may still keep its own platform wallpaper services for app-level features, but the editor host path no longer depends on them.

## Alternatives Considered: Native C# JXL Decoding
An alternative to pre-converting the wallpaper via `ffmpeg` or `glycin-thumbnailer` is decoding `.jxl` natively within the Avalonia/C# application. However, this is currently not viable:
- **SkiaSharp**: While SkiaSharp exposes the `SKEncodedImageFormat.Jpegxl` enum, the official pre-compiled native binaries distributed via NuGet do not include the `libjxl` dependency (due to its size and experimental nature). Enabling it would require maintaining a custom compiled fork of Skia/SkiaSharp, which contradicts the project's requirement to stay on the standard SkiaSharp 2.88.9 release.
- **Alternative .NET Libraries**: Tools like `Magick.NET`, `NetVips`, `PhotoSauce.NativeCodecs.Libjxl`, and `jxl.Net` provide JXL support by wrapping the native `libjxl` binary. However, introducing a heavy dependency like ImageMagick or libvips solely to decode a Linux desktop background adds unacceptable bloat to the application. 
Given these constraints, the out-of-process background conversion (via pre-installed OS thumbnailers or `ffmpeg`) combined with proper prewarming remains the most lightweight and reliable approach.
