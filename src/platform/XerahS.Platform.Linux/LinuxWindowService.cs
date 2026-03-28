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
using XerahS.Common;
using XerahS.Platform.Abstractions;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace XerahS.Platform.Linux
{
    public class LinuxWindowService : IWindowService, IDisposable
    {
        private const long MaxPropertyLongLength = 4096;
        private static readonly string[] ExcludedWindowTypeNames =
        [
            "_NET_WM_WINDOW_TYPE_DESKTOP",
            "_NET_WM_WINDOW_TYPE_DOCK",
            "_NET_WM_WINDOW_TYPE_DROPDOWN_MENU",
            "_NET_WM_WINDOW_TYPE_POPUP_MENU",
            "_NET_WM_WINDOW_TYPE_TOOLTIP",
            "_NET_WM_WINDOW_TYPE_NOTIFICATION",
            "_NET_WM_WINDOW_TYPE_COMBO",
            "_NET_WM_WINDOW_TYPE_DND",
            "_NET_WM_WINDOW_TYPE_SPLASH"
        ];
        private static readonly string[] ExcludedWindowStateNames =
        [
            "_NET_WM_STATE_HIDDEN",
            "_NET_WM_STATE_SKIP_PAGER",
            "_NET_WM_STATE_SKIP_TASKBAR"
        ];
        private readonly IntPtr _display;
        private readonly IntPtr _rootWindow;
        private readonly Dictionary<string, IntPtr> _atomCache = new(StringComparer.Ordinal);

        public LinuxWindowService()
        {
            try
            {
                _display = NativeMethods.XOpenDisplay(null);
                if (_display != IntPtr.Zero)
                {
                    _rootWindow = NativeMethods.XDefaultRootWindow(_display);
                }
                else
                {
                    DebugHelper.WriteLine("LinuxWindowService: XOpenDisplay returned null (display not available).");
                    DebugHelper.WriteLine("  This is normal on Wayland without XWayland or in restricted environments.");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "LinuxWindowService: Failed to open X display");
                DebugHelper.WriteLine("  Window management features may be limited.");
                _display = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_display != IntPtr.Zero)
            {
                NativeMethods.XCloseDisplay(_display);
            }
        }

        public IntPtr GetForegroundWindow()
        {
            DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow called");
            if (_display == IntPtr.Zero)
            {
                DebugHelper.WriteLine("LinuxWindowService: GetForegroundWindow: Display is IntPtr.Zero");
                return IntPtr.Zero;
            }

            NativeMethods.XGetInputFocus(_display, out IntPtr focus, out int revert_to);
            DebugHelper.WriteLine($"LinuxWindowService: XGetInputFocus returned: focus={focus} (0x{focus:X}), revert_to={revert_to}");

            // The focused window might be a child widget (like an input field).
            // Walk up the window tree to find the top-level window
            IntPtr topLevelWindow = GetTopLevelWindow(focus);
            if (topLevelWindow != focus)
            {
                DebugHelper.WriteLine($"LinuxWindowService: Walked up window tree: focus={focus} (0x{focus:X}) -> top-level={topLevelWindow} (0x{topLevelWindow:X})");
            }

            return topLevelWindow;
        }

        /// <summary>
        /// Traverse up the window hierarchy to find the top-level window
        /// (the window whose parent is the root window)
        /// </summary>
        private IntPtr GetTopLevelWindow(IntPtr window)
        {
            if (_display == IntPtr.Zero || window == IntPtr.Zero)
            {
                return window;
            }

            // If the window is already the root window, return it
            if (window == _rootWindow)
            {
                return window;
            }

            IntPtr currentWindow = window;
            int maxDepth = 50; // Prevent infinite loops
            int depth = 0;

            try
            {
                // Walk up the window tree until we find a window whose parent is the root window
                while (depth < maxDepth)
                {
                    depth++;

                    int result = NativeMethods.XQueryTree(
                        _display,
                        currentWindow,
                        out IntPtr root,
                        out IntPtr parent,
                        out IntPtr children,
                        out uint nchildren
                    );

                    // Free the children list if allocated
                    if (children != IntPtr.Zero)
                    {
                        try
                        {
                            NativeMethods.XFree(children);
                        }
                        catch
                        {
                            // Ignore errors when freeing
                        }
                    }

                    if (result == 0)
                    {
                        // XQueryTree failed
                        DebugHelper.WriteLine($"LinuxWindowService: XQueryTree failed for window {currentWindow:X} at depth {depth}");
                        break;
                    }

                    // If parent is root or zero, currentWindow is the top-level window
                    if (parent == _rootWindow || parent == IntPtr.Zero)
                    {
                        DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                        return currentWindow;
                    }

                    // Move up to the parent
                    currentWindow = parent;
                }

                if (depth >= maxDepth)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Hit max depth ({maxDepth}), returning current window");
                }

                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: {window:X} -> {currentWindow:X} (depth={depth})");
                return currentWindow;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"LinuxWindowService: GetTopLevelWindow: Exception - {ex.Message}, returning original window");
                return window;
            }
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XSetInputFocus(_display, handle, 1 /* RevertToParent */, IntPtr.Zero /* CurrentTime */);
            NativeMethods.XRaiseWindow(_display, handle);
            return true;
        }

        public string GetWindowText(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;

            if (TryGetUtf8StringProperty(handle, "_NET_WM_VISIBLE_NAME", out string visibleName))
            {
                return visibleName;
            }

            if (TryGetUtf8StringProperty(handle, "_NET_WM_NAME", out string title))
            {
                return title;
            }

            if (NativeMethods.XFetchName(_display, handle, out IntPtr namePtr) != 0 && namePtr != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStringAnsi(namePtr) ?? string.Empty;
                }
                finally
                {
                    NativeMethods.XFree(namePtr);
                }
            }
            return string.Empty;
        }

        public string GetWindowClassName(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return string.Empty;
            if (NativeMethods.XGetClassHint(_display, handle, out XClassHint hint) != 0)
            {
                string resClass = Marshal.PtrToStringAnsi(hint.res_class) ?? string.Empty;
                if (hint.res_class != IntPtr.Zero) NativeMethods.XFree(hint.res_class);
                if (hint.res_name != IntPtr.Zero) NativeMethods.XFree(hint.res_name);
                return resClass;
            }
            return string.Empty;
        }

        public Rectangle GetWindowBounds(IntPtr handle)
        {
            return GetWindowBoundsCore(handle, logDiagnostics: true);
        }

        private Rectangle GetWindowBoundsCore(IntPtr handle, bool logDiagnostics)
        {
            if (logDiagnostics)
            {
                DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds called for handle {handle} (0x{handle:X})");
            }

            if (_display == IntPtr.Zero)
            {
                if (logDiagnostics)
                {
                    DebugHelper.WriteLine("LinuxWindowService: GetWindowBounds: Display is IntPtr.Zero");
                }

                return Rectangle.Empty;
            }

            var attrs = new XWindowAttributes();
            int result = NativeMethods.XGetWindowAttributes(_display, handle, ref attrs);
            if (logDiagnostics)
            {
                DebugHelper.WriteLine($"LinuxWindowService: XGetWindowAttributes returned: {result}");
            }

            if (result != 0)
            {
                if (logDiagnostics)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: XWindowAttributes (relative): x={attrs.x}, y={attrs.y}, width={attrs.width}, height={attrs.height}, map_state={attrs.map_state}, border_width={attrs.border_width}");
                }

                // Check if window is actually viewable
                if (logDiagnostics)
                {
                    string mapStateStr = attrs.map_state switch
                    {
                        0 => "IsUnviewable",
                        1 => "IsViewable",
                        2 => "IsUnmapped",
                        _ => $"Unknown({attrs.map_state})"
                    };
                    DebugHelper.WriteLine($"LinuxWindowService: Window map state: {mapStateStr}");
                }

                // Translate coordinates to root window (absolute screen coordinates)
                // The coordinates from XGetWindowAttributes are relative to the parent window
                int absoluteX, absoluteY;
                IntPtr child;
                int translateResult = NativeMethods.XTranslateCoordinates(
                    _display,
                    handle,           // source window
                    _rootWindow,      // destination (root window)
                    0, 0,             // source coordinates (0,0 of the window)
                    out absoluteX,
                    out absoluteY,
                    out child
                );

                if (logDiagnostics)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: XTranslateCoordinates returned: {translateResult}, absolute: x={absoluteX}, y={absoluteY}");
                }

                // Use the absolute coordinates instead of the relative ones
                var rect = new Rectangle(absoluteX, absoluteY, attrs.width, attrs.height);
                if (TryGetFrameExtents(handle, out var frameExtents))
                {
                    rect = ApplyFrameExtents(rect, frameExtents.Left, frameExtents.Right, frameExtents.Top, frameExtents.Bottom);
                }

                if (logDiagnostics)
                {
                    DebugHelper.WriteLine($"LinuxWindowService: GetWindowBounds returning: {rect}");
                }

                // Sanity check
                if (logDiagnostics && (attrs.width <= 0 || attrs.height <= 0))
                {
                    DebugHelper.WriteLine("LinuxWindowService: WARNING: Window has invalid dimensions!");
                }
                if (logDiagnostics && (absoluteX < -10000 || absoluteY < -10000 || absoluteX > 10000 || absoluteY > 10000))
                {
                    DebugHelper.WriteLine("LinuxWindowService: WARNING: Window coordinates seem out of reasonable range!");
                }

                return rect;
            }

            if (logDiagnostics)
            {
                DebugHelper.WriteLine("LinuxWindowService: XGetWindowAttributes failed, returning Rectangle.Empty");
            }

            return Rectangle.Empty;
        }

        public Rectangle GetWindowClientBounds(IntPtr handle)
        {
            return GetWindowBounds(handle);
        }

        public bool IsWindowVisible(IntPtr handle)
        {
            if (_display == IntPtr.Zero) return false;
            var attrs = new XWindowAttributes();
            if (NativeMethods.XGetWindowAttributes(_display, handle, ref attrs) != 0)
            {
                return attrs.map_state == NativeMethods.IsViewable;
            }
            return false;
        }

        public bool IsWindowMaximized(IntPtr handle)
        {
            // Not implemented in MVP
            return false;
        }

        public bool IsWindowMinimized(IntPtr handle)
        {
            // Not implemented in MVP
            return false;
        }

        public bool ShowWindow(IntPtr handle, int cmdShow)
        {
            if (_display == IntPtr.Zero) return false;

            if (cmdShow == 0)
                NativeMethods.XIconifyWindow(_display, handle, 0);
            else
                NativeMethods.XRaiseWindow(_display, handle);

            return true;
        }

        public bool SetWindowPos(IntPtr handle, IntPtr handleInsertAfter, int x, int y, int width, int height, uint flags)
        {
            if (_display == IntPtr.Zero) return false;
            NativeMethods.XMoveResizeWindow(_display, handle, x, y, width, height);
            return true;
        }

        public WindowInfo[] GetAllWindows()
        {
            if (_display == IntPtr.Zero) return Array.Empty<WindowInfo>();

            var list = new List<WindowInfo>();
            var seenHandles = new HashSet<IntPtr>();

            foreach (var handle in EnumerateCandidateWindows())
            {
                if (handle == IntPtr.Zero || handle == _rootWindow || !seenHandles.Add(handle))
                    continue;

                if (!TryCreateWindowInfo(handle, out var windowInfo))
                    continue;

                list.Add(windowInfo);
            }

            return list.ToArray();
        }

        private IEnumerable<IntPtr> EnumerateCandidateWindows()
        {
            if (TryGetManagedWindowHandles(out var managedWindows))
            {
                for (int i = managedWindows.Length - 1; i >= 0; i--)
                {
                    yield return managedWindows[i];
                }

                yield break;
            }

            if (!TryGetRootChildWindows(out var rootChildren))
                yield break;

            for (int i = rootChildren.Length - 1; i >= 0; i--)
            {
                yield return rootChildren[i];
            }
        }

        private bool TryGetManagedWindowHandles(out IntPtr[] handles)
        {
            return TryGetWindowHandleArrayProperty(_rootWindow, "_NET_CLIENT_LIST_STACKING", out handles) && handles.Length > 0;
        }

        private bool TryGetRootChildWindows(out IntPtr[] handles)
        {
            handles = Array.Empty<IntPtr>();

            if (NativeMethods.XQueryTree(_display, _rootWindow, out _, out _, out IntPtr children, out uint childCount) == 0)
            {
                return false;
            }

            try
            {
                if (childCount == 0 || children == IntPtr.Zero)
                {
                    return false;
                }

                handles = new IntPtr[childCount];
                Marshal.Copy(children, handles, 0, (int)childCount);
                return true;
            }
            finally
            {
                if (children != IntPtr.Zero)
                {
                    NativeMethods.XFree(children);
                }
            }
        }

        private bool TryCreateWindowInfo(IntPtr handle, out WindowInfo windowInfo)
        {
            windowInfo = default!;

            if (!TryGetWindowAttributes(handle, out var attrs))
                return false;

            if (attrs.map_state != NativeMethods.IsViewable || attrs.override_redirect)
                return false;

            if (HasAnyPropertyAtom(handle, "_NET_WM_WINDOW_TYPE", ExcludedWindowTypeNames) ||
                HasAnyPropertyAtom(handle, "_NET_WM_STATE", ExcludedWindowStateNames))
            {
                return false;
            }

            string title = GetWindowText(handle);
            if (string.IsNullOrWhiteSpace(title))
                return false;

            var bounds = GetWindowBoundsCore(handle, logDiagnostics: false);
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return false;

            windowInfo = new WindowInfo
            {
                Handle = handle,
                Title = title,
                ClassName = GetWindowClassName(handle),
                Bounds = bounds,
                IsVisible = true
            };

            return true;
        }

        private bool TryGetWindowAttributes(IntPtr handle, out XWindowAttributes attributes)
        {
            attributes = new XWindowAttributes();
            return _display != IntPtr.Zero && NativeMethods.XGetWindowAttributes(_display, handle, ref attributes) != 0;
        }

        private bool HasAnyPropertyAtom(IntPtr handle, string propertyName, IEnumerable<string> atomNames)
        {
            if (!TryGetWindowHandleArrayProperty(handle, propertyName, out var propertyAtoms) || propertyAtoms.Length == 0)
                return false;

            foreach (var atomName in atomNames)
            {
                IntPtr atom = GetAtom(atomName);
                if (atom == IntPtr.Zero)
                    continue;

                for (int i = 0; i < propertyAtoms.Length; i++)
                {
                    if (propertyAtoms[i] == atom)
                        return true;
                }
            }

            return false;
        }

        private bool TryGetWindowHandleArrayProperty(IntPtr handle, string propertyName, out IntPtr[] values)
        {
            values = Array.Empty<IntPtr>();

            if (!TryGetProperty(handle, propertyName, out var property))
                return false;

            using (property)
            {
                if (property.Format != 32 || property.ItemCount <= 0)
                    return false;

                values = ReadIntPtrArray(property.Data, property.ItemCount);
                return values.Length > 0;
            }
        }

        private bool TryGetUtf8StringProperty(IntPtr handle, string propertyName, out string value)
        {
            value = string.Empty;

            if (!TryGetProperty(handle, propertyName, out var property))
                return false;

            using (property)
            {
                if (property.Format != 8 || property.ItemCount <= 0)
                    return false;

                int length = checked((int)property.ItemCount);
                var bytes = new byte[length];
                Marshal.Copy(property.Data, bytes, 0, length);
                value = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        private bool TryGetFrameExtents(IntPtr handle, out FrameExtents extents)
        {
            extents = default;

            if (!TryGetProperty(handle, "_NET_FRAME_EXTENTS", out var property))
                return false;

            using (property)
            {
                if (property.Format != 32 || property.ItemCount < 4)
                    return false;

                var values = ReadIntPtrArray(property.Data, property.ItemCount);
                if (values.Length < 4)
                    return false;

                int left = ToInt32(values[0]);
                int right = ToInt32(values[1]);
                int top = ToInt32(values[2]);
                int bottom = ToInt32(values[3]);

                if (left < 0 || right < 0 || top < 0 || bottom < 0)
                    return false;

                extents = new FrameExtents(left, right, top, bottom);
                return true;
            }
        }

        private bool TryGetProperty(IntPtr handle, string propertyName, out XProperty property)
        {
            property = default;

            if (_display == IntPtr.Zero)
                return false;

            IntPtr propertyAtom = GetAtom(propertyName);
            if (propertyAtom == IntPtr.Zero)
                return false;

            int result = NativeMethods.XGetWindowProperty(
                _display,
                handle,
                propertyAtom,
                0,
                MaxPropertyLongLength,
                false,
                IntPtr.Zero,
                out IntPtr actualType,
                out int actualFormat,
                out IntPtr itemCount,
                out _,
                out IntPtr data);

            if (result != 0 || actualType == IntPtr.Zero || data == IntPtr.Zero)
            {
                if (data != IntPtr.Zero)
                {
                    NativeMethods.XFree(data);
                }

                return false;
            }

            long count = itemCount.ToInt64();
            if (count <= 0)
            {
                NativeMethods.XFree(data);
                return false;
            }

            property = new XProperty(data, count, actualFormat);
            return true;
        }

        private IntPtr GetAtom(string atomName)
        {
            if (_display == IntPtr.Zero)
                return IntPtr.Zero;

            if (_atomCache.TryGetValue(atomName, out var atom))
                return atom;

            atom = NativeMethods.XInternAtom(_display, atomName, only_if_exists: false);
            _atomCache[atomName] = atom;
            return atom;
        }

        private static IntPtr[] ReadIntPtrArray(IntPtr data, long itemCount)
        {
            int length = checked((int)itemCount);
            var values = new IntPtr[length];
            Marshal.Copy(data, values, 0, length);
            return values;
        }

        private static int ToInt32(IntPtr value)
        {
            long intValue = value.ToInt64();
            return checked((int)intValue);
        }

        internal static Rectangle ApplyFrameExtents(Rectangle clientBounds, int left, int right, int top, int bottom)
        {
            if (left == 0 && right == 0 && top == 0 && bottom == 0)
                return clientBounds;

            return new Rectangle(
                clientBounds.X - left,
                clientBounds.Y - top,
                clientBounds.Width + left + right,
                clientBounds.Height + top + bottom);
        }

        internal static bool ContainsExcludedWindowTypeName(IEnumerable<string> windowTypes)
        {
            ArgumentNullException.ThrowIfNull(windowTypes);

            return windowTypes.Any(windowType =>
                ExcludedWindowTypeNames.Contains(windowType, StringComparer.Ordinal));
        }

        internal static bool ContainsExcludedWindowStateName(IEnumerable<string> windowStates)
        {
            ArgumentNullException.ThrowIfNull(windowStates);

            return windowStates.Any(windowState =>
                ExcludedWindowStateNames.Contains(windowState, StringComparer.Ordinal));
        }

        public uint GetWindowProcessId(IntPtr handle)
        {
            return 0;
        }

        public IntPtr SearchWindow(string windowTitle)
        {
            // TODO: Implement proper X11 window search
            if (string.IsNullOrEmpty(windowTitle) || _display == IntPtr.Zero)
                return IntPtr.Zero;

            // Fallback: iterate through all windows and find one with matching title
            var windows = GetAllWindows();
            foreach (var w in windows)
            {
                if (!string.IsNullOrEmpty(w.Title) && w.Title.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return w.Handle;
                }
            }
            return IntPtr.Zero;
        }

        public bool ActivateWindow(IntPtr handle)
        {
            // Use SetForegroundWindow which does XSetInputFocus + XRaiseWindow
            return SetForegroundWindow(handle);
        }

        public bool SetWindowClickThrough(IntPtr handle)
        {
            // Click-through windows are not easily supported on X11/Wayland without compositor extensions.
            // This is a no-op for Linux; recording borders will still be visible but interactable.
            return false;
        }

        private readonly struct XProperty : IDisposable
        {
            public XProperty(IntPtr data, long itemCount, int format)
            {
                Data = data;
                ItemCount = itemCount;
                Format = format;
            }

            public IntPtr Data { get; }

            public long ItemCount { get; }

            public int Format { get; }

            public void Dispose()
            {
                if (Data != IntPtr.Zero)
                {
                    NativeMethods.XFree(Data);
                }
            }
        }

        private readonly struct FrameExtents
        {
            public FrameExtents(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public int Left { get; }

            public int Right { get; }

            public int Top { get; }

            public int Bottom { get; }
        }
    }
}
