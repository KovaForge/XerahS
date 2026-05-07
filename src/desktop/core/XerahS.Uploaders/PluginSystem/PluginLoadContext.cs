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
using System.Runtime.Loader;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation
/// Allows plugins to be loaded and potentially unloaded
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath, string pluginDirectory)
        : base(isCollectible: true) // Enable unloading
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = pluginDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Check shared dependencies FIRST so the host's version is always used,
        // even if the resolver finds a path via the plugin's .deps.json.
        // Loading a shared assembly in the plugin context causes type-identity
        // mismatches (TypeLoadException: "does not have an implementation").
        if (IsSharedDependency(assemblyName))
        {
            return null; // Let the default (host) context handle it
        }

        // Resolve plugin-private assemblies from the plugin directory
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        assemblyPath = ResolveAssemblyFromPluginDirectory(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        libraryPath = ResolveUnmanagedDllFromPluginDirectory(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    private bool IsSharedDependency(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;

        // These assemblies must come from the host, not be duplicated in the plugin context.
        return string.Equals(name, "XerahS.Uploaders", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "XerahS.UploaderPluginSdk", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "XerahS.Common", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "CommunityToolkit.Mvvm", StringComparison.OrdinalIgnoreCase) ||
               name?.StartsWith("System.", StringComparison.OrdinalIgnoreCase) == true ||
               name?.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) == true ||
               name?.StartsWith("Avalonia.", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string? ResolveAssemblyFromPluginDirectory(AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
        {
            return null;
        }

        string pluginDirectory = Path.GetFullPath(_pluginDirectory);
        string assemblyPath = Path.GetFullPath(Path.Combine(pluginDirectory, $"{assemblyName.Name}.dll"));
        string directoryPrefix = pluginDirectory.EndsWith(Path.DirectorySeparatorChar) ? pluginDirectory : pluginDirectory + Path.DirectorySeparatorChar;

        if (!assemblyPath.StartsWith(directoryPrefix, StringComparison.Ordinal) ||
            !File.Exists(assemblyPath) ||
            !AssemblyIdentityMatchesRequest(assemblyPath, assemblyName))
        {
            return null;
        }

        return assemblyPath;
    }

    internal static bool AssemblyIdentityMatchesRequest(string assemblyPath, AssemblyName requestedName)
    {
        AssemblyName candidateName;
        try
        {
            candidateName = AssemblyName.GetAssemblyName(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }

        if (!string.Equals(candidateName.Name, requestedName.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requestedName.Version != null && candidateName.Version != requestedName.Version)
        {
            return false;
        }

        if (!string.Equals(candidateName.CultureName ?? string.Empty, requestedName.CultureName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] requestedToken = requestedName.GetPublicKeyToken() ?? Array.Empty<byte>();
        byte[] candidateToken = candidateName.GetPublicKeyToken() ?? Array.Empty<byte>();
        if (!requestedToken.SequenceEqual(candidateToken))
        {
            return false;
        }

        return true;
    }

    protected string? ResolveUnmanagedDllFromPluginDirectory(string unmanagedDllName)
    {
        if (string.IsNullOrWhiteSpace(unmanagedDllName) ||
            unmanagedDllName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
        {
            return null;
        }

        string pluginDirectory = Path.GetFullPath(_pluginDirectory);
        string directoryPrefix = pluginDirectory.EndsWith(Path.DirectorySeparatorChar) ? pluginDirectory : pluginDirectory + Path.DirectorySeparatorChar;

        foreach (string candidateName in GetUnmanagedDllCandidateNames(unmanagedDllName))
        {
            string libraryPath = Path.GetFullPath(Path.Combine(pluginDirectory, candidateName));
            if (libraryPath.StartsWith(directoryPrefix, StringComparison.Ordinal) && File.Exists(libraryPath))
            {
                return libraryPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetUnmanagedDllCandidateNames(string unmanagedDllName)
    {
        yield return unmanagedDllName;

        string extension = Path.GetExtension(unmanagedDllName);
        if (!string.IsNullOrEmpty(extension))
        {
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            yield return $"{unmanagedDllName}.dll";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return $"{unmanagedDllName}.dylib";
            yield return $"lib{unmanagedDllName}.dylib";
            yield break;
        }

        yield return $"{unmanagedDllName}.so";
        yield return $"lib{unmanagedDllName}.so";
    }
}
