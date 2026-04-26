#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Root document for a community plugin registry index.
/// </summary>
public sealed class CommunityPluginIndex
{
    [JsonProperty("indexVersion")]
    public string IndexVersion { get; set; } = "1.0";

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty("plugins")]
    public List<CommunityPluginIndexEntry> Plugins { get; set; } = [];

    public bool IsValid(out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(IndexVersion))
        {
            error = "IndexVersion is required.";
            return false;
        }

        if (Plugins == null)
        {
            error = "Plugins list is required.";
            return false;
        }

        var seenPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in Plugins)
        {
            if (!plugin.IsValid(out error))
            {
                return false;
            }

            if (!seenPluginIds.Add(plugin.PluginId))
            {
                error = $"Duplicate pluginId '{plugin.PluginId}' in community plugin index.";
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Metadata for a downloadable community plugin package.
/// </summary>
public sealed class CommunityPluginIndexEntry
{
    [JsonProperty("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("apiVersion")]
    public string ApiVersion { get; set; } = "1.0";

    [JsonProperty("supportedCategories")]
    public List<string> SupportedCategories { get; set; } = [];

    [JsonProperty("homepageUrl")]
    public string? HomepageUrl { get; set; }

    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonProperty("checksum")]
    public string Checksum { get; set; } = string.Empty;

    [JsonProperty("minAppVersion")]
    public string? MinAppVersion { get; set; }

    [JsonProperty("dependencies")]
    public List<string> Dependencies { get; set; } = [];

    public string VersionAuthor => $"Version {Version} by {Author}";

    public string CategorySummary => SupportedCategories.Count > 0
        ? string.Join(", ", SupportedCategories)
        : "Uncategorized";

    public bool IsValid(out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(PluginId))
        {
            error = "PluginId is required.";
            return false;
        }

        if (!IsSafePluginId(PluginId))
        {
            error = $"PluginId '{PluginId}' contains invalid characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            error = $"Plugin '{PluginId}' is missing a name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            error = $"Plugin '{PluginId}' is missing a version.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Author))
        {
            error = $"Plugin '{PluginId}' is missing an author.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiVersion))
        {
            error = $"Plugin '{PluginId}' is missing an API version.";
            return false;
        }

        if (!new PluginManifest { ApiVersion = ApiVersion }.IsCompatibleWith(PluginDiscovery.GetCurrentApiVersion()))
        {
            error = $"Plugin '{PluginId}' targets unsupported API version '{ApiVersion}'.";
            return false;
        }

        if (!IsValidHttpsUri(DownloadUrl))
        {
            error = $"Plugin '{PluginId}' has an invalid downloadUrl. HTTPS is required.";
            return false;
        }

        if (!DownloadUrl.EndsWith(".xsdp", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Plugin '{PluginId}' downloadUrl must point to a .xsdp package.";
            return false;
        }

        if (!PluginIndexService.IsValidSha256Checksum(Checksum))
        {
            error = $"Plugin '{PluginId}' must include a sha256 checksum.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(HomepageUrl) && !IsValidHttpsUri(HomepageUrl))
        {
            error = $"Plugin '{PluginId}' has an invalid homepageUrl. HTTPS is required.";
            return false;
        }

        return true;
    }

    private static bool IsValidHttpsUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafePluginId(string pluginId)
    {
        if (pluginId is "." or "..")
        {
            return false;
        }

        foreach (char c in pluginId)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
