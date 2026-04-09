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

using System.Drawing;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Detection;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal static class WaylandWindowPointQueryHelperFactory
{
    public static IWaylandWindowPointQueryHelper Create()
    {
        bool isWayland = string.Equals(
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            "wayland",
            StringComparison.OrdinalIgnoreCase);

        if (!isWayland)
        {
            return new UnsupportedWaylandWindowPointQueryHelper(
                "Wayland session is not active.");
        }

        string? desktop = DesktopEnvironmentDetector.Detect();
        string compositor = CompositorDetector.Detect(isWayland, desktop);

        return compositor switch
        {
            "HYPRLAND" => new HyprlandWindowPointQueryHelper(),
            "SWAY" => new SwayWindowPointQueryHelper(),
            _ when string.Equals(desktop, "GNOME", StringComparison.Ordinal) => new GnomeShellWindowPointQueryHelper(),
            _ when string.Equals(desktop, "KDE", StringComparison.Ordinal) => new KdeKdotoolWindowPointQueryHelper(),
            _ => new UnsupportedWaylandWindowPointQueryHelper(
                $"Wayland session: no compositor helper is available for '{desktop ?? compositor}'.")
        };
    }

    private sealed class UnsupportedWaylandWindowPointQueryHelper(string message) : IWaylandWindowPointQueryHelper
    {
        public WindowPointQueryCapability Capability { get; } =
            new(WindowPointQuerySupportLevel.Unsupported, message);

        public WindowInfo? GetWindowAtPoint(Point logicalPoint)
        {
            return null;
        }
    }
}
