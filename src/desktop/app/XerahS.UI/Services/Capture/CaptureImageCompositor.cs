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

using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services.Capture;

/// <summary>
/// Composites ghost cursors and annotation layers onto captured bitmaps.
/// Extracted from <see cref="ScreenCaptureService"/> (XIP-0052 §3.2).
/// </summary>
public static class CaptureImageCompositor
{
    /// <summary>
    /// Draws the ghost cursor (captured at workflow start) onto the bitmap
    /// at a position relative to the captured region.
    /// </summary>
    public static void CompositeGhostCursor(SKBitmap bitmap, CursorInfo ghostCursor, SKRectI selection)
    {
        try
        {
            int cursorX = ghostCursor.Position.X - selection.Left - ghostCursor.Hotspot.X;
            int cursorY = ghostCursor.Position.Y - selection.Top - ghostCursor.Hotspot.Y;

            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };
            canvas.DrawBitmap(ghostCursor.Image, cursorX, cursorY, paint);
        }
        catch
        {
            // Ignore cursor drawing errors
        }
    }

    /// <summary>
    /// Composites an annotation layer (drawn during region capture) onto the bitmap.
    /// The annotation layer is monitor-sized; the selection is in absolute screen coords.
    /// </summary>
    public static void CompositeAnnotationLayer(
        SKBitmap bitmap,
        SKBitmap annotationLayer,
        SKRectI selection,
        RegionCapture.Models.PixelPoint annotationMonitorOrigin)
    {
        try
        {
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };

            var srcRect = new SKRect(
                selection.Left - (float)annotationMonitorOrigin.X,
                selection.Top - (float)annotationMonitorOrigin.Y,
                selection.Right - (float)annotationMonitorOrigin.X,
                selection.Bottom - (float)annotationMonitorOrigin.Y);
            var dstRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);

            canvas.DrawBitmap(annotationLayer, srcRect, dstRect, paint);
        }
        catch
        {
            // Ignore annotation compositing errors
        }
        finally
        {
            annotationLayer.Dispose();
        }
    }
}
