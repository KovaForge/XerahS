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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows
{
    [SupportedOSPlatform("windows")]
    public class WindowsScrollingCaptureService : IScrollingCaptureService
    {
        public bool IsSupported => true;

        public async Task ScrollWindowAsync(IntPtr windowHandle, ScrollMethod method, int amount)
        {
            switch (method)
            {
                case ScrollMethod.MouseWheel:
                    // Save cursor position so the user's cursor isn't permanently hijacked
                    NativeMethods.GetCursorPos(out POINT savedCursor);

                    // Move mouse to center of target window's client area for reliable wheel delivery
                    var clientRect = NativeMethods.GetClientRect(windowHandle);
                    if (clientRect.Width > 0 && clientRect.Height > 0)
                    {
                        POINT centerPoint = new POINT
                        {
                            X = clientRect.Left + clientRect.Width / 2,
                            Y = clientRect.Top + clientRect.Height / 2
                        };
                        NativeMethods.ClientToScreen(windowHandle, ref centerPoint);
                        InputHelpers.SendMouseMove(centerPoint.X, centerPoint.Y);
                        await Task.Delay(50);
                    }
                    // WHEEL_DELTA = 120; negative = scroll down
                    InputHelpers.SendMouseWheel(-120 * amount);

                    // Restore cursor to where the user left it
                    NativeMethods.SetCursorPos(savedCursor.X, savedCursor.Y);
                    break;

                case ScrollMethod.DownArrow:
                    for (int i = 0; i < amount; i++)
                    {
                        InputHelpers.SendKeyPress(VirtualKeyCode.DOWN);
                    }
                    break;

                case ScrollMethod.PageDown:
                    InputHelpers.SendKeyPress(VirtualKeyCode.NEXT);
                    break;

                case ScrollMethod.MouseWheelMessage:
                    // Post WM_MOUSEWHEEL directly to the window without moving the physical cursor.
                    // Works for standard Win32/WPF/WinForms controls. Falls back to MouseWheel
                    // for apps that require real input (e.g. raw-input games).
                    var msgClientRect = NativeMethods.GetClientRect(windowHandle);
                    POINT msgCenter = new POINT
                    {
                        X = msgClientRect.Left + msgClientRect.Width / 2,
                        Y = msgClientRect.Top + msgClientRect.Height / 2
                    };
                    NativeMethods.ClientToScreen(windowHandle, ref msgCenter);

                    // wParam: HIWORD = wheel delta, LOWORD = key state (0)
                    // lParam: HIWORD = Y screen coord, LOWORD = X screen coord
                    int msgWheelDelta = -120 * amount;
                    IntPtr msgWParam = (IntPtr)unchecked((int)((uint)(msgWheelDelta << 16)));
                    IntPtr msgLParam = (IntPtr)unchecked((int)((uint)((msgCenter.Y << 16) | (msgCenter.X & 0xFFFF))));
                    NativeMethods.PostMessage(windowHandle, (uint)WindowsMessages.WM_MOUSEWHEEL, msgWParam, msgLParam);
                    break;

                case ScrollMethod.ScrollMessage:
                    {
                        // WM_VSCROLL sent to the main window only scrolls the window's own
                        // non-client scroll bar. To scroll the main content (which lives in a
                        // child window with WS_VSCROLL), we must send the message to that
                        // child window directly. Enumerate children and find the first one
                        // that has a vertical scroll bar.
                        IntPtr scrollTarget = FindScrollableChildWindow(windowHandle);

                        // Fall back to the main window if no suitable child was found.
                        if (scrollTarget == IntPtr.Zero)
                        {
                            scrollTarget = windowHandle;
                        }

                        for (int i = 0; i < amount; i++)
                        {
                            NativeMethods.SendMessage(
                                scrollTarget,
                                (uint)WindowsMessages.WM_VSCROLL,
                                (IntPtr)ScrollBarCommand.SB_LINEDOWN,
                                IntPtr.Zero);
                        }
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task ScrollToTopAsync(IntPtr windowHandle)
        {
            // Send HOME key to scroll to top
            InputHelpers.SendKeyPress(VirtualKeyCode.HOME);
            await Task.Delay(100);

            // Also send WM_VSCROLL SB_TOP as fallback.
            // Target the scrollable child window (the main content area) rather than
            // the main window itself, so SB_TOP reaches the right scroll bar.
            IntPtr scrollTarget = FindScrollableChildWindow(windowHandle);
            if (scrollTarget == IntPtr.Zero)
            {
                scrollTarget = windowHandle;
            }

            NativeMethods.SendMessage(
                scrollTarget,
                (uint)WindowsMessages.WM_VSCROLL,
                (IntPtr)ScrollBarCommand.SB_TOP,
                IntPtr.Zero);
        }

        public ScrollBarInfo? GetScrollBarInfo(IntPtr windowHandle)
        {
            SCROLLINFO scrollInfo = new SCROLLINFO();
            scrollInfo.cbSize = (uint)Marshal.SizeOf<SCROLLINFO>();
            scrollInfo.fMask = ScrollInfoMask.SIF_ALL;

            if (!NativeMethods.GetScrollInfo(windowHandle, (int)ScrollBarOrientation.SB_VERT, ref scrollInfo))
            {
                return null;
            }

            return new ScrollBarInfo(
                Position: scrollInfo.nPos,
                MinRange: scrollInfo.nMin,
                MaxRange: scrollInfo.nMax,
                PageSize: (int)scrollInfo.nPage);
        }

        /// <summary>
        /// Finds the first direct child window that has a vertical scroll bar (WS_VSCROLL).
        /// This is the main content scrollable area of a window, as opposed to the
        /// non-client scroll bar attached to the main window itself.
        /// </summary>
        private static IntPtr FindScrollableChildWindow(IntPtr parentWindow)
        {
            IntPtr found = IntPtr.Zero;

            NativeMethods.EnumChildWindows(parentWindow, (hWnd, _) =>
            {
                if (found != IntPtr.Zero)
                {
                    return false; // Already found, stop enumerating
                }

                // Check if this child has the WS_VSCROLL style
                var style = (WindowStyles)NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_STYLE);
                if (style.HasFlag(WindowStyles.WS_VSCROLL))
                {
                    found = hWnd;
                    return false; // Stop enumeration
                }

                return true; // Continue
            }, IntPtr.Zero);

            return found;
        }
    }
}
