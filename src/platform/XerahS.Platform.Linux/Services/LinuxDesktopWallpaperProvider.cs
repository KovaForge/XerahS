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

using System.Diagnostics;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    internal static class LinuxDesktopWallpaperProvider
    {
        private static readonly string[] SandboxHostRootPrefixes =
        [
            "/run/host",
            "/var/run/host"
        ];

        private enum Provider
        {
            Gnome,
            Cinnamon,
            Mate,
            Xfce,
            Kde
        }

        public static bool IsSupported
        {
            get
            {
                return TryGetDesktopWallpaper(out _);
            }
        }

        public static bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            foreach (Provider provider in GetPreferredProviders())
            {
                if (!IsAvailable(provider))
                {
                    continue;
                }

                if (TryGetDesktopWallpaper(provider, out wallpaper))
                {
                    return true;
                }
            }

            wallpaper = null;
            return false;
        }

        private static IEnumerable<Provider> GetPreferredProviders()
        {
            string desktopHint = GetDesktopHint();
            List<Provider> providers = new List<Provider>();

            if (desktopHint.Contains("gnome", StringComparison.OrdinalIgnoreCase) ||
                desktopHint.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(Provider.Gnome);
            }

            if (desktopHint.Contains("cinnamon", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(Provider.Cinnamon);
            }

            if (desktopHint.Contains("mate", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(Provider.Mate);
            }

            if (desktopHint.Contains("xfce", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(Provider.Xfce);
            }

            if (desktopHint.Contains("kde", StringComparison.OrdinalIgnoreCase) ||
                desktopHint.Contains("plasma", StringComparison.OrdinalIgnoreCase))
            {
                providers.Add(Provider.Kde);
            }

            Provider[] fallbackOrder =
            [
                Provider.Gnome,
                Provider.Cinnamon,
                Provider.Mate,
                Provider.Xfce,
                Provider.Kde
            ];

            foreach (Provider provider in fallbackOrder)
            {
                if (!providers.Contains(provider))
                {
                    providers.Add(provider);
                }
            }

            return providers;
        }

        private static string GetDesktopHint()
        {
            string? currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            if (!string.IsNullOrWhiteSpace(currentDesktop))
            {
                return currentDesktop;
            }

            string? desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
            if (!string.IsNullOrWhiteSpace(desktopSession))
            {
                return desktopSession;
            }

            return string.Empty;
        }

        private static bool IsAvailable(Provider provider)
        {
            return provider switch
            {
                Provider.Gnome => CommandExists("gsettings") && GSettingsSchemaExists("org.gnome.desktop.background"),
                Provider.Cinnamon => CommandExists("gsettings") && GSettingsSchemaExists("org.cinnamon.desktop.background"),
                Provider.Mate => CommandExists("gsettings") && GSettingsSchemaExists("org.mate.background"),
                Provider.Xfce => CommandExists("xfconf-query"),
                Provider.Kde => File.Exists(GetKdeWallpaperConfigPath()),
                _ => false
            };
        }

        private static bool TryGetDesktopWallpaper(Provider provider, out DesktopWallpaperInfo? wallpaper)
        {
            return provider switch
            {
                Provider.Gnome => TryGetGSettingsWallpaper("org.gnome.desktop.background", true, out wallpaper),
                Provider.Cinnamon => TryGetGSettingsWallpaper("org.cinnamon.desktop.background", true, out wallpaper),
                Provider.Mate => TryGetMateWallpaper(out wallpaper),
                Provider.Xfce => TryGetXfceWallpaper(out wallpaper),
                Provider.Kde => TryGetKdeWallpaper(out wallpaper),
                _ => ReturnFalse(out wallpaper)
            };
        }

        private static bool TryGetGSettingsWallpaper(string schema, bool allowDarkVariant, out DesktopWallpaperInfo? wallpaper)
        {
            wallpaper = null;

            string? pictureValue = null;
            if (allowDarkVariant && IsDarkWallpaperPreferred() &&
                TryReadGSettingsValue(schema, "picture-uri-dark", out string? darkPictureValue))
            {
                pictureValue = darkPictureValue;
            }

            if (string.IsNullOrWhiteSpace(pictureValue) &&
                !TryReadGSettingsValue(schema, "picture-uri", out pictureValue))
            {
                return false;
            }

            string? path = ResolveAccessibleWallpaperPath(ParseWallpaperPath(pictureValue));
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            DesktopWallpaperLayout layout = DesktopWallpaperLayout.Fill;
            if (TryReadGSettingsValue(schema, "picture-options", out string? pictureOptions))
            {
                layout = MapGSettingsPictureOptions(pictureOptions);
            }

            wallpaper = new DesktopWallpaperInfo
            {
                Path = path,
                Layout = layout
            };

            return true;
        }

        private static bool TryGetMateWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            wallpaper = null;

            if (!TryReadGSettingsValue("org.mate.background", "picture-filename", out string? pictureValue))
            {
                return false;
            }

            string? path = ResolveAccessibleWallpaperPath(ParseWallpaperPath(pictureValue));
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            DesktopWallpaperLayout layout = DesktopWallpaperLayout.Fill;
            if (TryReadGSettingsValue("org.mate.background", "picture-options", out string? pictureOptions))
            {
                layout = MapGSettingsPictureOptions(pictureOptions);
            }

            wallpaper = new DesktopWallpaperInfo
            {
                Path = path,
                Layout = layout
            };

            return true;
        }

        private static bool TryGetXfceWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            wallpaper = null;

            if (!TryReadCommandOutput("xfconf-query", "-c xfce4-desktop -l", out string propertyList))
            {
                return false;
            }

            IEnumerable<string> candidateProperties = propertyList
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.EndsWith("/last-image", StringComparison.Ordinal) ||
                               line.EndsWith("/image-path", StringComparison.Ordinal))
                .OrderBy(line => line.Contains("workspace0", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(line => line, StringComparer.Ordinal);

            foreach (string property in candidateProperties)
            {
                if (!TryReadCommandOutput("xfconf-query", $"-c xfce4-desktop -p {QuoteArgument(property)}", out string pictureValue))
                {
                    continue;
                }

                string? path = ResolveAccessibleWallpaperPath(ParseWallpaperPath(pictureValue));
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                DesktopWallpaperLayout layout = DesktopWallpaperLayout.Fill;
                string styleProperty = property.EndsWith("/last-image", StringComparison.Ordinal)
                    ? property.Substring(0, property.Length - "/last-image".Length) + "/image-style"
                    : property.Substring(0, property.Length - "/image-path".Length) + "/image-style";

                if (TryReadCommandOutput("xfconf-query", $"-c xfce4-desktop -p {QuoteArgument(styleProperty)}", out string styleValue))
                {
                    layout = MapXfceStyle(styleValue);
                }

                wallpaper = new DesktopWallpaperInfo
                {
                    Path = path,
                    Layout = layout
                };

                return true;
            }

            return false;
        }

        private static bool TryGetKdeWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            wallpaper = null;

            string configPath = GetKdeWallpaperConfigPath();
            if (!File.Exists(configPath))
            {
                return false;
            }

            string? path = null;
            string? fillMode = null;
            bool inWallpaperSection = false;

            foreach (string rawLine in File.ReadLines(configPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        break;
                    }

                    inWallpaperSection = line.Contains("Wallpaper][org.kde.image][General]", StringComparison.Ordinal);
                    continue;
                }

                if (!inWallpaperSection)
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();

                if (key.Equals("Image", StringComparison.Ordinal))
                {
                    string? candidatePath = ResolveAccessibleWallpaperPath(ParseWallpaperPath(value));
                    if (!string.IsNullOrWhiteSpace(candidatePath))
                    {
                        path = candidatePath;
                    }
                }
                else if (key.Equals("FillMode", StringComparison.Ordinal))
                {
                    fillMode = value;
                }
            }

            path = ResolveAccessibleWallpaperPath(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            wallpaper = new DesktopWallpaperInfo
            {
                Path = path,
                Layout = MapKdeFillMode(fillMode)
            };

            return true;
        }

        private static bool IsDarkWallpaperPreferred()
        {
            return TryReadGSettingsValue("org.gnome.desktop.interface", "color-scheme", out string? colorScheme) &&
                   string.Equals(colorScheme, "prefer-dark", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReadGSettingsValue(string schema, string key, out string? value)
        {
            value = null;

            if (!TryReadCommandOutput("gsettings", $"get {schema} {key}", out string output))
            {
                return false;
            }

            value = output.Trim().Trim('\'', '"');
            return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "nothing", StringComparison.OrdinalIgnoreCase);
        }

        private static DesktopWallpaperLayout MapGSettingsPictureOptions(string? option)
        {
            return option?.Trim().Trim('\'', '"').ToLowerInvariant() switch
            {
                "wallpaper" => DesktopWallpaperLayout.Tile,
                "centered" => DesktopWallpaperLayout.Center,
                "scaled" => DesktopWallpaperLayout.Fit,
                "stretched" => DesktopWallpaperLayout.Stretch,
                "zoom" => DesktopWallpaperLayout.Fill,
                "spanned" => DesktopWallpaperLayout.Span,
                _ => DesktopWallpaperLayout.Fill
            };
        }

        private static DesktopWallpaperLayout MapXfceStyle(string? styleValue)
        {
            if (!int.TryParse(styleValue?.Trim(), out int style))
            {
                return DesktopWallpaperLayout.Fill;
            }

            return style switch
            {
                1 => DesktopWallpaperLayout.Center,
                2 => DesktopWallpaperLayout.Tile,
                3 => DesktopWallpaperLayout.Stretch,
                4 => DesktopWallpaperLayout.Fit,
                5 => DesktopWallpaperLayout.Fill,
                6 => DesktopWallpaperLayout.Span,
                _ => DesktopWallpaperLayout.Fill
            };
        }

        private static DesktopWallpaperLayout MapKdeFillMode(string? fillModeValue)
        {
            if (int.TryParse(fillModeValue?.Trim(), out int fillMode))
            {
                return fillMode switch
                {
                    0 => DesktopWallpaperLayout.Stretch,
                    1 => DesktopWallpaperLayout.Fit,
                    2 => DesktopWallpaperLayout.Fill,
                    3 => DesktopWallpaperLayout.Tile,
                    4 => DesktopWallpaperLayout.Tile,
                    5 => DesktopWallpaperLayout.Tile,
                    6 => DesktopWallpaperLayout.Center,
                    _ => DesktopWallpaperLayout.Fill
                };
            }

            return fillModeValue?.Trim().ToLowerInvariant() switch
            {
                "stretch" => DesktopWallpaperLayout.Stretch,
                "preserveaspectfit" => DesktopWallpaperLayout.Fit,
                "preserveaspectcrop" => DesktopWallpaperLayout.Fill,
                "tile" => DesktopWallpaperLayout.Tile,
                "pad" => DesktopWallpaperLayout.Center,
                _ => DesktopWallpaperLayout.Fill
            };
        }

        private static string? ParseWallpaperPath(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            string value = rawValue.Trim().Trim('\'', '"');
            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(value, UriKind.Absolute, out Uri? fileUri))
            {
                return fileUri.LocalPath;
            }

            return value;
        }

        internal static IEnumerable<string> GetAccessiblePathCandidates(string path)
        {
            yield return path;

            if (string.IsNullOrWhiteSpace(path) ||
                !Path.IsPathRooted(path) ||
                IsSandboxHostMirrorPath(path))
            {
                yield break;
            }

            string relativePath = path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string hostRootPrefix in SandboxHostRootPrefixes)
            {
                yield return Path.Combine(hostRootPrefix, relativePath);
            }
        }

        internal static string? ResolveAccessibleWallpaperPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            foreach (string candidatePath in GetAccessiblePathCandidates(path))
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }

        private static bool IsSandboxHostMirrorPath(string path)
        {
            foreach (string hostRootPrefix in SandboxHostRootPrefixes)
            {
                if (path.Equals(hostRootPrefix, StringComparison.Ordinal) ||
                    path.StartsWith(hostRootPrefix + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    path.StartsWith(hostRootPrefix + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GSettingsSchemaExists(string schema)
        {
            return TryReadCommandOutput("gsettings", "list-schemas", out string schemas) &&
                   schemas.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Any(line => line.Equals(schema, StringComparison.Ordinal));
        }

        private static string GetKdeWallpaperConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "plasma-org.kde.plasma.desktop-appletsrc");
        }

        private static bool CommandExists(string command)
        {
            return TryReadCommandOutput("sh", $"-lc {QuoteArgument($"command -v {command}")}", out _);
        }

        private static bool TryReadCommandOutput(string fileName, string arguments, out string output)
        {
            output = string.Empty;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null)
                {
                    return false;
                }

                output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool ReturnFalse(out DesktopWallpaperInfo? wallpaper)
        {
            wallpaper = null;
            return false;
        }
    }
}
