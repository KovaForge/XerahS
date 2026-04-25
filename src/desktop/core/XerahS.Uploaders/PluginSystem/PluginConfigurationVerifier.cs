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

#nullable disable
using Newtonsoft.Json;
using XerahS.Common;

namespace XerahS.Uploaders.PluginSystem;

/// <summary>
/// Plugin configuration verification status
/// </summary>
public enum PluginVerificationStatus
{
    /// <summary>
    /// Plugin is properly configured (3 files in folder)
    /// </summary>
    Valid,

    /// <summary>
    /// Plugin may have minor configuration issues (any file count outside 3)
    /// </summary>
    Warning,

    /// <summary>
    /// Plugin has critical configuration errors (duplicate framework DLLs)
    /// </summary>
    Error
}

/// <summary>
/// Result of plugin configuration verification
/// </summary>
public class PluginVerificationResult
{
    public PluginVerificationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
    public int FileCount { get; set; }
    public List<string> ProblematicFiles { get; set; } = new();
}

/// <summary>
/// Verifies plugin folder configuration to detect duplicate framework DLLs
/// </summary>
public static class PluginConfigurationVerifier
{
    private static readonly string[] ProblematicDlls = new[]
    {
        "Avalonia.Base.dll",
        "Avalonia.Controls.dll",
        "Avalonia.Themes.Fluent.dll",
        "Avalonia.Markup.Xaml.dll",
        "Avalonia.Markup.dll",
        "CommunityToolkit.Mvvm.dll",
        "Newtonsoft.Json.dll"
    };

    /// <summary>
    /// Verifies a plugin's folder configuration
    /// </summary>
    /// <param name="providerId">The plugin provider ID</param>
    /// <returns>Verification result</returns>
    public static PluginVerificationResult VerifyPluginConfiguration(string providerId)
    {
        var result = new PluginVerificationResult();

        if (string.IsNullOrWhiteSpace(providerId))
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin provider ID is missing";
            result.Issues.Add("A plugin provider ID is required for verification.");
            return result;
        }

        // Custom uploaders are single .sxcu files, not plugin folders - skip verification
        if (providerId.StartsWith("custom_", StringComparison.OrdinalIgnoreCase))
        {
            result.Status = PluginVerificationStatus.Valid;
            result.Message = "Custom uploader (.sxcu file)";
            result.Issues.Add("Custom uploaders are single JSON files and do not require folder verification.");
            return result;
        }

        // Find plugin folder - prefer loaded metadata, then search known plugin roots.
        var pluginsPath = ResolvePluginDirectory(providerId);

        if (pluginsPath == null)
        {
            var searchedPaths = PathsManager.GetPluginDirectories()
                .Select(d => Path.Combine(d, providerId));
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin folder not found";
            result.Issues.Add($"Plugin folder does not exist in any known plugin directory.");
            result.Issues.AddRange(searchedPaths.Select(p => $"  Checked: {p}"));
            return result;
        }

        // Count files (excluding subdirectories like runtimes/)
        var files = Directory.GetFiles(pluginsPath, "*.*", SearchOption.TopDirectoryOnly);
        result.FileCount = files.Length;

        // Check for problematic DLLs
        foreach (var dll in ProblematicDlls)
        {
            if (files.Any(f => Path.GetFileName(f).Equals(dll, StringComparison.OrdinalIgnoreCase)))
            {
                result.ProblematicFiles.Add(dll);
            }
        }

        var manifestPath = Path.Combine(pluginsPath, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin manifest not found";
            result.Issues.Add($"Missing required file: {manifestPath}");
            return result;
        }

        PluginManifest manifest;
        try
        {
            manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifestPath));
        }
        catch (Exception ex)
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin manifest is unreadable";
            result.Issues.Add($"Failed to read plugin.json: {ex.Message}");
            return result;
        }

        string manifestError = null;
        if (manifest == null || !manifest.IsValid(out manifestError))
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin manifest is invalid";
            result.Issues.Add($"Invalid plugin.json: {manifestError ?? "Failed to deserialize manifest."}");
            return result;
        }

        var assemblyPath = Path.Combine(pluginsPath, manifest.GetAssemblyFileName());
        if (!File.Exists(assemblyPath))
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = "Plugin assembly not found";
            result.Issues.Add($"Missing plugin assembly: {assemblyPath}");
            return result;
        }

        if (result.ProblematicFiles.Count > 0)
        {
            result.Status = PluginVerificationStatus.Error;
            result.Message = $"\u26A0\uFE0F Config view may not load - {result.ProblematicFiles.Count} duplicate framework DLL(s) detected";
            result.Issues.Add($"Found {result.ProblematicFiles.Count} duplicate framework assemblies in the plugin folder:");
            result.Issues.AddRange(result.ProblematicFiles);
            result.Issues.Add("");
            result.Issues.Add("Fix: Delete these duplicate DLLs from the plugin folder, then restart the app.");
        }
        else
        {
            result.Status = PluginVerificationStatus.Valid;
            result.Message = $"\u2713 Plugin properly configured ({result.FileCount} top-level files)";
            result.Issues.Add("Plugin manifest and assembly were found.");
            result.Issues.Add("No duplicate framework assemblies detected.");
        }

        return result;
    }

    /// <summary>
    /// Cleans duplicate framework DLLs from a plugin folder
    /// </summary>
    /// <param name="providerId">The plugin provider ID</param>
    /// <returns>Number of files deleted</returns>
    public static int CleanDuplicateFrameworkDlls(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return 0;
        }

        // Only clean user RID-scoped plugin folders; app-bundled plugins may be read-only.
        var pluginsPath = ResolvePluginDirectory(providerId);
        if (pluginsPath == null ||
            !Directory.Exists(pluginsPath) ||
            !pluginsPath.StartsWith(PathsManager.PluginsArchitectureFolder, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int deletedCount = 0;
        var files = Directory.GetFiles(pluginsPath, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var dll in ProblematicDlls)
        {
            var filePath = Path.Combine(pluginsPath, dll);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    Common.DebugHelper.WriteLine($"[PluginVerifier] Deleted duplicate DLL: {dll}");
                }
                catch (Exception ex)
                {
                    Common.DebugHelper.WriteException(ex, $"Failed to delete {dll}");
                }
            }
        }

        // Also clean up other Avalonia-related DLLs that might be duplicates
        var avaloniaPatterns = new[] { "Avalonia.*.dll", "MicroCom.*.dll" };
        foreach (var pattern in avaloniaPatterns)
        {
            var matchingFiles = Directory.GetFiles(pluginsPath, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in matchingFiles)
            {
                var fileName = Path.GetFileName(file);
                // Skip the main Avalonia.dll if it's in the list already
                if (ProblematicDlls.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    continue; // Already handled above
                }

                // Delete other Avalonia assemblies
                try
                {
                    File.Delete(file);
                    deletedCount++;
                    Common.DebugHelper.WriteLine($"[PluginVerifier] Deleted Avalonia-related DLL: {fileName}");
                }
                catch (Exception ex)
                {
                    Common.DebugHelper.WriteException(ex, $"Failed to delete {fileName}");
                }
            }
        }

        return deletedCount;
    }

    private static string ResolvePluginDirectory(string providerId)
    {
        if (!IsSafeProviderDirectoryName(providerId))
        {
            return null;
        }

        var metadata = ProviderCatalog.GetPluginMetadata(providerId);
        if (!string.IsNullOrWhiteSpace(metadata?.PluginDirectory) && Directory.Exists(metadata.PluginDirectory))
        {
            return Path.GetFullPath(metadata.PluginDirectory);
        }

        return PathsManager.GetPluginDirectories()
            .Select(directory => Path.Combine(directory, providerId))
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists);
    }

    private static bool IsSafeProviderDirectoryName(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return false;
        }

        if (providerId == "." || providerId == "..")
        {
            return false;
        }

        return providerId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            providerId.IndexOf(Path.DirectorySeparatorChar) < 0 &&
            providerId.IndexOf(Path.AltDirectorySeparatorChar) < 0 &&
            providerId.IndexOf('/') < 0 &&
            providerId.IndexOf('\\') < 0;
    }
}
