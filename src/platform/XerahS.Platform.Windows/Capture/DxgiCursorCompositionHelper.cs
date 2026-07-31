using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

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

    public static PhysicalRectangle CreateCaptureRegion(int left, int top, int right, int bottom)
    {
        int width = right - left;
        int height = bottom - top;

        if (width <= 0 || height <= 0)
            return default;

        return new PhysicalRectangle(left, top, width, height);
    }

    [SupportedOSPlatform("windows")]
    public static bool TryCompositeCursor(
        SKBitmap bitmap,
        bool cursorVisible,
        Point cursorPosition,
        Point hotspot,
        Size cursorSize,
        PhysicalRectangle captureRegion,
        Action<IntPtr, Point> drawCursor)
    {
        var placement = CreatePlacement(
            includeCursor: true,
            cursorVisible,
            cursorPosition,
            hotspot,
            cursorSize,
            captureRegion);

        if (!placement.ShouldDraw)
            return false;

        using var overlay = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(overlay))
        {
            graphics.Clear(Color.Transparent);
            IntPtr hdc = graphics.GetHdc();
            try
            {
                drawCursor(hdc, new Point(captureRegion.X, captureRegion.Y));
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        using var stream = new MemoryStream();
        overlay.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        using var cursorBitmap = SKBitmap.Decode(stream);
        if (cursorBitmap == null)
            return false;

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };
        canvas.DrawBitmap(cursorBitmap, 0, 0, SKSamplingOptions.Default, paint);

        return true;
    }
}
