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

using System.CommandLine;
using System.Text.Json;
using XerahS.Core;

namespace XerahS.CLI.Commands;

public static class ReClipCommand
{
    public const string DefaultWatchFolder = "/Users/mike/Library/CloudStorage/OneDrive-Personal/Videos/ReClip";
    public const string ConfigFileName = "ReClipConfig.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigFilePath => Path.Combine(SettingsManager.SettingsFolder, ConfigFileName);

    public static Command Create()
    {
        var command = new Command("reclip", "Manage ReClip integration settings");
        var jsonOption = new Option<bool>("--json") { Description = "Output machine-readable JSON." };

        var statusCommand = new Command("status", "Show ReClip integration configuration");
        statusCommand.Add(jsonOption);
        statusCommand.SetAction(parseResult =>
        {
            Environment.ExitCode = ShowStatus(parseResult.GetValue(jsonOption));
        });

        var setWatchFolderCommand = new Command("set-watch-folder", "Set the ReClip watch folder used by local automation");
        var watchFolderArgument = new Argument<string>("folder")
        {
            Description = "Folder path to use for ReClip handoff files."
        };
        setWatchFolderCommand.Add(watchFolderArgument);
        setWatchFolderCommand.Add(jsonOption);
        setWatchFolderCommand.SetAction(parseResult =>
        {
            var folder = parseResult.GetValue(watchFolderArgument) ?? DefaultWatchFolder;
            Environment.ExitCode = SetWatchFolder(folder, parseResult.GetValue(jsonOption));
        });

        var useDefaultCommand = new Command("use-default-watch-folder", "Set ReClip watch folder to Michael's OneDrive ReClip folder");
        useDefaultCommand.Add(jsonOption);
        useDefaultCommand.SetAction(parseResult =>
        {
            Environment.ExitCode = SetWatchFolder(DefaultWatchFolder, parseResult.GetValue(jsonOption));
        });

        command.Add(statusCommand);
        command.Add(setWatchFolderCommand);
        command.Add(useDefaultCommand);

        return command;
    }

    private static int ShowStatus(bool json)
    {
        try
        {
            var config = ReClipIntegrationConfig.Load(ConfigFilePath);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(ToStatus(config), JsonOptions));
                return 0;
            }

            Console.WriteLine("ReClip Integration:");
            Console.WriteLine($"  Enabled:      {config.Enabled}");
            Console.WriteLine($"  Watch Folder: {config.WatchFolder ?? "(not set)"}");
            Console.WriteLine($"  Config File:  {ConfigFilePath}");
            Console.WriteLine($"  Exists:       {File.Exists(ConfigFilePath)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to show ReClip configuration: {ex.Message}");
            return 1;
        }
    }

    private static int SetWatchFolder(string folder, bool json)
    {
        try
        {
            if (!TryValidateWatchFolder(folder, out string fullPath, out string? error))
            {
                Console.Error.WriteLine($"Failed to set ReClip watch folder: {error}");
                return 1;
            }

            Directory.CreateDirectory(fullPath);

            var config = ReClipIntegrationConfig.Load(ConfigFilePath);
            config.Enabled = true;
            config.WatchFolder = fullPath;
            config.UpdatedAtUtc = DateTimeOffset.UtcNow;
            config.Save(ConfigFilePath);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(ToStatus(config), JsonOptions));
                return 0;
            }

            Console.WriteLine($"ReClip watch folder set to: {fullPath}");
            Console.WriteLine($"Config saved to: {ConfigFilePath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to set ReClip watch folder: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Validates and canonicalizes a ReClip watch-folder path.
    /// Rejects empty/whitespace, embedded nulls, invalid path characters,
    /// unresolved ".." segments in the input, and filesystem roots.
    /// </summary>
    internal static bool TryValidateWatchFolder(string? folder, out string fullPath, out string? error)
    {
        fullPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(folder))
        {
            error = "Watch folder path is required.";
            return false;
        }

        if (folder.Contains('\0'))
        {
            error = "Watch folder path contains an invalid null character.";
            return false;
        }

        char[] invalidChars = Path.GetInvalidPathChars();
        if (folder.IndexOfAny(invalidChars) >= 0)
        {
            error = "Watch folder path contains invalid characters.";
            return false;
        }

        // Reject explicit ".." segments in the raw input before canonicalization.
        // Path.GetFullPath collapses ".." which would otherwise hide traversal intent
        // when the CLI is invoked with a relative handoff path.
        string[] segments = folder.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (segment == "..")
            {
                error = "Watch folder path must not contain parent-directory segments ('..').";
                return false;
            }
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(folder);
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Watch folder path is invalid: {ex.Message}";
            fullPath = string.Empty;
            return false;
        }

        // Reject filesystem roots (/, C:\, etc.) — watch folders must be concrete directories.
        string? root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Watch folder path must not be a filesystem root.";
            fullPath = string.Empty;
            return false;
        }

        return true;
    }

    private static ReClipStatus ToStatus(ReClipIntegrationConfig config)
    {
        return new ReClipStatus(
            Enabled: config.Enabled,
            WatchFolder: config.WatchFolder,
            ConfigFile: ConfigFilePath,
            Exists: File.Exists(ConfigFilePath),
            UpdatedAtUtc: config.UpdatedAtUtc);
    }

    private sealed class ReClipIntegrationConfig
    {
        public bool Enabled { get; set; }
        public string? WatchFolder { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        public static ReClipIntegrationConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                return new ReClipIntegrationConfig();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ReClipIntegrationConfig>(json, JsonOptions) ?? new ReClipIntegrationConfig();
            }
            catch
            {
                return new ReClipIntegrationConfig();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
    }

    private sealed record ReClipStatus(
        bool Enabled,
        string? WatchFolder,
        string ConfigFile,
        bool Exists,
        DateTimeOffset? UpdatedAtUtc);
}
