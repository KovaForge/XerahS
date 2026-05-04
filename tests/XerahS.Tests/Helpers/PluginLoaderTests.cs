#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Collections.ObjectModel;
using System.Reflection;
using NUnit.Framework;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class PluginLoaderTests
{
    [Test]
    public void ProviderCatalog_GetProvider_UsesCaseInsensitiveProviderIds()
    {
        ProviderCatalog.Clear();
        var provider = new MismatchedPluginProvider();

        ProviderCatalog.RegisterProvider(provider);

        Assert.Multiple(() =>
        {
            Assert.That(ProviderCatalog.GetProvider(MismatchedPluginProvider.ProviderIdValue.ToUpperInvariant()), Is.SameAs(provider));
            Assert.That(ProviderCatalog.GetProvider(MismatchedPluginProvider.ProviderIdValue.ToLowerInvariant()), Is.SameAs(provider));
            Assert.That(ProviderCatalog.GetAllProviders(), Has.Count.EqualTo(1));
        });

        ProviderCatalog.Clear();
    }

    [Test]
    public void ProviderCatalog_BlankProviderIdLookups_ReturnNull()
    {
        ProviderCatalog.Clear();
        var provider = new MismatchedPluginProvider();

        ProviderCatalog.RegisterProvider(provider);

        Assert.Multiple(() =>
        {
            Assert.That(ProviderCatalog.GetProvider(null!), Is.Null);
            Assert.That(ProviderCatalog.GetProvider(string.Empty), Is.Null);
            Assert.That(ProviderCatalog.GetProvider("   "), Is.Null);
            Assert.That(ProviderCatalog.GetPluginMetadata(null!), Is.Null);
            Assert.That(ProviderCatalog.GetPluginMetadata(string.Empty), Is.Null);
            Assert.That(ProviderCatalog.GetPluginMetadata("   "), Is.Null);
            Assert.That(ProviderCatalog.GetExplorer(null!), Is.Null);
            Assert.That(ProviderCatalog.GetExplorer(string.Empty), Is.Null);
            Assert.That(ProviderCatalog.GetExplorer("   "), Is.Null);
            Assert.That(ProviderCatalog.GetAllProviders(), Has.Count.EqualTo(1));
        });

        ProviderCatalog.Clear();
    }

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
    public void GetLoadedContexts_ReturnsSnapshotSoUnloadHandlesCannotBeClearedExternally()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(CreateMismatchedProviderManifest("manifest-plugin-id"), Path.GetDirectoryName(assemblyPath)!, assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Not.Null);
        var contexts = loader.GetLoadedContexts();

        Assert.Multiple(() =>
        {
            Assert.That(contexts, Is.InstanceOf<ReadOnlyDictionary<string, PluginLoadContext>>());
            Assert.That(contexts, Has.Count.EqualTo(1));
            Assert.That(loader.GetLoadedContexts(), Has.Count.EqualTo(1));
        });

        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, PluginLoadContext>)contexts).Clear());
        Assert.That(loader.GetLoadedContexts(), Has.Count.EqualTo(1));

        Assert.That(loader.UnloadPlugin(MismatchedPluginProvider.ProviderIdValue), Is.True);
        Assert.That(loader.GetLoadedContexts(), Is.Empty);
    }

    [Test]
    public void UnloadPlugin_BlankPluginId_ReturnsFalseWithoutMutatingLoadedContexts()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(CreateMismatchedProviderManifest("manifest-plugin-id"), Path.GetDirectoryName(assemblyPath)!, assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(loader.UnloadPlugin(null!), Is.False);
            Assert.That(loader.UnloadPlugin(string.Empty), Is.False);
            Assert.That(loader.UnloadPlugin("   "), Is.False);
            Assert.That(loader.GetLoadedContexts(), Has.Count.EqualTo(1));
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

    [Test]
    public void LoadPlugin_DuplicateProviderIdDifferentCasing_ReplacesPreviousContextAndUnloadsCaseInsensitively()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        string pluginDirectory = Path.GetDirectoryName(assemblyPath)!;
        var firstMetadata = new PluginMetadata(CreateMismatchedProviderManifest("first-manifest-id"), pluginDirectory, assemblyPath);
        var secondMetadata = new PluginMetadata(CreateUppercaseProviderManifest("second-manifest-id"), pluginDirectory, assemblyPath);

        Assert.That(loader.LoadPlugin(firstMetadata), Is.Not.Null);
        var firstContext = loader.GetLoadedContexts()[MismatchedPluginProvider.ProviderIdValue];

        Assert.That(loader.LoadPlugin(secondMetadata), Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(loader.GetLoadedContexts(), Has.Count.EqualTo(1));
            Assert.That(loader.GetLoadedContexts().ContainsKey(UppercasePluginProvider.ProviderIdValue), Is.True);
            Assert.That(loader.GetLoadedContexts()[UppercasePluginProvider.ProviderIdValue], Is.Not.SameAs(firstContext));
        });

        Assert.That(loader.UnloadPlugin(MismatchedPluginProvider.ProviderIdValue.ToUpperInvariant()), Is.True);
        Assert.That(loader.GetLoadedContexts(), Is.Empty);
    }

    [Test]
    public void LoadPlugin_MissingAssembly_ReportsAssemblyNotFound()
    {
        var loader = new PluginLoader();
        string missingAssemblyPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"missing-plugin-{Guid.NewGuid():N}.dll");
        var metadata = new PluginMetadata(
            CreateMismatchedProviderManifest("missing-assembly-plugin"),
            Path.GetDirectoryName(missingAssemblyPath)!,
            missingAssemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Is.EqualTo($"Assembly not found: {missingAssemblyPath}"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_BlankProviderId_ReportsErrorAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateBlankProviderIdManifest("blank-provider-id-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Is.EqualTo("Provider ID is empty"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_ProviderConstructorMissingDependency_ReportsDependencyNotFoundAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateMissingDependencyProviderManifest("missing-dependency-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Is.EqualTo("Dependency not found: Missing.Plugin.Dependency.dll"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_ProviderConstructorFileLoadException_ReportsDependencyLoadFailureAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateFileLoadFailureProviderManifest("file-load-failure-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Does.StartWith("Dependency load failed: Blocked.Plugin.Dependency.dll:"));
            Assert.That(metadata.LoadError, Does.Contain("Could not load plugin dependency"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_ProviderConstructorTypeLoadException_ReportsTypeLoadErrorAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateTypeLoadFailureProviderManifest("type-load-failure-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Does.StartWith("Type load error: Missing provider dependency type"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_ProviderConstructorReflectionTypeLoadException_ReportsLoaderExceptionAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateReflectionTypeLoadFailureProviderManifest("reflection-type-load-failure-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Does.StartWith("Reflection type load error: Reflection load failed"));
            Assert.That(metadata.LoadError, Does.Contain("Missing reflection dependency"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    [Test]
    public void LoadPlugin_ProviderConstructorBadImageFormatException_ReportsIncompatibleAssemblyAndDoesNotTrackContext()
    {
        var loader = new PluginLoader();
        string assemblyPath = typeof(PluginLoaderTests).Assembly.Location;
        var metadata = new PluginMetadata(
            CreateBadImageFormatFailureProviderManifest("bad-image-format-failure-plugin"),
            Path.GetDirectoryName(assemblyPath)!,
            assemblyPath);

        Assert.That(loader.LoadPlugin(metadata), Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LoadError, Does.StartWith("Invalid or incompatible assembly image: Unsupported processor architecture"));
            Assert.That(loader.GetLoadedContexts(), Is.Empty);
        });
    }

    private static PluginManifest CreateMismatchedProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Mismatched provider test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(MismatchedPluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateBlankProviderIdManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Blank provider ID test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(BlankProviderIdPluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateUppercaseProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Uppercase provider ID test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(UppercasePluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateMissingDependencyProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Missing dependency test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(MissingDependencyPluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateFileLoadFailureProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "File load failure test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(FileLoadFailurePluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateTypeLoadFailureProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Type load failure test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(TypeLoadFailurePluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateReflectionTypeLoadFailureProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Reflection type load failure test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(ReflectionTypeLoadFailurePluginProvider).FullName!,
        SupportedCategories = new List<string> { nameof(UploaderCategory.Image) }
    };

    private static PluginManifest CreateBadImageFormatFailureProviderManifest(string pluginId) => new()
    {
        PluginId = pluginId,
        Name = "Bad image format failure test",
        ApiVersion = PluginDiscovery.GetCurrentApiVersion(),
        EntryPoint = typeof(BadImageFormatFailurePluginProvider).FullName!,
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

    public sealed class BlankProviderIdPluginProvider : IUploaderProvider
    {
        public string ProviderId => "   ";
        public string Name => "Blank provider ID plugin provider";
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

    public sealed class UppercasePluginProvider : IUploaderProvider
    {
        public const string ProviderIdValue = "ACTUAL-PROVIDER-ID";

        public string ProviderId => ProviderIdValue;
        public string Name => "Uppercase provider ID plugin provider";
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

    public sealed class MissingDependencyPluginProvider : IUploaderProvider
    {
        public MissingDependencyPluginProvider() =>
            throw new FileNotFoundException("Missing dependency", "Missing.Plugin.Dependency.dll");

        public string ProviderId => "missing-dependency-provider";
        public string Name => "Missing dependency plugin provider";
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

    public sealed class FileLoadFailurePluginProvider : IUploaderProvider
    {
        public FileLoadFailurePluginProvider() =>
            throw new FileLoadException("Could not load plugin dependency", "Blocked.Plugin.Dependency.dll");

        public string ProviderId => "file-load-failure-provider";
        public string Name => "File load failure plugin provider";
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

    public sealed class TypeLoadFailurePluginProvider : IUploaderProvider
    {
        public TypeLoadFailurePluginProvider() =>
            throw new TypeLoadException("Missing provider dependency type");

        public string ProviderId => "type-load-failure-provider";
        public string Name => "Type load failure plugin provider";
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

    public sealed class ReflectionTypeLoadFailurePluginProvider : IUploaderProvider
    {
        public ReflectionTypeLoadFailurePluginProvider() =>
            throw new ReflectionTypeLoadException(
                Array.Empty<Type>(),
                new Exception[] { new FileNotFoundException("Missing reflection dependency", "Missing.Reflection.Dependency.dll") },
                "Reflection load failed");

        public string ProviderId => "reflection-type-load-failure-provider";
        public string Name => "Reflection type load failure plugin provider";
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

    public sealed class BadImageFormatFailurePluginProvider : IUploaderProvider
    {
        public BadImageFormatFailurePluginProvider() =>
            throw new BadImageFormatException("Unsupported processor architecture");

        public string ProviderId => "bad-image-format-failure-provider";
        public string Name => "Bad image format failure plugin provider";
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
