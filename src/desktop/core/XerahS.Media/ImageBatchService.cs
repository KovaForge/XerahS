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

using System.Globalization;
using SkiaSharp;

namespace XerahS.Media;

public enum ImageBatchOperation
{
    Resize,
    Convert,
    Watermark
}

public enum ImageResizeMode
{
    /// <summary>Scale to cover the target and crop the overflow (keeps aspect ratio).</summary>
    Fill,
    /// <summary>Scale to fit inside the target, padding with transparency (keeps aspect ratio).</summary>
    Fit,
    /// <summary>Scale to exactly the target size (ignores aspect ratio).</summary>
    Stretch
}

public enum ImageBatchOutputFormat
{
    Png,
    Jpeg,
    Webp
}

public enum ImageWatermarkType
{
    Text,
    Image
}

public enum ImageWatermarkPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public sealed record ImageWatermarkOptions
{
    public ImageWatermarkType Type { get; init; } = ImageWatermarkType.Text;
    public string Text { get; init; } = string.Empty;
    public ImageWatermarkPosition Position { get; init; } = ImageWatermarkPosition.BottomRight;
    public int Margin { get; init; } = 16;
    /// <summary>0-100.</summary>
    public int Opacity { get; init; } = 60;
    public float TextSize { get; init; } = 32;
    public SKColor TextColor { get; init; } = SKColors.White;
    public string? ImagePath { get; init; }
    /// <summary>Watermark image size as a percentage (1-100) of the photo's shorter fit.</summary>
    public int ImageScale { get; init; } = 20;
    public float Rotation { get; init; }
}

public sealed record ImageBatchOptions
{
    public ImageBatchOperation Operation { get; init; } = ImageBatchOperation.Convert;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public ImageResizeMode ResizeMode { get; init; } = ImageResizeMode.Fit;
    public ImageWatermarkOptions Watermark { get; init; } = new();
    public ImageBatchOutputFormat Format { get; init; } = ImageBatchOutputFormat.Png;
    /// <summary>1-100, used for JPEG and WebP.</summary>
    public int Quality { get; init; } = 90;
    /// <summary>Fill for transparent pixels when saving JPEG (which has no alpha).</summary>
    public SKColor BackgroundColor { get; init; } = SKColors.White;
    /// <summary>Output folder; empty keeps each image next to its source.</summary>
    public string OutputFolder { get; init; } = string.Empty;
    /// <summary>File name without extension; $filename is the source name.</summary>
    public string FileNamePattern { get; init; } = "$filename";
}

/// <summary>
/// Batch image tools (ShareX Image Resizer / Image Converter / Image Watermark, ported to SkiaSharp).
/// </summary>
public static class ImageBatchService
{
    public const int MaxPreviewDimension = 1600;

    public static string GetDefaultFileNamePattern(ImageBatchOperation operation) => operation switch
    {
        ImageBatchOperation.Resize => "$filename_resized",
        ImageBatchOperation.Watermark => "$filename_watermarked",
        _ => "$filename"
    };

    public static string GetExtension(ImageBatchOutputFormat format) => format switch
    {
        ImageBatchOutputFormat.Jpeg => ".jpg",
        ImageBatchOutputFormat.Webp => ".webp",
        _ => ".png"
    };

    /// <summary>Output path for <paramref name="sourcePath"/>. Throws if it would overwrite the source.</summary>
    public static string GetOutputPath(string sourcePath, ImageBatchOptions options)
    {
        string folder = string.IsNullOrWhiteSpace(options.OutputFolder)
            ? Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty
            : options.OutputFolder;
        string pattern = string.IsNullOrWhiteSpace(options.FileNamePattern) ? "$filename" : options.FileNamePattern;
        string name = pattern.Replace("$filename", Path.GetFileNameWithoutExtension(sourcePath), StringComparison.OrdinalIgnoreCase);
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        string output = Path.Combine(folder, name + GetExtension(options.Format));
        if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(sourcePath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{Path.GetFileName(sourcePath)}' would be overwritten. Change the file name pattern or output folder.");
        }

        return output;
    }

    /// <summary>Processes one file and returns the written path.</summary>
    public static string ProcessFile(string sourcePath, ImageBatchOptions options)
    {
        using SKBitmap source = Load(sourcePath);
        using SKBitmap result = Apply(source, options);
        string outputPath = GetOutputPath(sourcePath, options);
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] encoded = Encode(result, options);
        File.WriteAllBytes(outputPath, encoded);
        return outputPath;
    }

    /// <summary>
    /// Encoded preview of <paramref name="sourcePath"/> with the options applied, capped at
    /// <see cref="MaxPreviewDimension"/> so large batches stay responsive.
    /// </summary>
    public static byte[] CreatePreview(string sourcePath, ImageBatchOptions options)
    {
        using SKBitmap source = Load(sourcePath);
        using SKBitmap result = Apply(source, options);
        using SKBitmap preview = ScaleDown(result, MaxPreviewDimension);
        return Encode(preview, options);
    }

    public static SKBitmap Load(string path)
    {
        SKBitmap? decoded = SKBitmap.Decode(path);
        if (decoded == null)
        {
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' is not a supported image.");
        }

        if (decoded.ColorType == SKColorType.Rgba8888 || decoded.ColorType == SKColorType.Bgra8888)
        {
            return decoded;
        }

        SKBitmap converted = decoded.Copy(SKColorType.Rgba8888);
        decoded.Dispose();
        return converted;
    }

    public static SKBitmap Apply(SKBitmap source, ImageBatchOptions options)
    {
        return options.Operation switch
        {
            ImageBatchOperation.Resize => Resize(source, options.Width, options.Height, options.ResizeMode),
            ImageBatchOperation.Watermark => ApplyWatermark(source, options.Watermark),
            _ => source.Copy()
        };
    }

    public static SKBitmap Resize(SKBitmap source, int width, int height, ImageResizeMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);

        SKRect sourceRect = SKRect.Create(source.Width, source.Height);
        SKRect destinationRect = SKRect.Create(width, height);
        switch (mode)
        {
            case ImageResizeMode.Fill:
                sourceRect = GetFillSourceRect(source.Width, source.Height, width, height);
                break;
            case ImageResizeMode.Fit:
                destinationRect = GetFitDestinationRect(source.Width, source.Height, width, height);
                break;
        }

        using SKImage image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, sourceRect, destinationRect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        return output;
    }

    public static SKBitmap ApplyWatermark(SKBitmap source, ImageWatermarkOptions options)
    {
        var output = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);
        DrawAt(canvas, source, 0, 0);

        using SKBitmap? watermark = options.Type == ImageWatermarkType.Text
            ? CreateTextWatermark(options)
            : CreateImageWatermark(options, source.Width, source.Height);
        if (watermark == null)
        {
            return output;
        }

        using SKBitmap rotated = Rotate(watermark, options.Rotation);
        SKPointI location = GetLocation(source.Width, source.Height, rotated.Width, rotated.Height, options.Position, options.Margin);
        DrawAt(canvas, rotated, location.X, location.Y);
        return output;
    }

    public static byte[] Encode(SKBitmap bitmap, ImageBatchOptions options)
    {
        int quality = Math.Clamp(options.Quality, 1, 100);
        if (options.Format == ImageBatchOutputFormat.Jpeg)
        {
            using var flattened = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(flattened))
            {
                canvas.Clear(options.BackgroundColor.WithAlpha(255));
                DrawAt(canvas, bitmap, 0, 0);
            }

            return EncodeImage(flattened, SKEncodedImageFormat.Jpeg, quality);
        }

        return options.Format == ImageBatchOutputFormat.Webp
            ? EncodeImage(bitmap, SKEncodedImageFormat.Webp, quality)
            : EncodeImage(bitmap, SKEncodedImageFormat.Png, 100);
    }

    public static bool TryParseColor(string? text, out SKColor color)
    {
        color = SKColors.White;
        return !string.IsNullOrWhiteSpace(text) && SKColor.TryParse(text.Trim(), out color);
    }

    private static byte[] EncodeImage(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData? data = image.Encode(format, quality);
        return data?.ToArray() ?? throw new InvalidOperationException($"Could not encode the image as {format}.");
    }

    private static SKBitmap ScaleDown(SKBitmap bitmap, int maxDimension)
    {
        int largest = Math.Max(bitmap.Width, bitmap.Height);
        if (largest <= maxDimension)
        {
            return bitmap.Copy();
        }

        double scale = maxDimension / (double)largest;
        return Resize(bitmap, Math.Max(1, (int)Math.Round(bitmap.Width * scale)),
            Math.Max(1, (int)Math.Round(bitmap.Height * scale)), ImageResizeMode.Stretch);
    }

    private static SKRect GetFillSourceRect(int sourceWidth, int sourceHeight, int width, int height)
    {
        double sourceAspect = sourceWidth / (double)sourceHeight;
        double destinationAspect = width / (double)height;
        if (sourceAspect > destinationAspect)
        {
            float cropWidth = (float)(sourceHeight * destinationAspect);
            return SKRect.Create((sourceWidth - cropWidth) / 2f, 0, cropWidth, sourceHeight);
        }

        float cropHeight = (float)(sourceWidth / destinationAspect);
        return SKRect.Create(0, (sourceHeight - cropHeight) / 2f, sourceWidth, cropHeight);
    }

    private static SKRect GetFitDestinationRect(int sourceWidth, int sourceHeight, int width, int height)
    {
        double scale = Math.Min(width / (double)sourceWidth, height / (double)sourceHeight);
        int fitWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int fitHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return SKRect.Create((width - fitWidth) / 2, (height - fitHeight) / 2, fitWidth, fitHeight);
    }

    private static SKBitmap? CreateTextWatermark(ImageWatermarkOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Text))
        {
            return null;
        }

        using SKTypeface typeface = SKTypeface.FromFamilyName(null) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, Math.Max(1, options.TextSize)) { Edging = SKFontEdging.Antialias };
        byte alpha = (byte)Math.Round(Math.Clamp(options.Opacity, 0, 100) / 100d * 255);
        using var paint = new SKPaint { IsAntialias = true, Color = options.TextColor.WithAlpha(alpha) };

        string[] lines = options.Text.Replace("\r\n", "\n").Split('\n');
        float lineHeight = font.Spacing;
        float width = lines.Max(line => font.MeasureText(line));
        var bitmap = new SKBitmap(new SKImageInfo(
            Math.Max(1, (int)Math.Ceiling(width) + 4),
            Math.Max(1, (int)Math.Ceiling(lineHeight * lines.Length) + 4),
            SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], 2, 2 - font.Metrics.Ascent + i * lineHeight, SKTextAlign.Left, font, paint);
        }

        return bitmap;
    }

    private static SKBitmap? CreateImageWatermark(ImageWatermarkOptions options, int canvasWidth, int canvasHeight)
    {
        if (string.IsNullOrWhiteSpace(options.ImagePath) || !File.Exists(options.ImagePath))
        {
            return null;
        }

        using SKBitmap? image = SKBitmap.Decode(options.ImagePath);
        if (image == null || image.Width < 1 || image.Height < 1)
        {
            return null;
        }

        double percentage = Math.Clamp(options.ImageScale, 1, 100) / 100d;
        double scale = Math.Min(canvasWidth * percentage / image.Width, canvasHeight * percentage / image.Height);
        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(image.Height * scale));

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using SKImage skImage = SKImage.FromBitmap(image);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(options.Opacity, 0, 100) / 100d * 255))
        };
        canvas.DrawImage(skImage, SKRect.Create(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        return bitmap;
    }

    private static SKBitmap Rotate(SKBitmap source, float angle)
    {
        angle %= 360;
        if (Math.Abs(angle) < 0.01f)
        {
            return source.Copy();
        }

        double radians = angle * Math.PI / 180d;
        double sin = Math.Abs(Math.Sin(radians));
        double cos = Math.Abs(Math.Cos(radians));
        int width = Math.Max(1, (int)Math.Ceiling(source.Width * cos + source.Height * sin));
        int height = Math.Max(1, (int)Math.Ceiling(source.Width * sin + source.Height * cos));
        var output = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(width / 2f, height / 2f);
        canvas.RotateDegrees(angle);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        DrawAt(canvas, source, 0, 0);
        return output;
    }

    private static void DrawAt(SKCanvas canvas, SKBitmap bitmap, float x, float y)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, x, y, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
    }

    internal static SKPointI GetLocation(int canvasWidth, int canvasHeight, int width, int height, ImageWatermarkPosition position, int margin)
    {
        int left = margin;
        int centerX = (canvasWidth - width) / 2;
        int right = canvasWidth - width - margin;
        int top = margin;
        int centerY = (canvasHeight - height) / 2;
        int bottom = canvasHeight - height - margin;
        return position switch
        {
            ImageWatermarkPosition.TopLeft => new SKPointI(left, top),
            ImageWatermarkPosition.TopCenter => new SKPointI(centerX, top),
            ImageWatermarkPosition.TopRight => new SKPointI(right, top),
            ImageWatermarkPosition.MiddleLeft => new SKPointI(left, centerY),
            ImageWatermarkPosition.Center => new SKPointI(centerX, centerY),
            ImageWatermarkPosition.MiddleRight => new SKPointI(right, centerY),
            ImageWatermarkPosition.BottomLeft => new SKPointI(left, bottom),
            ImageWatermarkPosition.BottomCenter => new SKPointI(centerX, bottom),
            _ => new SKPointI(right, bottom)
        };
    }
}
