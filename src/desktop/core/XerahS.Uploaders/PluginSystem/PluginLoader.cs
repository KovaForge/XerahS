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
using System.Reflection;
using XerahS.Common;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Loads plugin assemblies and instantiates providers
/// </summary>
public class PluginLoader
{
    private readonly Dictionary<string, PluginLoadContext> _loadedContexts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Load a plugin from its metadata
    /// </summary>
    public IUploaderProvider? LoadPlugin(PluginMetadata metadata)
    {
        PluginLoadContext? loadContext = null;

        try
        {
            if (!File.Exists(metadata.AssemblyPath))
            {
                metadata.LoadError = $"Assembly not found: {metadata.AssemblyPath}";
                DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
                return null;
            }

            if (!IsAssemblyCompatibleWithCurrentProcess(metadata.AssemblyPath, out string? compatibilityError))
            {
                metadata.LoadError = compatibilityError;
                DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
                return null;
            }

            // Create isolated load context
            loadContext = new PluginLoadContext(metadata.AssemblyPath, metadata.PluginDirectory);

            // Load the plugin assembly
            var assembly = loadContext.LoadFromAssemblyPath(metadata.AssemblyPath);

            // Find and instantiate the provider type
            var providerType = assembly.GetType(metadata.Manifest.EntryPoint);
            if (providerType == null)
            {
                metadata.LoadError = $"Entry point type not found: {metadata.Manifest.EntryPoint}";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                UnloadFailedContext(loadContext);
                return null;
            }

            // Verify it implements IUploaderProvider
            if (!typeof(IUploaderProvider).IsAssignableFrom(providerType))
            {
                metadata.LoadError = $"Type {providerType.FullName} does not implement IUploaderProvider";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                UnloadFailedContext(loadContext);
                return null;
            }

            // Instantiate the provider
            var provider = Activator.CreateInstance(providerType) as IUploaderProvider;
            if (provider == null)
            {
                metadata.LoadError = "Failed to instantiate provider";
                DebugHelper.WriteLine($"ERROR: {metadata.LoadError}");
                UnloadFailedContext(loadContext);
                return null;
            }

            if (string.IsNullOrWhiteSpace(provider.ProviderId))
            {
                metadata.LoadError = "Provider ID is empty";
                DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
                UnloadFailedContext(loadContext);
                return null;
            }

            // Verify plugin ID matches
            if (provider.ProviderId != metadata.Manifest.PluginId)
            {
                DebugHelper.WriteLine($"WARNING: Plugin ID mismatch - manifest: {metadata.Manifest.PluginId}, provider: {provider.ProviderId}");
                // Allow it but warn
            }

            // Store load context by the runtime provider ID. ProviderCatalog registers and unloads
            // plugins by provider.ProviderId, so using the manifest ID here can leave a
            // mismatched plugin's collectible context alive after force reload/removal.
            // If the same provider ID is loaded again before the caller removes the old
            // provider, unload the previous collectible context instead of overwriting the
            // dictionary entry and losing the only handle ProviderCatalog can unload later.
            if (_loadedContexts.TryGetValue(provider.ProviderId, out var existingContext))
            {
                UnloadFailedContext(existingContext);
            }

            _loadedContexts[provider.ProviderId] = loadContext;

            metadata.Provider = provider;

            return provider;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is FileNotFoundException fileNotFoundException)
        {
            metadata.LoadError = FormatDependencyNotFoundError(fileNotFoundException);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is FileLoadException fileLoadException)
        {
            metadata.LoadError = FormatDependencyLoadError(fileLoadException);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is TypeLoadException typeLoadException)
        {
            metadata.LoadError = FormatTypeLoadError(typeLoadException);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ReflectionTypeLoadException reflectionTypeLoadException)
        {
            metadata.LoadError = FormatReflectionTypeLoadError(reflectionTypeLoadException);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
            WriteLoaderExceptions(reflectionTypeLoadException);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is BadImageFormatException badImageFormatException)
        {
            metadata.LoadError = FormatBadImageFormatError(badImageFormatException);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (FileNotFoundException ex)
        {
            metadata.LoadError = FormatDependencyNotFoundError(ex);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (FileLoadException ex)
        {
            metadata.LoadError = FormatDependencyLoadError(ex);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (BadImageFormatException ex)
        {
            metadata.LoadError = FormatBadImageFormatError(ex);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (TypeLoadException ex)
        {
            metadata.LoadError = FormatTypeLoadError(ex);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
        }
        catch (ReflectionTypeLoadException ex)
        {
            metadata.LoadError = FormatReflectionTypeLoadError(ex);
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
            WriteLoaderExceptions(ex);
        }
        catch (Exception ex)
        {
            metadata.LoadError = $"Unexpected error: {ex.Message}";
            DebugHelper.WriteLine($"ERROR loading plugin {metadata.Manifest.PluginId}: {metadata.LoadError}");
            DebugHelper.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        if (loadContext != null)
        {
            UnloadFailedContext(loadContext);
        }

        return null;
    }

    /// <summary>
    /// Unload a plugin (experimental - requires further testing)
    /// </summary>
    public bool UnloadPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || !_loadedContexts.TryGetValue(pluginId, out var context))
        {
            return false;
        }

        try
        {
            context.Unload();
            _loadedContexts.Remove(pluginId);

            ForceUnloadCollection();

            DebugHelper.WriteLine($"Unloaded plugin: {pluginId}");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Error unloading plugin {pluginId}: {ex.Message}");
            return false;
        }
    }

    internal void UnloadAllPlugins()
    {
        foreach (var (pluginId, context) in _loadedContexts.ToList())
        {
            try
            {
                context.Unload();
                DebugHelper.WriteLine($"Unloaded plugin: {pluginId}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Error unloading plugin {pluginId}: {ex.Message}");
            }
        }

        _loadedContexts.Clear();
        ForceUnloadCollection();
    }

    /// <summary>
    /// Get list of loaded plugin contexts
    /// </summary>
    public IReadOnlyDictionary<string, PluginLoadContext> GetLoadedContexts() =>
        new ReadOnlyDictionary<string, PluginLoadContext>(new Dictionary<string, PluginLoadContext>(_loadedContexts, _loadedContexts.Comparer));

    private static void UnloadFailedContext(PluginLoadContext loadContext)
    {
        loadContext.Unload();
        ForceUnloadCollection();
    }

    private static void ForceUnloadCollection()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static string FormatTypeLoadError(TypeLoadException ex) => $"Type load error: {ex.Message}";

    private static string FormatBadImageFormatError(BadImageFormatException ex) => $"Invalid or incompatible assembly image: {ex.Message}";

    private static string FormatDependencyNotFoundError(FileNotFoundException ex)
    {
        string fileName = string.IsNullOrWhiteSpace(ex.FileName) ? "unknown assembly" : ex.FileName;
        return $"Dependency not found: {fileName}: {ex.Message}";
    }

    private static string FormatDependencyLoadError(FileLoadException ex)
    {
        string fileName = string.IsNullOrWhiteSpace(ex.FileName) ? "unknown assembly" : ex.FileName;
        return $"Dependency load failed: {fileName}: {ex.Message}";
    }

    private static string FormatReflectionTypeLoadError(ReflectionTypeLoadException ex)
    {
        string[] loaderMessages = ex.LoaderExceptions
            .Where(loaderException => loaderException != null)
            .Select(loaderException => FormatLoaderException(loaderException!))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        string error = $"Reflection type load error: {ex.Message}";

        if (loaderMessages.Length > 0)
        {
            error += $" Loader exceptions: {string.Join("; ", loaderMessages)}";
        }

        return error;
    }

    private static string FormatLoaderException(Exception ex) => ex switch
    {
        FileNotFoundException fileNotFoundException => FormatDependencyNotFoundError(fileNotFoundException),
        FileLoadException fileLoadException => FormatDependencyLoadError(fileLoadException),
        BadImageFormatException badImageFormatException => FormatBadImageFormatError(badImageFormatException),
        TypeLoadException typeLoadException => FormatTypeLoadError(typeLoadException),
        ReflectionTypeLoadException reflectionTypeLoadException => FormatReflectionTypeLoadError(reflectionTypeLoadException),
        _ => ex.Message
    };

    private static void WriteLoaderExceptions(ReflectionTypeLoadException ex)
    {
        foreach (var loaderEx in ex.LoaderExceptions)
        {
            DebugHelper.WriteLine($"  Loader exception: {loaderEx?.Message}");
        }
    }

    private static bool IsAssemblyCompatibleWithCurrentProcess(string assemblyPath, out string? error)
    {
        error = null;

        try
        {
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader peReader = new(stream);
            PEHeaders peHeaders = peReader.PEHeaders;

            bool isAnyCpu = peHeaders.CorHeader != null &&
                (peHeaders.CorHeader.Flags & CorFlags.ILOnly) != 0 &&
                (peHeaders.CorHeader.Flags & CorFlags.Requires32Bit) == 0;

            Architecture? assemblyArchitecture = peHeaders.CoffHeader.Machine switch
            {
                Machine.Arm64 => Architecture.Arm64,
                Machine.Amd64 => Architecture.X64,
                Machine.I386 when isAnyCpu => null,
                Machine.I386 => Architecture.X86,
                _ => null
            };

            if (assemblyArchitecture == null)
            {
                return true;
            }

            if (assemblyArchitecture != RuntimeInformation.ProcessArchitecture)
            {
                error =
                    $"Plugin assembly architecture '{assemblyArchitecture}' is not compatible with current process architecture '{RuntimeInformation.ProcessArchitecture}'.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to inspect plugin assembly architecture: {ex.Message}";
            return false;
        }
    }
}
