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

internal static class DxgiCropRectHelper
{
    public static bool TryCreateCropRect(SKRect rect, Rectangle virtualBounds, int bitmapWidth, int bitmapHeight, out SKRectI cropRect)
    {
        cropRect = default;

        if (bitmapWidth <= 0 || bitmapHeight <= 0 ||
            !IsFinite(rect.Left) || !IsFinite(rect.Top) || !IsFinite(rect.Right) || !IsFinite(rect.Bottom))
        {
            return false;
        }

        double left = Math.Floor(rect.Left) - virtualBounds.X;
        double top = Math.Floor(rect.Top) - virtualBounds.Y;
        double right = Math.Ceiling(rect.Right) - virtualBounds.X;
        double bottom = Math.Ceiling(rect.Bottom) - virtualBounds.Y;

        if (right <= left || bottom <= top)
        {
            return false;
        }

        left = Math.Clamp(left, 0, bitmapWidth);
        top = Math.Clamp(top, 0, bitmapHeight);
        right = Math.Clamp(right, 0, bitmapWidth);
        bottom = Math.Clamp(bottom, 0, bitmapHeight);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        cropRect = new SKRectI((int)left, (int)top, (int)right, (int)bottom);
        return cropRect.Width > 0 && cropRect.Height > 0;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
