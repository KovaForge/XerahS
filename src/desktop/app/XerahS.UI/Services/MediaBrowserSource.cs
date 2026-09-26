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

using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Services;

/// <summary>
/// One browsable remote storage: an uploader instance whose provider implements
/// <see cref="IUploaderExplorer"/>. Each source carries its own settings so operations
/// always target the right account.
/// </summary>
public sealed class MediaBrowserSource
{
    public MediaBrowserSource(UploaderInstance instance, IUploaderExplorer explorer, string providerName)
    {
        Instance = instance;
        Explorer = explorer;
        ProviderName = providerName;
        Context = new ExplorerContext { SettingsJson = instance.SettingsJson, InstanceId = instance.InstanceId };
    }

    public UploaderInstance Instance { get; }
    public IUploaderExplorer Explorer { get; }
    public string ProviderName { get; }
    public ExplorerContext Context { get; }
    public ExplorerCapabilities Capabilities => Explorer.BrowserCapabilities;

    public string DisplayName => string.IsNullOrWhiteSpace(Instance.DisplayName) || Instance.DisplayName == ProviderName
        ? ProviderName
        : $"{ProviderName} — {Instance.DisplayName}";

    public bool Has(ExplorerCapabilities capability) => Capabilities.HasFlag(capability);

    public override string ToString() => DisplayName;

    /// <summary>
    /// Every configured instance that can be browsed. Instances sharing a provider and settings
    /// (the same bucket registered for Image and File uploads) collapse into one entry.
    /// </summary>
    public static IReadOnlyList<MediaBrowserSource> Discover(IEnumerable<UploaderInstance> instances, Func<string, IUploaderProvider?> getProvider)
    {
        var sources = new List<MediaBrowserSource>();
        var seen = new HashSet<(string, string)>();
        foreach (UploaderInstance instance in instances)
        {
            if (!instance.IsAvailable || getProvider(instance.ProviderId) is not { } provider || provider is not IUploaderExplorer explorer)
            {
                continue;
            }

            if (seen.Add((instance.ProviderId, instance.SettingsJson ?? string.Empty)))
            {
                sources.Add(new MediaBrowserSource(instance, explorer, provider.Name));
            }
        }

        return sources
            .OrderBy(source => source.ProviderName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(source => source.Instance.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
