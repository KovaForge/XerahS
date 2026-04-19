#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Common;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginConfigurationVerifierTests
{
    private string _tempRoot = null!;
    private string _originalPersonalFolder = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"xerahs-plugin-verifier-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _originalPersonalFolder = PathsManager.PersonalFolder;
        PathsManager.PersonalFolder = Path.Combine(_tempRoot, "Personal");
    }

    [TearDown]
    public void TearDown()
    {
        PathsManager.PersonalFolder = _originalPersonalFolder;

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    [Test]
    public void VerifyPluginConfiguration_ValidPluginWithRuntimeFolder_ReturnsValid()
    {
        string pluginId = "sample-plugin";
        string pluginDirectory = CreatePluginDirectory(pluginId, includeRuntimeAsset: true);

        PluginVerificationResult result = PluginConfigurationVerifier.VerifyPluginConfiguration(pluginId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PluginVerificationStatus.Valid));
            Assert.That(result.FileCount, Is.EqualTo(3));
            Assert.That(result.Message, Does.Contain("properly configured"));
            Assert.That(result.Issues, Does.Contain("Plugin manifest and assembly were found."));
            Assert.That(Directory.Exists(Path.Combine(pluginDirectory, "runtimes")), Is.True);
        });
    }

    [Test]
    public void VerifyPluginConfiguration_MissingAssembly_ReturnsError()
    {
        string pluginId = "broken-plugin";
        CreatePluginDirectory(pluginId, includeAssembly: false);

        PluginVerificationResult result = PluginConfigurationVerifier.VerifyPluginConfiguration(pluginId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PluginVerificationStatus.Error));
            Assert.That(result.Message, Is.EqualTo("Plugin assembly not found"));
            Assert.That(result.Issues.Single(), Does.Contain("broken-plugin.dll"));
        });
    }

    [Test]
    public void VerifyPluginConfiguration_MissingProviderId_ReturnsErrorInsteadOfThrowing()
    {
        PluginVerificationResult result = PluginConfigurationVerifier.VerifyPluginConfiguration(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PluginVerificationStatus.Error));
            Assert.That(result.Message, Is.EqualTo("Plugin provider ID is missing"));
            Assert.That(result.Issues, Does.Contain("A plugin provider ID is required for verification."));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void CleanDuplicateFrameworkDlls_MissingProviderId_IsIgnored(string? providerId)
    {
        Assert.That(PluginConfigurationVerifier.CleanDuplicateFrameworkDlls(providerId!), Is.Zero);
    }

    private static string CreatePluginDirectory(string pluginId, bool includeAssembly = true, bool includeRuntimeAsset = false)
    {
        string pluginDirectory = Path.Combine(PathsManager.PluginsArchitectureFolder, pluginId);
        Directory.CreateDirectory(pluginDirectory);

        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), $$"""
        {
          "PluginId": "{{pluginId}}",
          "Name": "Sample Plugin",
          "Version": "1.0.0",
          "Author": "Tests",
          "Description": "Test plugin",
          "ApiVersion": "1.0",
          "EntryPoint": "Sample.Plugin.Provider",
          "SupportedCategories": ["ImageUploader"]
        }
        """);

        if (includeAssembly)
        {
            File.WriteAllBytes(Path.Combine(pluginDirectory, $"{pluginId}.dll"), [0x4D, 0x5A]);
        }

        File.WriteAllText(Path.Combine(pluginDirectory, $"{pluginId}.deps.json"), "{}");

        if (includeRuntimeAsset)
        {
            string runtimeDirectory = Path.Combine(pluginDirectory, "runtimes", "linux-x64", "native");
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(Path.Combine(runtimeDirectory, "helper.so"), "binary");
        }

        return pluginDirectory;
    }
}
