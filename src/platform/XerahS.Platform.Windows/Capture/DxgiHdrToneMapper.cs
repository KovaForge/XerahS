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
    private const float ToneMapKnee = 0.75f;

    public static bool IsHdrFormat(Format format) =>
        format is Format.R16G16B16A16_Float or Format.R10G10B10A2_UNorm;

    public static unsafe SKBitmap? TryConvertToBgra(
        MappedSubresource mapped,
        Texture2DDescription description,
        HdrToneMapContext context = default)
    {
        if (!IsHdrFormat(description.Format))
            return null;

        HdrToneMapContext toneMapContext = context == default ? HdrToneMapContext.Default : context.Normalize();

        try
        {
            return ConvertWithWindowsToneMapper(mapped, description);
        }
        catch (Exception ex)
        {
            XerahS.Common.DebugHelper.WriteLine($"DxgiHdrToneMapper: WIC tone-map failed ({ex.Message}); using color-managed fallback.");
            return ConvertToSrgb(mapped, description, toneMapContext);
        }
    }

    public static unsafe SKBitmap ConvertToSdrReference(
        MappedSubresource mapped,
        Texture2DDescription description,
        HdrToneMapContext context)
    {
        HdrToneMapContext toneMapContext = context.Normalize();
        int width = (int)description.Width;
        int height = (int)description.Height;
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        byte* destination = (byte*)bitmap.GetPixels();
        int destinationStride = bitmap.RowBytes;

        for (int y = 0; y < height; y++)
        {
            byte* sourceRow = (byte*)mapped.DataPointer + (y * mapped.RowPitch);
            byte* destinationRow = destination + (y * destinationStride);

            for (int x = 0; x < width; x++)
            {
                DecodeHdrChannels(sourceRow, x, description.Format, toneMapContext.SdrWhiteScale,
                    out float red, out float green, out float blue);
                bool extendedRange = !IsSdrChannel(red) || !IsSdrChannel(green) || !IsSdrChannel(blue);
                byte* destinationPixel = destinationRow + (x * 4);
                destinationPixel[0] = LinearToSrgbByte(blue);
                destinationPixel[1] = LinearToSrgbByte(green);
                destinationPixel[2] = LinearToSrgbByte(red);
                // Alpha is an internal mask: extended-range pixels must use the tone-mapped result.
                destinationPixel[3] = extendedRange ? (byte)255 : (byte)0;
            }
        }

        return bitmap;
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

    private static unsafe SKBitmap ConvertToSrgb(
        MappedSubresource mapped,
        Texture2DDescription description,
        HdrToneMapContext context)
    {
        int width = (int)description.Width;
        int height = (int)description.Height;
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        byte* destination = (byte*)bitmap.GetPixels();
        int destinationStride = bitmap.RowBytes;
        float relativePeak = Math.Clamp(
            context.PeakNits / (HdrToneMapContext.SceneReferredSdrWhiteNits * context.SdrWhiteScale),
            1.25f,
            125f);

        for (int y = 0; y < height; y++)
        {
            byte* sourceRow = (byte*)mapped.DataPointer + (y * mapped.RowPitch);
            byte* destinationRow = destination + (y * destinationStride);

            for (int x = 0; x < width; x++)
            {
                DecodeHdrChannels(sourceRow, x, description.Format, context.SdrWhiteScale,
                    out float red, out float green, out float blue);
                ToneMapAndCompressGamut(ref red, ref green, ref blue, relativePeak);

                byte* destinationPixel = destinationRow + (x * 4);
                destinationPixel[0] = LinearToSrgbByte(blue);
                destinationPixel[1] = LinearToSrgbByte(green);
                destinationPixel[2] = LinearToSrgbByte(red);
                destinationPixel[3] = 255;
            }
        }

        return bitmap;
    }

    internal static unsafe void DecodeHdrChannels(
        byte* sourceRow,
        int x,
        Format format,
        float sdrWhiteScale,
        out float red,
        out float green,
        out float blue)
    {
        if (format == Format.R16G16B16A16_Float)
        {
            ushort* sourcePixel = (ushort*)(sourceRow + (x * 8));
            red = (float)BitConverter.UInt16BitsToHalf(sourcePixel[0]) / sdrWhiteScale;
            green = (float)BitConverter.UInt16BitsToHalf(sourcePixel[1]) / sdrWhiteScale;
            blue = (float)BitConverter.UInt16BitsToHalf(sourcePixel[2]) / sdrWhiteScale;
            return;
        }

        uint packedPixel = *(uint*)(sourceRow + (x * 4));
        red = PqToLinearSrgb((packedPixel & 0x3FF) / 1023f, sdrWhiteScale);
        green = PqToLinearSrgb(((packedPixel >> 10) & 0x3FF) / 1023f, sdrWhiteScale);
        blue = PqToLinearSrgb(((packedPixel >> 20) & 0x3FF) / 1023f, sdrWhiteScale);
        ConvertRec2020ToSrgb(ref red, ref green, ref blue);
    }

    internal static void ToneMapAndCompressGamut(ref float red, ref float green, ref float blue, float relativePeak)
    {
        red = Math.Max(red, 0f);
        green = Math.Max(green, 0f);
        blue = Math.Max(blue, 0f);

        float maximum = Math.Max(red, Math.Max(green, blue));
        if (maximum <= 1f)
        {
            return;
        }

        float luminance = red * 0.2126f + green * 0.7152f + blue * 0.0722f;
        float mappedLuminance = ToneMapShoulder(luminance, relativePeak);

        if (luminance > 0.00001f)
        {
            float luminanceScale = mappedLuminance / luminance;
            red *= luminanceScale;
            green *= luminanceScale;
            blue *= luminanceScale;
        }

        maximum = Math.Max(red, Math.Max(green, blue));
        if (maximum > 1f)
        {
            float gray = Math.Clamp(mappedLuminance, 0f, 1f);
            float chromaScale = maximum > gray ? (1f - gray) / (maximum - gray) : 0f;
            red = gray + (red - gray) * chromaScale;
            green = gray + (green - gray) * chromaScale;
            blue = gray + (blue - gray) * chromaScale;
        }
    }

    internal static float ToneMapShoulder(float value, float relativePeak)
    {
        if (value <= ToneMapKnee)
        {
            return Math.Max(value, 0f);
        }

        float peak = Math.Max(relativePeak, ToneMapKnee + 0.001f);
        float normalized = Math.Clamp((value - ToneMapKnee) / (peak - ToneMapKnee), 0f, 1f);
        float curveStrength = (peak - ToneMapKnee) / (1f - ToneMapKnee);
        float shoulder = curveStrength * normalized / (1f + (curveStrength - 1f) * normalized);
        return ToneMapKnee + (1f - ToneMapKnee) * shoulder;
    }

    internal static float PqToLinearSrgb(float encoded, float sdrWhiteScale)
    {
        const float m1 = 2610f / 16384f;
        const float m2 = 2523f / 32f;
        const float c1 = 3424f / 4096f;
        const float c2 = 2413f / 128f;
        const float c3 = 2392f / 128f;

        float power = MathF.Pow(Math.Clamp(encoded, 0f, 1f), 1f / m2);
        float normalizedNits = MathF.Pow(Math.Max(power - c1, 0f) / Math.Max(c2 - c3 * power, 0.00001f), 1f / m1);
        float nits = normalizedNits * 10000f;
        return nits / (HdrToneMapContext.SceneReferredSdrWhiteNits * sdrWhiteScale);
    }

    internal static void ConvertRec2020ToSrgb(ref float red, ref float green, ref float blue)
    {
        float sourceRed = red;
        float sourceGreen = green;
        float sourceBlue = blue;

        red = 1.660491f * sourceRed - 0.587641f * sourceGreen - 0.072850f * sourceBlue;
        green = -0.124550f * sourceRed + 1.132900f * sourceGreen - 0.008349f * sourceBlue;
        blue = -0.018151f * sourceRed - 0.100579f * sourceGreen + 1.118730f * sourceBlue;
    }

    internal static bool IsSdrChannel(float value) =>
        float.IsFinite(value) && value >= 0f && value <= 1f;

    internal static byte LinearToSrgbByte(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        float encoded = value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;

        return (byte)Math.Clamp((int)MathF.Round(encoded * 255f), 0, 255);
    }
}
