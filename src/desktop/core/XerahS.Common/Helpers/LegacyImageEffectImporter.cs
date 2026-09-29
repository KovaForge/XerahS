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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SkiaSharp;
using System.IO.Compression;
using XerahS.Common;

namespace XerahS.Common.Helpers;

/// <summary>
/// Imports legacy ShareX .sxie image effect presets.
/// Maps compatible effects to ShareX.ImageEditor.ImageEffects classes.
/// </summary>
public static class LegacyImageEffectImporter
{
    private const string ConfigFileName = "Config.json";

    /// <summary>
    /// Maps legacy ShareX.ImageEffectsLib type names to their property schema.
    /// Key: legacy class name (e.g., "Brightness")
    /// Value: tuple of (target type name in ShareX.Editor, property mappings)
    /// </summary>
    private static readonly Dictionary<string, EffectMapping> SupportedEffects = new()
    {
        // Adjustments
        ["Brightness"] = new("BrightnessImageEffect", new() { ["Value"] = "Amount" }),
        ["Contrast"] = new("ContrastImageEffect", new() { ["Value"] = "Amount" }),
        ["Hue"] = new("HueImageEffect", new() { ["Value"] = "Amount" }),
        ["Saturation"] = new("SaturationImageEffect", new() { ["Value"] = "Amount" }),
        ["Gamma"] = new("GammaImageEffect", new() { ["Value"] = "Amount" }),
        ["Alpha"] = new("AlphaImageEffect", new() { ["Value"] = "Amount" }),
        ["Colorize"] = new("ColorizeImageEffect", new() { ["Color"] = "Color", ["Strength"] = "Strength" }),
        ["ReplaceColor"] = new("ReplaceColorImageEffect", new() { ["SourceColor"] = "TargetColor", ["TargetColor"] = "ReplaceColor", ["Threshold"] = "Tolerance" }),
        ["SelectiveColor"] = new("SelectiveColorImageEffect", new()), // Complex, needs special handling
        
        // Filters
        ["Inverse"] = new("InvertImageEffect", new()),
        ["Grayscale"] = new("GrayscaleImageEffect", new() { ["Percentage"] = "Strength" }),
        ["BlackWhite"] = new("BlackAndWhiteImageEffect", new()),
        ["Sepia"] = new("SepiaImageEffect", new()),
        ["Polaroid"] = new("PolaroidImageEffect", new()),
        
        // Transforms
        ["Flip"] = new("FlipImageEffect", new() { ["Horizontally"] = "Horizontal", ["Vertically"] = "Vertical" }),
        ["Rotate"] = new("RotateImageEffect", new() { ["Angle"] = "Angle" }),
        ["Resize"] = new("ResizeImageEffect", new() { ["Width"] = "_width", ["Height"] = "_height" }),
        ["AutoCrop"] = new("AutoCropImageEffect", new()),
    };

    /// <summary>
    /// Legacy effects whose schema needs more than a property rename
    /// (padding strings, 0-1 opacity, point offsets, gradients).
    /// </summary>
    private static readonly Dictionary<string, Func<JObject, MappedEffect?>> CustomMappers = new()
    {
        ["Canvas"] = MapCanvas,
        ["DrawBackground"] = MapDrawBackground,
        ["Shadow"] = MapShadow,
    };

    /// <summary>
    /// Import an .sxie file and return the preset data as JSON compatible with ShareX.ImageEditor.
    /// </summary>
    public static LegacyPresetImportResult? ImportSxieFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            string? configJson = ExtractConfigJson(filePath);
            if (string.IsNullOrEmpty(configJson))
                return null;

            return ImportFromJson(configJson);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return new LegacyPresetImportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract Config.json from .sxie ZIP archive.
    /// </summary>
    private static string? ExtractConfigJson(string sxieFilePath)
    {
        using var archive = ZipFile.OpenRead(sxieFilePath);
        var configEntry = archive.Entries.FirstOrDefault(e => 
            e.FullName.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase));

        if (configEntry == null)
            return null;

        using var stream = configEntry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parse legacy JSON and convert to new format.
    /// </summary>
    public static LegacyPresetImportResult ImportFromJson(string json)
    {
        var result = new LegacyPresetImportResult();

        try
        {
            var legacyPreset = JObject.Parse(json);
            
            result.PresetName = legacyPreset["Name"]?.ToString() ?? "Imported Preset";
            
            var effectsArray = legacyPreset["Effects"] as JArray;
            if (effectsArray == null)
            {
                result.Success = true;
                return result;
            }

            foreach (var effectToken in effectsArray)
            {
                var effectObj = effectToken as JObject;
                if (effectObj == null) continue;

                var typeString = effectObj["$type"]?.ToString();
                if (string.IsNullOrEmpty(typeString)) continue;

                // Parse type: "ShareX.ImageEffectsLib.Brightness, ShareX.ImageEffectsLib"
                var typeParts = typeString.Split(',');
                if (typeParts.Length < 1) continue;

                var fullTypeName = typeParts[0].Trim();
                var className = fullTypeName.Split('.').LastOrDefault();

                if (string.IsNullOrEmpty(className)) continue;

                // Check if enabled (default true)
                var enabled = effectObj["Enabled"]?.Value<bool>() ?? true;
                if (!enabled)
                {
                    result.SkippedEffects.Add($"{className} (disabled)");
                    continue;
                }

                if (CustomMappers.TryGetValue(className, out var customMapper))
                {
                    var mappedEffect = customMapper(effectObj);
                    if (mappedEffect != null)
                    {
                        result.MappedEffects.Add(mappedEffect);
                    }
                    else
                    {
                        result.SkippedEffects.Add($"{className} (unsupported settings)");
                    }
                }
                else if (SupportedEffects.TryGetValue(className, out var mapping))
                {
                    var mappedEffect = MapEffect(effectObj, className, mapping);
                    if (mappedEffect != null)
                    {
                        result.MappedEffects.Add(mappedEffect);
                    }
                }
                else
                {
                    result.SkippedEffects.Add(className);
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            DebugHelper.WriteException(ex);
        }

        return result;
    }

    private static MappedEffect? MapEffect(JObject effectObj, string legacyClassName, EffectMapping mapping)
    {
        var mapped = new MappedEffect
        {
            TargetTypeName = mapping.TargetTypeName,
            Properties = new Dictionary<string, object?>()
        };

        foreach (var propMap in mapping.PropertyMappings)
        {
            var legacyPropName = propMap.Key;
            var targetPropName = propMap.Value;

            var value = effectObj[legacyPropName];
            if (value != null)
            {
                // Handle color conversion
                if (targetPropName.Contains("Color", StringComparison.OrdinalIgnoreCase))
                {
                    var colorValue = ParseLegacyColor(value.ToString());
                    mapped.Properties[targetPropName] = colorValue;
                }
                else
                {
                    mapped.Properties[targetPropName] = value.ToObject<object>();
                }
            }
        }

        return mapped;
    }

    /// <summary>ShareX Canvas: "Margin" is a Padding string "Left, Top, Right, Bottom".</summary>
    private static MappedEffect? MapCanvas(JObject effectObj)
    {
        // Percentage margins depend on the image size at apply time; there is no equivalent.
        if (string.Equals(effectObj["MarginMode"]?.ToString(), "PercentageOfCanvas", StringComparison.OrdinalIgnoreCase))
            return null;

        int[] margin = ParseIntList(effectObj["Margin"]?.ToString(), 4);
        return new MappedEffect
        {
            TargetTypeName = "ResizeCanvasImageEffect",
            Properties = new Dictionary<string, object?>
            {
                ["Left"] = margin[0],
                ["Top"] = margin[1],
                ["Right"] = margin[2],
                ["Bottom"] = margin[3],
                ["BackgroundColor"] = ParseLegacyColor(effectObj["Color"]?.ToString())
            }
        };
    }

    /// <summary>ShareX DrawBackground: solid color or a GradientInfo with percentage stops.</summary>
    private static MappedEffect MapDrawBackground(JObject effectObj)
    {
        var properties = new Dictionary<string, object?>
        {
            ["Color"] = ParseLegacyColor(effectObj["Color"]?.ToString() ?? "Black")
        };

        bool useGradient = effectObj["UseGradient"]?.Value<bool>() ?? false;
        if (useGradient && effectObj["Gradient"] is JObject gradient && gradient["Colors"] is JArray colors)
        {
            var stops = new List<LegacyGradientStop>();
            foreach (var colorToken in colors.OfType<JObject>())
            {
                stops.Add(new LegacyGradientStop
                {
                    Color = ParseLegacyColor(colorToken["Color"]?.ToString()),
                    Location = colorToken["Location"]?.Value<float>() ?? 0f
                });
            }

            properties["UseGradient"] = true;
            properties["GradientType"] = gradient["Type"]?.ToString() ?? "Vertical";
            properties["GradientStops"] = stops;
        }

        return new MappedEffect
        {
            TargetTypeName = "DrawBackgroundEffect",
            Properties = properties
        };
    }

    /// <summary>ShareX Shadow: Opacity is 0-1 and Offset is a Point string "X, Y".</summary>
    private static MappedEffect MapShadow(JObject effectObj)
    {
        int[] offset = ParseIntList(effectObj["Offset"]?.ToString() ?? "5, 5", 2);
        float opacity = effectObj["Opacity"]?.Value<float>() ?? 0.6f;
        return new MappedEffect
        {
            TargetTypeName = "ShadowImageEffect",
            Properties = new Dictionary<string, object?>
            {
                ["Opacity"] = Math.Clamp(opacity * 100f, 0f, 100f),
                ["Size"] = effectObj["Size"]?.Value<int>() ?? 10,
                ["Color"] = ParseLegacyColor(effectObj["Color"]?.ToString() ?? "Black"),
                ["OffsetX"] = offset[0],
                ["OffsetY"] = offset[1],
                ["AutoResize"] = effectObj["AutoResize"]?.Value<bool>() ?? true
            }
        };
    }

    private static int[] ParseIntList(string? text, int count)
    {
        var values = new int[count];
        if (string.IsNullOrWhiteSpace(text))
            return values;

        string[] parts = text.Split(',');
        for (int i = 0; i < count && i < parts.Length; i++)
        {
            int.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out values[i]);
        }

        return values;
    }

    /// <summary>
    /// Parse legacy color format: "A, R, G, B", "R, G, B", "#hex", or a named color.
    /// </summary>
    internal static SKColor ParseLegacyColor(string? colorString)
    {
        if (string.IsNullOrEmpty(colorString))
            return SKColors.Transparent;

        // Named colors (System.Drawing names such as "Transparent", "Black", "Gold")
        var namedColor = typeof(SKColors).GetField(colorString.Trim(),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
        if (namedColor?.GetValue(null) is SKColor named)
            return named;

        if (colorString.TrimStart().StartsWith('#') && SKColor.TryParse(colorString.Trim(), out var hexColor))
            return hexColor;

        // "A, R, G, B" format
        var parts = colorString.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length == 4 &&
            byte.TryParse(parts[0], out var a) &&
            byte.TryParse(parts[1], out var r) &&
            byte.TryParse(parts[2], out var g) &&
            byte.TryParse(parts[3], out var b))
        {
            return new SKColor(r, g, b, a);
        }

        // "R, G, B" format
        if (parts.Length == 3 &&
            byte.TryParse(parts[0], out r) &&
            byte.TryParse(parts[1], out g) &&
            byte.TryParse(parts[2], out b))
        {
            return new SKColor(r, g, b);
        }

        return SKColors.Transparent;
    }
}

/// <summary>
/// Result of importing a legacy preset.
/// </summary>
public class LegacyPresetImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string PresetName { get; set; } = "";
    public List<MappedEffect> MappedEffects { get; set; } = new();
    public List<string> SkippedEffects { get; set; } = new();
}

/// <summary>
/// A mapped effect ready for instantiation.
/// </summary>
public class MappedEffect
{
    public string TargetTypeName { get; set; } = "";
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// A legacy ShareX gradient stop. <see cref="Location"/> is a percentage (0-100).
/// </summary>
public class LegacyGradientStop
{
    public SKColor Color { get; set; }
    public float Location { get; set; }
}

/// <summary>
/// Describes how to map a legacy effect to a new one.
/// </summary>
internal record EffectMapping(string TargetTypeName, Dictionary<string, string> PropertyMappings);

