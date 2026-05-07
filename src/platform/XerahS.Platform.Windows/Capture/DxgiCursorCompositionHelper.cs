using System.Drawing;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace XerahS.Platform.Windows.Capture;

internal readonly record struct CursorOverlayPlacement(bool ShouldDraw, Point DrawOffset);

internal static class DxgiCursorCompositionHelper
{
    private const int DefaultCursorExtent = 32;

    public static CursorOverlayPlacement CreatePlacement(
        bool includeCursor,
        bool cursorVisible,
        Point cursorPosition,
        Point hotspot,
        Size cursorSize,
        PhysicalRectangle captureRegion)
    {
        if (!includeCursor || !cursorVisible || captureRegion.IsEmpty)
            return default;

        int width = cursorSize.Width > 0 ? cursorSize.Width : DefaultCursorExtent;
        int height = cursorSize.Height > 0 ? cursorSize.Height : DefaultCursorExtent;

        int drawX = cursorPosition.X - hotspot.X - captureRegion.X;
        int drawY = cursorPosition.Y - hotspot.Y - captureRegion.Y;

        if (drawX >= captureRegion.Width || drawY >= captureRegion.Height ||
            drawX + width <= 0 || drawY + height <= 0)
        {
            return default;
        }

        return new CursorOverlayPlacement(true, new Point(drawX, drawY));
    }
}
