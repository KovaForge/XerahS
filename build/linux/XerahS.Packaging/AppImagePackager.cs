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
using System.Net.Http;

namespace XerahS.Packaging;

/// <summary>
/// Stages an AppDir from a self-contained Linux publish and wraps it with appimagetool.
/// </summary>
public static class AppImagePackager
{
    public const string DesktopFileName = "xerahs.desktop";
    public const string IconFileName = "xerahs.png";
    public const string PayloadDirectory = "usr/lib/xerahs";
    public const string AppRunFileName = "AppRun";

    // Pin a released appimagetool so CI does not float on "continuous".
    internal const string AppImageToolVersion = "1.9.0";
    internal const string AppImageToolDownloadBase =
        "https://github.com/AppImage/appimagetool/releases/download/" + AppImageToolVersion;
    internal const string Type2RuntimeDownloadBase =
        "https://github.com/AppImage/type2-runtime/releases/download/continuous";

    public static string BuildDesktopEntry()
    {
        return """
            [Desktop Entry]
            Name=XerahS
            Comment=Cross-platform screen capture and sharing tool
            GenericName=Screen Capture
            Exec=xerahs %U
            Icon=xerahs
            Terminal=false
            Type=Application
            Categories=Utility;Graphics;GTK;
            Keywords=screenshot;screen;capture;share;upload;
            StartupWMClass=xerahs
            X-GNOME-UsesNotifications=true
            X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2

            """.Replace("\r\n", "\n");
    }

    public static string BuildAppRunScript()
    {
        return """
            #!/bin/sh
            set -eu
            HERE="$(dirname "$(readlink -f "$0")")"
            export PATH="${HERE}/usr/bin:${PATH:-}"
            exec "${HERE}/usr/lib/xerahs/XerahS" "$@"

            """.Replace("\r\n", "\n");
    }

    public static string MapAppImageArch(string runtimeIdentifier)
    {
        return runtimeIdentifier switch
        {
            "linux-x64" => "x86_64",
            "linux-arm64" => "aarch64",
            "amd64" => "x86_64",
            "x64" => "x86_64",
            "arm64" => "aarch64",
            "aarch64" => "aarch64",
            _ => throw new ArgumentException(
                $"Unsupported AppImage architecture '{runtimeIdentifier}'. Expected linux-x64 or linux-arm64.",
                nameof(runtimeIdentifier))
        };
    }

    public static void StageAppDir(string publishDir, string appDir, string? iconSource)
    {
        if (!Directory.Exists(publishDir))
        {
            throw new DirectoryNotFoundException($"Publish directory not found: {publishDir}");
        }

        if (Directory.Exists(appDir))
        {
            Directory.Delete(appDir, true);
        }

        Directory.CreateDirectory(appDir);

        string payloadDir = Path.Combine(appDir, "usr", "lib", "xerahs");
        CopyDirectory(publishDir, payloadDir);

        string binDir = Path.Combine(appDir, "usr", "bin");
        Directory.CreateDirectory(binDir);
        string launcher = Path.Combine(binDir, "xerahs");
        CreateRelativeSymlinkOrCopy(launcher, Path.Combine("..", "lib", "xerahs", "XerahS"));
        CreateRelativeSymlinkOrCopy(Path.Combine(binDir, "omaxerahs"), Path.Combine("..", "lib", "xerahs", "omaxerahs"));

        string desktop = BuildDesktopEntry();
        File.WriteAllText(Path.Combine(appDir, DesktopFileName), desktop);
        string applicationsDir = Path.Combine(appDir, "usr", "share", "applications");
        Directory.CreateDirectory(applicationsDir);
        File.WriteAllText(Path.Combine(applicationsDir, DesktopFileName), desktop);

        string appRunPath = Path.Combine(appDir, AppRunFileName);
        File.WriteAllText(appRunPath, BuildAppRunScript());
        MarkExecutable(appRunPath);

        string xerahsBinary = Path.Combine(payloadDir, "XerahS");
        if (File.Exists(xerahsBinary))
        {
            MarkExecutable(xerahsBinary);
        }

        string daemonBinary = Path.Combine(payloadDir, "xerahs-watchfolder-daemon");
        if (File.Exists(daemonBinary))
        {
            MarkExecutable(daemonBinary);
        }

        string omaxerahsBinary = Path.Combine(payloadDir, "omaxerahs");
        if (File.Exists(omaxerahsBinary))
        {
            MarkExecutable(omaxerahsBinary);
        }

        StageIcon(appDir, iconSource);
    }

    public static bool TryCreate(
        string publishDir,
        string outputPath,
        string version,
        string runtimeIdentifier,
        string? iconSource,
        out string? error)
    {
        if (!OperatingSystem.IsLinux())
        {
            error = "AppImage packaging requires Linux (appimagetool / squashfs-tools).";
            return false;
        }

        string appImageArch;
        try
        {
            appImageArch = MapAppImageArch(runtimeIdentifier);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        if (!IsToolAvailable("mksquashfs"))
        {
            error = "mksquashfs not found. Install squashfs-tools.";
            return false;
        }

        string workDir = Path.Combine(Path.GetTempPath(), "xerahs_appimage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            string appDir = Path.Combine(workDir, "AppDir");
            StageAppDir(publishDir, appDir, iconSource);

            if (!File.Exists(Path.Combine(appDir, "usr", "lib", "xerahs", "XerahS")))
            {
                error = "Staged AppDir is missing usr/lib/xerahs/XerahS.";
                return false;
            }

            if (!TryResolveAppImageTool(appImageArch, out string appImageToolPath, out string? toolError))
            {
                error = toolError;
                return false;
            }

            if (!TryResolveRuntime(appImageArch, out string runtimePath, out string? runtimeError))
            {
                error = runtimeError;
                return false;
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var psi = new ProcessStartInfo
            {
                FileName = appImageToolPath,
                ArgumentList =
                {
                    "--runtime-file",
                    runtimePath,
                    appDir,
                    outputPath
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["ARCH"] = appImageArch;
            psi.Environment["VERSION"] = version;
            psi.Environment["APPIMAGE_EXTRACT_AND_RUN"] = "1";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                error = "Failed to start appimagetool.";
                return false;
            }

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0 || !File.Exists(outputPath))
            {
                error = $"appimagetool failed (exit {proc.ExitCode}).\n{stdout}\n{stderr}";
                return false;
            }

            MarkExecutable(outputPath);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    internal static void StageIcon(string appDir, string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource) || !File.Exists(iconSource))
        {
            Console.WriteLine("Warning: No icon source for AppImage; desktop Icon=xerahs may not resolve.");
            return;
        }

        string rootIcon = Path.Combine(appDir, IconFileName);
        File.Copy(iconSource, rootIcon, true);
        File.Copy(iconSource, Path.Combine(appDir, ".DirIcon"), true);

        string pixmaps = Path.Combine(appDir, "usr", "share", "pixmaps");
        Directory.CreateDirectory(pixmaps);
        File.Copy(iconSource, Path.Combine(pixmaps, IconFileName), true);

        string hicolor = Path.Combine(appDir, "usr", "share", "icons", "hicolor", "512x512", "apps");
        Directory.CreateDirectory(hicolor);
        File.Copy(iconSource, Path.Combine(hicolor, IconFileName), true);
    }

    static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    static void CreateRelativeSymlinkOrCopy(string linkPath, string relativeTarget)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, relativeTarget);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not create symlink {linkPath} -> {relativeTarget} ({ex.Message}). Writing a launcher script instead.");
            string script = $"""
                #!/bin/sh
                set -eu
                HERE="$(dirname "$(readlink -f "$0")")"
                exec "$HERE/{relativeTarget.Replace('\\', '/')}" "$@"

                """.Replace("\r\n", "\n");
            File.WriteAllText(linkPath, script);
        }

        MarkExecutable(linkPath);
    }

    static void MarkExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not mark executable {path}: {ex.Message}");
        }
    }

    static bool IsToolAvailable(string toolName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = toolName,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return false;
            }

            proc.WaitForExit(3000);
            return proc.ExitCode == 0 || proc.ExitCode == 1;
        }
        catch
        {
            return false;
        }
    }

    static bool TryResolveAppImageTool(string appImageArch, out string path, out string? error)
    {
        string? fromPath = FindOnPath("appimagetool");
        if (fromPath != null)
        {
            path = fromPath;
            error = null;
            return true;
        }

        string cacheDir = Path.Combine(Path.GetTempPath(), "xerahs-appimagetool", AppImageToolVersion);
        Directory.CreateDirectory(cacheDir);
        string fileName = $"appimagetool-{appImageArch}.AppImage";
        path = Path.Combine(cacheDir, fileName);
        string url = $"{AppImageToolDownloadBase}/{fileName}";

        if (!File.Exists(path) || new FileInfo(path).Length < 1024)
        {
            if (!TryDownload(url, path, out error))
            {
                return false;
            }
        }

        MarkExecutable(path);
        error = null;
        return true;
    }

    static bool TryResolveRuntime(string appImageArch, out string path, out string? error)
    {
        string cacheDir = Path.Combine(Path.GetTempPath(), "xerahs-appimage-runtime");
        Directory.CreateDirectory(cacheDir);
        string fileName = $"runtime-{appImageArch}";
        path = Path.Combine(cacheDir, fileName);
        string url = $"{Type2RuntimeDownloadBase}/{fileName}";

        if (!File.Exists(path) || new FileInfo(path).Length < 1024)
        {
            if (!TryDownload(url, path, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    static bool TryDownload(string url, string destination, out string? error)
    {
        try
        {
            Console.WriteLine($"Downloading {url}");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XerahS-AppImagePackager");
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (bytes.Length < 1024)
            {
                error = $"Downloaded file too small from {url} ({bytes.Length} bytes).";
                return false;
            }

            File.WriteAllBytes(destination, bytes);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to download {url}: {ex.Message}";
            return false;
        }
    }

    static string? FindOnPath(string toolName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, toolName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
