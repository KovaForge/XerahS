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
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace XerahS.Common
{
    public static class PathsManager
    {
        private static string _personalFolder = "";

        public static string PersonalFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_personalFolder))
                {
                    _personalFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                        AppResources.AppName);
                }
                return _personalFolder;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _personalFolder = value;
                }
            }
        }

        public static string ScreenshotsFolder => Path.Combine(PersonalFolder, AppResources.ScreenshotsFolderName);
        public static string ScreencastsFolder => Path.Combine(PersonalFolder, AppResources.ScreencastsFolderName);
        public static string FrameDumpsFolder => Path.Combine(ScreencastsFolder, "FrameDumps");

        /// <summary>Base folder for all log files (e.g. PersonalFolder/Logs).</summary>
        public static string LogsFolderBase => Path.Combine(PersonalFolder, "Logs");

        /// <summary>Logs subfolder for the given month (e.g. Logs/yyyy-MM). Uses current date if null.</summary>
        public static string GetLogsFolderForMonth(DateTime? date = null) =>
            Path.Combine(LogsFolderBase, (date ?? DateTime.Now).ToString("yyyy-MM"));

        /// <summary>Filename prefix for the dedicated error log (full name: XerahS-errors-yyyyMMdd.log).</summary>
        public const string ErrorLogFileNamePrefix = "XerahS-errors";

        /// <summary>Full path to the error log file for today: Logs/yyyy-MM/XerahS-errors-yyyyMMdd.log.</summary>
        public static string GetErrorLogFilePath()
        {
            var date = DateTime.Now;
            return Path.Combine(GetLogsFolderForMonth(date), $"{ErrorLogFileNamePrefix}-{date:yyyyMMdd}.log");
        }

        /// <summary>Full path to the main log file for today: Logs/yyyy-MM/AppName-yyyyMMdd.log.</summary>
        public static string GetMainLogFilePath()
        {
            var date = DateTime.Now;
            return Path.Combine(GetLogsFolderForMonth(date), $"{AppResources.AppName}-{date:yyyyMMdd}.log");
        }

        public static string SettingsFolder => Path.Combine(PersonalFolder, AppResources.SettingsFolderName);
        public static string HistoryFolder => Path.Combine(PersonalFolder, AppResources.HistoryFolderName);
        public static string BackupFolder => Path.Combine(SettingsFolder, AppResources.BackupFolderName);
        public static string HistoryBackupFolder => Path.Combine(HistoryFolder, AppResources.BackupFolderName);
        /// <summary>Folder for troubleshooting / diagnostic logs (e.g. DPI, capture).</summary>
        public static string TroubleshootingFolder => Path.Combine(PersonalFolder, "Troubleshooting");
        /// <summary>Base folder for capture verification outputs (region/recording verify).</summary>
        public static string CaptureTroubleshootingFolder => Path.Combine(PersonalFolder, "CaptureTroubleshooting");
        public static string ToolsFolder => Path.Combine(PersonalFolder, "Tools");
        public static string ToolsArchitectureFolder => Path.Combine(ToolsFolder, GetArchitectureFolderName());
        public static string PluginsFolder
        {
            get
            {
#if DEBUG
                if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
                    return Path.Combine(PersonalFolder, AppResources.PluginsFolderName);
                return Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
#else
                string personalPlugins = Path.Combine(PersonalFolder, AppResources.PluginsFolderName);
                if (Directory.Exists(personalPlugins) && Directory.GetFileSystemEntries(personalPlugins).Length > 0)
                {
                    return personalPlugins;
                }
                return Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
#endif
            }
        }

        public static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(PersonalFolder))
                Directory.CreateDirectory(PersonalFolder);
            
            if (!Directory.Exists(ScreenshotsFolder))
                Directory.CreateDirectory(ScreenshotsFolder);
            
            if (!Directory.Exists(ScreencastsFolder))
                Directory.CreateDirectory(ScreencastsFolder);
            
            if (!Directory.Exists(FrameDumpsFolder))
                Directory.CreateDirectory(FrameDumpsFolder);
            
            if (!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);
            
            if (!Directory.Exists(HistoryFolder))
                Directory.CreateDirectory(HistoryFolder);
            
            if (!Directory.Exists(BackupFolder))
                Directory.CreateDirectory(BackupFolder);
            
            if (!Directory.Exists(PluginsFolder))
                Directory.CreateDirectory(PluginsFolder);

            if (!Directory.Exists(ToolsFolder))
                Directory.CreateDirectory(ToolsFolder);

            if (!Directory.Exists(ToolsArchitectureFolder))
                Directory.CreateDirectory(ToolsArchitectureFolder);
        }

        public static System.Collections.Generic.IEnumerable<string> GetPluginDirectories()
        {
            var paths = new System.Collections.Generic.List<string>();

            // 1. App-bundled plugins (BaseDirectory/Plugins)
            // In Release, we also want to check this location adjacent to the executable
            string appPluginsPath = Path.Combine(AppContext.BaseDirectory, AppResources.PluginsFolderName);
            if (Directory.Exists(appPluginsPath))
            {
                paths.Add(appPluginsPath);
            }

            // 2. User-installed plugins (PluginsFolder -> PersonalFolder/Plugins)
            // This allows users to add plugins without modifying the app installation
            string userPluginsPath = PluginsFolder;
            
            // Only add if it exists and is different from the app plugins path
            if (Directory.Exists(userPluginsPath) && 
                !string.Equals(appPluginsPath, userPluginsPath, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(userPluginsPath);
            }

            return paths;
        }

        public static string GetFFmpegPath()
        {
            return GetToolPath("FFmpeg", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        }

        public static string GetFFprobePath()
        {
            return GetToolPath("FFprobe", OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        }

        private static string GetToolPath(string toolName, string executableName)
        {
            // 1. Check Personal Tools Architecture Folder (Prioritized)
            string toolsExecutablePath = Path.Combine(ToolsArchitectureFolder, executableName);
            DebugHelper.WriteLine($"[{toolName}] Checking architecture tools path: {toolsExecutablePath}");
            if (File.Exists(toolsExecutablePath))
            {
                DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {toolsExecutablePath}");
                return toolsExecutablePath;
            }

            // Check without extension on macOS/Linux if strict naming is used
            if (!OperatingSystem.IsWindows())
            {
                string toolsExecutableNoExt = Path.Combine(ToolsArchitectureFolder, Path.GetFileNameWithoutExtension(executableName));
                if (toolsExecutablePath != toolsExecutableNoExt)
                {
                    DebugHelper.WriteLine($"[{toolName}] Checking architecture tools path: {toolsExecutableNoExt}");
                    if (File.Exists(toolsExecutableNoExt))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {toolsExecutableNoExt}");
                        return toolsExecutableNoExt;
                    }
                }
            }

            // 1b. Check legacy Personal Tools Folder
            string legacyToolsExecutablePath = Path.Combine(ToolsFolder, executableName);
            DebugHelper.WriteLine($"[{toolName}] Checking legacy tools path: {legacyToolsExecutablePath}");
            if (File.Exists(legacyToolsExecutablePath))
            {
                DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {legacyToolsExecutablePath}");
                return legacyToolsExecutablePath;
            }

            if (!OperatingSystem.IsWindows())
            {
                string legacyToolsExecutableNoExt = Path.Combine(ToolsFolder, Path.GetFileNameWithoutExtension(executableName));
                if (legacyToolsExecutablePath != legacyToolsExecutableNoExt)
                {
                    DebugHelper.WriteLine($"[{toolName}] Checking legacy tools path: {legacyToolsExecutableNoExt}");
                    if (File.Exists(legacyToolsExecutableNoExt))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {legacyToolsExecutableNoExt}");
                        return legacyToolsExecutableNoExt;
                    }
                }
            }

            // 2. Check Common System Locations
            string appToolsDir = GetAppToolsDirectory();
            string[] commonPaths = new[]
            {
                Path.Combine(appToolsDir, executableName),
                Path.Combine(AppContext.BaseDirectory, executableName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FFmpeg", "bin", executableName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FFmpeg", "bin", executableName),
                $"/opt/homebrew/bin/{Path.GetFileNameWithoutExtension(executableName)}",
                $"/usr/local/bin/{Path.GetFileNameWithoutExtension(executableName)}",
                $"/usr/bin/{Path.GetFileNameWithoutExtension(executableName)}"
            };

            foreach (var path in commonPaths)
            {
                DebugHelper.WriteLine($"[{toolName}] Checking common path: {path}");
                if (File.Exists(path))
                {
                    DebugHelper.WriteLine($"[{toolName}] Found {toolName} at: {path}");
                    return path;
                }
            }

            // 3. Check PATH Environment Variable
            DebugHelper.WriteLine($"[{toolName}] Searching PATH environment variable...");
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var toolPath = Path.Combine(dir, executableName);
                    if (File.Exists(toolPath))
                    {
                        DebugHelper.WriteLine($"[{toolName}] Found {toolName} in PATH at: {toolPath}");
                        return toolPath;
                    }
                }
            }

            DebugHelper.WriteLine($"[{toolName}] {toolName} not found in any standard location.");
            return string.Empty;
        }

        /// <summary>App-bundled tools directory (BaseDirectory/Tools). Used for FFmpeg lookup and path consistency.</summary>
        private static string GetAppToolsDirectory() =>
            Path.Combine(AppContext.BaseDirectory, "Tools");

        private static string GetArchitectureFolderName()
        {
            if (OperatingSystem.IsWindows())
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    Architecture.Arm64 => "win-arm64",
                    Architecture.X64 => "win-x64",
                    _ => "win-x86"
                };
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos64";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux64";
            }

            return "win-x64";
        }
    }
}
