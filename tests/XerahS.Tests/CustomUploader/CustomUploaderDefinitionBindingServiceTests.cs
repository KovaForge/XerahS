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

using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Uploaders;
using XerahS.Uploaders.CustomUploader;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.CustomUploader;

[TestFixture]
[NonParallelizable]
public class CustomUploaderDefinitionBindingServiceTests
{
    private string _rootPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XerahS.Tests", "XIP0056", Guid.NewGuid().ToString("N"));
        var personalFolder = Path.Combine(_rootPath, "Personal");

        Directory.CreateDirectory(_rootPath);
        PathsManager.PersonalFolder = personalFolder;
        SettingsManager.PersonalFolder = personalFolder;
        PathsManager.EnsureDirectoriesExist();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        CleanupProviders();
        ClearInstances();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        CleanupProviders();
        ClearInstances();
    }

    [TestCase(CustomUploaderDestinationType.ImageUploader, UploaderCategory.Image)]
    [TestCase(CustomUploaderDestinationType.FileUploader, UploaderCategory.File)]
    [TestCase(CustomUploaderDestinationType.ImageUploader | CustomUploaderDestinationType.FileUploader, UploaderCategory.Image, UploaderCategory.File)]
    public void CreateMissingInstances_RespectsDestinationTypes(
        CustomUploaderDestinationType destinationType,
        params UploaderCategory[] expectedCategories)
    {
        string filePath = CreateUniqueFilePath("destinations");
        var item = CreateItem(destinationType);

        Assert.That(CustomUploaderRepository.SaveToFile(item, filePath), Is.True);
        Assert.That(ProviderCatalog.ReloadCustomUploader(filePath), Is.True);

        var provider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        Assert.That(provider, Is.Not.Null);

        var result = CustomUploaderDefinitionBindingService.CreateMissingInstances(provider!);

        Assert.That(result.CreatedInstances.Select(instance => instance.Category), Is.EquivalentTo(expectedCategories));
        Assert.That(result.SkippedCategories, Is.Empty);
    }

    [Test]
    public void CreateMissingInstances_IsIdempotent_AndPersistsInstanceIdsMetadata()
    {
        string filePath = CreateUniqueFilePath("metadata");
        var item = CreateItem(CustomUploaderDestinationType.ImageUploader | CustomUploaderDestinationType.FileUploader);

        Assert.That(CustomUploaderRepository.SaveToFile(item, filePath), Is.True);
        Assert.That(ProviderCatalog.ReloadCustomUploader(filePath), Is.True);

        var provider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        Assert.That(provider, Is.Not.Null);

        var firstResult = CustomUploaderDefinitionBindingService.CreateMissingInstances(provider!);
        var firstBoundIds = CustomUploaderDefinitionBindingService.GetBoundInstanceIds(provider!.ProviderId);

        Assert.That(firstResult.CreatedInstances.Count, Is.EqualTo(2));
        Assert.That(CustomUploaderDefinitionBindingService.SaveDefinition(provider.Item, provider.FilePath, firstBoundIds), Is.True);
        Assert.That(ProviderCatalog.ReloadCustomUploader(filePath), Is.True);

        var reloaded = CustomUploaderRepository.LoadFromFile(filePath);
        Assert.That(reloaded.IsValid, Is.True, reloaded.LoadError);
        Assert.That(reloaded.Metadata, Is.Not.Null);
        Assert.That(reloaded.Metadata!.InstanceIds, Is.EquivalentTo(firstBoundIds));

        var reloadedProvider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        Assert.That(reloadedProvider, Is.Not.Null);

        var secondResult = CustomUploaderDefinitionBindingService.CreateMissingInstances(reloadedProvider!);

        Assert.That(secondResult.CreatedInstances, Is.Empty);
        Assert.That(secondResult.SkippedCategories, Is.EquivalentTo(new[] { UploaderCategory.Image, UploaderCategory.File }));
    }

    [Test]
    public void GetBindingInfo_FallsBackToCurrentInstance_WhenMetadataIsMissing()
    {
        string filePath = CreateUniqueFilePath("fallback");
        var item = CreateItem(CustomUploaderDestinationType.TextUploader);

        Assert.That(CustomUploaderRepository.SaveToFile(item, filePath), Is.True);
        Assert.That(ProviderCatalog.ReloadCustomUploader(filePath), Is.True);

        var provider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        Assert.That(provider, Is.Not.Null);

        var createResult = CustomUploaderDefinitionBindingService.CreateMissingInstances(provider!);
        Assert.That(createResult.CreatedInstances.Count, Is.EqualTo(1));

        var bindingInfo = CustomUploaderDefinitionBindingService.GetBindingInfo(provider!, createResult.CreatedInstances[0].InstanceId);

        Assert.That(bindingInfo.BoundInstanceIds, Is.EquivalentTo(new[] { createResult.CreatedInstances[0].InstanceId }));
        Assert.That(bindingInfo.PrimaryInstanceId, Is.EqualTo(createResult.CreatedInstances[0].InstanceId));
    }

    [Test]
    public void ProviderCatalogViewModel_AddSelected_CanAddToAllSupportedCategories()
    {
        string filePath = CreateUniqueFilePath("catalog");
        var item = CreateItem(CustomUploaderDestinationType.ImageUploader | CustomUploaderDestinationType.FileUploader);

        Assert.That(CustomUploaderRepository.SaveToFile(item, filePath), Is.True);
        Assert.That(ProviderCatalog.ReloadCustomUploader(filePath), Is.True);

        var provider = CustomUploaderDefinitionBindingService.GetProviderByFilePath(filePath);
        Assert.That(provider, Is.Not.Null);

        var viewModel = new ProviderCatalogViewModel(UploaderCategory.Image);
        viewModel.SelectedProvider = viewModel.AvailableProviders.Single(candidate => candidate.ProviderId == provider!.ProviderId);

        List<UploaderInstance>? addedInstances = null;
        viewModel.OnInstancesAdded = instances => addedInstances = instances;

        viewModel.AddSelectedCommand.Execute(null);

        Assert.That(addedInstances, Is.Not.Null);
        Assert.That(addedInstances!.Select(instance => instance.Category), Is.EquivalentTo(new[] { UploaderCategory.Image, UploaderCategory.File }));

        var reloaded = CustomUploaderRepository.LoadFromFile(filePath);
        Assert.That(reloaded.IsValid, Is.True, reloaded.LoadError);
        Assert.That(reloaded.Metadata, Is.Not.Null);
        Assert.That(reloaded.Metadata!.InstanceIds.Count, Is.EqualTo(2));
    }

    private static CustomUploaderItem CreateItem(CustomUploaderDestinationType destinationType)
    {
        var item = CustomUploaderItem.Init();
        item.Name = $"Test {destinationType}";
        item.DestinationType = destinationType;
        item.RequestURL = "https://example.com/upload";
        item.RequestMethod = XerahS.Uploaders.HttpMethod.POST;
        item.Body = CustomUploaderBody.MultipartFormData;
        item.FileFormName = "file";
        item.URL = "{response}";
        return item;
    }

    private string CreateUniqueFilePath(string folderName)
    {
        string directory = Path.Combine(_rootPath, folderName);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.sxcu");
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }

    private void CleanupProviders()
    {
        foreach (var provider in ProviderCatalog.GetCustomUploaderProviders()
                     .Where(provider => provider.FilePath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            ProviderCatalog.RemoveCustomUploader(provider.ProviderId);
        }

        CustomUploaderRepository.Clear();
    }
}
