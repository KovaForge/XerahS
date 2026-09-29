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

namespace XerahS.RegionCapture.UI;

/// <summary>
/// Shared layout and pixel-count rules for the region-capture magnifier.
/// Pixel count stays odd so a single source pixel sits in the center cell.
/// </summary>
public static class MagnifierLayout
{
    public const double MagnifierSize = 150;
    public const double OuterSize = 152;
    public const double PointerOffset = 18;
    public const int MinimumPhysicalPixelSize = 6;
    public const int PixelCountMinimum = 3;
    public const int PixelCountMaximum = 35;
    public const int DefaultPixelCount = 15;

    public static int NormalizePixelCount(int requestedCount, double renderScale)
    {
        if (!double.IsFinite(renderScale) || renderScale <= 0)
        {
            renderScale = 1;
        }

        int sizeLimitedMaximum = (int)Math.Floor(MagnifierSize * renderScale / MinimumPhysicalPixelSize);
        int maximum = Math.Clamp(sizeLimitedMaximum, PixelCountMinimum, PixelCountMaximum);
        if ((maximum & 1) == 0)
        {
            maximum--;
        }

        int count = Math.Clamp(requestedCount, PixelCountMinimum, maximum);
        if ((count & 1) == 0)
        {
            count = count < maximum ? count + 1 : count - 1;
        }

        return count;
    }

    public static int PixelCountFromWheel(int currentCount, double wheelDeltaY, double renderScale)
    {
        int delta = wheelDeltaY > 0 ? -2 : 2;
        return NormalizePixelCount(currentCount + delta, renderScale);
    }
}
