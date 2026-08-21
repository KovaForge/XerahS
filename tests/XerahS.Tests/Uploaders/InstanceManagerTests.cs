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
    public void GetAndUpdateInstance_MatchesInstanceIdCaseInsensitively()
    {
        var instance = new UploaderInstance
        {
            InstanceId = "ABC123",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Original",
            SettingsJson = "{}"
        };

        InstanceManager.Instance.AddInstance(instance);

        var fetched = InstanceManager.Instance.GetInstance("abc123");
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.DisplayName, Is.EqualTo("Original"));

        instance.InstanceId = "abc123";
        instance.DisplayName = "Updated";
        InstanceManager.Instance.UpdateInstance(instance);

        Assert.That(InstanceManager.Instance.GetInstance("ABC123")?.DisplayName, Is.EqualTo("Updated"));
    }

    [Test]
    public void RoutingExclusion_MatchesInstanceIdCaseInsensitively()
    {
        var existing = new UploaderInstance
        {
            InstanceId = "ROUTE123",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "PNG Handler",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        InstanceManager.Instance.AddInstance(existing);

        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, "route123", "png"), Is.True);
        Assert.That(InstanceManager.Instance.GetBlockedFileTypes(UploaderCategory.Image, "route123"), Is.Empty);
        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(new UploaderInstance
        {
            InstanceId = "route123",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "PNG Handler",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        }), Is.Null);
    }

    [Test]
    public void DuplicateAndDefaultLookup_MatchesInstanceIdCaseInsensitively()
    {
        var instance = new UploaderInstance
        {
            InstanceId = "DEF123",
            ProviderId = "test-provider",
            Category = UploaderCategory.File,
            DisplayName = "Default",
            SettingsJson = "{}"
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, "def123");

        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.File)?.InstanceId, Is.EqualTo("DEF123"));
        Assert.That(InstanceManager.Instance.DuplicateInstance("def123").DisplayName, Is.EqualTo("Default (Copy)"));

        InstanceManager.Instance.RemoveInstance("def123");

        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.File), Is.Null);
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

    [Test]
    public void GetDestinationForFile_NormalizesCallerProvidedExtension()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "PNG Handler",
            SettingsJson = "{}",
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        InstanceManager.Instance.AddInstance(instance);

        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.Image, " .PNG ")?.InstanceId, Is.EqualTo(instance.InstanceId));
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.Image, "   "), Is.Null);
        Assert.That(InstanceManager.Instance.GetDestinationForFile(UploaderCategory.Image, null), Is.Null);
    }

    [Test]
    public void CanAddFileType_NormalizesCallerProvidedExtension()
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
                FileExtensions = new List<string> { "png" }
            }
        };

        InstanceManager.Instance.AddInstance(existing);

        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, "candidate", " .PNG "), Is.False);
        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, "candidate", " jpg "), Is.True);
        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, "candidate", "   "), Is.False);
        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, "candidate", null), Is.False);
    }

    [Test]
    public void FileTypeRouting_IgnoresUnavailableInstancesWhenReportingConflicts()
    {
        var unavailablePng = new UploaderInstance
        {
            InstanceId = "unavailable-png",
            ProviderId = "missing-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Unavailable PNG",
            SettingsJson = "{}",
            IsAvailable = false,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        InstanceManager.Instance.AddInstance(unavailablePng);

        var candidate = new UploaderInstance
        {
            InstanceId = "candidate",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Replacement PNG",
            SettingsJson = "{}",
            IsAvailable = true,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        Assert.That(InstanceManager.Instance.CanAddFileType(UploaderCategory.Image, candidate.InstanceId, ".png"), Is.True);
        Assert.That(InstanceManager.Instance.GetBlockedFileTypes(UploaderCategory.Image, candidate.InstanceId), Is.Empty);
        Assert.That(InstanceManager.Instance.ValidateFileTypeConfiguration(candidate), Is.Null);
        Assert.That(InstanceManager.Instance.CanSetAllFileTypes(UploaderCategory.Image, candidate.InstanceId), Is.True);
    }

    [Test]
    public void GetDestinationForFile_SkipsUnavailableExtensionSpecificInstanceAndFallsBackToAvailableAllTypes()
    {
        var unavailablePng = new UploaderInstance
        {
            ProviderId = "png-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Unavailable PNG",
            SettingsJson = "{}",
            IsAvailable = false,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = false,
                FileExtensions = new List<string> { "png" }
            }
        };

        var fallback = new UploaderInstance
        {
            ProviderId = "fallback-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Available Fallback",
            SettingsJson = "{}",
            IsAvailable = true,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = true,
                FileExtensions = new List<string>()
            }
        };

        InstanceManager.Instance.AddInstance(unavailablePng);
        InstanceManager.Instance.AddInstance(fallback);

        var destination = InstanceManager.Instance.GetDestinationForFile(UploaderCategory.Image, ".png");

        Assert.That(destination?.InstanceId, Is.EqualTo(fallback.InstanceId));
    }

    [Test]
    public void ResolveAutoInstance_SkipsUnavailableDefaultAndReturnsAvailableAlternative()
    {
        var unavailableDefault = new UploaderInstance
        {
            ProviderId = "default-provider",
            Category = UploaderCategory.File,
            DisplayName = "Unavailable Default",
            SettingsJson = "{}",
            IsAvailable = false,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = true,
                FileExtensions = new List<string>()
            }
        };

        var availableFallback = new UploaderInstance
        {
            ProviderId = "fallback-provider",
            Category = UploaderCategory.File,
            DisplayName = "Available Fallback",
            SettingsJson = "{}",
            IsAvailable = true,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = true,
                FileExtensions = new List<string>()
            }
        };

        InstanceManager.Instance.AddInstance(unavailableDefault);
        InstanceManager.Instance.AddInstance(availableFallback);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.File, unavailableDefault.InstanceId);

        var resolved = InstanceManager.Instance.ResolveAutoInstance(UploaderCategory.File);

        Assert.That(resolved?.InstanceId, Is.EqualTo(availableFallback.InstanceId));
    }

    [Test]
    public void ResolveAutoInstance_SkipsCategoryMismatchedDefault()
    {
        var staleDefault = new UploaderInstance
        {
            ProviderId = "image-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Stale Image Default",
            SettingsJson = "{}",
            IsAvailable = true,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = true,
                FileExtensions = new List<string>()
            }
        };

        var fileFallback = new UploaderInstance
        {
            ProviderId = "file-provider",
            Category = UploaderCategory.File,
            DisplayName = "File Fallback",
            SettingsJson = "{}",
            IsAvailable = true,
            FileTypeRouting = new FileTypeScope
            {
                AllFileTypes = true,
                FileExtensions = new List<string>()
            }
        };

        InstanceManager.Instance.AddInstance(staleDefault);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, staleDefault.InstanceId);
        staleDefault.Category = UploaderCategory.File;
        InstanceManager.Instance.AddInstance(fileFallback);

        var resolved = InstanceManager.Instance.ResolveAutoInstance(UploaderCategory.Image);

        Assert.That(resolved, Is.Null);
        Assert.That(InstanceManager.Instance.ResolveAutoInstance(UploaderCategory.File)?.InstanceId, Is.EqualTo(staleDefault.InstanceId));
    }

    [Test]
    public void UpdateInstance_CategoryChange_RemovesStaleDefaultMapping()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Mover",
            SettingsJson = "{}"
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        // Change category
        instance.Category = UploaderCategory.File;
        InstanceManager.Instance.UpdateInstance(instance);

        // Old category default should be cleaned up
        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image), Is.Null);
        // Instance should exist in new category
        Assert.That(InstanceManager.Instance.GetInstancesByCategory(UploaderCategory.File).Count, Is.EqualTo(1));
    }

    [Test]
    public void GetDefaultInstance_ReturnsNullWhenInstanceCategoryMismatches()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Mismatch",
            SettingsJson = "{}"
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        // Simulate stale mapping by changing category directly (bypassing UpdateInstance)
        instance.Category = UploaderCategory.File;

        // GetDefaultInstance should detect mismatch and return null
        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image), Is.Null);
        Assert.That(InstanceManager.Instance.GetInstance(instance.InstanceId)?.Category, Is.EqualTo(UploaderCategory.File));
    }

    [Test]
    public void GetDefaultInstance_ReturnsNullWhenDefaultInstanceIsUnavailable()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Unavailable",
            SettingsJson = "{}",
            IsAvailable = false
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image), Is.Null);
    }

    [Test]
    public void GetDefaultInstance_LogsWhenCleaningStaleDefaultMapping()
    {
        var instance = new UploaderInstance
        {
            InstanceId = "stale-default",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Unavailable Default",
            SettingsJson = "{}",
            IsAvailable = false
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        string logPath = Path.Combine(_rootPath, "stale-default.log");
        DebugHelper.Init(logPath);

        try
        {
            Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image), Is.Null);

            DebugHelper.Flush();
            string log = File.ReadAllText(logPath);
            Assert.That(log, Does.Contain("Removed stale default Image uploader 'stale-default'"));
            Assert.That(log, Does.Contain("instance is unavailable"));
        }
        finally
        {
            DebugHelper.Shutdown();
        }
    }

    [Test]
    public void RemoveInstance_LogsWhenRemovingStaleDefaultMapping()
    {
        var instance = new UploaderInstance
        {
            InstanceId = "removable-default",
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Removable Default",
            SettingsJson = "{}"
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        string logPath = Path.Combine(_rootPath, "remove-default.log");
        DebugHelper.Init(logPath);

        try
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);

            DebugHelper.Flush();
            string log = File.ReadAllText(logPath);
            Assert.That(log, Does.Contain("Removed stale default Image uploader 'removable-default'"));
            Assert.That(log, Does.Contain("instance was removed"));
        }
        finally
        {
            DebugHelper.Shutdown();
        }
    }

    [Test]
    public void IsDefaultInstance_ReturnsTrueWhenDefault()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "Test Default",
            SettingsJson = "{}",
            IsAvailable = true
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        Assert.That(InstanceManager.Instance.IsDefaultInstance(UploaderCategory.Image, instance.InstanceId), Is.True);
        Assert.That(InstanceManager.Instance.IsDefaultInstance(UploaderCategory.File, instance.InstanceId), Is.False);
        Assert.That(InstanceManager.Instance.IsDefaultInstance(UploaderCategory.Image, "nonexistent"), Is.False);
    }

    [Test]
    public void IsDefaultInstance_DoesNotCleanStaleMapping()
    {
        var instance = new UploaderInstance
        {
            ProviderId = "test-provider",
            Category = UploaderCategory.Image,
            DisplayName = "No Clean",
            SettingsJson = "{}",
            IsAvailable = false
        };

        InstanceManager.Instance.AddInstance(instance);
        InstanceManager.Instance.SetDefaultInstance(UploaderCategory.Image, instance.InstanceId);

        // IsDefaultInstance should report true even for unavailable instances (it's informational)
        Assert.That(InstanceManager.Instance.IsDefaultInstance(UploaderCategory.Image, instance.InstanceId), Is.True);

        // It should NOT have cleaned the mapping (unlike GetDefaultInstance)
        Assert.That(InstanceManager.Instance.GetDefaultInstance(UploaderCategory.Image), Is.Null,
            "GetDefaultInstance still cleans stale mappings (expected)");
    }

    private static void ClearInstances()
    {
        foreach (var instance in InstanceManager.Instance.GetInstances().ToList())
        {
            InstanceManager.Instance.RemoveInstance(instance.InstanceId);
        }
    }
}
