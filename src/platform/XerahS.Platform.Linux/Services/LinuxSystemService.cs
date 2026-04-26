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
using System.IO;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    public class LinuxSystemService : ISystemService
    {
        public bool IsDesktopWallpaperSupported => LinuxDesktopWallpaperProvider.IsSupported;

        public bool ShowFileInExplorer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string normalizedFilePath = NormalizeExistingPath(filePath);

                if (Uri.TryCreate(normalizedFilePath, UriKind.Absolute, out var uri))
                {
                    if (TryShowItemsViaDbus(uri.AbsoluteUri))
                    {
                        return true;
                    }
                }

                // Linux selecting file is not standardized. Open parent dir.
                string? folderPath = Path.GetDirectoryName(normalizedFilePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    Process.Start(CreateOpenStartInfo(folderPath));
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return false;
        }

        public bool OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            try
            {
                Process.Start(CreateOpenStartInfo(url));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return false;
        }

        public bool OpenFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath))) return false;

            try
            {
                Process.Start(CreateOpenStartInfo(NormalizeExistingPath(filePath)));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return false;
        }

        public bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
        {
            return LinuxDesktopWallpaperProvider.TryGetDesktopWallpaper(out wallpaper);
        }

        public bool TryGetDesktopWallpaperPath(out string? path)
        {
            if (TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper) && wallpaper != null)
            {
                path = wallpaper.Path;
                return true;
            }

            path = null;
            return false;
        }

        internal static ProcessStartInfo CreateOpenStartInfo(string target)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-open",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            startInfo.ArgumentList.Add(target);
            return startInfo;
        }

        internal static string NormalizeExistingPath(string path)
        {
            return Path.GetFullPath(path);
        }

        private static bool TryShowItemsViaDbus(string fileUri)
        {
            try
            {
                string escaped = fileUri.Replace("\"", "\\\"");
                string args = $"--session --print-reply --type=method_call --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"{escaped}\" string:\"\"";

                using var process = Process.Start(new ProcessStartInfo("dbus-send", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(2000))
                {
                    process.Kill();
                    process.WaitForExit();
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
    }
}
