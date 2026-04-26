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

namespace XerahS.RegionCapture.Platform;

internal static class NativeWindowCaptureFilter
{
    private const uint WsVisible = 0x10000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExAppWindow = 0x00040000;
    private const uint WsDisabled = 0x08000000;

    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "Button",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow"
    };

    public static bool ShouldIncludeWindowForCapture(
        bool isVisible,
        bool isMinimized,
        bool isCloaked,
        string title,
        string className,
        nint style,
        nint exStyle)
    {
        if (!isVisible || isMinimized || isCloaked)
            return false;

        if ((style & (nint)WsVisible) == 0 || (style & (nint)WsDisabled) != 0)
            return false;

        if ((exStyle & (nint)WsExToolWindow) != 0)
            return false;

        if ((exStyle & (nint)WsExNoActivate) != 0 && (exStyle & (nint)WsExAppWindow) == 0)
            return false;

        if (string.IsNullOrWhiteSpace(title))
            return false;

        return !IgnoredWindowClasses.Contains(className);
    }
}
