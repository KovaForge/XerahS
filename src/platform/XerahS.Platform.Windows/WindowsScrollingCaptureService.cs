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

        public async Task ScrollWindowAsync(IntPtr windowHandle, ScrollMethod method, int amount, System.Drawing.Point? targetPoint = null)
        {
            switch (method)
            {
                case ScrollMethod.MouseWheel:
                    // Save cursor position so the user's cursor isn't permanently hijacked
                    NativeMethods.GetCursorPos(out POINT savedCursor);

                    POINT? wheelPoint = GetWheelPoint(windowHandle, targetPoint);
                    if (wheelPoint.HasValue)
                    {
                        InputHelpers.SendMouseMove(wheelPoint.Value.X, wheelPoint.Value.Y);
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
                    POINT? msgPoint = GetWheelPoint(windowHandle, targetPoint);
                    if (!msgPoint.HasValue)
                    {
                        break;
                    }

                    POINT messagePoint = msgPoint.Value;
                    IntPtr messageTarget = ResolveWheelMessageTarget(windowHandle, messagePoint);

                    // wParam: HIWORD = wheel delta, LOWORD = key state (0)
                    // lParam: HIWORD = Y screen coord, LOWORD = X screen coord
                    int msgWheelDelta = -120 * amount;
                    IntPtr msgWParam = (IntPtr)unchecked((int)((uint)(msgWheelDelta << 16)));
                    IntPtr msgLParam = (IntPtr)unchecked((int)((uint)((messagePoint.Y << 16) | (messagePoint.X & 0xFFFF))));
                    NativeMethods.PostMessage(messageTarget, (uint)WindowsMessages.WM_MOUSEWHEEL, msgWParam, msgLParam);
                    break;

                case ScrollMethod.ScrollMessage:
                    {
                        // WM_VSCROLL sent to the main window only scrolls the window's own
                        // non-client scroll bar. To scroll the main content (which lives in a
                        // child window with WS_VSCROLL), we must send the message to that
                        // child window directly. Enumerate children and find the first one
                        // that has a vertical scroll bar.
                        IntPtr scrollTarget = ResolveScrollTarget(windowHandle);

                        // Preserve foreground window to prevent scroll messages sent to a
                        // child window (e.g., a tab page) from triggering focus changes that
                        // cause the parent tab control to switch active tabs.
                        IntPtr savedForeground = NativeMethods.GetForegroundWindow();

                        for (int i = 0; i < amount; i++)
                        {
                            NativeMethods.SendMessage(
                                scrollTarget,
                                (uint)WindowsMessages.WM_VSCROLL,
                                (IntPtr)ScrollBarCommand.SB_LINEDOWN,
                                IntPtr.Zero);
                        }

                        // Restore foreground window after scrolling to prevent tab switches.
                        if (savedForeground != IntPtr.Zero)
                        {
                            NativeMethods.SetForegroundWindow(savedForeground);
                        }
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task ScrollToTopAsync(IntPtr windowHandle, System.Drawing.Point? targetPoint = null)
        {
            // Send HOME key to scroll to top
            InputHelpers.SendKeyPress(VirtualKeyCode.HOME);
            await Task.Delay(100);

            // Also send WM_VSCROLL SB_TOP as fallback.
            // Target the scrollable child window (the main content area) rather than
            // the main window itself, so SB_TOP reaches the right scroll bar.
            IntPtr scrollTarget = ResolveScrollTarget(windowHandle);

            // Preserve foreground window to prevent scroll messages sent to a
            // child window (e.g., a tab page) from triggering focus changes that
            // cause the parent tab control to switch active tabs.
            IntPtr savedForeground = NativeMethods.GetForegroundWindow();

            NativeMethods.SendMessage(
                scrollTarget,
                (uint)WindowsMessages.WM_VSCROLL,
                (IntPtr)ScrollBarCommand.SB_TOP,
                IntPtr.Zero);

            // Restore foreground window after scrolling to prevent tab switches.
            if (savedForeground != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(savedForeground);
            }
        }

        public ScrollBarInfo? GetScrollBarInfo(IntPtr windowHandle)
        {
            // Read the same scroll bar that ScrollMessage targets, otherwise bottom
            // detection can stop early after the child-window targeting fixes.
            IntPtr scrollTarget = ResolveScrollTarget(windowHandle);
            SCROLLINFO scrollInfo = new SCROLLINFO();
            scrollInfo.cbSize = (uint)Marshal.SizeOf<SCROLLINFO>();
            scrollInfo.fMask = ScrollInfoMask.SIF_ALL;

            if (!NativeMethods.GetScrollInfo(scrollTarget, (int)ScrollBarOrientation.SB_VERT, ref scrollInfo))
            {
                return null;
            }

            return new ScrollBarInfo(
                Position: scrollInfo.nPos,
                MinRange: scrollInfo.nMin,
                MaxRange: scrollInfo.nMax,
                PageSize: (int)scrollInfo.nPage);
        }

        private static POINT? GetWheelPoint(IntPtr windowHandle, System.Drawing.Point? targetPoint)
        {
            if (targetPoint.HasValue)
            {
                return new POINT { X = targetPoint.Value.X, Y = targetPoint.Value.Y };
            }

            var clientRect = NativeMethods.GetClientRect(windowHandle);
            if (clientRect.Width <= 0 || clientRect.Height <= 0)
            {
                return null;
            }

            POINT centerPoint = new POINT
            {
                X = clientRect.Left + clientRect.Width / 2,
                Y = clientRect.Top + clientRect.Height / 2
            };

            NativeMethods.ClientToScreen(windowHandle, ref centerPoint);
            return centerPoint;
        }

        private static IntPtr ResolveWheelMessageTarget(IntPtr windowHandle, POINT point)
        {
            IntPtr pointWindow = NativeMethods.WindowFromPoint(point);
            return IsDescendantOrSelf(pointWindow, windowHandle) ? pointWindow : windowHandle;
        }

        private static bool IsDescendantOrSelf(IntPtr handle, IntPtr ancestor)
        {
            for (IntPtr current = handle; current != IntPtr.Zero; current = NativeMethods.GetParent(current))
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        internal static IntPtr ResolveScrollTarget(IntPtr windowHandle, IEnumerable<ScrollTargetCandidate> candidates)
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

        internal static IntPtr ResolveScrollTarget(IntPtr windowHandle)
        {
            return ResolveScrollTarget(windowHandle, EnumerateScrollTargetCandidates(windowHandle));
        }

        /// <summary>
        /// Enumerates visible descendant windows that could own the content scrollbar.
        /// </summary>
        private static IReadOnlyList<ScrollTargetCandidate> EnumerateScrollTargetCandidates(IntPtr parentWindow)
        {
            List<ScrollTargetCandidate> candidates = [];

            NativeMethods.EnumChildWindows(parentWindow, (hWnd, _) =>
            {
                var style = (WindowStyles)NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_STYLE);
                System.Drawing.Rectangle clientRect = NativeMethods.GetClientRect(hWnd);
                string className = NativeMethods.GetClassNameString(hWnd);

                candidates.Add(new ScrollTargetCandidate(
                    Handle: hWnd,
                    HasVerticalScrollStyle: style.HasFlag(WindowStyles.WS_VSCROLL),
                    IsVisible: NativeMethods.IsWindowVisible(hWnd),
                    ClientWidth: clientRect.Width,
                    ClientHeight: clientRect.Height,
                    ClassName: className));

                return true; // Continue
            }, IntPtr.Zero);

            return candidates;
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
    }
}
