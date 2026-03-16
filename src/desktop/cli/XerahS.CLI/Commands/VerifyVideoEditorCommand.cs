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
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands;

public static class VerifyVideoEditorCommand
{
    private static readonly string[] VideoExtensions =
    {
        ".mp4", ".webm", ".mov", ".mkv", ".avi", ".wmv", ".m4v"
    };

    private const int DefaultStartupWaitSeconds = 8;

    public static Command Create()
    {
        var cmd = new Command("verify-video-editor", "Launch the VideoEditor and print FFmpeg startup diagnostics");

        var videoOption = new Option<string?>("--video")
        {
            Description = "Optional video path. If omitted, the newest screencast under Documents\\XerahS\\Screencasts is used."
        };

        var ffmpegOption = new Option<string?>("--ffmpeg")
        {
            Description = "Optional FFmpeg override path. If omitted, PathsManager.GetFFmpegPath() is used."
        };

        var waitOption = new Option<int>("--wait")
        {
            Description = "Seconds to keep the editor open while collecting startup diagnostics (default: 8)."
        };

        cmd.Add(videoOption);
        cmd.Add(ffmpegOption);
        cmd.Add(waitOption);

        cmd.SetAction(parseResult =>
        {
            string? videoPath = parseResult.GetValue(videoOption);
            string? ffmpegPath = parseResult.GetValue(ffmpegOption);
            int waitSeconds = parseResult.GetValue(waitOption);

            Environment.ExitCode = RunVerifyVideoEditorAsync(videoPath, ffmpegPath, waitSeconds).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static async Task<int> RunVerifyVideoEditorAsync(string? videoPath, string? ffmpegPath, int waitSeconds)
    {
        try
        {
            if (!PlatformServices.IsInitialized)
            {
                Console.Error.WriteLine("Platform services not initialized.");
                return 2;
            }

            string? resolvedVideoPath = ResolveVideoPath(videoPath);
            if (string.IsNullOrWhiteSpace(resolvedVideoPath))
            {
                Console.Error.WriteLine("No video file was found. Pass --video or record something into Documents\\XerahS\\Screencasts first.");
                return 2;
            }

            if (!File.Exists(resolvedVideoPath))
            {
                Console.Error.WriteLine($"Video file does not exist: {resolvedVideoPath}");
                return 2;
            }

            string detectedFfmpegPath = PathsManager.GetFFmpegPath();
            string normalizedHostFfmpegPath = VideoEditorFfmpegResolver.NormalizePath(ffmpegPath);
            var ffmpegResolution = VideoEditorFfmpegResolver.Resolve(normalizedHostFfmpegPath, detectedFfmpegPath);

            waitSeconds = waitSeconds <= 0 ? DefaultStartupWaitSeconds : waitSeconds;

            Console.WriteLine("=== Verify Video Editor ===");
            Console.WriteLine($"Video: {resolvedVideoPath}");
            Console.WriteLine($"Host FFmpeg candidate: {DisplayPath(normalizedHostFfmpegPath)}");
            Console.WriteLine($"PathsManager FFmpeg: {DisplayPath(detectedFfmpegPath)}");
            Console.WriteLine($"Resolved FFmpeg: {DisplayPath(ffmpegResolution.ConfiguredPath)}");
            Console.WriteLine($"FFmpeg available: {ffmpegResolution.IsAvailable} ({ffmpegResolution.Source})");
            Console.WriteLine($"Startup wait: {waitSeconds}s");
            Console.WriteLine("Launching editor...");

            Task<string?> editorTask = PlatformServices.UI.ShowVideoEditorAsync(resolvedVideoPath, normalizedHostFfmpegPath);
            Task completedTask = await Task.WhenAny(editorTask, Task.Delay(TimeSpan.FromSeconds(waitSeconds)));

            if (completedTask == editorTask)
            {
                string? exportedPath = await editorTask;
                Console.WriteLine($"Editor closed. Exported path: {DisplayPath(exportedPath)}");
                return 0;
            }

            Console.WriteLine("Startup wait elapsed. Exiting after collecting diagnostics.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Video editor verification failed: {ex}");
            return 2;
        }
    }

    private static string? ResolveVideoPath(string? videoPath)
    {
        if (!string.IsNullOrWhiteSpace(videoPath))
        {
            return VideoEditorFfmpegResolver.NormalizePath(videoPath);
        }

        if (!Directory.Exists(PathsManager.ScreencastsFolder))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(PathsManager.ScreencastsFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => info.FullName)
            .FirstOrDefault();
    }

    private static string DisplayPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "(not set)" : path;
}
