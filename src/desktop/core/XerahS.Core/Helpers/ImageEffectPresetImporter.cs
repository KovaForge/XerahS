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

using ShareX.ImageEditor.Core.ImageEffects;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.ImageEditor.Core.ImageEffects.Manipulations;
using SkiaSharp;
using XerahS.Common;
using XerahS.Common.Helpers;

namespace XerahS.Core.Helpers;

/// <summary>
/// Loads image effect presets from XerahS (.xsie) and ShareX (.sxie) files.
/// Shared by the Image Effects dialog, the CLI, and automation hosts.
/// </summary>
public static class ImageEffectPresetImporter
{
    public static readonly string[] SupportedExtensions = [".xsie", ".sxie"];

    /// <summary>
    /// Loads a preset file. Returns null when the file is missing, not a supported
    /// extension, or cannot be parsed. The preset is named after the file, as in the dialog.
    /// </summary>
    public static ImageEffectPreset? LoadPresetFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            return null;

        try
        {
            var preset = extension == ".sxie"
                ? LoadSxiePreset(filePath)
                : ImageEffectPresetSerializer.LoadXsieFile(filePath);

            if (preset != null)
            {
                preset.Name = Path.GetFileNameWithoutExtension(filePath);
            }

            return preset;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to load image effects preset.");
            return null;
        }
    }

    private static ImageEffectPreset? LoadLegacyPreset(string filePath)
    {
        var importResult = LegacyImageEffectImporter.ImportSxieFile(filePath);
        if (importResult == null || !importResult.Success)
        {
            DebugHelper.WriteLine($"[ImageEffectPresetImporter] Legacy import failed: {importResult?.ErrorMessage}");
            return null;
        }

        if (importResult.SkippedEffects.Count > 0)
        {
            DebugHelper.WriteLine($"[ImageEffectPresetImporter] Legacy import of '{filePath}' skipped: {string.Join(", ", importResult.SkippedEffects)}");
        }

        var preset = new ImageEffectPreset
        {
            Name = importResult.PresetName ?? "Imported Preset"
        };

        foreach (var mapped in importResult.MappedEffects)
        {
            var effect = CreateEffectFromMapped(mapped);
            if (effect != null)
            {
                preset.Effects.Add(effect);
            }
        }

        return preset;
    }

    private static ImageEffectPreset? LoadSxiePreset(string filePath)
    {
        try
        {
            var preset = ImageEffectPresetSerializer.LoadXsieFile(filePath);
            if (preset != null)
            {
                return preset;
            }
        }
        catch
        {
            // Legacy .sxie files may use ShareX.ImageEffectsLib schema.
        }

        return LoadLegacyPreset(filePath);
    }



    private static ImageEffect? CreateEffectFromMapped(MappedEffect mapped)
    {
        if (string.IsNullOrWhiteSpace(mapped.TargetTypeName))
            return null;

        if (mapped.TargetTypeName == nameof(RotateImageEffect))
        {
            if (mapped.Properties.TryGetValue("Angle", out var angleValue))
            {
                var angle = ReadSingle(angleValue, 0f);
                return RotateImageEffect.Custom(angle);
            }
        }

        if (mapped.TargetTypeName == nameof(FlipImageEffect))
        {
            bool horizontal = mapped.Properties.TryGetValue("Horizontal", out var horizontalValue) && Convert.ToBoolean(horizontalValue);
            bool vertical = mapped.Properties.TryGetValue("Vertical", out var verticalValue) && Convert.ToBoolean(verticalValue);

            if (vertical && !horizontal)
                return FlipImageEffect.Vertical;

            return FlipImageEffect.Horizontal;
        }

        if (mapped.TargetTypeName == nameof(ResizeImageEffect))
        {
            int width = mapped.Properties.TryGetValue("_width", out var widthValue) ? ReadInt(widthValue, 0) : 0;
            int height = mapped.Properties.TryGetValue("_height", out var heightValue) ? ReadInt(heightValue, 0) : 0;
            return new ResizeImageEffect
            {
                Width = width,
                Height = height
            };
        }

        if (mapped.TargetTypeName == nameof(DrawBackgroundEffect))
        {
            var background = new DrawBackgroundEffect();
            if (mapped.Properties.TryGetValue("Color", out var colorValue) && colorValue is SKColor color)
                background.Color = color;

            if (mapped.Properties.TryGetValue("GradientStops", out var stopsValue) &&
                stopsValue is IEnumerable<LegacyGradientStop> stops)
            {
                background.UseGradient = true;
                background.GradientStops = stops.Select(stop => new DrawingGradientStop(stop.Color, stop.Location)).ToList();
                if (mapped.Properties.TryGetValue("GradientType", out var gradientTypeValue) &&
                    Enum.TryParse(gradientTypeValue?.ToString(), ignoreCase: true, out DrawingGradientType gradientType))
                {
                    background.GradientType = gradientType;
                }
            }

            return background;
        }

        var assembly = typeof(ImageEffect).Assembly;
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name.Equals(mapped.TargetTypeName, StringComparison.Ordinal));
        if (type == null)
            return null;

        if (Activator.CreateInstance(type) is not ImageEffect effect)
            return null;

        ApplyMappedProperties(effect, mapped.Properties);
        return effect;
    }

    private static void ApplyMappedProperties(ImageEffect effect, Dictionary<string, object?> properties)
    {
        var type = effect.GetType();

        foreach (var pair in properties)
        {
            var property = type.GetProperty(pair.Key);
            if (property == null || !property.CanWrite)
                continue;

            var converted = ConvertPropertyValue(pair.Value, property.PropertyType);
            property.SetValue(effect, converted);
        }
    }

    private static object? ConvertPropertyValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        if (value is Newtonsoft.Json.Linq.JToken token)
            return token.ToObject(targetType);

        if (targetType == typeof(SKColor))
        {
            if (value is SKColor color)
                return color;
        }

        if (targetType.IsEnum)
        {
            if (value is string text)
                return Enum.Parse(targetType, text, ignoreCase: true);

            return Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType);
    }

    private static float ReadSingle(object? value, float fallback)
    {
        if (value == null)
            return fallback;

        if (value is Newtonsoft.Json.Linq.JToken token)
            return token.ToObject<float>();

        return Convert.ToSingle(value);
    }

    private static int ReadInt(object? value, int fallback)
    {
        if (value == null)
            return fallback;

        if (value is Newtonsoft.Json.Linq.JToken token)
            return token.ToObject<int>();

        return Convert.ToInt32(value);
    }
}
