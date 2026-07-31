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

namespace XerahS.Platform.Linux.Input.Evdev;

/// <summary>
/// Minimal libc P/Invoke surface for reading raw input events from <c>/dev/input/event*</c>
/// using the evdev ABI. Only read-only access is required for hotkey detection.
/// </summary>
internal static class EvdevNative
{
    private const string LibC = "libc";

    public const int O_RDONLY = 0x0000;
    public const int O_NONBLOCK = 0x0800;

    // errno values returned through Marshal.GetLastWin32Error().
    public const int EINTR = 4;
    public const int EACCES = 13;
    public const int EBADF = 9;
    public const int EBUSY = 16;
    public const int EAGAIN = 11;

    // Fixed-size ioctl requests (little-endian x86_64 ABI, matching the kernel _IOR macros).
    public const ulong EVIOCGNAME_256 = 0x81004506; // EVIOCGNAME(256)
    public const ulong EVIOCGID = 0x80084502;       // struct input_id
    public const ulong EVIOCGKEY_96 = 0x80604518;   // EVIOCGKEY(96)

    /// <summary>
    /// Builds the EVIOCGBIT(ev, len) ioctl request used to query the capability bitmap
    /// for a given event type.
    /// </summary>
    public static ulong EVIOCGBIT(int eventType, int length)
    {
        const ulong iocRead = 2;
        const int iocDirShift = 30;
        const int iocSizeShift = 16;
        const int iocTypeShift = 8;
        const int evdevIoctlType = 0x45;
        const int evdevGetBitBase = 0x20;

        return (iocRead << iocDirShift) |
               ((ulong)length << iocSizeShift) |
               ((ulong)evdevIoctlType << iocTypeShift) |
               (uint)(evdevGetBitBase + eventType);
    }

    [DllImport(LibC, SetLastError = true)]
    public static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);

    [DllImport(LibC, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(LibC, SetLastError = true)]
    public static extern IntPtr read(int fd, IntPtr buf, IntPtr count);

    [DllImport(LibC, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, byte[] data);

    /// <summary>
    /// Layout-compatible mirror of the kernel <c>struct input_event</c>.
    /// <c>time</c> is two native words (struct timeval) which are ignored here.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InputEvent
    {
        public IntPtr TimeSec;
        public IntPtr TimeUsec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }
}
