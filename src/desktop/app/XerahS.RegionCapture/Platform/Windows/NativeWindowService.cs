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
#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using XerahS.RegionCapture.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Graphics.Dwm;

namespace XerahS.RegionCapture.Platform.Windows;

/// <summary>
/// Native Windows window enumeration and detection using DWM APIs.
/// Uses DWMWA_EXTENDED_FRAME_BOUNDS to get the *visual* bounds of windows
/// (excluding invisible shadow borders) for accurate snapping.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeWindowService
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_EX_APPWINDOW = 0x00040000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_DISABLED = 0x08000000;

    // Cache for our own overlay windows to exclude them
    private static readonly HashSet<nint> ExcludedHandles = [];
    private static readonly HashSet<string> IgnoredWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "Button",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow"
    };

    /// <summary>
    /// Registers a window handle to be excluded from enumeration (our overlay windows).
    /// </summary>
    public static void ExcludeHandle(nint handle) => ExcludedHandles.Add(handle);

    /// <summary>
    /// Removes a window handle from the exclusion list.
    /// </summary>
    public static void RemoveExcludedHandle(nint handle) => ExcludedHandles.Remove(handle);

    /// <summary>
    /// Gets the window at the specified physical point.
    /// Uses Z-order from EnumWindows (topmost first) for correct layering.
    /// </summary>
    public static WindowInfo? GetWindowAtPoint(PixelPoint point)
    {
        var windows = EnumerateVisibleWindows();

        // EnumWindows returns windows in Z-order (topmost first)
        // so the first window containing the point is the topmost one
        foreach (var window in windows)
        {
            if (window.SnapBounds.Contains(point))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates all visible windows with their visual bounds.
    /// Windows are returned in Z-order (topmost first) as provided by EnumWindows.
    /// </summary>
    public static IReadOnlyList<WindowInfo> EnumerateVisibleWindows()
    {
        var windows = new List<WindowInfo>();
        var zOrder = 0;

        PInvoke.EnumWindows((hWnd, lParam) =>
        {
            // Skip our own overlay windows
            if (ExcludedHandles.Contains((nint)hWnd))
                return true;

            var info = GetWindowInfo(hWnd, zOrder);
            if (info is not null)
            {
                windows.Add(info);
                zOrder++;
            }

            return true;
        }, 0);

        return windows;
    }

    internal static bool ShouldIncludeWindowForCapture(
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

        if ((style & (nint)WS_VISIBLE) == 0 || (style & (nint)WS_DISABLED) != 0)
            return false;

        if ((exStyle & (nint)WS_EX_TOOLWINDOW) != 0)
            return false;

        if ((exStyle & (nint)WS_EX_NOACTIVATE) != 0 && (exStyle & (nint)WS_EX_APPWINDOW) == 0)
            return false;

        if (string.IsNullOrWhiteSpace(title))
            return false;

        return !IgnoredWindowClasses.Contains(className);
    }

    private static WindowInfo? GetWindowInfo(HWND hWnd, int zOrder)
    {
        bool isVisible = PInvoke.IsWindowVisible(hWnd);
        bool isMinimized = PInvoke.IsIconic(hWnd);
        bool isCloaked = IsWindowCloaked(hWnd);
        var style = GetWindowLongAuto(hWnd, GWL_STYLE);
        var exStyle = GetWindowLongAuto(hWnd, GWL_EXSTYLE);
        string title = GetWindowTitle(hWnd);
        string className = GetWindowClassName(hWnd);

        if (!ShouldIncludeWindowForCapture(isVisible, isMinimized, isCloaked, title, className, style, exStyle))
            return null;

        // Get standard window rect
        if (!PInvoke.GetWindowRect(hWnd, out var windowRect))
            return null;

        var bounds = new PixelRect(
            windowRect.X,
            windowRect.Y,
            windowRect.Width,
            windowRect.Height);

        if (bounds.Width <= 1 || bounds.Height <= 1)
            return null;

        // Get visual bounds using DWM (excludes shadow/invisible borders)
        var visualBounds = GetDwmFrameBounds(hWnd) ?? bounds;
        if (visualBounds.Width <= 1 || visualBounds.Height <= 1)
            return null;

        return new WindowInfo(
            Handle: (nint)hWnd,
            Title: title,
            ClassName: className,
            Bounds: bounds,
            VisualBounds: visualBounds,
            IsMinimized: PInvoke.IsIconic(hWnd),
            ZOrder: zOrder);
    }

    private static unsafe string GetWindowTitle(HWND hWnd)
    {
        int titleLength = PInvoke.GetWindowTextLength(hWnd);
        if (titleLength <= 0)
            return string.Empty;

        var titleBuilder = new char[titleLength + 1];
        fixed (char* pTitle = titleBuilder)
        {
            PInvoke.GetWindowText(hWnd, pTitle, titleLength + 1);
            return new string(pTitle);
        }
    }

    private static unsafe string GetWindowClassName(HWND hWnd)
    {
        var classBuilder = new char[256];
        fixed (char* pClass = classBuilder)
        {
            PInvoke.GetClassName(hWnd, pClass, 256);
            return new string(pClass);
        }
    }

    private static unsafe bool IsWindowCloaked(HWND hWnd)
    {
        int cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(
            hWnd,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked,
            (uint)sizeof(int));

        return !hr.Failed && cloaked != 0;
    }

    private static PixelRect? GetDwmFrameBounds(HWND hWnd)
    {
        var rect = new RECT();

        unsafe
        {
            var hr = PInvoke.DwmGetWindowAttribute(
                hWnd,
                DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                &rect,
                (uint)sizeof(RECT));

            if (hr.Failed)
                return null;
        }

        return new PixelRect(
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height);
    }

    // GetWindowLongPtr isn't available on 32-bit via CsWin32, so we use DllImport
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(HWND hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(HWND hWnd, int nIndex);

    private static nint GetWindowLongAuto(HWND hWnd, int index)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, index);
        }
        else
        {
            return GetWindowLong32(hWnd, index);
        }
    }
}
#endif

