using ShareX.Avalonia.Platform.Abstractions.Capture;
using Vortice.Mathematics;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiRotationHelper
{
    public static Box CreateSourceBox(
        PhysicalRectangle desktopLocalRegion,
        int rotationDegrees,
        int sourceWidth,
        int sourceHeight)
    {
        if (desktopLocalRegion.IsEmpty)
            throw new ArgumentException("Region must not be empty.", nameof(desktopLocalRegion));

        if (sourceWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));

        if (sourceHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceHeight));

        return NormalizeRotation(rotationDegrees) switch
        {
            90 => new Box(
                desktopLocalRegion.Y,
                sourceHeight - (desktopLocalRegion.X + desktopLocalRegion.Width),
                0,
                desktopLocalRegion.Y + desktopLocalRegion.Height,
                sourceHeight - desktopLocalRegion.X,
                1),
            180 => new Box(
                sourceWidth - (desktopLocalRegion.X + desktopLocalRegion.Width),
                sourceHeight - (desktopLocalRegion.Y + desktopLocalRegion.Height),
                0,
                sourceWidth - desktopLocalRegion.X,
                sourceHeight - desktopLocalRegion.Y,
                1),
            270 => new Box(
                sourceWidth - (desktopLocalRegion.Y + desktopLocalRegion.Height),
                desktopLocalRegion.X,
                0,
                sourceWidth - desktopLocalRegion.Y,
                desktopLocalRegion.X + desktopLocalRegion.Width,
                1),
            _ => new Box(
                desktopLocalRegion.X,
                desktopLocalRegion.Y,
                0,
                desktopLocalRegion.X + desktopLocalRegion.Width,
                desktopLocalRegion.Y + desktopLocalRegion.Height,
                1)
        };
    }

    public static int GetSourceWidth(Box sourceBox) => sourceBox.Right - sourceBox.Left;

    public static int GetSourceHeight(Box sourceBox) => sourceBox.Bottom - sourceBox.Top;

    private static int NormalizeRotation(int rotationDegrees) => rotationDegrees switch
    {
        90 => 90,
        180 => 180,
        270 => 270,
        _ => 0
    };
}
