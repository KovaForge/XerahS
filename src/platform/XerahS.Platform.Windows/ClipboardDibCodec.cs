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
using SkiaSharp;

namespace XerahS.Platform.Windows;

internal static class ClipboardDibCodec
{
    private const int BitmapInfoHeaderSize = 40;
    private const int BitmapV5HeaderSize = 124;
    private const int BiRgb = 0;
    private const int BiBitfields = 3;
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
        writer.Write((uint)BiBitfields);
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
        writer.Write((uint)BiRgb);
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

        try
        {
            int headerSize = BitConverter.ToInt32(dibV5, 0);
            int width = BitConverter.ToInt32(dibV5, 4);
            int height = BitConverter.ToInt32(dibV5, 8);
            short planes = BitConverter.ToInt16(dibV5, 12);
            short bitCount = BitConverter.ToInt16(dibV5, 14);
            int compression = BitConverter.ToInt32(dibV5, 16);
            uint sizeImage = BitConverter.ToUInt32(dibV5, 20);
            uint redMask = BitConverter.ToUInt32(dibV5, 40);
            uint greenMask = BitConverter.ToUInt32(dibV5, 44);
            uint blueMask = BitConverter.ToUInt32(dibV5, 48);
            uint alphaMask = BitConverter.ToUInt32(dibV5, 52);

            if (headerSize < BitmapV5HeaderSize || width <= 0 || height == 0 || planes != 1 || bitCount != 32 ||
                (compression != BiRgb && compression != BiBitfields))
            {
                return null;
            }

            if (compression == BiBitfields &&
                (redMask != RedMask || greenMask != GreenMask || blueMask != BlueMask || alphaMask != AlphaMask))
            {
                return null;
            }

            return DecodeBgra32Pixels(dibV5, headerSize, width, height, sizeImage);
        }
        catch
        {
            return null;
        }
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
            short planes = BitConverter.ToInt16(dib, 12);
            short bitCount = BitConverter.ToInt16(dib, 14);
            int compression = BitConverter.ToInt32(dib, 16);
            uint sizeImage = BitConverter.ToUInt32(dib, 20);

            if (headerSize < BitmapInfoHeaderSize || width <= 0 || height == 0 || planes != 1 || bitCount != 32 ||
                (compression != BiRgb && compression != BiBitfields))
            {
                return null;
            }

            int pixelsOffset = headerSize;
            if (compression == BiBitfields && headerSize == BitmapInfoHeaderSize)
            {
                pixelsOffset += 12;
            }

            return DecodeBgra32Pixels(dib, pixelsOffset, width, height, sizeImage);
        }
        catch
        {
            return null;
        }
    }

    private static SKBitmap? DecodeBgra32Pixels(byte[] dib, int pixelsOffset, int width, int signedHeight, uint sizeImage)
    {
        int rows = Math.Abs(signedHeight);
        long minimumRowBytes = checked((long)width * 4);
        long rowBytes = sizeImage > 0 ? sizeImage / rows : minimumRowBytes;
        long pixelDataSize = checked(rowBytes * rows);

        if (pixelsOffset < 0 || rowBytes < minimumRowBytes || rowBytes > int.MaxValue ||
            pixelsOffset + pixelDataSize > dib.LongLength)
        {
            return null;
        }

        SKImageInfo info = new(width, rows, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        IntPtr pixels = bitmap.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            bitmap.Dispose();
            return null;
        }

        bool topDown = signedHeight < 0;
        for (int y = 0; y < rows; y++)
        {
            int sourceRow = topDown ? y : rows - 1 - y;
            long sourceOffset = pixelsOffset + (sourceRow * rowBytes);
            Marshal.Copy(dib, checked((int)sourceOffset), pixels + (y * bitmap.RowBytes), checked((int)minimumRowBytes));
        }

        return bitmap;
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
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
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
