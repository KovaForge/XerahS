#region License Information (GPL v3)
/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
*/
#endregion

namespace XerahS.Uploaders.PluginSystem;

/// <summary>Plugin manifest model (deserialized from plugin.json).</summary>
public class PluginManifest
{
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "1.0";
    public string EntryPoint { get; set; } = string.Empty;
    public string? AssemblyFileName { get; set; }
    public List<string> SupportedCategories { get; set; } = new();
    public string? ConfigViewId { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? HomepageUrl { get; set; }
    public bool SupportsExplorer { get; set; }

    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(PluginId)) { error = "PluginId is required"; return false; }
        if (!IsSafePluginId(PluginId)) { error = "PluginId may only contain letters, digits, '.', '_' and '-' and must not be a path"; return false; }
        if (string.IsNullOrWhiteSpace(Name)) { error = "Name is required"; return false; }
        if (string.IsNullOrWhiteSpace(EntryPoint)) { error = "EntryPoint is required"; return false; }
        if (string.IsNullOrWhiteSpace(ApiVersion)) { error = "ApiVersion is required"; return false; }
        if (!string.IsNullOrWhiteSpace(AssemblyFileName) && !IsSafeAssemblyFileName(AssemblyFileName)) { error = "AssemblyFileName must be a simple .dll file name"; return false; }
        if (Dependencies == null) { error = "Dependencies must be a list when provided"; return false; }
        if (Dependencies.Any(string.IsNullOrWhiteSpace)) { error = "Dependencies must not contain empty values"; return false; }
        if (Dependencies.Any(dependency => !IsSafeDependencyPath(dependency))) { error = "Dependencies must be canonical relative file paths"; return false; }
        if (!SupportedCategories.Any()) { error = "At least one SupportedCategory is required"; return false; }
        error = null;
        return true;
    }

    public bool IsCompatibleWith(string currentApiVersion)
    {
        var pluginMajor = GetMajorVersion(ApiVersion);
        var currentMajor = GetMajorVersion(currentApiVersion);
        return pluginMajor == currentMajor;
    }

    private static int GetMajorVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0;
    }

    public string GetAssemblyFileName()
    {
        return string.IsNullOrWhiteSpace(AssemblyFileName) ? $"{PluginId}.dll" : AssemblyFileName;
    }

    private static bool IsSafePluginId(string pluginId)
    {
        if (pluginId is "." or "..")
        {
            return false;
        }

        foreach (char c in pluginId)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeAssemblyFileName(string assemblyFileName)
    {
        if (assemblyFileName is "." or ".." || Path.IsPathRooted(assemblyFileName))
        {
            return false;
        }

        if (assemblyFileName.Contains('/') || assemblyFileName.Contains('\\'))
        {
            return false;
        }

        return string.Equals(Path.GetExtension(assemblyFileName), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeDependencyPath(string dependencyPath)
    {
        if (Path.IsPathRooted(dependencyPath) || dependencyPath.Contains('\\') || dependencyPath.Contains(':'))
        {
            return false;
        }

        string[] segments = dependencyPath.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(Path.GetFileName(dependencyPath));
    }
}
