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
using ShareX.ImageEditor.Core.ImageEffects;
using SkiaSharp;
using System.IO.Compression;
using System.Reflection;
using XerahS.Common;

namespace XerahS.Core.Helpers;

public static class ImageEffectPresetSerializer
{
    private const string ConfigFileName = "Config.json";

    public static void SaveXsieFile(string filePath, ImageEffectPreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));

        var payload = new XsiePreset
        {
            Name = preset.Name,
            Effects = preset.Effects ?? new List<ImageEffect>()
        };

        string json = JsonConvert.SerializeObject(payload, Formatting.Indented, CreateSerializerSettings());
        WriteZip(filePath, json);
    }

    public static ImageEffectPreset? LoadXsieFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var configJson = ExtractConfigJson(filePath);
            if (string.IsNullOrWhiteSpace(configJson))
                return null;

            var payload = JsonConvert.DeserializeObject<XsiePreset>(configJson, CreateSerializerSettings());
            if (payload == null)
                return null;

            return new ImageEffectPreset
            {
                Name = payload.Name ?? "Preset",
                Effects = payload.Effects ?? new List<ImageEffect>()
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            DebugHelper.WriteException(ex, $"ImageEffectPresetSerializer: Failed to load preset file '{filePath}'.");
            return null;
        }
    }

    internal static JsonSerializerSettings CreateSerializerSettings()
    {
        return new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            SerializationBinder = new ImageEffectSerializationBinder(),
            ContractResolver = new ImageEffectPresetContractResolver(),
            Converters = { new SkColorJsonConverter() }
        };
    }

    private static string? ExtractConfigJson(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var configEntry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase));

        if (configEntry == null)
            return null;

        using var stream = configEntry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void WriteZip(string filePath, string configJson)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(ConfigFileName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(configJson);
    }
}

internal sealed class XsiePreset
{
    public int Version { get; set; } = 1;
    public string? Name { get; set; }
    public List<ImageEffect> Effects { get; set; } = new();
}

internal sealed class ImageEffectPresetContractResolver : DefaultContractResolver
{
    private static readonly HashSet<string> IgnoredEffectMetadataProperties =
    [
        nameof(ImageEffect.HasParameters),
        "Parameters"
    ];

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        if (typeof(ImageEffect).IsAssignableFrom(member.DeclaringType) &&
            IgnoredEffectMetadataProperties.Contains(property.PropertyName ?? member.Name))
        {
            property.Ignored = true;
        }

        return property;
    }
}

/// <summary>
/// Constrains List&lt;ImageEffect&gt; serialization for settings JSON so TypeNameHandling.Auto
/// on SettingsBase cannot instantiate arbitrary $type payloads into ImageEffectPreset.Effects.
/// Reuses <see cref="ImageEffectSerializationBinder"/> for the known-type allow-list.
/// </summary>
internal sealed class ImageEffectListJsonConverter : JsonConverter<List<ImageEffect>>
{
    private static readonly ImageEffectSerializationBinder Binder = new();

    public override void WriteJson(JsonWriter writer, List<ImageEffect>? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // Serialize via binder-aware settings so $type names stay on the allow-list path.
        var settings = ImageEffectPresetSerializer.CreateSerializerSettings();
        // Avoid recursive converter invocation on List<ImageEffect>.
        // Converters is IList<JsonConverter> (no List.RemoveAll).
        for (int i = settings.Converters.Count - 1; i >= 0; i--)
        {
            if (settings.Converters[i] is ImageEffectListJsonConverter)
                settings.Converters.RemoveAt(i);
        }
        JsonSerializer.Create(settings).Serialize(writer, value);
    }

    public override List<ImageEffect>? ReadJson(
        JsonReader reader,
        Type objectType,
        List<ImageEffect>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var array = JArray.Load(reader);
        var result = new List<ImageEffect>(array.Count);
        var settings = ImageEffectPresetSerializer.CreateSerializerSettings();
        // Converters is IList<JsonConverter> (no List.RemoveAll).
        for (int i = settings.Converters.Count - 1; i >= 0; i--)
        {
            if (settings.Converters[i] is ImageEffectListJsonConverter)
                settings.Converters.RemoveAt(i);
        }
        var effectSerializer = JsonSerializer.Create(settings);

        foreach (var token in array)
        {
            if (token is not JObject obj)
                throw new JsonSerializationException("Image effect entries must be JSON objects.");

            var typeToken = obj["$type"]?.ToString();
            if (string.IsNullOrWhiteSpace(typeToken))
                throw new JsonSerializationException("Image effect entry is missing $type.");

            // Validate type against the allow-list before materializing properties.
            string typeName = typeToken;
            string? assemblyName = null;
            int comma = typeToken.IndexOf(',');
            if (comma >= 0)
            {
                typeName = typeToken[..comma].Trim();
                assemblyName = typeToken[(comma + 1)..].Trim();
            }

            Type boundType = Binder.BindToType(assemblyName, typeName);
            if (!typeof(ImageEffect).IsAssignableFrom(boundType) || boundType.IsAbstract)
                throw new JsonSerializationException($"Unsupported image effect type: {typeToken}");

            var effect = (ImageEffect?)obj.ToObject(boundType, effectSerializer)
                ?? throw new JsonSerializationException($"Failed to deserialize image effect: {typeToken}");
            result.Add(effect);
        }

        return result;
    }
}

internal sealed class ImageEffectSerializationBinder : ISerializationBinder
{
    private const string CurrentEffectsNamespacePrefix = "ShareX.ImageEditor.Core.ImageEffects.";

    private static readonly string[] LegacyEffectsNamespacePrefixes =
    [
        "ShareX.Editor.ImageEffects.",
        "XerahS.Editor.ImageEffects.",
        "ShareX.ImageEditor.ImageEffects."
    ];

    public Type BindToType(string? assemblyName, string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new JsonSerializationException("Missing type name for image effect.");

        string normalizedTypeName = NormalizeTypeName(typeName);

        var assembly = typeof(ImageEffect).Assembly;
        var type = assembly.GetType(normalizedTypeName, throwOnError: false);

        if (type == null)
            throw new JsonSerializationException($"Unknown image effect type: {typeName}");

        if (!typeof(ImageEffect).IsAssignableFrom(type) || type.IsAbstract)
            throw new JsonSerializationException($"Unsupported image effect type: {typeName}");

        return type;
    }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }

    private static string NormalizeTypeName(string typeName)
    {
        if (typeName.StartsWith(CurrentEffectsNamespacePrefix, StringComparison.Ordinal))
            return typeName;

        foreach (string legacyPrefix in LegacyEffectsNamespacePrefixes)
        {
            if (typeName.StartsWith(legacyPrefix, StringComparison.Ordinal))
            {
                return CurrentEffectsNamespacePrefix + typeName[legacyPrefix.Length..];
            }
        }

        throw new JsonSerializationException($"Unsupported image effect type: {typeName}");
    }
}

internal sealed class SkColorJsonConverter : JsonConverter<SKColor>
{
    public override void WriteJson(JsonWriter writer, SKColor value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.Alpha}, {value.Red}, {value.Green}, {value.Blue}");
    }

    public override SKColor ReadJson(JsonReader reader, Type objectType, SKColor existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String && reader.Value is string text)
        {
            return ParseColor(text);
        }

        return existingValue;
    }

    private static SKColor ParseColor(string? colorString)
    {
        if (string.IsNullOrWhiteSpace(colorString))
            return SKColors.Transparent;

        if (colorString.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return SKColors.Transparent;
        if (colorString.Equals("Black", StringComparison.OrdinalIgnoreCase))
            return SKColors.Black;
        if (colorString.Equals("White", StringComparison.OrdinalIgnoreCase))
            return SKColors.White;

        var parts = colorString.Split(',').Select(p => p.Trim()).ToArray();
        if (parts.Length == 4 &&
            byte.TryParse(parts[0], out var a) &&
            byte.TryParse(parts[1], out var r) &&
            byte.TryParse(parts[2], out var g) &&
            byte.TryParse(parts[3], out var b))
        {
            return new SKColor(r, g, b, a);
        }

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

