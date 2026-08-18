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
using XerahS.RegionCapture.Models;

namespace XerahS.RegionCapture.Services;

/// <summary>
/// ShareX-style size-preset snapping for a drag rectangle.
/// </summary>
internal static class SelectionSnapHelper
{
    public static PixelPoint SnapEndPoint(
        PixelPoint start,
        PixelPoint current,
        IReadOnlyList<CaptureSnapSize> snapSizes,
        double snapDistance)
    {
        if (snapSizes.Count == 0 || snapDistance <= 0)
            return current;

        double width = Math.Abs(current.X - start.X);
        double height = Math.Abs(current.Y - start.Y);
        CaptureSnapSize? best = null;
        double bestDistance = snapDistance;

        foreach (var size in snapSizes)
        {
            double dx = width - size.Width;
            double dy = height - size.Height;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance > 0 && distance < bestDistance)
            {
                bestDistance = distance;
                best = size;
            }
        }

        return best is { } snap ? CalculateNewPosition(start, current, snap) : current;
    }

    internal static PixelPoint CalculateNewPosition(PixelPoint start, PixelPoint current, CaptureSnapSize size)
    {
        double signedWidth = current.X >= start.X ? size.Width - 1 : -(size.Width - 1);
        double signedHeight = current.Y >= start.Y ? size.Height - 1 : -(size.Height - 1);
        return new PixelPoint(start.X + signedWidth, start.Y + signedHeight);
    }

    public static SelectionHandle HitTest(PixelRect selection, PixelPoint point, double handleSize)
    {
        if (selection.IsEmpty)
            return SelectionHandle.None;

        double half = Math.Max(4, handleSize) / 2;
        var normalized = selection.Normalize();

        if (ContainsHandle(normalized.TopLeft, point, half))
            return SelectionHandle.TopLeft;
        if (ContainsHandle(normalized.TopRight, point, half))
            return SelectionHandle.TopRight;
        if (ContainsHandle(normalized.BottomLeft, point, half))
            return SelectionHandle.BottomLeft;
        if (ContainsHandle(normalized.BottomRight, point, half))
            return SelectionHandle.BottomRight;
        if (ContainsHandle(new PixelPoint(normalized.Center.X, normalized.Top), point, half))
            return SelectionHandle.Top;
        if (ContainsHandle(new PixelPoint(normalized.Center.X, normalized.Bottom), point, half))
            return SelectionHandle.Bottom;
        if (ContainsHandle(new PixelPoint(normalized.Left, normalized.Center.Y), point, half))
            return SelectionHandle.Left;
        if (ContainsHandle(new PixelPoint(normalized.Right, normalized.Center.Y), point, half))
            return SelectionHandle.Right;

        return normalized.Contains(point) ? SelectionHandle.Body : SelectionHandle.None;
    }

    private static bool ContainsHandle(PixelPoint handleCenter, PixelPoint point, double half) =>
        Math.Abs(point.X - handleCenter.X) <= half &&
        Math.Abs(point.Y - handleCenter.Y) <= half;
}
