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

using System.Collections.ObjectModel;
using XerahS.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

public partial class PluginInstallerViewModel : ViewModelBase
{
    private readonly IViewDialogService _dialogService;
    private readonly PluginIndexService _pluginIndexService;

    public PluginInstallerViewModel(IViewDialogService dialogService)
        : this(dialogService, new PluginIndexService())
    {
    }

    public PluginInstallerViewModel(IViewDialogService dialogService, PluginIndexService pluginIndexService)
    {
        _dialogService = dialogService;
        _pluginIndexService = pluginIndexService;
    }
    [ObservableProperty]
    private string _packageFilePath = string.Empty;

    [ObservableProperty]
    private PluginManifest? _manifest;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isLoadingCommunityPlugins;

    [ObservableProperty]
    private CommunityPluginIndexEntry? _selectedCommunityPlugin;

    public ObservableCollection<CommunityPluginIndexEntry> CommunityPlugins { get; } = [];

    public Action<bool?>? RequestClose { get; set; }

    public bool CanInstall => Manifest != null && !IsInstalling;

    public bool CanInstallCommunityPlugin => SelectedCommunityPlugin != null && !IsInstalling && !IsLoadingCommunityPlugins;

    public bool CanRefreshCommunityPlugins => !IsInstalling && !IsLoadingCommunityPlugins;

    public string ManifestVersionAuthor =>
        Manifest != null ? $"Version {Manifest.Version} by {Manifest.Author}" : string.Empty;

    public string ManifestCategories =>
        Manifest != null ? $"Categories: {string.Join(", ", Manifest.SupportedCategories)}" : string.Empty;

    public string CommunityPluginSummary =>
        CommunityPlugins.Count == 1 ? "1 community plugin available" : $"{CommunityPlugins.Count} community plugins available";

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanInstallCommunityPlugin));
        OnPropertyChanged(nameof(CanRefreshCommunityPlugins));
    }

    partial void OnIsLoadingCommunityPluginsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInstallCommunityPlugin));
        OnPropertyChanged(nameof(CanRefreshCommunityPlugins));
    }

    partial void OnSelectedCommunityPluginChanged(CommunityPluginIndexEntry? value)
    {
        OnPropertyChanged(nameof(CanInstallCommunityPlugin));
    }

    [RelayCommand]
    private async Task BrowsePackage()
    {
        var filePath = await _dialogService.ShowFilePickerAsync("Select Plugin Package", new[] { "*.xsdp" });

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            PackageFilePath = filePath;
            await LoadManifestPreview();
        }
    }

    private async Task LoadManifestPreview()
    {
        ErrorMessage = null;
        Manifest = null;

        try
        {
            Manifest = PluginPackager.PreviewPackage(PackageFilePath);
            if (Manifest == null)
            {
                ErrorMessage = "Invalid package: plugin.json not found";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load package: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanInstall));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Install()
    {
        if (Manifest == null || string.IsNullOrWhiteSpace(PackageFilePath))
        {
            ErrorMessage = "Please select a valid package.";
            return;
        }

        IsInstalling = true;
        ErrorMessage = null;

        try
        {
            string pluginsDir = PathsManager.PluginsArchitectureFolder;
            Directory.CreateDirectory(pluginsDir);

            var metadata = PluginPackager.InstallPackage(PackageFilePath, pluginsDir);

            if (metadata != null)
            {
                ProviderCatalog.LoadPlugins(pluginsDir, forceReload: true);
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Installation failed: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    [RelayCommand]
    private async Task RefreshCommunityPlugins()
    {
        IsLoadingCommunityPlugins = true;
        ErrorMessage = null;

        try
        {
            var index = await _pluginIndexService.FetchIndexAsync();
            CommunityPlugins.Clear();

            foreach (var plugin in index.Plugins.OrderBy(plugin => plugin.CategorySummary).ThenBy(plugin => plugin.Name))
            {
                CommunityPlugins.Add(plugin);
            }

            SelectedCommunityPlugin = CommunityPlugins.FirstOrDefault();
            OnPropertyChanged(nameof(CommunityPluginSummary));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load community plugin registry: {ex.Message}";
        }
        finally
        {
            IsLoadingCommunityPlugins = false;
        }
    }

    [RelayCommand]
    private async Task InstallCommunityPlugin()
    {
        if (SelectedCommunityPlugin == null)
        {
            ErrorMessage = "Please select a community plugin.";
            return;
        }

        IsInstalling = true;
        ErrorMessage = null;
        string? packagePath = null;

        try
        {
            packagePath = await _pluginIndexService.DownloadPackageAsync(SelectedCommunityPlugin);
            Manifest = PluginPackager.PreviewPackage(packagePath);

            if (Manifest == null)
            {
                ErrorMessage = "Downloaded package is invalid: plugin.json not found.";
                return;
            }

            if (!Manifest.PluginId.Equals(SelectedCommunityPlugin.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Downloaded package manifest does not match the registry plugin ID.";
                return;
            }

            string pluginsDir = PathsManager.PluginsArchitectureFolder;
            Directory.CreateDirectory(pluginsDir);

            var metadata = PluginPackager.InstallPackage(packagePath, pluginsDir);
            if (metadata != null)
            {
                ProviderCatalog.LoadPlugins(pluginsDir, forceReload: true);
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Community plugin installation failed: {ex.Message}";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(packagePath) && File.Exists(packagePath))
            {
                try
                {
                    File.Delete(packagePath);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to remove downloaded plugin package");
                }
            }

            IsInstalling = false;
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
