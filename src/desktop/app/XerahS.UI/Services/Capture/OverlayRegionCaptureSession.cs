#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia.Threading;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.UI.Services.Capture;

internal static class OverlayRegionCaptureSession
{
    internal readonly record struct OverlayRegionCaptureResult(
        SKRectI Selection,
        SKBitmap? AnnotationLayer,
        PixelPoint AnnotationMonitorOrigin);

    public static async Task<SKRectI> SelectRegionAsync(
        IScreenCaptureService platformImpl,
        CaptureOptions? options,
        bool useFastOverlay)
    {
        SKRectI selection = SKRectI.Empty;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                CursorInfo? cursorInfo = null;
                if (options?.ShowCursor == true)
                {
                    try
                    {
                        cursorInfo = await platformImpl.CaptureCursorAsync();
                    }
                    catch
                    {
                        // Ignore cursor capture errors.
                    }
                }

                SKBitmap? backgroundForMagnifier = null;
                if (!useFastOverlay)
                {
                    try
                    {
                        backgroundForMagnifier = await platformImpl.CaptureFullScreenAsync(new CaptureOptions
                        {
                            ShowCursor = false,
                            UseModernCapture = options?.UseModernCapture ?? true,
                            LinuxRegionSelectorPreference = LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options),
                            MacOSRegionSelectorPreference = options?.MacOSRegionSelectorPreference ??
                                MacOSInteractiveRegionSelectorPreference.Automatic,
                            MacOSPlayCaptureSound = false
                        });
                    }
                    catch
                    {
                        // Ignore background capture errors.
                    }
                }

                var captureService = new RegionCaptureService
                {
                    Options = new XerahS.RegionCapture.RegionCaptureOptions
                    {
                        ShowCursor = options?.ShowCursor ?? false,
                        BackgroundImage = backgroundForMagnifier,
                        UseTransparentOverlay = useFastOverlay,
                        EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(options?.WorkflowId),
                    }
                };

                RegionSelectionResult? result;
                try
                {
                    result = await captureService.CaptureRegionAsync(cursorInfo);
                }
                finally
                {
                    RegionCaptureAnnotationOptionsStore.Persist();
                }

                if (result is not null)
                {
                    var region = result.Value.Region;
                    selection = new SKRectI((int)region.X, (int)region.Y, (int)region.Right, (int)region.Bottom);
                }
            });
        }
        catch
        {
            // Ignore errors to keep capture resilient.
        }

        return selection;
    }

    public static async Task<OverlayRegionCaptureResult> CaptureRegionAsync(
        CaptureOptions? effectiveOptions,
        DateTime sessionStartUtc,
        bool useFastOverlay,
        SKBitmap? fullScreenBitmap,
        CursorInfo? ghostCursor)
    {
        SKRectI selection = SKRectI.Empty;
        SKBitmap? annotationLayer = null;
        PixelPoint annotationMonitorOrigin = default;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var captureService = new RegionCaptureService
                {
                    Options = new XerahS.RegionCapture.RegionCaptureOptions
                    {
                        ShowCursor = effectiveOptions?.ShowCursor ?? false,
                        BackgroundImage = fullScreenBitmap,
                        UseTransparentOverlay = useFastOverlay,
                        EditorOptions = RegionCaptureAnnotationOptionsStore.GetEditorOptions(effectiveOptions?.WorkflowId),
                        SessionStartUtc = sessionStartUtc,
                    }
                };

                RegionSelectionResult? result;
                try
                {
                    result = await captureService.CaptureRegionAsync(ghostCursor);
                }
                finally
                {
                    RegionCaptureAnnotationOptionsStore.Persist();
                }

                if (result is not null)
                {
                    var region = result.Value.Region;
                    selection = new SKRectI((int)region.X, (int)region.Y, (int)region.Right, (int)region.Bottom);
                    annotationLayer = result.Value.AnnotationLayer;
                    annotationMonitorOrigin = result.Value.MonitorOrigin;
                }
            });
        }
        catch
        {
            // Ignore overlay session errors to preserve existing behavior.
        }

        return new OverlayRegionCaptureResult(selection, annotationLayer, annotationMonitorOrigin);
    }
}
