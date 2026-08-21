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
using System.Runtime.InteropServices;
using System.Text;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS.Native
{
    /// <summary>
    /// One on-screen window as reported by CGWindowListCopyWindowInfo.
    /// Bounds are in global display points (top-left origin), front-to-back order.
    /// </summary>
    internal readonly record struct QuartzWindowInfo(
        uint WindowNumber,
        int OwnerPid,
        string OwnerName,
        string Title,
        Rectangle Bounds,
        int Layer);

    /// <summary>
    /// Native window enumeration via CGWindowListCopyWindowInfo (XIP0078 P5).
    /// Replaces the frontmost-only osascript/System Events query: returns all on-screen windows
    /// in a few milliseconds with no Automation prompt. Window titles require Screen Recording
    /// permission on macOS 10.15+; without it enumeration still works with empty titles.
    /// </summary>
    internal static class QuartzWindowList
    {
        private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const uint OptionOnScreenOnly = 1 << 0;          // kCGWindowListOptionOnScreenOnly
        private const uint ExcludeDesktopElements = 1 << 4;      // kCGWindowListExcludeDesktopElements
        private const uint NullWindowId = 0;                     // kCGNullWindowID
        private const int CFNumberIntType = 9;                   // kCFNumberIntType
        private const uint CFStringEncodingUTF8 = 0x08000100;

        [StructLayout(LayoutKind.Sequential)]
        private struct CGRect
        {
            public double X, Y, Width, Height;
        }

        [DllImport(CoreGraphicsLib)]
        private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);

        [DllImport(CoreFoundationLib)]
        private static extern nint CFArrayGetCount(IntPtr array);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFNumberGetValue(IntPtr number, nint type, out int value);

        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFStringGetCString(IntPtr str, byte[] buffer, nint bufferSize, uint encoding);

        [DllImport(CoreFoundationLib)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport("libSystem.dylib")]
        private static extern IntPtr dlopen(string path, int mode);

        [DllImport("libSystem.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private const int RTLD_LAZY = 0x00001;

        // CGWindow dictionary keys resolved once via dlsym (same pattern as Accessibility.cs)
        // so we depend on the exported CFString constants rather than their string contents.
        private static readonly IntPtr KeyWindowNumber;
        private static readonly IntPtr KeyOwnerPid;
        private static readonly IntPtr KeyOwnerName;
        private static readonly IntPtr KeyName;
        private static readonly IntPtr KeyLayer;
        private static readonly IntPtr KeyBounds;
        private static readonly bool KeysAvailable;

        static QuartzWindowList()
        {
            IntPtr coreGraphics = dlopen(CoreGraphicsLib, RTLD_LAZY);
            if (coreGraphics == IntPtr.Zero)
            {
                return;
            }

            KeyWindowNumber = ReadConstant(coreGraphics, "kCGWindowNumber");
            KeyOwnerPid = ReadConstant(coreGraphics, "kCGWindowOwnerPID");
            KeyOwnerName = ReadConstant(coreGraphics, "kCGWindowOwnerName");
            KeyName = ReadConstant(coreGraphics, "kCGWindowName");
            KeyLayer = ReadConstant(coreGraphics, "kCGWindowLayer");
            KeyBounds = ReadConstant(coreGraphics, "kCGWindowBounds");

            KeysAvailable = KeyWindowNumber != IntPtr.Zero && KeyOwnerPid != IntPtr.Zero &&
                            KeyLayer != IntPtr.Zero && KeyBounds != IntPtr.Zero;
        }

        private static IntPtr ReadConstant(IntPtr library, string symbol)
        {
            IntPtr address = dlsym(library, symbol);
            return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
        }

        /// <summary>
        /// Returns all on-screen windows (desktop elements excluded), front-to-back.
        /// Returns an empty list when enumeration is unavailable.
        /// </summary>
        public static List<QuartzWindowInfo> GetOnScreenWindows()
        {
            var windows = new List<QuartzWindowInfo>();

            if (!KeysAvailable)
            {
                DebugHelper.WriteLine("[QuartzWindowList] CGWindow dictionary keys unavailable; enumeration skipped.");
                return windows;
            }

            IntPtr array = IntPtr.Zero;

            try
            {
                array = CGWindowListCopyWindowInfo(OptionOnScreenOnly | ExcludeDesktopElements, NullWindowId);
                if (array == IntPtr.Zero)
                {
                    return windows;
                }

                nint count = CFArrayGetCount(array);
                for (nint i = 0; i < count; i++)
                {
                    IntPtr dict = CFArrayGetValueAtIndex(array, i);
                    if (dict == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (!TryGetInt(dict, KeyWindowNumber, out int windowNumber) ||
                        !TryGetInt(dict, KeyOwnerPid, out int ownerPid) ||
                        !TryGetInt(dict, KeyLayer, out int layer))
                    {
                        continue;
                    }

                    Rectangle bounds = Rectangle.Empty;
                    IntPtr boundsDict = CFDictionaryGetValue(dict, KeyBounds);
                    if (boundsDict != IntPtr.Zero && CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect rect))
                    {
                        bounds = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                    }

                    string ownerName = GetString(dict, KeyOwnerName);
                    string title = GetString(dict, KeyName);

                    windows.Add(new QuartzWindowInfo((uint)windowNumber, ownerPid, ownerName, title, bounds, layer));
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[QuartzWindowList] Window enumeration failed");
            }
            finally
            {
                if (array != IntPtr.Zero)
                {
                    CFRelease(array);
                }
            }

            return windows;
        }

        /// <summary>
        /// Returns normal application windows (layer 0, non-empty bounds), front-to-back.
        /// </summary>
        public static List<QuartzWindowInfo> GetApplicationWindows()
        {
            var windows = GetOnScreenWindows();
            windows.RemoveAll(w => w.Layer != 0 || w.Bounds.Width <= 1 || w.Bounds.Height <= 1);
            return windows;
        }

        private static bool TryGetInt(IntPtr dict, IntPtr key, out int value)
        {
            value = 0;
            if (key == IntPtr.Zero)
            {
                return false;
            }

            IntPtr number = CFDictionaryGetValue(dict, key);
            return number != IntPtr.Zero && CFNumberGetValue(number, CFNumberIntType, out value);
        }

        private static string GetString(IntPtr dict, IntPtr key)
        {
            if (key == IntPtr.Zero)
            {
                return string.Empty;
            }

            IntPtr cfString = CFDictionaryGetValue(dict, key);
            if (cfString == IntPtr.Zero)
            {
                return string.Empty;
            }

            var buffer = new byte[1024];
            if (!CFStringGetCString(cfString, buffer, buffer.Length, CFStringEncodingUTF8))
            {
                return string.Empty;
            }

            int length = Array.IndexOf(buffer, (byte)0);
            if (length < 0)
            {
                length = buffer.Length;
            }

            return Encoding.UTF8.GetString(buffer, 0, length);
        }
    }
}
