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

namespace XerahS.Common;

public sealed class LinuxXdgDirectories
{
    public const string AppDirectoryName = "xerahs";

    private LinuxXdgDirectories(
        string homeDirectory,
        string configHome,
        string dataHome,
        string stateHome,
        string cacheHome,
        string? runtimeDirectory)
    {
        HomeDirectory = homeDirectory;
        ConfigHome = configHome;
        DataHome = dataHome;
        StateHome = stateHome;
        CacheHome = cacheHome;
        RuntimeDirectory = runtimeDirectory;
    }

    public string HomeDirectory { get; }

    public string ConfigHome { get; }

    public string DataHome { get; }

    public string StateHome { get; }

    public string CacheHome { get; }

    public string? RuntimeDirectory { get; }

    public string ConfigDirectory => Path.Combine(ConfigHome, AppDirectoryName);

    public string DataDirectory => Path.Combine(DataHome, AppDirectoryName);

    public string StateDirectory => Path.Combine(StateHome, AppDirectoryName);

    public string CacheDirectory => Path.Combine(CacheHome, AppDirectoryName);

    public string PicturesDirectory => ResolveUserDirectory("XDG_PICTURES_DIR", "Pictures");

    public string VideosDirectory => ResolveUserDirectory("XDG_VIDEOS_DIR", "Videos");

    public string DocumentsDirectory => ResolveUserDirectory("XDG_DOCUMENTS_DIR", "Documents");

    public string DownloadsDirectory => ResolveUserDirectory("XDG_DOWNLOAD_DIR", "Downloads");

    public static LinuxXdgDirectories Detect()
    {
        return Resolve(Environment.GetEnvironmentVariable, GetCurrentHomeDirectory());
    }

    public static LinuxXdgDirectories Resolve(Func<string, string?> getEnvironmentVariable, string? homeDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        string home = ResolveHomeDirectory(getEnvironmentVariable, homeDirectory);
        string configHome = ResolveBaseDirectory(getEnvironmentVariable("XDG_CONFIG_HOME"), home, ".config");
        string dataHome = ResolveBaseDirectory(getEnvironmentVariable("XDG_DATA_HOME"), home, Path.Combine(".local", "share"));
        string stateHome = ResolveBaseDirectory(getEnvironmentVariable("XDG_STATE_HOME"), home, Path.Combine(".local", "state"));
        string cacheHome = ResolveBaseDirectory(getEnvironmentVariable("XDG_CACHE_HOME"), home, ".cache");
        string? runtimeDirectory = NormalizeAbsolutePath(getEnvironmentVariable("XDG_RUNTIME_DIR"));

        return new LinuxXdgDirectories(home, configHome, dataHome, stateHome, cacheHome, runtimeDirectory);
    }

    private string ResolveUserDirectory(string key, string fallbackFolderName)
    {
        string userDirsPath = Path.Combine(ConfigHome, "user-dirs.dirs");
        if (File.Exists(userDirsPath))
        {
            try
            {
                foreach (string rawLine in File.ReadLines(userDirsPath))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith(key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] parts = line.Split('=', 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    string value = parts[1].Trim().Trim('"');
                    value = value.Replace("$HOME", HomeDirectory, StringComparison.Ordinal);
                    string? absoluteValue = NormalizeAbsolutePath(value);
                    if (!string.IsNullOrWhiteSpace(absoluteValue))
                    {
                        return absoluteValue;
                    }
                }
            }
            catch
            {
                // Best-effort user-dir parsing. Fall back to the spec default.
            }
        }

        return Path.Combine(HomeDirectory, fallbackFolderName);
    }

    private static string ResolveHomeDirectory(Func<string, string?> getEnvironmentVariable, string? homeDirectory)
    {
        string? normalizedHome = NormalizeAbsolutePath(homeDirectory);
        if (!string.IsNullOrWhiteSpace(normalizedHome))
        {
            return normalizedHome;
        }

        normalizedHome = NormalizeAbsolutePath(getEnvironmentVariable("HOME"));
        if (!string.IsNullOrWhiteSpace(normalizedHome))
        {
            return normalizedHome;
        }

        return GetCurrentHomeDirectory();
    }

    private static string GetCurrentHomeDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            return home;
        }

        home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        return string.IsNullOrWhiteSpace(home) ? "/" : home;
    }

    private static string ResolveBaseDirectory(string? configuredValue, string homeDirectory, string defaultRelativePath)
    {
        string? normalizedConfiguredValue = NormalizeAbsolutePath(configuredValue);
        if (!string.IsNullOrWhiteSpace(normalizedConfiguredValue))
        {
            return normalizedConfiguredValue;
        }

        return Path.Combine(homeDirectory, defaultRelativePath);
    }

    private static string? NormalizeAbsolutePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return Path.IsPathRooted(trimmed) ? trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : null;
    }
}
