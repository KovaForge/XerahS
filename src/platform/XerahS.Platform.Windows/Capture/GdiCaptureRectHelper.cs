#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Drawing;
using SkiaSharp;

namespace XerahS.Platform.Windows.Capture;

internal static class GdiCaptureRectHelper
{
    public static bool TryCreateCaptureRect(SKRect rect, Rectangle screenBounds, out Rectangle captureRect)
    {
        captureRect = default;

        if (!IsFinite(rect.Left) || !IsFinite(rect.Top) || !IsFinite(rect.Right) || !IsFinite(rect.Bottom) ||
            screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return false;
        }

        double left = Math.Floor(rect.Left);
        double top = Math.Floor(rect.Top);
        double right = Math.Ceiling(rect.Right);
        double bottom = Math.Ceiling(rect.Bottom);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        double screenLeft = screenBounds.Left;
        double screenTop = screenBounds.Top;
        double screenRight = screenLeft + screenBounds.Width;
        double screenBottom = screenTop + screenBounds.Height;

        left = Math.Clamp(left, screenLeft, screenRight);
        top = Math.Clamp(top, screenTop, screenBottom);
        right = Math.Clamp(right, screenLeft, screenRight);
        bottom = Math.Clamp(bottom, screenTop, screenBottom);

        if (right <= left || bottom <= top ||
            left < int.MinValue || top < int.MinValue || right > int.MaxValue || bottom > int.MaxValue)
        {
            return false;
        }

        int x = (int)left;
        int y = (int)top;
        int width = (int)(right - left);
        int height = (int)(bottom - top);

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        captureRect = new Rectangle(x, y, width, height);
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
