#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginLoaderTests
{
    [Test]
    public void LoadPlugin_MismatchedManifestId_TracksContextByProviderIdForUnload()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var manifest = CreateMismatchedProviderManifest("manifest-plugin-id");
        var metadata = new PluginMetadata(manifest, Path.GetDirectoryName(assemblyPath)!, assemblyPath);

        var provider = loader.LoadPlugin(metadata);

        Assert.Multiple(() =>
        {
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider!.ProviderId, Is.EqualTo(MismatchedPluginProvider.ProviderIdValue));
            Assert.That(loader.GetLoadedContexts().ContainsKey(MismatchedPluginProvider.ProviderIdValue), Is.True);
            Assert.That(loader.GetLoadedContexts().ContainsKey(manifest.PluginId), Is.False);
        });

        Assert.That(loader.UnloadPlugin(MismatchedPluginProvider.ProviderIdValue), Is.True);
        Assert.That(loader.GetLoadedContexts(), Is.Empty);
    }

    [Test]
    public void LoadPlugin_DuplicateProviderId_ReplacesPreviousContextWithoutLeakingHandle()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        string pluginDirectory = Path.GetDirectoryName(assemblyPath)!;
        var firstMetadata = new PluginMetadata(CreateMismatchedProviderManifest("first-manifest-id"), pluginDirectory, assemblyPath);
        var secondMetadata = new PluginMetadata(CreateMismatchedProviderManifest("second-manifest-id"), pluginDirectory, assemblyPath);

        Assert.That(loader.LoadPlugin(firstMetadata), Is.Not.Null);
        var firstContext = loader.GetLoadedContexts()[MismatchedPluginProvider.ProviderIdValue];

        Assert.That(loader.LoadPlugin(secondMetadata), Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(loader.GetLoadedContexts(), Has.Count.EqualTo(1));
            Assert.That(loader.GetLoadedContexts().ContainsKey(MismatchedPluginProvider.ProviderIdValue), Is.True);
            Assert.That(loader.GetLoadedContexts()[MismatchedPluginProvider.ProviderIdValue], Is.Not.SameAs(firstContext));
        });

        Assert.That(loader.UnloadPlugin(MismatchedPluginProvider.ProviderIdValue), Is.True);
        Assert.That(loader.GetLoadedContexts(), Is.Empty);
    }

    private static PluginManifest CreateMismatchedProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Mismatched provider test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(MismatchedPluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    public sealed class MismatchedPluginProvider : IUploaderProvider
    {
        public const string ProviderIdValue = "actual-provider-id";

        public string ProviderId => ProviderIdValue;
        public string Name => "Mismatched plugin provider";
        public string Description => "Provider used by PluginLoader tests.";
        public Version Version => new(1, 0, 0);
        public UploaderCategory[] SupportedCategories => new[] { UploaderCategory.Image };
        public Type ConfigModelType => typeof(object);

        public event EventHandler? ConfigChanged;

        public object? CreateConfigView() => null;
        public IUploaderConfigViewModel? CreateConfigViewModel() => null;
        public object CreateInstance(string settingsJson) => new object();
        public Dictionary<UploaderCategory, string[]> GetSupportedFileTypes() => new();
        public bool ValidateSettings(string settingsJson) => true;
        public string GetDefaultSettings(UploaderCategory category) => "{}";

        public void RaiseConfigChangedForTest() => ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
