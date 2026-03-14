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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace XerahS.Platform.Windows;

internal static class WindowsClipboardImageHelper
{
    internal const uint CfDibV5 = 17;

    private const int BitmapInfoHeaderSize = 40;
    private const int BitmapV5HeaderSize = 124;
    private const uint LcsSrgb = 0x73524742;
    private const uint LcsGmImages = 4;
    private const uint RedMask = 0x00FF0000;
    private const uint GreenMask = 0x0000FF00;
    private const uint BlueMask = 0x000000FF;
    private const uint AlphaMask = 0xFF000000;

    internal static byte[] EncodePng(SKBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var stream = new MemoryStream();
        image.Encode(stream, SKEncodedImageFormat.Png, 100);
        return stream.ToArray();
    }

    internal static byte[] BuildDibV5(SKBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var normalized = CreateBgraBitmap(image, SKAlphaType.Unpremul);
        byte[] pixelBytes = GetPixelBytes(normalized);

        using var stream = new MemoryStream(BitmapV5HeaderSize + pixelBytes.Length);
        using var writer = new BinaryWriter(stream);

        writer.Write(BitmapV5HeaderSize);
        writer.Write(normalized.Width);
        writer.Write(-normalized.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)BitmapCompressionMode.BI_BITFIELDS);
        writer.Write((uint)pixelBytes.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(RedMask);
        writer.Write(GreenMask);
        writer.Write(BlueMask);
        writer.Write(AlphaMask);
        writer.Write(LcsSrgb);
        writer.Write(new byte[36]);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(LcsGmImages);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(pixelBytes);

        return stream.ToArray();
    }

    internal static byte[] BuildOpaqueDib(SKBitmap image, SKColor backgroundColor)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var flattened = CreateOpaqueBitmap(image, backgroundColor);
        byte[] pixelBytes = GetPixelBytes(flattened);

        using var stream = new MemoryStream(BitmapInfoHeaderSize + pixelBytes.Length);
        using var writer = new BinaryWriter(stream);

        writer.Write(BitmapInfoHeaderSize);
        writer.Write(flattened.Width);
        writer.Write(-flattened.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)BitmapCompressionMode.BI_RGB);
        writer.Write((uint)pixelBytes.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(pixelBytes);

        return stream.ToArray();
    }

    internal static SKBitmap? DecodePng(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0)
        {
            return null;
        }

        return SKBitmap.Decode(pngData);
    }

    internal static SKBitmap? DecodeDibV5(byte[] dibV5)
    {
        if (dibV5 == null || dibV5.Length < BitmapV5HeaderSize)
        {
            return null;
        }

        using var bitmap = ClipboardHelpersEx.DIBV5ToBitmap(dibV5);
        if (bitmap == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    internal static SKBitmap? DecodeDib(byte[] dib)
    {
        if (dib == null || dib.Length < BitmapInfoHeaderSize)
        {
            return null;
        }

        try
        {
            int headerSize = BitConverter.ToInt32(dib, 0);
            int width = BitConverter.ToInt32(dib, 4);
            int height = BitConverter.ToInt32(dib, 8);
            short bitCount = BitConverter.ToInt16(dib, 14);
            int compression = BitConverter.ToInt32(dib, 16);

            if (headerSize < BitmapInfoHeaderSize || width <= 0 || Math.Abs(height) <= 0 || bitCount != 32)
            {
                return null;
            }

            int rowBytes = (width * 4 + 3) & ~3;
            int rows = Math.Abs(height);
            int pixelDataSize = rowBytes * rows;
            int pixelsOffset = headerSize;

            if (bitCount <= 8)
            {
                int colorsUsed = BitConverter.ToInt32(dib, 32);
                if (colorsUsed == 0)
                {
                    colorsUsed = 1 << bitCount;
                }

                pixelsOffset += colorsUsed * 4;
            }
            else if (compression == NativeConstants.BI_BITFIELDS && bitCount > 8 && headerSize == BitmapInfoHeaderSize)
            {
                pixelsOffset += 12;
            }

            if (pixelsOffset + (long)pixelDataSize > dib.Length)
            {
                return null;
            }

            int fileSize = 14 + dib.Length;
            int offsetToPixels = 14 + pixelsOffset;

            using var stream = new MemoryStream(fileSize);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write(0);
            writer.Write(offsetToPixels);
            writer.Write(dib, 0, dib.Length);
            writer.Flush();
            stream.Position = 0;

            return SKBitmap.Decode(stream);
        }
        catch
        {
            return null;
        }
    }

    private static SKBitmap CreateBgraBitmap(SKBitmap source, SKAlphaType alphaType)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("Clipboard image must have a positive size.", nameof(source));
        }

        SKImageInfo info = new(source.Width, source.Height, SKColorType.Bgra8888, alphaType);
        var bitmap = new SKBitmap(info);
        IntPtr pixels = bitmap.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Failed to allocate clipboard bitmap pixels.");
        }

        using var pixmap = source.PeekPixels();
        if (pixmap == null || !pixmap.ReadPixels(info, pixels, bitmap.RowBytes, 0, 0))
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Failed to normalize clipboard bitmap pixels.");
        }

        return bitmap;
    }

    private static SKBitmap CreateOpaqueBitmap(SKBitmap source, SKColor backgroundColor)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("Clipboard image must have a positive size.", nameof(source));
        }

        SKImageInfo info = new(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(backgroundColor);
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();

        return bitmap;
    }

    private static byte[] GetPixelBytes(SKBitmap bitmap)
    {
        int byteCount = checked(bitmap.RowBytes * bitmap.Height);
        byte[] bytes = new byte[byteCount];
        Marshal.Copy(bitmap.GetPixels(), bytes, 0, byteCount);
        return bytes;
    }
}
