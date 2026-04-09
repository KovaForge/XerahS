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

using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using XerahS.RegionCapture.Models;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;
using PixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using AvPixelRect = Avalonia.PixelRect;

namespace XerahS.RegionCapture.UI;

public partial class OverlayWindow
{
    #region Capture Completion

    // Stores the selection result when annotations exist, for use with ENTER key
    private RegionSelectionResult? _pendingSelectionResult;

    /// <summary>
    /// XIP-0023: Confirms capture with annotations using ENTER key.
    /// Uses the pending selection result if available, otherwise captures full monitor.
    /// </summary>
    private void ConfirmCaptureWithAnnotations()
    {
        // Save annotation options before completing
        _viewModel.SaveOptions();

        // Use the pending selection if user has made a region selection
        if (_pendingSelectionResult.HasValue)
        {
            var result = CreateResultWithAnnotations(_pendingSelectionResult.Value);
            _completionSource.TrySetResult(result);
            return;
        }

        // Fallback: Get the full monitor bounds if no selection was made
        var bounds = new PixelRect(0, 0, (int)_monitor.PhysicalBounds.Width, (int)_monitor.PhysicalBounds.Height);
        var cursorPos = new PixelPoint(bounds.Width / 2, bounds.Height / 2);
        var result2 = CreateResultWithAnnotations(new RegionSelectionResult(bounds, cursorPos));
        _completionSource.TrySetResult(result2);
    }

    private void OnRegionSelected(RegionSelectionResult result)
    {
        // If annotations have been drawn, don't auto-complete on region selection
        // User must press ENTER to confirm capture with annotations
        if (_viewModel.HasAnnotations || (_annotationCanvas?.Children.Count ?? 0) > 0)
        {
            // Store the selection result for later use when ENTER is pressed
            _pendingSelectionResult = result;

            // Update capture control to show the reminder
            _captureControl.HasPendingSelection = true;
            _captureControl.HasAnnotations = true;
            _captureControl.InvalidateVisual();
            return;
        }

        // Save annotation options before completing
        _viewModel.SaveOptions();

        _completionSource.TrySetResult(result);
    }

    /// <summary>
    /// Creates a RegionSelectionResult with the annotation layer rendered.
    /// </summary>
    private RegionSelectionResult CreateResultWithAnnotations(RegionSelectionResult baseResult)
    {
        // If no annotations, return the base result
        if (!_viewModel.HasAnnotations && (_annotationCanvas?.Children.Count ?? 0) == 0)
        {
            return baseResult;
        }

        // Render annotations to a transparent bitmap
        var annotationLayer = RenderAnnotationLayer();

        // Pass the monitor origin so the compositing code can adjust coordinates
        // (selection is in absolute screen coords, but annotation layer is monitor-relative)
        var monitorOrigin = new PixelPoint(
            (int)_monitor.PhysicalBounds.X,
            (int)_monitor.PhysicalBounds.Y);

        return new RegionSelectionResult(baseResult.Region, baseResult.CursorPosition, annotationLayer, monitorOrigin);
    }

    /// <summary>
    /// Renders all annotations to a transparent SKBitmap sized to the full monitor.
    /// The annotation layer can then be composited onto the captured image.
    /// </summary>
    private SKBitmap? RenderAnnotationLayer()
    {
        if (_annotationCanvas == null || _annotationCanvas.Children.Count == 0)
        {
            return null;
        }

        // Hide inline TextBox during capture so it doesn't render as a raw control
        bool textBoxWasVisible = _inlineTextBox?.IsVisible ?? false;
        if (_inlineTextBox != null) _inlineTextBox.IsVisible = false;

        try
        {
            // Physical pixel dimensions of the full monitor
            int width = (int)_monitor.PhysicalBounds.Width;
            int height = (int)_monitor.PhysicalBounds.Height;

            // Logical dimensions for layout (annotations are in logical coordinates)
            double logicalWidth = _monitor.PhysicalBounds.Width / _monitor.ScaleFactor;
            double logicalHeight = _monitor.PhysicalBounds.Height / _monitor.ScaleFactor;

            // Only force layout if the canvas isn't already at the expected size
            if (Math.Abs(_annotationCanvas.Bounds.Width - logicalWidth) > 1 ||
                Math.Abs(_annotationCanvas.Bounds.Height - logicalHeight) > 1)
            {
                _annotationCanvas.Measure(new Size(logicalWidth, logicalHeight));
                _annotationCanvas.Arrange(new Rect(0, 0, logicalWidth, logicalHeight));
            }

            // Render the Avalonia visual tree to a bitmap at physical resolution
            var dpi = 96.0 * _monitor.ScaleFactor;
            using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(dpi, dpi));
            rtb.Render(_annotationCanvas);

            // Direct pixel copy from Avalonia RenderTargetBitmap to SKBitmap (avoids PNG encode/decode)
            var skBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var pixmap = skBitmap.PeekPixels();
            int rowBytes = skBitmap.Info.RowBytes;
            rtb.CopyPixels(new AvPixelRect(0, 0, width, height), pixmap.GetPixels(), rowBytes * height, rowBytes);

            return skBitmap;
        }
        finally
        {
            if (_inlineTextBox != null) _inlineTextBox.IsVisible = textBoxWasVisible;
        }
    }

    private void OnCancelled()
    {
        // Save annotation options even when cancelled (user may have changed settings)
        _viewModel.SaveOptions();

        _completionSource.TrySetResult(null);
    }

    #endregion
}
