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

using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows;

/// <summary>
/// Windows implementation of IClipboardService using Win32 clipboard APIs only.
/// Publishes native image formats explicitly so modern Windows clipboard consumers do not
/// depend on shell or framework format synthesis.
/// </summary>
public class WindowsClipboardService : IClipboardService
{
    private const int ClipboardOpenRetryCount = 10;
    private const int ClipboardOpenRetryDelayMs = 25;

    private static class Native
    {
        public const uint GMEM_MOVEABLE = 0x0002;
        public const uint CF_TEXT = 1;
        public const uint CF_BITMAP = 2;
        public const uint CF_DIB = 8;
        public const uint CF_DIBV5 = WindowsClipboardImageHelper.CfDibV5;
        public const uint CF_UNICODETEXT = 13;
        public const uint CF_HDROP = 15;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nuint GlobalSize(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint RegisterClipboardFormat(string lpszFormat);
    }

    private static uint? _cfPng;

    private static uint CfPng
    {
        get
        {
            if (_cfPng == null)
                _cfPng = Native.RegisterClipboardFormat("PNG");
            return _cfPng.Value;
        }
    }

    public void Clear()
    {
        try
        {
            RunInStaThread(() =>
            {
                if (OpenClipboardWithRetry())
                {
                    try { Native.EmptyClipboard(); }
                    finally { Native.CloseClipboard(); }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear clipboard: {ex.Message}");
        }
    }

    public bool ContainsText()
    {
        try
        {
            return RunInStaThread(() => Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT));
        }
        catch { return false; }
    }

    public bool ContainsImage()
    {
        try
        {
            return RunInStaThread(() =>
                Native.IsClipboardFormatAvailable(CfPng) ||
                Native.IsClipboardFormatAvailable(Native.CF_DIBV5) ||
                Native.IsClipboardFormatAvailable(Native.CF_DIB));
        }
        catch { return false; }
    }

    public bool ContainsFileDropList()
    {
        try
        {
            return RunInStaThread(() => Native.IsClipboardFormatAvailable(Native.CF_HDROP));
        }
        catch { return false; }
    }

    public string? GetText()
    {
        try
        {
            return RunInStaThread(() =>
            {
                if (!OpenClipboardWithRetry()) return null;
                try
                {
                    var h = Native.GetClipboardData(Native.CF_UNICODETEXT);
                    if (h == IntPtr.Zero) return null;
                    var ptr = Native.GlobalLock(h);
                    if (ptr == IntPtr.Zero) return null;
                    try
                    {
                        var size = (long)(ulong)Native.GlobalSize(h);
                        if (size <= 0) return null;
                        return Marshal.PtrToStringUni(ptr, (int)(size / 2) - 1);
                    }
                    finally { Native.GlobalUnlock(h); }
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch { return null; }
    }

    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            RunInStaThread(() =>
            {
                IntPtr hMem = AllocateGlobalBytes(Encoding.Unicode.GetBytes(text + "\0"));
                if (hMem == IntPtr.Zero) return;
                if (!OpenClipboardWithRetry()) { Native.GlobalFree(hMem); return; }
                try
                {
                    Native.EmptyClipboard();
                    Native.SetClipboardData(Native.CF_UNICODETEXT, hMem);
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set clipboard text: {ex.Message}");
        }
    }

    public SKBitmap? GetImage()
    {
        try
        {
            return RunInStaThread(() =>
            {
                if (!OpenClipboardWithRetry()) return null;
                try
                {
                    return TryReadClipboardImage(CfPng, WindowsClipboardImageHelper.DecodePng) ??
                        TryReadClipboardImage(Native.CF_DIBV5, WindowsClipboardImageHelper.DecodeDibV5) ??
                        TryReadClipboardImage(Native.CF_DIB, WindowsClipboardImageHelper.DecodeDib);
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch { return null; }
    }

    public void SetImage(SKBitmap image)
    {
        if (image == null) return;

        try
        {
            byte[] pngBytes = WindowsClipboardImageHelper.EncodePng(image);
            byte[] dibV5Bytes = WindowsClipboardImageHelper.BuildDibV5(image);
            byte[] dibBytes = WindowsClipboardImageHelper.BuildOpaqueDib(image, SKColors.White);

            RunInStaThread(() =>
            {
                if (!OpenClipboardWithRetry()) return;
                try
                {
                    Native.EmptyClipboard();
                    int formatsWritten = 0;

                    if (TrySetClipboardBytes(CfPng, pngBytes))
                    {
                        formatsWritten++;
                    }

                    if (TrySetClipboardBytes(Native.CF_DIBV5, dibV5Bytes))
                    {
                        formatsWritten++;
                    }

                    if (TrySetClipboardBytes(Native.CF_DIB, dibBytes))
                    {
                        formatsWritten++;
                    }

                    if (formatsWritten == 0)
                    {
                        throw new InvalidOperationException("Failed to publish any clipboard image formats.");
                    }
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set clipboard image: {ex.Message}");
        }
    }

    public string[]? GetFileDropList()
    {
        try
        {
            return RunInStaThread(() =>
            {
                if (!OpenClipboardWithRetry()) return null;
                try
                {
                    var h = Native.GetClipboardData(Native.CF_HDROP);
                    if (h == IntPtr.Zero) return null;
                    var ptr = Native.GlobalLock(h);
                    if (ptr == IntPtr.Zero) return null;
                    try
                    {
                        var count = DragQueryFile(h, 0xFFFFFFFF, null, 0);
                        if (count == 0) return null;
                        var list = new string[count];
                        var sb = new StringBuilder(260);
                        for (uint i = 0; i < count; i++)
                        {
                            if (DragQueryFile(h, i, sb, sb.Capacity) > 0)
                                list[i] = sb.ToString();
                        }
                        return list;
                    }
                    finally { Native.GlobalUnlock(h); }
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch { return null; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, [Out] StringBuilder? lpszFile, int cch);

    public void SetFileDropList(string[] files)
    {
        if (files == null || files.Length == 0) return;
        try
        {
            RunInStaThread(() =>
            {
                // DROPFILES: pFiles=20, pt=(0,0), fNC=0, fWide=1; then double-null-terminated Unicode paths
                using var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(20);   // pFiles offset
                bw.Write(0L);   // pt.x, pt.y
                bw.Write(0);    // fNC
                bw.Write(1);    // fWide (Unicode)
                bw.Flush();
                var pathBytes = Encoding.Unicode.GetBytes(string.Join("\0", files.Where(f => !string.IsNullOrEmpty(f))) + "\0\0");
                ms.Write(pathBytes, 0, pathBytes.Length);
                var bytes = ms.ToArray();
                IntPtr hMem = AllocateGlobalBytes(bytes);
                if (hMem == IntPtr.Zero) return;
                if (!OpenClipboardWithRetry()) { Native.GlobalFree(hMem); return; }
                try
                {
                    Native.EmptyClipboard();
                    Native.SetClipboardData(Native.CF_HDROP, hMem);
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set clipboard file drop list: {ex.Message}");
        }
    }

    public object? GetData(string format)
    {
        try
        {
            return RunInStaThread<object?>(() =>
            {
                uint fmt = uint.TryParse(format, out var n) ? n : Native.RegisterClipboardFormat(format);
                if (!Native.IsClipboardFormatAvailable(fmt)) return null;
                if (!OpenClipboardWithRetry()) return null;
                try
                {
                    var h = Native.GetClipboardData(fmt);
                    if (h == IntPtr.Zero) return null;
                    var ptr = Native.GlobalLock(h);
                    if (ptr == IntPtr.Zero) return null;
                    try
                    {
                        var size = (int)(ulong)Native.GlobalSize(h);
                        if (fmt == Native.CF_UNICODETEXT)
                            return Marshal.PtrToStringUni(ptr, (size / 2) - 1);
                        var bytes = new byte[size];
                        Marshal.Copy(ptr, bytes, 0, size);
                        return bytes;
                    }
                    finally { Native.GlobalUnlock(h); }
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch { return null; }
    }

    public void SetData(string format, object data)
    {
        if (string.IsNullOrEmpty(format) || data == null) return;
        try
        {
            RunInStaThread(() =>
            {
                byte[]? bytes = data is string s
                    ? Encoding.Unicode.GetBytes(s + "\0")
                    : data is byte[] b ? b : null;
                if (bytes == null || bytes.Length == 0) return;
                var fmt = uint.TryParse(format, out var n) ? n : Native.RegisterClipboardFormat(format);
                IntPtr hMem = AllocateGlobalBytes(bytes);
                if (hMem == IntPtr.Zero) return;
                if (!OpenClipboardWithRetry()) { Native.GlobalFree(hMem); return; }
                try
                {
                    Native.EmptyClipboard();
                    Native.SetClipboardData(fmt, hMem);
                }
                finally { Native.CloseClipboard(); }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set clipboard data: {ex.Message}");
        }
    }

    public bool ContainsData(string format)
    {
        if (string.IsNullOrEmpty(format)) return false;
        try
        {
            var fmt = uint.TryParse(format, out var n) ? n : Native.RegisterClipboardFormat(format);
            return RunInStaThread(() => Native.IsClipboardFormatAvailable(fmt));
        }
        catch { return false; }
    }

    public Task<string?> GetTextAsync() => Task.FromResult(GetText());
    public Task SetTextAsync(string text) => Task.Run(() => SetText(text));

    private static bool OpenClipboardWithRetry()
    {
        for (int attempt = 0; attempt < ClipboardOpenRetryCount; attempt++)
        {
            if (Native.OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(ClipboardOpenRetryDelayMs);
        }

        return false;
    }

    private static IntPtr AllocateGlobalBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return IntPtr.Zero;
        }

        IntPtr hMem = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (hMem == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr ptr = Native.GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            Native.GlobalFree(hMem);
            return IntPtr.Zero;
        }

        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return hMem;
        }
        finally
        {
            Native.GlobalUnlock(hMem);
        }
    }

    private static byte[]? ReadGlobalBytes(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        IntPtr ptr = Native.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            int size = checked((int)(ulong)Native.GlobalSize(handle));
            if (size <= 0)
            {
                return null;
            }

            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Native.GlobalUnlock(handle);
        }
    }

    private static SKBitmap? TryReadClipboardImage(uint format, Func<byte[], SKBitmap?> decoder)
    {
        if (decoder == null)
        {
            return null;
        }

        IntPtr handle = Native.GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        byte[]? bytes = ReadGlobalBytes(handle);
        return bytes == null ? null : decoder(bytes);
    }

    private static bool TrySetClipboardBytes(uint format, byte[] bytes)
    {
        IntPtr hMem = AllocateGlobalBytes(bytes);
        if (hMem == IntPtr.Zero)
        {
            return false;
        }

        if (Native.SetClipboardData(format, hMem) != IntPtr.Zero)
        {
            return true;
        }

        Native.GlobalFree(hMem);
        return false;
    }

    private static void RunInStaThread(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }
        Exception? captured = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { captured = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        t.Join();
        if (captured != null) throw captured;
    }

    private static T? RunInStaThread<T>(Func<T?> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return func();
        Exception? captured = null;
        T? result = default;
        var t = new Thread(() => { try { result = func(); } catch (Exception ex) { captured = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        t.Join();
        if (captured != null) throw captured;
        return result;
    }
}
