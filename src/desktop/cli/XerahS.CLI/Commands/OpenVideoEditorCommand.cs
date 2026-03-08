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
using ShareX.VideoEditor.Hosting;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Commands;

public static class OpenVideoEditorCommand
{
    public static Command Create()
    {
        var cmd = new Command("open-video-editor", "Open a video file in the VideoEditor and wait for it to close");

        var videoOption = new Option<string>("--video")
        {
            Description = "Path to the video file to open.",
            Required = true
        };

        var ffmpegOption = new Option<string?>("--ffmpeg")
        {
            Description = "Optional FFmpeg override path. If omitted, PathsManager.GetFFmpegPath() is used."
        };

        var headlessOption = new Option<bool>("--headless")
        {
            Description = "Skip the UI and trim/export through ShareX.VideoEditor automation APIs."
        };

        var trimStartOption = new Option<double>("--trim-start")
        {
            Description = "Seconds to trim from the start of the video (headless mode only)."
        };

        var trimEndOffsetOption = new Option<double>("--trim-end-offset")
        {
            Description = "Seconds to trim from the end of the video (headless mode only)."
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Output path for the trimmed file (headless mode only). Defaults to <input>_trimmed.mp4 next to the input."
        };

        cmd.Add(videoOption);
        cmd.Add(ffmpegOption);
        cmd.Add(headlessOption);
        cmd.Add(trimStartOption);
        cmd.Add(trimEndOffsetOption);
        cmd.Add(outputOption);

        cmd.SetAction(parseResult =>
        {
            string videoPath = parseResult.GetValue(videoOption)!;
            string? ffmpegPath = parseResult.GetValue(ffmpegOption);
            bool headless = parseResult.GetValue(headlessOption);
            double trimStart = parseResult.GetValue(trimStartOption);
            double trimEndOffset = parseResult.GetValue(trimEndOffsetOption);
            string? output = parseResult.GetValue(outputOption);

            Environment.ExitCode = headless
                ? RunHeadlessAsync(videoPath, ffmpegPath, trimStart, trimEndOffset, output).GetAwaiter().GetResult()
                : RunAsync(videoPath, ffmpegPath).GetAwaiter().GetResult();
        });

        return cmd;
    }

    // Interactive (Photino window) mode

    private static async Task<int> RunAsync(string videoPath, string? ffmpegOverride)
    {
        try
        {
            if (!PlatformServices.IsInitialized)
            {
                Console.Error.WriteLine("Platform services not initialized.");
                return 2;
            }

            if (!File.Exists(videoPath))
            {
                Console.Error.WriteLine($"Video file does not exist: {videoPath}");
                return 2;
            }

            string detectedFfmpegPath = PathsManager.GetFFmpegPath();
            string normalizedOverride = VideoEditorFfmpegResolver.NormalizePath(ffmpegOverride);
            var resolution = VideoEditorFfmpegResolver.Resolve(normalizedOverride, detectedFfmpegPath);

            Console.WriteLine("=== Open Video Editor ===");
            Console.WriteLine($"Video    : {videoPath}");
            Console.WriteLine($"FFmpeg   : {(resolution.IsAvailable ? resolution.ConfiguredPath : "(unavailable)")}");
            Console.WriteLine($"Source   : {resolution.Source}");

            if (!resolution.IsAvailable)
            {
                Console.Error.WriteLine("[WARNING] FFmpeg was not found. Export and thumbnails will be unavailable.");
                Console.Error.WriteLine($"  Checked PathsManager path : {(string.IsNullOrWhiteSpace(detectedFfmpegPath) ? "(not found)" : detectedFfmpegPath)}");
                Console.Error.WriteLine($"  Override provided         : {(string.IsNullOrWhiteSpace(normalizedOverride) ? "(none)" : normalizedOverride)}");
            }

            Console.WriteLine("Opening editor... (waiting for window to close)");

            string? exportedPath = await PlatformServices.UI.ShowVideoEditorAsync(videoPath, normalizedOverride);

            if (!string.IsNullOrWhiteSpace(exportedPath))
            {
                Console.WriteLine($"Export saved to: {exportedPath}");
                return 0;
            }

            Console.WriteLine("Editor closed without exporting.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"open-video-editor failed: {ex.Message}");
            return 2;
        }
    }

    // Headless trim mode

    private static async Task<int> RunHeadlessAsync(
        string videoPath,
        string? ffmpegOverride,
        double trimStartSeconds,
        double trimEndOffsetSeconds,
        string? outputPath)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                Console.Error.WriteLine($"Video file does not exist: {videoPath}");
                return 2;
            }

            string detectedFfmpegPath = PathsManager.GetFFmpegPath();
            string normalizedOverride = VideoEditorFfmpegResolver.NormalizePath(ffmpegOverride);
            var resolution = VideoEditorFfmpegResolver.Resolve(normalizedOverride, detectedFfmpegPath);

            if (!resolution.IsAvailable)
            {
                Console.Error.WriteLine($"FFmpeg not found. Checked: {detectedFfmpegPath}");
                return 2;
            }

            string ffprobePath = await EnsureFFprobeAvailableAsync(resolution.ConfiguredPath);
            string resolvedOutputPath = !string.IsNullOrWhiteSpace(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.Combine(
                    Path.GetDirectoryName(videoPath) ?? ".",
                    $"{Path.GetFileNameWithoutExtension(videoPath)}_trimmed.mp4");
            var trimService = new VideoEditorAutomationService(resolution.ConfiguredPath, ffprobePath);
            var trimRequest = new VideoEditorTrimRequest
            {
                InputPath = videoPath,
                OutputPath = resolvedOutputPath,
                TrimStart = TimeSpan.FromSeconds(trimStartSeconds),
                TrimEndOffset = TimeSpan.FromSeconds(trimEndOffsetSeconds),
                OutputFormat = "MP4",
                QualityScale = 1.0
            };

            Console.WriteLine("=== Open Video Editor (headless trim) ===");
            Console.WriteLine($"Input    : {videoPath}");
            Console.WriteLine($"Output   : {resolvedOutputPath}");
            Console.WriteLine($"Trim     : start +{trimStartSeconds:F2}s, end -{trimEndOffsetSeconds:F2}s");
            Console.WriteLine($"FFmpeg   : {resolution.ConfiguredPath}");
            Console.WriteLine($"FFprobe  : {ffprobePath}");

            Console.Write("Encoding");
            VideoEditorTrimResult result = await trimService.TrimAsync(
                trimRequest,
                progress =>
                {
                    string speed = progress.Speed > 0 ? $"{progress.Speed:F1}x" : "-";
                    Console.Write(
                        $"\rEncoding {progress.ProgressPercent:F0}%  {progress.CurrentTime:hh\\:mm\\:ss\\.ff}  {speed,-6}");
                });
            Console.WriteLine();

            if (!File.Exists(result.OutputPath))
            {
                Console.Error.WriteLine("Export completed but output file not found.");
                return 2;
            }

            var info = new FileInfo(result.OutputPath);
            Console.WriteLine($"Source   : {result.SourceDuration.TotalSeconds:F2}s");
            Console.WriteLine(
                $"Export   : {result.TrimStart.TotalSeconds:F2}s -> {result.TrimEnd.TotalSeconds:F2}s ({result.OutputDuration.TotalSeconds:F2}s)");
            Console.WriteLine($"Done. Output: {result.OutputPath}  ({info.Length / 1024:N0} KB)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nHeadless trim failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<string> EnsureFFprobeAvailableAsync(string ffmpegPath)
    {
        string existingProbePath = ResolveFFprobePath(ffmpegPath);
        if (!string.IsNullOrWhiteSpace(existingProbePath))
        {
            return existingProbePath;
        }

        string downloadFolder = Path.GetDirectoryName(ffmpegPath) ?? PathsManager.ToolsArchitectureFolder;
        Console.WriteLine($"FFprobe missing. Downloading tools package to: {downloadFolder}");

        FFmpegDownloadResult downloadResult = await FFmpegDownloader.DownloadLatestAsync(downloadFolder);
        if (!downloadResult.Success)
        {
            Console.WriteLine(
                $"Primary FFmpeg package download did not complete cleanly: {downloadResult.ErrorMessage ?? "unknown error"}");
        }

        string downloadedProbePath = ResolveFFprobePath(ffmpegPath);
        if (!string.IsNullOrWhiteSpace(downloadedProbePath))
        {
            return downloadedProbePath;
        }

        Console.WriteLine("Primary FFmpeg package did not contain ffprobe. Downloading ffprobe from the online fallback source...");

        string? fallbackProbePath = await FFmpegDownloader.DownloadFFprobeFallbackAsync(downloadFolder);
        if (!string.IsNullOrWhiteSpace(fallbackProbePath))
        {
            return fallbackProbePath;
        }

        throw new InvalidOperationException("FFprobe was downloaded but could not be located.");
    }

    private static string ResolveFFprobePath(string ffmpegPath)
    {
        string siblingProbePath = Path.Combine(
            Path.GetDirectoryName(ffmpegPath) ?? string.Empty,
            OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
        if (File.Exists(siblingProbePath))
        {
            return siblingProbePath;
        }

        string detectedProbePath = PathsManager.GetFFprobePath();
        return string.IsNullOrWhiteSpace(detectedProbePath) ? string.Empty : detectedProbePath;
    }
}
