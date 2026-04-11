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

using XerahS.Common;
using XerahS.Core.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.Onboarding;

internal static class OnboardingFileUploaderHelper
{
    public static IReadOnlyList<IUploaderProvider> GetFileUploaderProviders()
    {
        InitializeProviders();

        return ProviderCatalog.GetProvidersByCategory(UploaderCategory.File)
            .Where(provider => !string.Equals(provider.ProviderId, ProviderIds.Auto, StringComparison.OrdinalIgnoreCase))
            .OrderBy(provider => provider.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static UploaderInstance EnsureFileUploaderInstance(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID is required.", nameof(providerId));
        }

        InitializeProviders();

        IUploaderProvider provider = ProviderCatalog.GetProvider(providerId)
            ?? throw new InvalidOperationException($"File uploader provider '{providerId}' is not available.");

        if (!provider.SupportedCategories.Contains(UploaderCategory.File))
        {
            throw new InvalidOperationException($"Provider '{providerId}' is not a file uploader.");
        }

        List<UploaderInstance> fileUploaderInstances = InstanceManager.Instance.GetInstancesByCategory(UploaderCategory.File);
        UploaderInstance? instance = fileUploaderInstances.FirstOrDefault(existingInstance =>
            string.Equals(existingInstance.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));

        if (instance == null)
        {
            instance = new UploaderInstance
            {
                ProviderId = provider.ProviderId,
                Category = UploaderCategory.File,
                DisplayName = provider.Name,
                SettingsJson = provider.GetDefaultSettings(UploaderCategory.File),
                FileTypeRouting = new FileTypeScope
                {
                    AllFileTypes = fileUploaderInstances.Count == 0
                },
                IsAvailable = true
            };

            InstanceManager.Instance.AddInstance(instance);
        }

        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, instance.InstanceId);
        return instance;
    }

    private static void InitializeProviders()
    {
        ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders();
        ProviderCatalog.LoadPlugins(PathsManager.GetPluginDirectories());
    }
}
