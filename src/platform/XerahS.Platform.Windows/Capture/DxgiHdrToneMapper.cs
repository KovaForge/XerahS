#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using SkiaSharp;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.WIC;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiHdrToneMapper
{
    public static bool IsHdrFormat(Format format) =>
        format is Format.R16G16B16A16_Float or Format.R10G10B10A2_UNorm;

    public static unsafe SKBitmap? TryConvertToBgra(MappedSubresource mapped, Texture2DDescription description)
    {
        if (!IsHdrFormat(description.Format))
            return null;

        try
        {
            return ConvertWithWindowsToneMapper(mapped, description);
        }
        catch (Exception ex)
        {
            XerahS.Common.DebugHelper.WriteLine($"DxgiHdrToneMapper: WIC tone-map failed ({ex.Message}); using clip fallback.");
            return ConvertWithClipFallback(mapped, description);
        }
    }

    private static unsafe SKBitmap ConvertWithWindowsToneMapper(MappedSubresource mapped, Texture2DDescription description)
    {
        Guid sourcePixelFormat = description.Format switch
        {
            Format.R16G16B16A16_Float => PixelFormat.Format64bppRGBAHalf,
            Format.R10G10B10A2_UNorm => PixelFormat.Format32bppR10G10B10A2HDR10,
            _ => throw new NotSupportedException($"Unsupported HDR capture format: {description.Format}")
        };

        uint width = description.Width;
        uint height = description.Height;
        uint sourceBufferSize = checked(mapped.RowPitch * height);

        using IWICImagingFactory factory = new IWICImagingFactory();
        using IWICImagingFactory3 factory3 = factory.QueryInterface<IWICImagingFactory3>();
        using IWICBitmap sourceBitmap = factory.CreateBitmapFromMemory(
            width,
            height,
            sourcePixelFormat,
            mapped.RowPitch,
            sourceBufferSize,
            mapped.DataPointer.ToPointer());
        using IWICBitmapToneMapper toneMapper = factory3.CreateBitmapToneMapper();

        toneMapper.InitializeForSdrTarget(
            sourceBitmap,
            PixelFormat.Format32bppBGRA,
            BitmapToneMappingMode.ToneMappingMode_Default);

        var bitmap = new SKBitmap((int)width, (int)height, SKColorType.Bgra8888, SKAlphaType.Premul);
        uint destinationStride = (uint)bitmap.RowBytes;
        uint destinationBufferSize = checked(destinationStride * height);
        toneMapper.CopyPixels(
            new RectI(0, 0, (int)width, (int)height),
            destinationStride,
            destinationBufferSize,
            bitmap.GetPixels());
        return bitmap;
    }

    private static unsafe SKBitmap ConvertWithClipFallback(MappedSubresource mapped, Texture2DDescription description)
    {
        int width = (int)description.Width;
        int height = (int)description.Height;
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        byte* destination = (byte*)bitmap.GetPixels();
        int destinationStride = bitmap.RowBytes;

        for (int y = 0; y < height; y++)
        {
            byte* sourceRow = (byte*)mapped.DataPointer + (y * mapped.RowPitch);
            byte* destinationRow = destination + (y * destinationStride);

            for (int x = 0; x < width; x++)
            {
                float red;
                float green;
                float blue;

                if (description.Format == Format.R16G16B16A16_Float)
                {
                    ushort* sourcePixel = (ushort*)(sourceRow + (x * 8));
                    red = (float)BitConverter.UInt16BitsToHalf(sourcePixel[0]);
                    green = (float)BitConverter.UInt16BitsToHalf(sourcePixel[1]);
                    blue = (float)BitConverter.UInt16BitsToHalf(sourcePixel[2]);
                }
                else
                {
                    uint packedPixel = *(uint*)(sourceRow + (x * 4));
                    red = (packedPixel & 0x3FF) / 1023f;
                    green = ((packedPixel >> 10) & 0x3FF) / 1023f;
                    blue = ((packedPixel >> 20) & 0x3FF) / 1023f;
                }

                byte* destinationPixel = destinationRow + (x * 4);
                destinationPixel[0] = LinearToSrgbByte(blue);
                destinationPixel[1] = LinearToSrgbByte(green);
                destinationPixel[2] = LinearToSrgbByte(red);
                destinationPixel[3] = 255;
            }
        }

        return bitmap;
    }

    internal static byte LinearToSrgbByte(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        float encoded = value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;

        return (byte)Math.Clamp((int)MathF.Round(encoded * 255f), 0, 255);
    }
}
