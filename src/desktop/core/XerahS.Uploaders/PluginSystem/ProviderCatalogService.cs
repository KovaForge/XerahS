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

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Injectable catalog. Process state remains in <see cref="ProviderCatalog"/> so existing
/// static callers keep working; hosts should take <see cref="IProviderCatalog"/>.
/// </summary>
public sealed class ProviderCatalogService : IProviderCatalog
{
    public static ProviderCatalogService Shared { get; } = new();

    public void SetProviderContext(IProviderContext context) => ProviderCatalog.SetProviderContext(context);

    public IProviderContext? GetProviderContext() => ProviderCatalog.GetProviderContext();

    public void LoadPlugins(IEnumerable<string> pluginDirectories, bool forceReload = false) =>
        ProviderCatalog.LoadPlugins(pluginDirectories, forceReload);

    public void LoadPlugins(string pluginsDirectory, bool forceReload = false) =>
        ProviderCatalog.LoadPlugins(pluginsDirectory, forceReload);

    public void RegisterProvider(IUploaderProvider provider) => ProviderCatalog.RegisterProvider(provider);

    public IUploaderProvider? GetProvider(string providerId) => ProviderCatalog.GetProvider(providerId);

    public IReadOnlyList<IUploaderProvider> GetAllProviders() => ProviderCatalog.GetAllProviders();

    public IReadOnlyList<IUploaderProvider> GetProvidersByCategory(UploaderCategory category) =>
        ProviderCatalog.GetProvidersByCategory(category);

    public bool ArePluginsLoaded() => ProviderCatalog.ArePluginsLoaded();

    public IUploaderExplorer? GetExplorer(string providerId) => ProviderCatalog.GetExplorer(providerId);

    public IReadOnlyList<IUploaderProvider> GetBrowsableProviders() => ProviderCatalog.GetBrowsableProviders();
}
