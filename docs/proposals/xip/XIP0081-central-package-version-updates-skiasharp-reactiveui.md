# XIP0081: Central Package Version Updates — SkiaSharp 4.x, ReactiveUI 24.x, and Minor/Patch Bumps

**Status:** Proposed
**Author:** Claude
**Date:** 2026-08-01
**Target:** XerahS develop branch

---

## Summary

Update all central package versions in `Directory.Packages.props` to their latest stable releases. This includes:

1. **SkiaSharp 3.119.4 → 4.151.0** (major version, breaking changes)
2. **ReactiveUI 22.3.1 → 24.0.0** (major version, low-risk for our usage)
3. **All other minor/patch bumps** (FluentFTP, NUnit, xunit, Avalonia, etc.)

---

## Motivation

`dotnet list package --outdated` reported ~25 packages with available updates. The two major-version bumps must be addressed before the rest of the codebase moves forward:

- **SkiaSharp 3.x is EOL**. The 4.x line is the supported stable release as of 2026. 4.x fixes use-after-free bugs in native object disposal, improves Vulkan interop, and aligns with Skia engine m148.
- **ReactiveUI 22.x predates Avalonia 12**. 24.x is the recommended companion for Avalonia 12.
- **Other packages** have accumulated minor-version drift over time. Bringing them current reduces technical debt and surfaces known security fixes.

---

## Risk Classification

| Package | From | To | Risk | Notes |
|---|---|---|---|---|
| SkiaSharp | 3.119.4 | 4.151.0 | **HIGH** | Major; ~250 files; API breakage |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | 4.151.0 | **HIGH** | Tied to SkiaSharp |
| ReactiveUI | 22.3.1 | 24.0.0 | **Low** | 2 APIs used in 1 base class |
| Avalonia.* | 12.0.5 | 12.1.1 | Low | Minor |
| NUnit / xunit / Microsoft.NET.Test.Sdk | various | latest | Low | Test infra only |
| Others | various | latest | Low | Minor/patch |

---

## Part 1: ReactiveUI 22.3.1 → 24.0.0

### Why upgrade

- Recommended companion for Avalonia 12.
- Includes upstream security fixes.

### Source impact

Only one file imports ReactiveUI:

- `src/desktop/core/XerahS.ViewModels/ViewModelBase.cs`
  - Uses `ReactiveObject` (base class) — unchanged in 24.x
  - Uses `RaiseAndSetIfChanged(ref, value)` — unchanged in 24.x

**No source code changes required**. Build verification suffices.

### Files to modify

- `Directory.Packages.props` line 37: `ReactiveUI 22.3.1` → `24.0.0`

### Verification

```powershell
dotnet build src/desktop/core/XerahS.ViewModels/XerahS.ViewModels.csproj
```

---

## Part 2: SkiaSharp 3.119.4 → 4.151.0

### Breaking changes (from official SkiaSharp 4.x release notes)

#### 2.1 `SKPaint` text APIs → `SKFont` (COMPILE ERRORS)

~115 text/font members were removed from `SKPaint` and moved to `SKFont`. Key migrations:

| 3.x (`SKPaint`) | 4.x (`SKFont`) |
|---|---|
| `paint.Typeface` | `font.Typeface` |
| `paint.TextSize` | `font.Size` |
| `paint.TextScaleX` | `font.ScaleX` |
| `paint.TextSkewX` | `font.SkewX` |
| `paint.IsLinearText` | `font.LinearMetrics` |
| `paint.SubpixelText` | `font.Subpixel` |
| `paint.LcdRenderText` | `font.Edging` |
| `paint.FakeBoldText` | `font.Embolden` |
| `paint.FontMetrics` | `font.Metrics` |
| `paint.GetFontMetrics()` | `font.GetFontMetrics()` |
| `paint.TextAlign` | explicit `SKTextAlign` argument |
| `paint.MeasureText(...)` | `font.MeasureText(...)` |
| `paint.GetTextPath(...)` | `SKPathBuilder` or `SKTextBlob` |

**Critical runtime trap:** `new SKFont()` in 4.x uses `SKTypeface.Empty` (no glyphs). Text will silently render nothing. Always construct with `new SKFont(SKTypeface.Default, size)` or similar.

Text-drawing overload `canvas.DrawText(text, x, y, paint)` (no `SKFont`) is now an error. Use `canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint)`.

#### 2.2 `SKFilterQuality` → `SKSamplingOptions` (COMPILE ERRORS)

`SKFilterQuality` enum is removed. Replace all uses with `SKSamplingOptions`:

```csharp
// 3.x (breaks in 4.x)
bitmap.ScalePixels(dest, SKFilterQuality.High);

// 4.x
var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
bitmap.ScalePixels(dest, sampling);
```

Affected: `SKBitmap.Resize`, `SKBitmap.ScalePixels`, `SKBitmap.ToShader`, `SKImage.ScalePixels`, `SKImage.ToShader`, `SKPixmap.ScalePixels`, `SKShader.CreateImage`.

#### 2.3 `SKImage.Encode(format, int)` signature change

15+ call sites use `SKImage.Encode(SKEncodedImageFormat, int quality)` which is replaced by strongly-typed overloads:

```csharp
// 3.x
image.Encode(SKEncodedImageFormat.Png, 100);

// 4.x
image.Encode(SKPngEncoderOptions.Default)  // or SKJpegEncoderOptions / SKWebpEncoderOptions
```

#### 2.4 `SKTypeface.FromFamilyName` null behavior changed

Unknown font families no longer return `null`; they return a fallback. Code checking for null must use `SKFontManager.Default.MatchFamily(...)` instead.

#### 2.5 Pixel output differences

Skia engine upgrade to m148 may change rendered pixels even with correct API migration. EXIF orientation, mipmap sharpening, color accuracy (Rec.709/HLG/PQ), text metrics may differ.

### Convergence prereq

Root `Directory.Packages.props` pins `SkiaSharp 3.119.4` (stable). `ShareX.ImageEditor/Directory.Packages.props` pins `SkiaSharp 3.119.4-preview.1.1` (preview). The preview version has a needed `SKBitmap.Resize(info, SKSamplingOptions)` overload. **Both must be aligned to 4.151.0 simultaneously.**

### Scope

#### Projects affected

| Project | SkiaSharp Usage |
|---|---|
| `XerahS.UI` | ImageHelpers, RegionCapture, QR codes |
| `XerahS.Core` | QR codes (ZXing) |
| `XerahS.Common` | ImageHelpers (resize/rotate/save) |
| `XerahS.RegionCapture` | SKCanvas overlay rendering |
| `XerahS.Platform.Windows` | Capture pipeline |
| `XerahS.Platform.MacOS` | Clipboard image, capture |
| `XerahS.Platform.Linux` | Capture pipeline |
| `XerahS.Platform.Mobile` | Capture pipeline |
| `XerahS.Media` | Clipboard codecs |
| `ShareX.ImageEditor` (submodule) | ~60+ image effect files |

#### Files requiring code changes (estimated 15–25 files)

| File pattern | Change needed |
|---|---|
| `ShareX.ImageEditor/src/ShareX.ImageEditor/Helpers/SkiaCompat.cs` | Verify `SKSamplingOptions(SKCubicResampler.Mitchell)` constructor compiles in 4.x; update if signature changed |
| `ShareX.ImageEditor/**/*ImageEffect.cs` | `SKPaint.TextSize` → `SKFont.Size` + explicit `SKTextAlign` |
| `ShareX.ImageEditor/**/DrawingEffect*.cs` | `SKPaint.TextSize` → `SKFont.Size` |
| `ShareX.ImageEditor/**/OutlinedTextControl.cs` | `SKPaint.TextSize` → `SKFont.Size` |
| `src/desktop/core/XerahS.Common/Helpers/ImageHelpers.cs` | `SKImage.Encode(format, int)` → typed encoder options |
| `src/desktop/core/XerahS.Media/Clipboard/ClipboardDibCodec.cs` | `EncodePng` → `SKPngEncoderOptions` |
| `src/desktop/app/XerahS.RegionCapture/**/*.cs` | `SKImage.Encode` + `SKPaint.TextSize` |
| `src/platform/XerahS.Platform.MacOS/**/*.cs` | `SKImage.Encode` + `SKPaint.TextSize` |
| `src/desktop/app/XerahS.UI/ViewModels/QrCodeGeneratorViewModel.cs` | `SKPaint.TextSize` → `SKFont.Size` |
| `src/desktop/core/XerahS.Core/Services/QrCodeService.cs` | ZXing + Skia encode |

#### Central convergence points

1. **`ShareX.ImageEditor/src/ShareX.ImageEditor/Helpers/SkiaCompat.cs`** — existing compatibility shim centralizes `SKSamplingOptions(SKCubicResampler.Mitchell)` construction. All ~38 call sites route through here. Fix here propagates to all effects.
2. **`src/desktop/core/XerahS.Common/Helpers/ImageHelpers.cs`** — `SaveBitmap` method using `SKImage.Encode(format, int)`.
3. **`src/desktop/core/XerahS.Media/Clipboard/ClipboardDibCodec.cs`** — `EncodePng` using `SKImage.Encode`.

### Implementation Steps

#### Step 0: Version alignment

Update `Directory.Packages.props` and `ShareX.ImageEditor/Directory.Packages.props` simultaneously to `SkiaSharp 4.151.0` and `SkiaSharp.NativeAssets.Win32 4.151.0`.

#### Step 1: SkiaCompat.cs fix

Inspect `SkiaCompat.cs` — verify the `SKSamplingOptions(SKCubicResampler.Mitchell)` constructor still compiles in 4.x. If the constructor signature changed, update here first.

#### Step 2: `SKPaint.TextSize` → `SKFont.Size` migration

Scan all files using `SKPaint` with `TextSize`, `TextScaleX`, `Typeface`, etc. Replace with explicit `SKFont` construction and pass `font` + `SKTextAlign` to drawing methods.

Pattern:
```csharp
// BEFORE (3.x)
using var paint = new SKPaint { TextSize = 16, Color = SKColors.Black };
canvas.DrawText(text, x, y, paint);

// AFTER (4.x)
using var font = new SKFont(SKTypeface.Default, 16);
using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
```

#### Step 3: `SKImage.Encode` → typed encoder options

Replace `image.Encode(SKEncodedImageFormat.X, quality)` with the appropriate encoder options object.

#### Step 4: NativeAssets update

`SkiaSharp.NativeAssets.Win32` likely renamed or restructured in 4.x. Check after bumping and update bindings accordingly.

#### Step 5: Vortice pairing

`Vortice.Direct2D1 3.8.3` may need a minor bump to stay compatible with SkiaSharp 4.x `GRVorticeD3DBackendContext`.

#### Step 6: Build and fix compilation errors

```powershell
dotnet build XerahS.sln 2>&1 | Select-String "error CS"
```

Iterate until clean. Expected error count: 50–150 initial errors, mostly in `ShareX.ImageEditor`.

#### Step 7: Regression testing

- Image export (PNG, JPEG, WebP, GIF)
- Region capture overlay rendering
- QR code generation/decode
- Screenshot capture pipeline (Windows, macOS, Linux)
- Thumbnail generation
- Text rendering (Latin, CJK if applicable)

---

## Part 3: All Other Package Updates (Minor/Patch)

### Packages to bump

| Package | Current | Latest | Notes |
|---|---|---|---|
| NUnit | 4.5.1 | 4.6.1 | |
| NUnit.Analyzers | 4.12.0 | 4.14.0 | |
| FluentFTP | 53.0.2 | 54.2.0 | |
| SharpHook | 7.1.1 | 7.1.3 | |
| Tmds.DBus | 0.92.0 | 0.94.2 | |
| xunit | 2.9.0 | 2.9.3 | |
| xunit.runner.visualstudio | 2.8.2 | 3.1.5 | |
| Microsoft.NET.Test.Sdk | 18.4.0 | 18.8.1 | |
| coverlet.collector | 8.0.1 | 10.0.1 | |
| ZXing.Net.Bindings.SkiaSharp | 0.16.9 | 0.16.22 | |
| System.CommandLine | 2.0.1 | 2.0.10 | |
| System.ServiceProcess.ServiceController | 10.0.0 | 10.0.10 | |
| Microsoft.Data.Sqlite | 10.0.1 | 10.0.10 | |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | 10.0.10 | |
| System.Drawing.Common | 10.0.1 | 10.0.10 | |
| System.Security.Cryptography.ProtectedData | 10.0.1 | 10.0.10 | |
| Microsoft.Windows.CsWin32 | 0.3.183 | 0.3.298 | |
| Microsoft.Windows.CsWinRT | 2.2.0 | 2.3.1 | |
| NETStandard.Library | 2.0.0 | 2.0.3 | |
| AWSSDK.S3 | 4.0.18.3 | 4.0.101.6 | |
| Avalonia.* | 12.0.5 | 12.1.1 | Multiple sub-packages |
| Avalonia.Controls.DataGrid | 12.0.1 | 12.1.0 | |

### Per-project Avalonia updates

The following `.csproj` files reference specific Avalonia versions and must be updated to `12.1.1`:

- `XerahS.UI/XerahS.UI.csproj`
- `XerahS.RegionCapture/XerahS.RegionCapture.csproj`
- `XerahS.Imgur.Plugin/XerahS.Imgur.Plugin.csproj`
- `XerahS.AmazonS3.Plugin/XerahS.AmazonS3.Plugin.csproj`
- `XerahS.Ftp.Plugin/XerahS.Ftp.Plugin.csproj`
- `XerahS.Paste2.Plugin/XerahS.Paste2.Plugin.csproj`
- `XerahS.Pastebin.Plugin/XerahS.Pastebin.Plugin.csproj`
- `XerahS.GitHubGist.Plugin/XerahS.GitHubGist.Plugin.csproj`
- `XerahS.Dropbox.Plugin/XerahS.Dropbox.Plugin.csproj`
- `XerahS.Bitly.Plugin/XerahS.Bitly.Plugin.csproj`
- `XerahS.Nextcloud.Plugin/XerahS.Nextcloud.Plugin.csproj`
- `XerahS.Immich.Plugin/XerahS.Immich.Plugin.csproj`

For each, change `<Avalonia>12.0.5</Avalonia>` to `<Avalonia>12.1.1</Avalonia>` (and same for other Avalonia package references).

---

## Files to Modify

1. `Directory.Packages.props` — all package version bumps
2. `ShareX.ImageEditor/Directory.Packages.props` — SkiaSharp version convergence
3. `src/desktop/core/XerahS.ViewModels/ViewModelBase.cs` — ReactiveUI 24.x compile verification (no code change expected)
4. `ShareX.ImageEditor/src/ShareX.ImageEditor/Helpers/SkiaCompat.cs` — SkiaSharp 4.x compatibility shim
5. `src/desktop/core/XerahS.Common/Helpers/ImageHelpers.cs` — `SKImage.Encode` signature update
6. `src/desktop/core/XerahS.Media/Clipboard/ClipboardDibCodec.cs` — `EncodePng` update
7. `src/desktop/app/XerahS.RegionCapture/**/*.cs` — various SkiaSharp 4.x updates
8. `src/platform/XerahS.Platform.MacOS/**/*.cs` — SkiaSharp 4.x Encode + TextSize updates
9. Plus ~10-15 additional files in `ShareX.ImageEditor/` for TextSize/Encode/Resize fixes
10. All `.csproj` files with hardcoded Avalonia `<Avalonia>12.0.x</Avalonia>` versions

---

## Verification

```powershell
# Build
dotnet restore XerahS.sln
dotnet build XerahS.sln

# Test
dotnet test XerahS.sln --no-build

# Smoke test
# Launch app, exercise capture + edit + upload workflow
```

---

## Execution Order

1. **ReactiveUI bump first** (low risk, verifies that one file compiles)
2. **Minor/patch package bumps** (independent of SkiaSharp)
3. **SkiaSharp 4.x migration** (highest risk, isolated to image-pipeline code)
4. **Build, test, commit, push**

This ordering ensures that if SkiaSharp 4.x cannot be completed, the rest of the package updates can still be merged.

---

## Risks and Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| SkiaSharp 4.x unbuildable mid-migration | High | Phase 3 is isolated; Parts 1 and 2 can merge without Part 3 |
| `new SKFont()` silently renders nothing | High | Audit all `new SKFont()` calls; ensure `SKTypeface.Default` is passed |
| `SKBitmap.Resize` overload gap in stable 4.x | High | Verify overload availability before committing to stable channel |
| ZXing.Net.Bindings.SkiaSharp not yet 4.x compatible | Medium | Check NuGet for 4.x-compatible binding; if not, defer SkiaSharp |
| Avalonia 12.1.1 + ReactiveUI 24.x combined compat | Medium | Build verification catches this |
| Pixel-diff regression in image output | Medium | Update golden images if test infra supports it; otherwise accept semantic equivalence |

---

## Rollback Plan

If SkiaSharp 4.x cannot be completed within this effort:

1. Revert `Directory.Packages.props` and `ShareX.ImageEditor/Directory.Packages.props` SkiaSharp entries to pinned 3.119.4
2. Keep ReactiveUI 24.x and minor/patch bumps (they are independent)
3. Document remaining SkiaSharp blockers in a follow-up issue

---

## Effort Estimate

| Phase | Effort |
|---|---|
| ReactiveUI bump + build verify | 30 min |
| SkiaSharp 4.x migration (parts 1–7) | 8–14 hrs |
| Minor/patch bumps + Avalonia .csproj edits | 1 hr |
| Build/fix cycle | 1–2 hrs |
| Regression testing | 1–2 hrs |
| Commit + push | 15 min |
| **Total** | **12–20 hrs** |

---

## Success Criteria

- [ ] `dotnet build XerahS.sln` compiles without errors
- [ ] All `XerahS.Tests` pass
- [ ] Region capture workflow works (exercise overlay + screenshot)
- [ ] Image export pipeline produces valid PNG/JPEG/WebP/GIF
- [ ] QR code generation/decode works
- [ ] Git commit and push to `develop`
