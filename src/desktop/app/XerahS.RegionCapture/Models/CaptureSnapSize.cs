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
namespace XerahS.RegionCapture.Models;

/// <summary>
/// A named region-size preset used while dragging a capture rectangle.
/// </summary>
public readonly record struct CaptureSnapSize(int Width, int Height)
{
    public static IReadOnlyList<CaptureSnapSize> DefaultPresets { get; } =
    [
        new(426, 240),
        new(640, 360),
        new(854, 480),
        new(1280, 720),
        new(1920, 1080)
    ];

    public override string ToString() => $"{Width}x{Height}";
}
