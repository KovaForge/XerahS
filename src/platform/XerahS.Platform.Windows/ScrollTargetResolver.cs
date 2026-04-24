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

namespace XerahS.Platform.Windows;

internal static class ScrollTargetResolver
{
    internal static IntPtr Resolve(IntPtr windowHandle, IEnumerable<ScrollTargetCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        ScrollTargetCandidate? bestNonScrollbar = null;
        ScrollTargetCandidate? bestFallback = null;

        foreach (ScrollTargetCandidate candidate in candidates)
        {
            if (candidate.Handle == IntPtr.Zero ||
                !candidate.HasVerticalScrollStyle ||
                !candidate.IsVisible ||
                candidate.ClientWidth <= 0 ||
                candidate.ClientHeight <= 0)
            {
                continue;
            }

            if (bestFallback is null || candidate.ClientArea > bestFallback.Value.ClientArea)
            {
                bestFallback = candidate;
            }

            if (candidate.IsScrollBarControl)
            {
                continue;
            }

            if (bestNonScrollbar is null || candidate.ClientArea > bestNonScrollbar.Value.ClientArea)
            {
                bestNonScrollbar = candidate;
            }
        }

        if (bestNonScrollbar is { } nonScrollbar)
        {
            return nonScrollbar.Handle;
        }

        if (bestFallback is { } fallback)
        {
            return fallback.Handle;
        }

        return windowHandle;
    }
}

internal readonly record struct ScrollTargetCandidate(
    IntPtr Handle,
    bool HasVerticalScrollStyle,
    bool IsVisible,
    int ClientWidth,
    int ClientHeight,
    string ClassName)
{
    public int ClientArea => ClientWidth * ClientHeight;

    public bool IsScrollBarControl =>
        string.Equals(ClassName, "ScrollBar", StringComparison.OrdinalIgnoreCase);
}
