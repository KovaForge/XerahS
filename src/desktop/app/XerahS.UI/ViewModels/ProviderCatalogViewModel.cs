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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;
using System.Collections.ObjectModel;
using System.Linq;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for the provider catalog dialog
/// </summary>
public partial class ProviderCatalogViewModel : ViewModelBase
{
    [ObservableProperty]
    private UploaderCategory _category;

    [ObservableProperty]
    private ObservableCollection<ProviderViewModel> _availableProviders = new();

    [ObservableProperty]
    private ProviderViewModel? _selectedProvider;

    [ObservableProperty]
    private bool _addToAllSupportedCategories = true;

    public Action<List<UploaderInstance>>? OnInstancesAdded { get; set; }
    public Action? OnCancelled { get; set; }

    public bool CanAddToAllSupportedCategories => SelectedProvider?.SupportsMultipleCategories == true;

    public string MultiCategorySelectionSummary => SelectedProvider?.SupportedCategoriesSummary ?? string.Empty;

    public ProviderCatalogViewModel(UploaderCategory category)
    {
        _category = category;
        LoadProviders();
    }

    private void LoadProviders()
    {
        AvailableProviders.Clear();

        var providers = ProviderCatalog.GetProvidersByCategory(Category);
        DebugHelper.WriteLine($"[ProviderCatalog] Loading {providers.Count} providers for category {Category}");

        foreach (var provider in providers)
        {
            var vm = new ProviderViewModel(provider, Category);
            AvailableProviders.Add(vm);
            DebugHelper.WriteLine($"[ProviderCatalog] Added provider: {provider.Name} (ID: {provider.ProviderId})");
        }

        DebugHelper.WriteLine($"[ProviderCatalog] Total providers in AvailableProviders: {AvailableProviders.Count}");
    }

    partial void OnSelectedProviderChanged(ProviderViewModel? value)
    {
        if (value?.SupportsMultipleCategories != true)
        {
            AddToAllSupportedCategories = false;
        }
        else if (!AddToAllSupportedCategories)
        {
            AddToAllSupportedCategories = true;
        }

        OnPropertyChanged(nameof(CanAddToAllSupportedCategories));
        OnPropertyChanged(nameof(MultiCategorySelectionSummary));
    }


    [RelayCommand]
    private void AddSelected()
    {
        DebugHelper.WriteLine($"[ProviderCatalog] AddSelected called, SelectedProvider: {SelectedProvider?.Name ?? "null"}");

        if (SelectedProvider == null)
        {
            DebugHelper.WriteLine("[ProviderCatalog] No provider selected");
            return;
        }

        try
        {
            DebugHelper.WriteLine($"[ProviderCatalog] Selected provider: {SelectedProvider.Name}");

            var provider = ProviderCatalog.GetProvider(SelectedProvider.ProviderId);
            if (provider == null)
            {
                DebugHelper.WriteLine($"[ProviderCatalog] ERROR: Provider not found in catalog: {SelectedProvider.ProviderId}");
                return;
            }

            DebugHelper.WriteLine($"[ProviderCatalog] Adding new instance for provider: {provider.Name}");

            if (InstanceManager.IsAutoProvider(provider.ProviderId) &&
                InstanceManager.Instance.GetInstances().Any(i => InstanceManager.IsAutoProvider(i.ProviderId)))
            {
                DebugHelper.WriteLine("[ProviderCatalog] Auto provider already exists; skipping duplicate instance creation.");
                OnInstancesAdded?.Invoke(new List<UploaderInstance>());
                return;
            }

            var targetCategories = GetTargetCategories(provider);
            var createdInstances = new List<UploaderInstance>();

            foreach (var targetCategory in targetCategories)
            {
                bool alreadyExists = InstanceManager.Instance.GetInstancesByCategory(targetCategory)
                    .Any(instance => string.Equals(instance.ProviderId, provider.ProviderId, StringComparison.OrdinalIgnoreCase));

                if (alreadyExists)
                {
                    DebugHelper.WriteLine($"[ProviderCatalog] Skipping duplicate instance for {provider.ProviderId} in {targetCategory}");
                    continue;
                }

                var instance = new UploaderInstance
                {
                    ProviderId = provider.ProviderId,
                    Category = targetCategory,
                    DisplayName = $"{provider.Name} ({targetCategory})",
                    SettingsJson = provider.GetDefaultSettings(targetCategory)
                };

                InstanceManager.Instance.AddInstance(instance);
                createdInstances.Add(instance);
            }

            if (provider is CustomUploaderProvider customUploaderProvider)
            {
                var boundInstanceIds = CustomUploaderDefinitionBindingService.GetBoundInstanceIds(customUploaderProvider.ProviderId);
                CustomUploaderDefinitionBindingService.SaveDefinition(
                    customUploaderProvider.Item,
                    customUploaderProvider.FilePath,
                    boundInstanceIds);
                ProviderCatalog.ReloadCustomUploader(customUploaderProvider.FilePath);
            }

            DebugHelper.WriteLine($"[ProviderCatalog] Instance added, invoking OnInstancesAdded callback...");
            OnInstancesAdded?.Invoke(createdInstances);
            DebugHelper.WriteLine($"[ProviderCatalog] OnInstancesAdded callback invoked");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to add provider");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }

    private UploaderCategory[] GetTargetCategories(IUploaderProvider provider)
    {
        return AddToAllSupportedCategories
            ? provider.SupportedCategories.Distinct().ToArray()
            : new[] { Category };
    }
}

/// <summary>
/// ViewModel for a provider in the catalog
/// </summary>
public partial class ProviderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _providerId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                DebugHelper.WriteLine($"[ProviderViewModel] IsSelected changed to {value} for provider: {Name}");
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private string _supportedFileTypesDisplay = string.Empty;

    public UploaderCategory[] SupportedCategories { get; }

    public bool SupportsMultipleCategories => SupportedCategories.Length > 1;

    public string SupportedCategoriesSummary { get; }

    public ProviderViewModel(IUploaderProvider provider, UploaderCategory? filterCategory = null)
    {
        _providerId = provider.ProviderId;
        _name = provider.Name;
        _description = provider.Description;
        SupportedCategories = provider.SupportedCategories;
        SupportedCategoriesSummary = string.Join(", ", SupportedCategories.Select(category => category.ToString()));

        // Display supported file types for the filter category if provided
        if (filterCategory.HasValue)
        {
            var fileTypes = provider.GetSupportedFileTypes();
            if (fileTypes.TryGetValue(filterCategory.Value, out var types))
            {
                var displayTypes = types.Take(8).Select(t => $".{t}");
                var typeStr = string.Join(", ", displayTypes);
                SupportedFileTypesDisplay = types.Length > 8 ? $"{typeStr}, +{types.Length - 8} more" : typeStr;
            }
        }
    }
}
