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
using System.Text.RegularExpressions;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Root model for the community plugin registry index (plugins-index.json).
/// </summary>
public class CommunityPluginIndex
{
    [JsonProperty("indexVersion")]
    public string IndexVersion { get; set; } = string.Empty;

    [JsonProperty("lastUpdated")]
    public string? LastUpdated { get; set; }

    [JsonProperty("plugins")]
    public List<CommunityPluginIndexEntry> Plugins { get; set; } = new();

    /// <summary>
    /// Validates the index structure: requires indexVersion, non-null plugins list,
    /// no duplicate pluginId values, and all entries individually valid.
    /// </summary>
    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(IndexVersion))
        {
            error = "indexVersion is required";
            return false;
        }

        if (Plugins == null)
        {
            error = "plugins list is required";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in Plugins)
        {
            if (!seen.Add(plugin.PluginId))
            {
                error = $"Duplicate pluginId: '{plugin.PluginId}'";
                return false;
            }
        }

        foreach (var plugin in Plugins)
        {
            if (!plugin.IsValid(out var entryError))
            {
                error = $"Invalid plugin entry '{plugin.PluginId}': {entryError}";
                return false;
            }
        }

        error = null;
        return true;
    }
}

/// <summary>
/// Describes one downloadable community plugin package in the registry index.
/// </summary>
public class CommunityPluginIndexEntry
{
    private static readonly Regex PluginIdPattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    private static readonly Regex Sha256Pattern = new("^(sha256:)?[A-Fa-f0-9]{64}$", RegexOptions.Compiled);

    [JsonProperty("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("apiVersion")]
    public string ApiVersion { get; set; } = string.Empty;

    [JsonProperty("supportedCategories")]
    public List<string> SupportedCategories { get; set; } = new();

    [JsonProperty("homepageUrl")]
    public string? HomepageUrl { get; set; }

    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonProperty("checksum")]
    public string Checksum { get; set; } = string.Empty;

    [JsonProperty("minAppVersion")]
    public string? MinAppVersion { get; set; }

    [JsonProperty("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name} {Version}";

    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(PluginId))
        {
            error = "pluginId is required";
            return false;
        }

        if (!PluginIdPattern.IsMatch(PluginId))
        {
            error = "pluginId may only contain letters, digits, '.', '_' and '-'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Version) ||
            string.IsNullOrWhiteSpace(Author) ||
            string.IsNullOrWhiteSpace(ApiVersion))
        {
            error = "name, version, author and apiVersion are required";
            return false;
        }

        if (!IsHttpsUrl(DownloadUrl, out Uri? downloadUri) || downloadUri == null)
        {
            error = "downloadUrl must be an HTTPS URL";
            return false;
        }

        if (!downloadUri.AbsolutePath.EndsWith(".xsdp", StringComparison.OrdinalIgnoreCase))
        {
            error = "downloadUrl must point to an .xsdp package";
            return false;
        }

        if (!Sha256Pattern.IsMatch(Checksum))
        {
            error = "checksum must be a SHA-256 hash";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(HomepageUrl) && !IsHttpsUrl(HomepageUrl, out _))
        {
            error = "homepageUrl must be an HTTPS URL";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsHttpsUrl(string? value, out Uri? uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
