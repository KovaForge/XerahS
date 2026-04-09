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
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Uploaders.CustomUploader;

public sealed class CustomUploaderXerahSMetadata
{
    [JsonProperty("instanceIds")]
    public List<string> InstanceIds { get; set; } = new();

    [JsonProperty("primaryInstanceId")]
    public string? PrimaryInstanceId { get; set; }

    [JsonIgnore]
    public bool HasBindings => InstanceIds.Count > 0 || !string.IsNullOrWhiteSpace(PrimaryInstanceId);

    public CustomUploaderXerahSMetadata Normalize()
    {
        InstanceIds = InstanceIds
            .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(PrimaryInstanceId) &&
            !InstanceIds.Contains(PrimaryInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            InstanceIds.Insert(0, PrimaryInstanceId);
        }

        if (string.IsNullOrWhiteSpace(PrimaryInstanceId) && InstanceIds.Count == 1)
        {
            PrimaryInstanceId = InstanceIds[0];
        }

        return this;
    }

    public static CustomUploaderXerahSMetadata? FromInstanceIds(IEnumerable<string>? instanceIds, string? primaryInstanceId = null)
    {
        var metadata = new CustomUploaderXerahSMetadata
        {
            PrimaryInstanceId = primaryInstanceId
        };

        if (instanceIds != null)
        {
            metadata.InstanceIds.AddRange(instanceIds);
        }

        metadata.Normalize();
        return metadata.HasBindings ? metadata : null;
    }
}

public sealed class CustomUploaderDefinitionBindingInfo
{
    public string FilePath { get; init; } = string.Empty;
    public IReadOnlyList<string> BoundInstanceIds { get; init; } = Array.Empty<string>();
    public string? PrimaryInstanceId { get; init; }
    public bool HasMultipleBindings => BoundInstanceIds.Count > 1;
}

public sealed class CustomUploaderInstanceCreationResult
{
    public List<UploaderInstance> CreatedInstances { get; } = new();
    public List<UploaderCategory> SkippedCategories { get; } = new();

    public IReadOnlyList<UploaderCategory> AffectedCategories =>
        CreatedInstances.Select(instance => instance.Category)
            .Concat(SkippedCategories)
            .Distinct()
            .ToList();
}

public static class CustomUploaderDefinitionBindingService
{
    public static CustomUploaderProvider? GetProviderByFilePath(string filePath)
    {
        return ProviderCatalog.GetCustomUploaderProviderByFilePath(filePath);
    }

    public static CustomUploaderDefinitionBindingInfo GetBindingInfo(CustomUploaderProvider provider, string? fallbackInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var metadataInstanceIds = provider.Metadata?.InstanceIds
            .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var boundInstanceIds = metadataInstanceIds is { Count: > 0 }
            ? metadataInstanceIds
            : GetBoundInstanceIds(provider.ProviderId, fallbackInstanceId);

        if (boundInstanceIds.Count == 0 && !string.IsNullOrWhiteSpace(fallbackInstanceId))
        {
            boundInstanceIds.Add(fallbackInstanceId);
        }

        string? primaryInstanceId = provider.Metadata?.PrimaryInstanceId;
        if (string.IsNullOrWhiteSpace(primaryInstanceId) && boundInstanceIds.Count == 1)
        {
            primaryInstanceId = boundInstanceIds[0];
        }

        return new CustomUploaderDefinitionBindingInfo
        {
            FilePath = provider.FilePath,
            BoundInstanceIds = boundInstanceIds,
            PrimaryInstanceId = primaryInstanceId
        };
    }

    public static List<string> GetBoundInstanceIds(string providerId, string? fallbackInstanceId = null)
    {
        var instanceIds = InstanceManager.Instance.GetInstances()
            .Where(instance => string.Equals(instance.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            .Select(instance => instance.InstanceId)
            .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (instanceIds.Count == 0 && !string.IsNullOrWhiteSpace(fallbackInstanceId))
        {
            instanceIds.Add(fallbackInstanceId);
        }

        return instanceIds;
    }

    public static CustomUploaderInstanceCreationResult CreateMissingInstances(CustomUploaderProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var result = new CustomUploaderInstanceCreationResult();

        foreach (var category in provider.SupportedCategories.Distinct())
        {
            bool alreadyExists = InstanceManager.Instance
                .GetInstancesByCategory(category)
                .Any(instance => string.Equals(instance.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                result.SkippedCategories.Add(category);
                continue;
            }

            var instance = new UploaderInstance
            {
                ProviderId = provider.ProviderId,
                Category = category,
                DisplayName = provider.Name,
                SettingsJson = provider.GetDefaultSettings(category),
                FileTypeRouting = new FileTypeScope { AllFileTypes = true }
            };

            InstanceManager.Instance.AddInstance(instance);
            result.CreatedInstances.Add(instance);
        }

        return result;
    }

    public static bool SaveDefinition(
        CustomUploaderItem item,
        string filePath,
        IEnumerable<string>? instanceIds,
        string? primaryInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var metadata = CustomUploaderXerahSMetadata.FromInstanceIds(instanceIds, primaryInstanceId);
        return CustomUploaderRepository.SaveToFile(item, filePath, metadata);
    }

    public static bool SaveDefinition(CustomUploaderProvider provider, CustomUploaderItem item, string? fallbackInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(item);

        var bindingInfo = GetBindingInfo(provider, fallbackInstanceId);
        return SaveDefinition(item, provider.FilePath, bindingInfo.BoundInstanceIds, bindingInfo.PrimaryInstanceId);
    }

    public static string BuildForkFilePath(string sourceFilePath, string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        string directory = Path.GetDirectoryName(sourceFilePath) ?? PathsManager.PluginsFolder;
        string baseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(sourceFilePath));
        string shortInstanceId = instanceId.Length > 8 ? instanceId[..8] : instanceId;

        string filePath = Path.Combine(directory, $"{baseName}__xerahs-{shortInstanceId}.sxcu");
        int counter = 0;

        while (File.Exists(filePath))
        {
            counter++;
            filePath = Path.Combine(directory, $"{baseName}__xerahs-{shortInstanceId}_{counter}.sxcu");
        }

        return filePath;
    }

    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "CustomUploader";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(name.Where(character => !invalidChars.Contains(character)).ToArray());
        safeName = safeName.Replace(' ', '_');

        return string.IsNullOrWhiteSpace(safeName) ? "CustomUploader" : safeName;
    }
}
