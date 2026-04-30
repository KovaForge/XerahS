#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.IO.Compression;
using NUnit.Framework;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginManifestSecurityTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"xerahs-plugin-security-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Test]
    public void IsValid_RejectsPluginIdPathTraversal()
    {
        var manifest = CreateValidManifest();
        manifest.PluginId = "../outside";

        bool valid = manifest.IsValid(out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("PluginId"));
        });
    }

    [Test]
    public void IsValid_RejectsAssemblyFileNamePathTraversal()
    {
        var manifest = CreateValidManifest();
        manifest.AssemblyFileName = "../outside.dll";

        bool valid = manifest.IsValid(out string? error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("AssemblyFileName"));
        });
    }

    [Test]
    public void DiscoverPlugins_SkipsManifestWithAssemblyOutsidePluginDirectory()
    {
        string pluginsRoot = Path.Combine(_tempRoot, "Plugins");
        string pluginDirectory = Path.Combine(pluginsRoot, "malicious");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginsRoot, "outside.dll"), "not really an assembly");
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), CreateManifestJson("malicious", "../outside.dll"));

        var discovered = new PluginDiscovery().DiscoverPlugins(pluginsRoot);

        Assert.That(discovered, Is.Empty);
    }

    [Test]
    public void InstallPackage_RejectsManifestWithPluginIdPathTraversal()
    {
        string packagePath = Path.Combine(_tempRoot, "malicious.xsdp");
        string pluginsRoot = Path.Combine(_tempRoot, "Plugins");
        Directory.CreateDirectory(pluginsRoot);

        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddTextEntry(archive, "plugin.json", CreateManifestJson("../outside", "malicious.dll"));
            AddTextEntry(archive, "malicious.dll", "not really an assembly");
        }

        Assert.Throws<InvalidDataException>(() => PluginPackager.InstallPackage(packagePath, pluginsRoot));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "outside")), Is.False);
    }

    [Test]
    public void InstallPackage_RejectsDuplicateEntryPaths()
    {
        string packagePath = Path.Combine(_tempRoot, "duplicate-manifest.xsdp");
        string pluginsRoot = Path.Combine(_tempRoot, "Plugins");
        Directory.CreateDirectory(pluginsRoot);

        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddTextEntry(archive, "plugin.json", CreateManifestJson("sample-plugin", "sample-plugin.dll"));
            AddTextEntry(archive, "sample-plugin.dll", "not really an assembly");
            AddTextEntry(archive, "plugin.json", CreateManifestJson("other-plugin", "sample-plugin.dll"));
        }

        var exception = Assert.Throws<InvalidDataException>(() => PluginPackager.InstallPackage(packagePath, pluginsRoot));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("duplicate entry path"));
            Assert.That(Directory.Exists(Path.Combine(pluginsRoot, "sample-plugin")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(pluginsRoot, "other-plugin")), Is.False);
        });
    }

    [Test]
    public void InstallPackage_CreatesMissingPluginsDirectory()
    {
        string packagePath = Path.Combine(_tempRoot, "sample.xsdp");
        string pluginsRoot = Path.Combine(_tempRoot, "MissingPlugins");

        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddTextEntry(archive, "plugin.json", CreateManifestJson("sample-plugin", "sample-plugin.dll"));
            AddTextEntry(archive, "sample-plugin.dll", "not really an assembly");
        }

        var metadata = PluginPackager.InstallPackage(packagePath, pluginsRoot);

        Assert.Multiple(() =>
        {
            Assert.That(metadata, Is.Not.Null);
            Assert.That(Directory.Exists(pluginsRoot), Is.True);
            Assert.That(File.Exists(Path.Combine(pluginsRoot, "sample-plugin", "plugin.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(pluginsRoot, "sample-plugin", "sample-plugin.dll")), Is.True);
        });
    }

    private static PluginManifest CreateValidManifest()
    {
        return new PluginManifest
        {
            PluginId = "sample-plugin",
            Name = "Sample Plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Test plugin",
            ApiVersion = "1.0",
            EntryPoint = "Sample.Plugin.Provider",
            AssemblyFileName = "sample-plugin.dll",
            SupportedCategories = ["Image"]
        };
    }

    private static string CreateManifestJson(string pluginId, string assemblyFileName)
    {
        return $$"""
        {
          "PluginId": "{{pluginId}}",
          "Name": "Sample Plugin",
          "Version": "1.0.0",
          "Author": "Tests",
          "Description": "Test plugin",
          "ApiVersion": "1.0",
          "EntryPoint": "Sample.Plugin.Provider",
          "AssemblyFileName": "{{assemblyFileName}}",
          "SupportedCategories": ["Image"]
        }
        """;
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
