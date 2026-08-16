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
            Description = "Output path for the exported file (headless mode only). Defaults next to the input."
        };

        var formatOption = new Option<string?>("--format")
        {
            Description = "Headless output format: MP4, WebM, GIF, or WebP. When set with crop/watermark, uses the general export pipeline."
        };

        var cropOption = new Option<string?>("--crop")
        {
            Description = "Headless crop rectangle as x,y,width,height in source pixels."
        };

        var watermarkOption = new Option<string?>("--watermark")
        {
            Description = "Headless text watermark to burn into the export."
        };

        cmd.Add(videoOption);
        cmd.Add(ffmpegOption);
        cmd.Add(headlessOption);
        cmd.Add(trimStartOption);
        cmd.Add(trimEndOffsetOption);
        cmd.Add(outputOption);
        cmd.Add(formatOption);
        cmd.Add(cropOption);
        cmd.Add(watermarkOption);

        cmd.SetAction(parseResult =>
        {
            string videoPath = parseResult.GetValue(videoOption)!;
            string? ffmpegPath = parseResult.GetValue(ffmpegOption);
            bool headless = parseResult.GetValue(headlessOption);
            double trimStart = parseResult.GetValue(trimStartOption);
            double trimEndOffset = parseResult.GetValue(trimEndOffsetOption);
            string? output = parseResult.GetValue(outputOption);
            string? format = parseResult.GetValue(formatOption);
            string? crop = parseResult.GetValue(cropOption);
            string? watermark = parseResult.GetValue(watermarkOption);

            Environment.ExitCode = headless
                ? RunHeadlessAsync(videoPath, ffmpegPath, trimStart, trimEndOffset, output, format, crop, watermark)
                    .GetAwaiter().GetResult()
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
        string? outputPath,
        string? format,
        string? crop,
        string? watermark)
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

            string ffprobePath = await VideoEditorFfprobeResolver.EnsureAvailableAsync(
                resolution.ConfiguredPath,
                Console.WriteLine);
            var service = new VideoEditorAutomationService(resolution.ConfiguredPath, ffprobePath);

            bool useGeneralExport =
                !string.IsNullOrWhiteSpace(format) ||
                !string.IsNullOrWhiteSpace(crop) ||
                !string.IsNullOrWhiteSpace(watermark);

            if (useGeneralExport)
            {
                return await RunHeadlessExportAsync(
                    service,
                    videoPath,
                    outputPath,
                    format,
                    crop,
                    watermark,
                    trimStartSeconds,
                    trimEndOffsetSeconds);
            }

            string resolvedOutputPath = !string.IsNullOrWhiteSpace(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.Combine(
                    Path.GetDirectoryName(videoPath) ?? ".",
                    $"{Path.GetFileNameWithoutExtension(videoPath)}_trimmed.mp4");
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
            VideoEditorTrimResult result = await service.TrimAsync(
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
            Console.Error.WriteLine($"\nHeadless export failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunHeadlessExportAsync(
        VideoEditorAutomationService service,
        string videoPath,
        string? outputPath,
        string? format,
        string? crop,
        string? watermark,
        double trimStartSeconds,
        double trimEndOffsetSeconds)
    {
        if (!TryParseCrop(crop, out int cropX, out int cropY, out int cropWidth, out int cropHeight, out string? cropError))
        {
            Console.Error.WriteLine(cropError);
            return 2;
        }

        string outputFormat = string.IsNullOrWhiteSpace(format) ? "MP4" : format.Trim();
        bool isTrimActive = trimStartSeconds > 0 || trimEndOffsetSeconds > 0;
        TimeSpan trimEnd = TimeSpan.Zero;

        if (isTrimActive && trimEndOffsetSeconds > 0)
        {
            TimeSpan duration = await service.ProbeDurationAsync(videoPath);
            trimEnd = duration - TimeSpan.FromSeconds(trimEndOffsetSeconds);
        }

        var exportRequest = new VideoEditorExportRequest
        {
            InputPath = videoPath,
            OutputPath = outputPath,
            OutputFormat = outputFormat,
            IsTrimActive = isTrimActive,
            TrimStart = TimeSpan.FromSeconds(Math.Max(0, trimStartSeconds)),
            TrimEnd = trimEnd,
            IsCropActive = cropWidth > 0 && cropHeight > 0,
            CropX = cropX,
            CropY = cropY,
            CropWidth = cropWidth,
            CropHeight = cropHeight,
            WatermarkEnabled = !string.IsNullOrWhiteSpace(watermark),
            WatermarkText = watermark ?? string.Empty,
            QualityScale = 1.0
        };

        Console.WriteLine("=== Open Video Editor (headless export) ===");
        Console.WriteLine($"Input    : {videoPath}");
        Console.WriteLine($"Format   : {outputFormat}");
        Console.WriteLine($"Crop     : {(exportRequest.IsCropActive ? $"{cropX},{cropY},{cropWidth},{cropHeight}" : "(none)")}");
        Console.WriteLine($"Watermark: {(exportRequest.WatermarkEnabled ? watermark : "(none)")}");

        Console.Write("Encoding");
        VideoEditorExportResult result = await service.ExportAsync(
            exportRequest,
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
        Console.WriteLine($"Done. Output: {result.OutputPath}  ({info.Length / 1024:N0} KB)");
        return 0;
    }

    private static bool TryParseCrop(
        string? crop,
        out int x,
        out int y,
        out int width,
        out int height,
        out string? error)
    {
        x = y = width = height = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(crop))
        {
            return true;
        }

        string[] parts = crop.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out x) ||
            !int.TryParse(parts[1], out y) ||
            !int.TryParse(parts[2], out width) ||
            !int.TryParse(parts[3], out height) ||
            width <= 0 ||
            height <= 0)
        {
            error = "Crop must be x,y,width,height with positive width and height.";
            return false;
        }

        return true;
    }

}
