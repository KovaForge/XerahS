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

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class DesktopEnvironmentDetector
{
    public static string? Detect()
    {
        foreach (string hint in EnumerateHints())
        {
            string? normalized = NormalizeHint(hint);
            if (!string.IsNullOrEmpty(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    internal static string? NormalizeHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return null;
        }

        foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = token.ToUpperInvariant();

            if (normalized.Contains("GNOME") ||
                normalized.Contains("UBUNTU") ||
                normalized.Contains("UNITY") ||
                normalized.Contains("BUDGIE") ||
                normalized.Contains("PANTHEON"))
            {
                return "GNOME";
            }

            if (normalized.Contains("KDE") || normalized.Contains("PLASMA"))
            {
                return "KDE";
            }

            if (normalized.Contains("HYPRLAND"))
            {
                return "HYPRLAND";
            }

            if (normalized.Contains("SWAY"))
            {
                return "SWAY";
            }

            if (normalized.Contains("XFCE"))
            {
                return "XFCE";
            }

            if (normalized.Contains("MATE"))
            {
                return "MATE";
            }

            if (normalized.Contains("CINNAMON"))
            {
                return "CINNAMON";
            }

            if (normalized.Contains("LXQT"))
            {
                return "LXQT";
            }

            if (normalized.Contains("LXDE"))
            {
                return "LXDE";
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateHints()
    {
        string? currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(currentDesktop))
        {
            yield return currentDesktop;
        }

        string? sessionDesktop = Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP");
        if (!string.IsNullOrWhiteSpace(sessionDesktop))
        {
            yield return sessionDesktop;
        }

        string? desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        if (!string.IsNullOrWhiteSpace(desktopSession))
        {
            yield return desktopSession;
        }
    }
}
