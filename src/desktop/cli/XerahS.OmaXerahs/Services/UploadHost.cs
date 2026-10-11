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

using System.Reflection;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core.Uploaders;
using XerahS.OmaXerahs.Models;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.OmaXerahs.Services;

internal sealed class ImageDestinationInspection
{
    public bool Ready { get; init; }
    public UploaderInstance? Instance { get; init; }
    public IUploaderProvider? Provider { get; init; }
    public string SecretStoreBackend { get; init; } = "unknown";
    public bool SecretStoreFallback { get; init; }
    public int PluginsLoaded { get; init; }
}

internal static class UploadHost
{
    private static bool _bootstrapped;
    private static bool _pluginsLoaded;
    private static readonly object _lock = new();

    internal static string GetVersion()
    {
        string? version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.1.0";
        }

        int plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    internal static async Task EnsureBootstrappedAsync()
    {
        if (_bootstrapped)
        {
            return;
        }

        var result = await ShareXBootstrap.InitializeAsync(new BootstrapOptions
        {
            EnableLogging = true,
            ConsoleLogging = false,
            InitializeRecording = false,
            UIService = new HeadlessUIService(),
            ToastService = new HeadlessToastService()
        });

        if (!result.PlatformServicesInitialized)
        {
            throw new InvalidOperationException("Failed to initialize platform services.");
        }

        if (!result.ConfigurationLoaded)
        {
            throw new InvalidOperationException("Failed to load configuration.");
        }

        _bootstrapped = true;
    }

    internal static void EnsurePluginsLoaded()
    {
        lock (_lock)
        {
            if (_pluginsLoaded && ProviderCatalog.ArePluginsLoaded())
            {
                return;
            }

            ProviderContextManager.EnsureProviderContext();
            ProviderCatalog.InitializeBuiltInProviders();
            ProviderCatalog.LoadPlugins(PathsManager.GetPluginDirectories());
            _pluginsLoaded = true;
        }
    }

    internal static ImageDestinationInspection InspectImageDestination()
    {
        EnsurePluginsLoaded();

        string backend = "unknown";
        bool fallback = false;
        var secrets = ProviderCatalog.GetProviderContext()?.Secrets;
        if (secrets is ISecretStoreInfo info)
        {
            backend = info.BackendName;
            fallback = info.IsFallback;
        }

        int pluginsLoaded = ProviderCatalog.GetAllPluginMetadata().Count;
        var usable = GetUsableImageInstances();
        var preferred = PreferDefault(usable);

        return new ImageDestinationInspection
        {
            Ready = preferred != null,
            Instance = preferred,
            Provider = preferred == null ? null : ProviderCatalog.GetProvider(preferred.ProviderId),
            SecretStoreBackend = backend,
            SecretStoreFallback = fallback,
            PluginsLoaded = pluginsLoaded
        };
    }

    /// <summary>
    /// Image destinations, or when none is usable, File destinations whose file-type routing
    /// explicitly includes images (e.g. one S3 destination set to handle all file types). That is
    /// the user's routing, not an implicit Image→File fallback, so omaxerahs stays fail-closed.
    /// </summary>
    internal static List<UploaderInstance> GetUsableImageInstances()
    {
        EnsurePluginsLoaded();
        var images = InstanceManager.Instance.GetInstancesByCategory(UploaderCategory.Image)
            .Where(IsUsableImageInstance)
            .ToList();
        if (images.Count > 0)
        {
            return images;
        }

        return InstanceManager.Instance.GetInstancesByCategory(UploaderCategory.File)
            .Where(i => RoutesImages(i.FileTypeRouting) && IsUsableInstance(i, UploaderCategory.File))
            .ToList();
    }

    private static readonly string[] ImageExtensions = ["png", "jpg", "jpeg", "gif", "webp", "bmp", "tif", "tiff"];

    /// <summary>True when the routing takes all file types or lists an image extension ("png" or ".png").</summary>
    internal static bool RoutesImages(FileTypeScope? routing)
    {
        if (routing == null)
        {
            return false;
        }

        return routing.AllFileTypes || routing.FileExtensions.Any(ext =>
            ImageExtensions.Contains(ext.TrimStart('.'), StringComparer.OrdinalIgnoreCase));
    }

    internal static bool IsUsableImageInstance(UploaderInstance instance) =>
        IsUsableInstance(instance, UploaderCategory.Image);

    private static bool IsUsableInstance(UploaderInstance instance, UploaderCategory category)
    {
        if (instance.Category != category || !instance.IsAvailable)
        {
            return false;
        }

        if (InstanceManager.IsAutoProvider(instance.ProviderId))
        {
            return false;
        }

        var provider = ProviderCatalog.GetProvider(instance.ProviderId);
        if (provider == null)
        {
            return false;
        }

        try
        {
            return provider.ValidateSettings(instance.SettingsJson);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"ValidateSettings failed for {category} instance");
            return false;
        }
    }

    private static UploaderInstance? PreferDefault(List<UploaderInstance> usable)
    {
        if (usable.Count == 0)
        {
            return null;
        }

        var preferred = usable.FirstOrDefault(i =>
            InstanceManager.Instance.IsDefaultInstance(i.Category, i.InstanceId));
        return preferred ?? usable.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
    }

    internal static DoctorResponse CreateDoctorResponse(ImageDestinationInspection inspection)
    {
        string version = GetVersion();
        return new DoctorResponse
        {
            SchemaVersion = 1,
            Ok = inspection.Ready,
            Cli = new DoctorCliInfo { Name = "omaxerahs", Version = version },
            Image = new DoctorImageInfo
            {
                Ready = inspection.Ready,
                ProviderId = inspection.Instance?.ProviderId,
                InstanceId = inspection.Instance?.InstanceId,
                DisplayName = inspection.Instance?.DisplayName
            },
            SecretStore = new DoctorSecretStoreInfo
            {
                Backend = inspection.SecretStoreBackend,
                Fallback = inspection.SecretStoreFallback
            },
            Plugins = new DoctorPluginsInfo { Loaded = inspection.PluginsLoaded },
            History = ProbeHistoryDatabase()
        };
    }

    /// <summary>
    /// Opens an in-memory SQLite connection. A single-file publish that cannot load the native
    /// e_sqlite3 provider fails here (TypeInitializationException) instead of silently losing
    /// history entries after an upload (XIP0088 Phase 0 item 3).
    /// </summary>
    internal static DoctorHistoryInfo ProbeHistoryDatabase()
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version();";
            _ = command.ExecuteScalar();
            return new DoctorHistoryInfo { Ok = true };
        }
        catch (Exception ex)
        {
            Exception root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return new DoctorHistoryInfo { Ok = false, Error = $"{root.GetType().Name}: {root.Message}" };
        }
    }
}
