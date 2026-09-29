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

using SkiaSharp;

namespace XerahS.Media;

/// <summary>
/// A watermark burned into video editor exports, seeded from the screenshot image-effect preset.
/// Positions are fractions of the frame: 0 = left/top, 1 = right/bottom.
/// </summary>
public sealed class VideoWatermarkSettings
{
    public bool Enabled { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Image overlaid during export; preferred over <see cref="Text"/> when both are set.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>0.0 (transparent) to 1.0 (opaque).</summary>
    public double Opacity { get; set; } = 0.8;

    public double PositionX { get; set; } = 0.95;
    public double PositionY { get; set; } = 0.95;
    public int FontSize { get; set; } = 24;
    public string FontColor { get; set; } = "#FFFFFF";
}

/// <summary>
/// Turns watermark settings into an image the video editor can overlay. Text is rendered to a
/// transparent PNG (with a soft shadow for legibility on any background), so exports never
/// depend on ffmpeg's drawtext filter or on font files being present for ffmpeg.
/// </summary>
public static class VideoWatermarkRenderer
{
    /// <summary>
    /// Returns the overlay image path, or null when the watermark is disabled or unusable.
    /// Rendered text is written to <paramref name="workDirectory"/>; the caller deletes it.
    /// </summary>
    public static string? ResolveImage(VideoWatermarkSettings? settings, string workDirectory)
    {
        if (settings is not { Enabled: true })
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(settings.ImagePath) && File.Exists(settings.ImagePath))
        {
            return settings.ImagePath;
        }

        if (string.IsNullOrWhiteSpace(settings.Text))
        {
            return null;
        }

        Directory.CreateDirectory(workDirectory);
        string path = Path.Combine(workDirectory, "watermark-" + Guid.NewGuid().ToString("N") + ".png");
        using SKBitmap bitmap = RenderText(settings.Text, Math.Clamp(settings.FontSize, 6, 400), ParseColor(settings.FontColor));
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    public static SKBitmap RenderText(string text, float fontSize, SKColor color)
    {
        using SKTypeface typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, fontSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        float lineHeight = font.Spacing;
        float width = lines.Max(line => font.MeasureText(line));
        float shadow = MathF.Max(1, fontSize / 16f);
        int pad = (int)MathF.Ceiling(shadow * 3);

        var bitmap = new SKBitmap(
            Math.Max(1, (int)MathF.Ceiling(width) + pad * 2),
            Math.Max(1, (int)MathF.Ceiling(lineHeight * lines.Length) + pad * 2),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0, 0, 0, 160),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow),
        };
        using var textPaint = new SKPaint { IsAntialias = true, Color = color };

        float baseline = pad - font.Metrics.Ascent;
        foreach (string line in lines)
        {
            canvas.DrawText(line, pad + shadow, baseline + shadow, SKTextAlign.Left, font, shadowPaint);
            canvas.DrawText(line, pad, baseline, SKTextAlign.Left, font, textPaint);
            baseline += lineHeight;
        }

        return bitmap;
    }

    private static SKColor ParseColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SKColor.TryParse(value.Trim(), out SKColor color) ? color : SKColors.White;
}
