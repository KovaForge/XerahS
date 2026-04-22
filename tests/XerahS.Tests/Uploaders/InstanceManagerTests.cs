using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
[NonParallelizable]
public class InstanceManagerTests
{
    private string _rootPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XerahS.Tests", "XIP0057", Guid.NewGuid().ToString("N"));
        var personalFolder = Path.Combine(_rootPath, "Personal");

        Directory.CreateDirectory(_rootPath);
        PathsManager.PersonalFolder = personalFolder;
        SettingsManager.PersonalFolder = personalFolder;
        PathsManager.EnsureDirectoriesExist();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ClearInstances();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        ClearInstances();
    }

    [Test]
    public void DuplicateInstance_PreservesFileTypeRouting()
    {
        var source = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Source",
            SettingsJson = "{\"quality\":90}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png", "jpg" }
            }
        };

        InstanceManager.Instance.AddInstance(source);

        var duplicate = InstanceManager.Instance.DuplicateInstance(source.InstanceId);

        Assert.That(duplicate.FileTypeRouting.AllFileTypes, Is.False);
        Assert.That(duplicate.FileTypeRouting.FileExtensions, Is.EqualTo(new[] { "png", "jpg" }));
        Assert.That(duplicate.FileTypeRouting, Is.Not.SameAs(source.FileTypeRouting));

        source.FileTypeRouting.FileExtensions.Add("gif");

        Assert.That(duplicate.FileTypeRouting.FileExtensions, Is.EqualTo(new[] { "png", "jpg" }));
    }

    [Test]
    public void DuplicateInstance_NormalizesMissingFileTypeRouting()
    {
        var source = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Legacy Source",
            SettingsJson = "{}",
            FileTypeRouting = null!
        };

        InstanceManager.Instance.AddInstance(source);

        var duplicate = InstanceManager.Instance.DuplicateInstance(source.InstanceId);

        Assert.That(source.FileTypeRouting, Is.Not.Null);
        Assert.That(duplicate.FileTypeRouting, Is.Not.Null);
        Assert.That(duplicate.FileTypeRouting.AllFileTypes, Is.False);
        Assert.That(duplicate.FileTypeRouting.FileExtensions, Is.Empty);
    }

    [Test]
    public void ValidateFileTypeConfiguration_NormalizesMissingFileTypeRouting()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Legacy Source",
            SettingsJson = "{}",
            FileTypeRouting = null!
        };

        InstanceManager.Instance.AddInstance(instance);

        var validationError = InstanceManager.Instance.ValidateFileTypeConfiguration(instance);

        Assert.That(validationError, Is.Null);
        Assert.That(instance.FileTypeRouting, Is.Not.Null);
        Assert.That(instance.FileTypeRouting.FileExtensions, Is.Empty);
    }

    [Test]
    public void AddInstance_NormalizesLegacyFileExtensionShapes()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Legacy Shapes",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { " .PNG ", "jpg", ".jpg", "   " }
            }
        };

        InstanceManager.Instance.AddInstance(instance);

        Assert.That(instance.FileTypeRouting.FileExtensions, Is.EqualTo(new[] { "png", "jpg" }));
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.Image, ".png")?.InstanceId, Is.EqualTo(instance.InstanceId));
    }

    [Test]
    public void ValidateFileTypeConfiguration_FindsConflictsForLegacyDottedExtensions()
    {
        var existing = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "PNG Handler",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { ".PNG" }
            }
        };

        var candidate = new UploaderInstance
        {
            InstanceId = "candidate",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Candidate",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        InstanceManager.Instance.AddInstance(existing);

        var validationError = InstanceManager.Instance.ValidateFileTypeConfiguration(candidate);

        Assert.That(validationError, Does.Contain("png").And.Contain("PNG Handler"));
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
