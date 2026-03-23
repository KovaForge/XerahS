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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Services;
using SkiaSharp;
using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using XerahS.UI.Services.Capture;

namespace XerahS.UI.Services
{
    public class ScreenCaptureService : IScreenCaptureService, ILinuxRegionCaptureCapabilityProvider, ILinuxRegionSelectorDiagnosticsProvider
    {
        private const string LinuxOverlayProviderId = "xerahs-overlay";
        private readonly IScreenCaptureService _platformImpl;
        private readonly LinuxRegionSelectorResolver _linuxResolver;

        public ScreenCaptureService(IScreenCaptureService platformImpl)
        {
            _platformImpl = platformImpl;
            _linuxResolver = new LinuxRegionSelectorResolver(LinuxOverlayProviderId);
        }

        public Task<SKBitmap?> CaptureRectAsync(SKRect rect, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureRectAsync(rect, options);
        }

        public async Task<SKRectI> SelectRegionAsync(CaptureOptions? options = null)
        {
            LinuxRegionCaptureCapability? linuxCapability =
                OperatingSystem.IsLinux() ? _linuxResolver.GetCapability(_platformImpl, options) : null;
            var effectiveLinuxPreference = _linuxResolver.ResolveEffectivePreference(options, linuxCapability, _platformImpl);
            bool canUseLinuxOverlayFallback = linuxCapability?.SupportsLegacyOverlayCapture == true;
            bool shouldTryLinuxNativeSelection = LinuxCaptureOptionsResolver.ShouldTryLinuxNativeRegionCapture(effectiveLinuxPreference, linuxCapability);

            if (shouldTryLinuxNativeSelection)
            {
                var nativeSelectionOptions = LinuxCaptureOptionsResolver.NormalizeLinuxNativeCaptureOptions(
                    options,
                    effectiveLinuxPreference,
                    "[RegionSelection] SelectRegionAsync");

                try
                {
                    var nativeSelection = await _platformImpl.SelectRegionAsync(nativeSelectionOptions);
                    if (!nativeSelection.IsEmpty && nativeSelection.Width > 0 && nativeSelection.Height > 0)
                    {
                        return nativeSelection;
                    }

                    if (OperatingSystem.IsLinux() && !canUseLinuxOverlayFallback)
                    {
                        DebugHelper.WriteLine("[RegionSelection] Native selector returned no region and overlay fallback is unavailable.");
                        return nativeSelection;
                    }

                    DebugHelper.WriteLine("[RegionSelection] Native selector returned no region; falling back to XerahS overlay.");
                }
                catch (OperationCanceledException)
                {
                    DebugHelper.WriteLine("[RegionSelection] Native selector cancelled by user.");
                    return SKRectI.Empty;
                }
                catch (Exception ex)
                {
                    if (OperatingSystem.IsLinux() && !canUseLinuxOverlayFallback)
                    {
                        DebugHelper.WriteLine($"[RegionSelection] Native selector failed ({ex.Message}) and overlay fallback is unavailable.");
                        return SKRectI.Empty;
                    }

                    DebugHelper.WriteLine($"[RegionSelection] Native selector failed ({ex.Message}); falling back to XerahS overlay.");
                }
            }

            bool useFastOverlay = options?.UseTransparentOverlay ?? false;
            SKRectI selection = await OverlayRegionCaptureSession.SelectRegionAsync(_platformImpl, options, useFastOverlay);

            if (OperatingSystem.IsLinux())
            {
                _linuxResolver.RecordDecision(_linuxResolver.CreateOverlayDecision(
                    operation: "Region selection",
                    requestedPreference: LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options),
                    outcome: selection.IsEmpty ? "Cancelled" : "Succeeded"));
            }

            return selection;
        }

        public async Task<SKBitmap?> CaptureFullScreenAsync(CaptureOptions? options = null)
        {
            var totalStopwatch = Stopwatch.StartNew();

            var captureStopwatch = Stopwatch.StartNew();
            var result = await _platformImpl.CaptureFullScreenAsync(options);
            captureStopwatch.Stop();

            var resultText = result == null ? "null" : $"{result.Width}x{result.Height}";
            totalStopwatch.Stop();

            return result;
        }

        public Task<SKBitmap?> CaptureActiveWindowAsync(IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureActiveWindowAsync(windowService, options);
        }

        public Task<XerahS.Platform.Abstractions.CursorInfo?> CaptureCursorAsync()
        {
            return _platformImpl.CaptureCursorAsync();
        }

        public async Task<SKBitmap?> CaptureRegionAsync(CaptureOptions? options = null)
        {
            LinuxRegionCaptureCapability? linuxCapability =
                OperatingSystem.IsLinux() ? _linuxResolver.GetCapability(_platformImpl, options) : null;
            var effectiveLinuxPreference = _linuxResolver.ResolveEffectivePreference(options, linuxCapability, _platformImpl);
            bool canUseLinuxOverlayFallback = linuxCapability?.SupportsLegacyOverlayCapture == true;
            bool shouldTryLinuxNativeRegionCapture = LinuxCaptureOptionsResolver.ShouldTryLinuxNativeRegionCapture(effectiveLinuxPreference, linuxCapability);

            if (shouldTryLinuxNativeRegionCapture)
            {
                var nativeCaptureOptions = LinuxCaptureOptionsResolver.NormalizeLinuxNativeCaptureOptions(
                    options,
                    effectiveLinuxPreference,
                    "[RegionCapture] CaptureRegionAsync");

                if (OperatingSystem.IsLinux() &&
                    effectiveLinuxPreference == LinuxInteractiveRegionSelectorPreference.XerahSOverlay &&
                    !canUseLinuxOverlayFallback)
                {
                    DebugHelper.WriteLine("[RegionCapture] XerahS overlay was requested but is unavailable; trying a native selector fallback.");
                }

                try
                {
                    var portalBitmap = await _platformImpl.CaptureRegionAsync(nativeCaptureOptions);
                    if (portalBitmap != null)
                    {
                        DebugHelper.WriteLine("[RegionCapture] Region captured via Linux native selector.");
                        return portalBitmap;
                    }

                    if (!canUseLinuxOverlayFallback)
                    {
                        DebugHelper.WriteLine("[RegionCapture] Platform region capture returned null and overlay fallback is unavailable.");
                        return null;
                    }

                    DebugHelper.WriteLine("[RegionCapture] Platform region capture returned null; falling back to XerahS overlay.");
                }
                catch (OperationCanceledException)
                {
                    DebugHelper.WriteLine("[RegionCapture] Platform region capture cancelled by user.");
                    return null;
                }
                catch (Exception ex)
                {
                    if (!canUseLinuxOverlayFallback)
                    {
                        DebugHelper.WriteLine($"[RegionCapture] Platform region capture failed ({ex.Message}) and overlay fallback is unavailable.");
                        return null;
                    }

                    DebugHelper.WriteLine($"[RegionCapture] Platform region capture failed ({ex.Message}); falling back to overlay.");
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (canUseLinuxOverlayFallback)
                {
                    DebugHelper.WriteLine(
                        $"[RegionCapture] Linux region capture will use the XerahS overlay for selector '{effectiveLinuxPreference}'. Reason={linuxCapability?.Reason ?? "Unknown"}");
                }
                else
                {
                    DebugHelper.WriteLine(
                        $"[RegionCapture] Linux selector '{effectiveLinuxPreference}' is unavailable and overlay fallback is unsupported. Reason={linuxCapability?.Reason ?? "Unknown"}");
                    return null;
                }
            }

            var effectiveOptions = LinuxCaptureOptionsResolver.NormalizeLinuxOverlayCaptureOptions(
                options,
                linuxCapability,
                "[RegionCapture] CaptureRegionAsync");

            DateTime sessionStartUtc = DateTime.UtcNow;
            DebugHelper.WriteLine($"[RegionCapture] Milestone: region capture started (+0 ms)");

            XerahS.Platform.Abstractions.CursorInfo? ghostCursor = null;
            if (effectiveOptions?.ShowCursor == true)
            {
                try
                {
                    ghostCursor = await _platformImpl.CaptureCursorAsync();
                }
                catch
                {
                    // Ignore cursor capture errors
                }
            }

            bool useFastOverlay = effectiveOptions?.UseTransparentOverlay ?? false;
            SKBitmap? fullScreenBitmap = null;
            if (!useFastOverlay)
            {
                try
                {
                    fullScreenBitmap = await _platformImpl.CaptureFullScreenAsync(new CaptureOptions
                    {
                        ShowCursor = false,
                        UseModernCapture = effectiveOptions?.UseModernCapture ?? true,
                        LinuxRegionSelectorPreference = effectiveOptions?.LinuxRegionSelectorPreference ??
                            LinuxInteractiveRegionSelectorPreference.Automatic
                    });
                    if (fullScreenBitmap != null)
                    {
                        DebugHelper.WriteLine($"[RegionCapture] Pre-capture fullScreenBitmap: {fullScreenBitmap.Width}x{fullScreenBitmap.Height}");
                    }
                    else
                    {
                        DebugHelper.WriteLine("[RegionCapture] Pre-capture fullScreenBitmap: null");
                    }
                }
                catch
                {
                    // Ignore - full screen capture is optional for fallback
                }
            }

            DebugHelper.WriteLine($"[RegionCapture] Milestone: region capture UI invoked (+{(DateTime.UtcNow - sessionStartUtc).TotalMilliseconds:F0} ms)");
            OverlayRegionCaptureSession.OverlayRegionCaptureResult overlayResult =
                await OverlayRegionCaptureSession.CaptureRegionAsync(
                    effectiveOptions,
                    sessionStartUtc,
                    useFastOverlay,
                    fullScreenBitmap,
                    ghostCursor);
            SKRectI selection = overlayResult.Selection;
            SKBitmap? annotationLayer = overlayResult.AnnotationLayer;
            XerahS.RegionCapture.Models.PixelPoint annotationMonitorOrigin = overlayResult.AnnotationMonitorOrigin;
            double elapsedMs = (DateTime.UtcNow - sessionStartUtc).TotalMilliseconds;
            DebugHelper.WriteLine($"[RegionCapture] Milestone: region UI returned (+{elapsedMs:F0} ms)");

            if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
            {
                if (OperatingSystem.IsLinux())
                {
                    _linuxResolver.RecordDecision(_linuxResolver.CreateOverlayDecision(
                        operation: "Region capture",
                        requestedPreference: LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options),
                        outcome: "Cancelled"));
                }

                DebugHelper.Flush();
                return null;
            }

            if (OperatingSystem.IsLinux())
            {
                _linuxResolver.RecordDecision(_linuxResolver.CreateOverlayDecision(
                    operation: "Region capture",
                    requestedPreference: LinuxCaptureOptionsResolver.GetLinuxRegionSelectorPreference(options),
                    outcome: "Succeeded"));
            }

            bool showCursor = effectiveOptions?.ShowCursor == true;

            if (effectiveOptions?.CaptureStartDelaySeconds > 0)
            {
                var delayMs = (int)Math.Round(effectiveOptions.CaptureStartDelaySeconds * 1000, MidpointRounding.AwayFromZero);
                var workflowId = string.IsNullOrWhiteSpace(effectiveOptions.WorkflowId) ? "none" : effectiveOptions.WorkflowId;
                var workflowCategory = string.IsNullOrWhiteSpace(effectiveOptions.WorkflowCategory) ? "Unknown" : effectiveOptions.WorkflowCategory;
                TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelaySeconds={effectiveOptions.CaptureStartDelaySeconds:F3}, DelayMs={delayMs}");

                try
                {
                    await Task.Delay(delayMs, effectiveOptions.CaptureStartDelayCancellationToken);
                    TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelayCompleted=true");
                }
                catch (OperationCanceledException)
                {
                    TroubleshootingHelper.Log("CaptureDelay", "REGION", $"WorkflowId={workflowId}, Category={workflowCategory}, DelayCancelled=true");
                    DebugHelper.Flush();
                    return null;
                }
            }

            await Task.Delay(200);
            DebugHelper.WriteLine($"[RegionCapture] Milestone: post-overlay delay done (+{(DateTime.UtcNow - sessionStartUtc).TotalMilliseconds:F0} ms)");

            var captureOptions = new CaptureOptions
            {
                ShowCursor = false,
                UseModernCapture = effectiveOptions?.UseModernCapture ?? true,
                LinuxRegionSelectorPreference = effectiveOptions?.LinuxRegionSelectorPreference ??
                    LinuxInteractiveRegionSelectorPreference.Automatic,
                WorkflowId = effectiveOptions?.WorkflowId,
                WorkflowCategory = effectiveOptions?.WorkflowCategory
            };

            SKBitmap? bitmap = null;
            try
            {
                if (fullScreenBitmap != null)
                {
                    bitmap = CropFromPreCapture(fullScreenBitmap, selection);
                }
            }
            catch
            {
                // Ignore crop errors and fall back to live capture
            }
            finally
            {
                fullScreenBitmap?.Dispose();
            }

            if (bitmap != null)
            {
                DebugHelper.WriteLine($"[RegionCapture] Milestone: bitmap obtained (+{(DateTime.UtcNow - sessionStartUtc).TotalMilliseconds:F0} ms)");
            }

            if (bitmap == null)
            {
                bitmap = await CaptureRectFromSelection(selection, captureOptions, sessionStartUtc);
            }

            // Composite ghost cursor and annotation layer (XIP-0052 §3.2 — delegated)
            if (bitmap != null && ghostCursor?.Image != null && showCursor)
            {
                CaptureImageCompositor.CompositeGhostCursor(bitmap, ghostCursor, selection);
            }

            if (bitmap != null && annotationLayer != null)
            {
                CaptureImageCompositor.CompositeAnnotationLayer(bitmap, annotationLayer, selection, annotationMonitorOrigin);
            }

            DebugHelper.Flush();

            return bitmap;
        }

        public Task<SKBitmap?> CaptureWindowAsync(IntPtr windowHandle, IWindowService windowService, CaptureOptions? options = null)
        {
            return _platformImpl.CaptureWindowAsync(windowHandle, windowService, options);
        }

        // ─── Linux diagnostics (delegated to LinuxRegionSelectorResolver) ─────────

        public LinuxRegionCaptureCapability GetLinuxRegionCaptureCapability(CaptureOptions? options = null)
        {
            return _linuxResolver.GetCapability(_platformImpl, options);
        }

        public LinuxRegionSelectorDiagnostics? GetLinuxRegionSelectorDiagnostics()
        {
            return _linuxResolver.GetDiagnostics(_platformImpl);
        }

        /// <summary>
        /// Kept for existing callers that reference this static helper directly.
        /// </summary>
        internal static LinuxRegionSelectorDiagnostics? MergeLinuxRegionSelectorDiagnostics(
            LinuxRegionSelectorDiagnostics? diagnostics,
            LinuxRegionSelectorRuntimeDecision? runtimeDecision)
        {
            return LinuxRegionSelectorResolver.MergeDiagnostics(diagnostics, runtimeDecision);
        }

        // ─── Private helpers ──────────────────────────────────────────────────────

        private static SKBitmap? CropFromPreCapture(SKBitmap fullScreenBitmap, SKRectI selection)
        {
            var cropRect = selection;
            try
            {
                var virtualBounds = PlatformServices.Screen.GetVirtualScreenBounds();
                DebugHelper.WriteLine($"[RegionCapture] Pre-capture crop: fullBitmap={fullScreenBitmap.Width}x{fullScreenBitmap.Height} virtualBounds=({virtualBounds.X},{virtualBounds.Y},{virtualBounds.Width}x{virtualBounds.Height}) selection=({selection.Left},{selection.Top},{selection.Right},{selection.Bottom})");
                if (!virtualBounds.IsEmpty)
                {
                    var offsetX = virtualBounds.X;
                    var offsetY = virtualBounds.Y;
                    cropRect = new SKRectI(
                        selection.Left - offsetX,
                        selection.Top - offsetY,
                        selection.Right - offsetX,
                        selection.Bottom - offsetY);
                    DebugHelper.WriteLine($"[RegionCapture] Pre-capture crop: offset=({offsetX},{offsetY}) cropRect=({cropRect.Left},{cropRect.Top},{cropRect.Right},{cropRect.Bottom}) size={cropRect.Width}x{cropRect.Height}");
                }
            }
            catch
            {
                // Ignore virtual screen lookup failures
            }

            SKBitmap cropped = ImageHelpers.Crop(fullScreenBitmap, cropRect);
            if (cropped.Width > 0 && cropped.Height > 0)
            {
                return cropped;
            }

            cropped.Dispose();
            return null;
        }

        private async Task<SKBitmap?> CaptureRectFromSelection(SKRectI selection, CaptureOptions captureOptions, DateTime sessionStartUtc)
        {
            DebugHelper.WriteLine($"[RegionCapture] CaptureRectAsync path (no pre-capture). Selection physical: L={selection.Left}, T={selection.Top}, R={selection.Right}, B={selection.Bottom}, Size={selection.Right - selection.Left}x{selection.Bottom - selection.Top}");

            SKRect rectForCapture = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
            try
            {
                var monitors = MonitorEnumerationService.GetAllMonitors();
                DebugHelper.WriteLine($"[RegionCapture] Monitor count={monitors.Count}, Linux={OperatingSystem.IsLinux()}");

                if (monitors.Count > 0)
                {
                    if (OperatingSystem.IsLinux())
                    {
                        rectForCapture = ComputeLinuxLogicalRect(monitors, selection, captureOptions);
                    }
                    else
                    {
                        ComputeNonLinuxVirtualBounds(monitors, captureOptions);
                    }
                }
                else
                {
                    DebugHelper.WriteLine("[RegionCapture] No monitors; rect passed as-is, no VirtualScreenBoundsForCrop");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[RegionCapture] Exception computing virtual bounds / logical rect: {ex.Message}");
            }

            DebugHelper.WriteLine($"[RegionCapture] Milestone: CaptureRectAsync called (+{(DateTime.UtcNow - sessionStartUtc).TotalMilliseconds:F0} ms)");
            DebugHelper.WriteLine($"[RegionCapture] Calling CaptureRectAsync: rect L={rectForCapture.Left:F1}, T={rectForCapture.Top:F1}, R={rectForCapture.Right:F1}, B={rectForCapture.Bottom:F1}, VirtualScreenBoundsForCrop={captureOptions.VirtualScreenBoundsForCrop?.ToString() ?? "null"}");
            var bitmap = await _platformImpl.CaptureRectAsync(rectForCapture, captureOptions);
            DebugHelper.WriteLine($"[RegionCapture] Milestone: CaptureRectAsync returned (+{(DateTime.UtcNow - sessionStartUtc).TotalMilliseconds:F0} ms)");

            return bitmap;
        }

        private static SKRect ComputeLinuxLogicalRect(
            IReadOnlyList<RegionCapture.Models.MonitorInfo> monitors,
            SKRectI selection,
            CaptureOptions captureOptions)
        {
            var ov = monitors[0].OverlayBounds;
            double vLeft = ov.X, vTop = ov.Y, vRight = ov.Right, vBottom = ov.Bottom;
            for (int i = 1; i < monitors.Count; i++)
            {
                var o = monitors[i].OverlayBounds;
                vLeft = Math.Min(vLeft, o.X);
                vTop = Math.Min(vTop, o.Y);
                vRight = Math.Max(vRight, o.Right);
                vBottom = Math.Max(vBottom, o.Bottom);
            }
            captureOptions.VirtualScreenBoundsForCrop = Rectangle.FromLTRB(
                (int)Math.Round(vLeft),
                (int)Math.Round(vTop),
                (int)Math.Round(vRight),
                (int)Math.Round(vBottom));
            DebugHelper.WriteLine($"[RegionCapture] Virtual bounds (logical, OverlayBounds union): L={vLeft:F0}, T={vTop:F0}, R={vRight:F0}, B={vBottom:F0}, Size={vRight - vLeft:F0}x{vBottom - vTop:F0}");

            var pb0 = monitors[0].PhysicalBounds;
            double physVLeft = pb0.X, physVTop = pb0.Y, physVRight = pb0.Right, physVBottom = pb0.Bottom;
            for (int i = 1; i < monitors.Count; i++)
            {
                var pb = monitors[i].PhysicalBounds;
                physVLeft = Math.Min(physVLeft, pb.X);
                physVTop = Math.Min(physVTop, pb.Y);
                physVRight = Math.Max(physVRight, pb.Right);
                physVBottom = Math.Max(physVBottom, pb.Bottom);
            }
            captureOptions.PhysicalVirtualScreenBoundsForCrop = Rectangle.FromLTRB(
                (int)Math.Round(physVLeft),
                (int)Math.Round(physVTop),
                (int)Math.Round(physVRight),
                (int)Math.Round(physVBottom));
            captureOptions.PhysicalRectForCrop = Rectangle.FromLTRB(
                selection.Left, selection.Top, selection.Right, selection.Bottom);
            DebugHelper.WriteLine($"[RegionCapture] Physical virtual bounds: L={physVLeft:F0}, T={physVTop:F0}, R={physVRight:F0}, B={physVBottom:F0}, Size={physVRight - physVLeft:F0}x{physVBottom - physVTop:F0}");
            DebugHelper.WriteLine($"[RegionCapture] PhysicalRectForCrop: L={selection.Left}, T={selection.Top}, R={selection.Right}, B={selection.Bottom}");

            double cx = (selection.Left + selection.Right) / 2.0;
            double cy = (selection.Top + selection.Bottom) / 2.0;
            var center = new XerahS.RegionCapture.Models.PixelPoint((int)cx, (int)cy);
            XerahS.RegionCapture.Models.MonitorInfo? monitorAtCenter = null;
            int monitorIndex = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                if (monitors[i].PhysicalBounds.Contains(center))
                {
                    monitorAtCenter = monitors[i];
                    monitorIndex = i;
                    break;
                }
            }
            monitorAtCenter ??= monitors[0];
            if (monitorIndex < 0) monitorIndex = 0;
            var phys = monitorAtCenter.PhysicalBounds;
            var over = monitorAtCenter.OverlayBounds;
            double s = monitorAtCenter.ScaleFactor;
            DebugHelper.WriteLine($"[RegionCapture] Monitor at selection center: index={monitorIndex}, DeviceName={monitorAtCenter.DeviceName}, ScaleFactor={s:F2}, PhysicalBounds=({phys.X:F0},{phys.Y:F0},{phys.Right:F0},{phys.Bottom:F0}), OverlayBounds=({over.X:F0},{over.Y:F0},{over.Right:F0},{over.Bottom:F0})");

            double logLeft = over.X + (selection.Left - phys.X) / s;
            double logTop = over.Y + (selection.Top - phys.Y) / s;
            double logRight = over.X + (selection.Right - phys.X) / s;
            double logBottom = over.Y + (selection.Bottom - phys.Y) / s;
            DebugHelper.WriteLine($"[RegionCapture] Selection converted to logical: L={logLeft:F1}, T={logTop:F1}, R={logRight:F1}, B={logBottom:F1}, Size={logRight - logLeft:F1}x{logBottom - logTop:F1}");

            return new SKRect((float)logLeft, (float)logTop, (float)logRight, (float)logBottom);
        }

        private static void ComputeNonLinuxVirtualBounds(
            IReadOnlyList<RegionCapture.Models.MonitorInfo> monitors,
            CaptureOptions captureOptions)
        {
            var b = monitors[0].PhysicalBounds;
            double left = b.X, top = b.Y, right = b.Right, bottom = b.Bottom;
            for (int i = 1; i < monitors.Count; i++)
            {
                var m = monitors[i].PhysicalBounds;
                left = Math.Min(left, m.X);
                top = Math.Min(top, m.Y);
                right = Math.Max(right, m.Right);
                bottom = Math.Max(bottom, m.Bottom);
            }
            captureOptions.VirtualScreenBoundsForCrop = Rectangle.FromLTRB(
                (int)Math.Round(left),
                (int)Math.Round(top),
                (int)Math.Round(right),
                (int)Math.Round(bottom));
            DebugHelper.WriteLine($"[RegionCapture] Virtual bounds (physical, PhysicalBounds union): L={left:F0}, T={top:F0}, R={right:F0}, B={bottom:F0}, Size={right - left:F0}x{bottom - top:F0}");
        }
    }
}
