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
using Omacut.Core;
using XerahS.Media;
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
            Description = "Skip the UI and trim/export through the Omacut export pipeline."
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
            Description = "Headless output format: MP4 (default), WebM, or GIF."
        };

        var cropOption = new Option<string?>("--crop")
        {
            Description = "Headless crop rectangle as x,y,width,height in source pixels."
        };

        var watermarkOption = new Option<string?>("--watermark")
        {
            Description = "Headless text watermark to burn into the export."
        };

        var watermarkImageOption = new Option<string?>("--watermark-image")
        {
            Description = "Headless image watermark path to overlay during export."
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
        cmd.Add(watermarkImageOption);

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
            string? watermarkImage = parseResult.GetValue(watermarkImageOption);

            Environment.ExitCode = headless
                ? RunHeadlessAsync(videoPath, ffmpegPath, trimStart, trimEndOffset, output, format, crop, watermark, watermarkImage)
                    .GetAwaiter().GetResult()
                : RunAsync(videoPath, ffmpegPath).GetAwaiter().GetResult();
        });

        return cmd;
    }

    // Interactive (native Omacut window) mode

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

    // Headless trim/export mode

    private static async Task<int> RunHeadlessAsync(
        string videoPath,
        string? ffmpegOverride,
        double trimStartSeconds,
        double trimEndOffsetSeconds,
        string? outputPath,
        string? format,
        string? crop,
        string? watermark,
        string? watermarkImage)
    {
        string? renderedWatermark = null;
        try
        {
            if (!File.Exists(videoPath))
            {
                Console.Error.WriteLine($"Video file does not exist: {videoPath}");
                return 2;
            }

            if (!TryParseFormat(format, out ExportFormat exportFormat))
            {
                Console.Error.WriteLine($"Unsupported format '{format}'. Use MP4, WebM or GIF.");
                return 2;
            }

            if (!TryParseCrop(crop, out int cropX, out int cropY, out int cropWidth, out int cropHeight, out string? cropError))
            {
                Console.Error.WriteLine(cropError);
                return 2;
            }

            if (!string.IsNullOrWhiteSpace(watermarkImage) && !File.Exists(watermarkImage))
            {
                Console.Error.WriteLine($"Watermark image does not exist: {watermarkImage}");
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

            string ffprobePath = await VideoEditorFfprobeResolver.EnsureAvailableAsync(resolution.ConfiguredPath, Console.WriteLine);
            var tools = new FfmpegTools(resolution.ConfiguredPath, ffprobePath);
            MediaInfo info = await MediaProbe.ProbeAsync(tools, Path.GetFullPath(videoPath));

            double start = Math.Clamp(trimStartSeconds, 0, info.Duration);
            double end = info.Duration - Math.Max(0, trimEndOffsetSeconds);
            if (end - start <= 0)
            {
                Console.Error.WriteLine($"The trim leaves nothing to export (source is {info.Duration:F2}s).");
                return 2;
            }

            WatermarkOverlay? overlay = null;
            if (!string.IsNullOrWhiteSpace(watermark) || !string.IsNullOrWhiteSpace(watermarkImage))
            {
                var settings = new VideoWatermarkSettings
                {
                    Enabled = true,
                    Text = watermark ?? string.Empty,
                    ImagePath = watermarkImage ?? string.Empty,
                };
                string workDirectory = Path.Combine(Path.GetTempPath(), "XerahS", "video-watermarks");
                string? image = VideoWatermarkRenderer.ResolveImage(settings, workDirectory);
                if (image != null)
                {
                    renderedWatermark = image == watermarkImage ? null : image;
                    overlay = new WatermarkOverlay(image, settings.Opacity, settings.PositionX, settings.PositionY);
                }
            }

            var options = new ExportOptions
            {
                SourcePath = info.Path,
                Start = start,
                End = end,
                Format = exportFormat,
                Crop = cropWidth > 0 && cropHeight > 0 ? new CropRect(cropX, cropY, cropWidth, cropHeight) : null,
                Watermark = overlay,
            };

            string requestedOutput = !string.IsNullOrWhiteSpace(outputPath)
                ? Path.GetFullPath(outputPath)
                : ExportFormats.SuggestedPath(info.Path, exportFormat);

            Console.WriteLine("=== Open Video Editor (headless export) ===");
            Console.WriteLine($"Input    : {info.Path}");
            Console.WriteLine($"Output   : {ExportFormats.WithExtension(requestedOutput, exportFormat)}");
            Console.WriteLine($"Format   : {ExportFormats.DisplayName(exportFormat)}");
            Console.WriteLine($"Trim     : {start:F2}s -> {end:F2}s ({end - start:F2}s of {info.Duration:F2}s)");
            Console.WriteLine($"Crop     : {(options.Crop is CropRect c ? $"{c.X},{c.Y},{c.Width},{c.Height}" : "(none)")}");
            Console.WriteLine($"Watermark: {(overlay == null ? "(none)" : watermark ?? "(image)")}");
            Console.WriteLine($"FFmpeg   : {tools.FfmpegPath}");

            var service = new ExportService(tools, await EncoderSupport.ProbeAsync(tools));
            Console.Write("Encoding");
            string written = await service.ExportAsync(
                options,
                info,
                requestedOutput,
                new ConsoleProgress());
            Console.WriteLine();

            var fileInfo = new FileInfo(written);
            Console.WriteLine($"Done. Output: {written}  ({fileInfo.Length / 1024:N0} KB)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nHeadless export failed: {ex.Message}");
            return 2;
        }
        finally
        {
            if (renderedWatermark != null)
            {
                try
                {
                    File.Delete(renderedWatermark);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    internal static bool TryParseFormat(string? format, out ExportFormat exportFormat)
    {
        exportFormat = ExportFormat.Mp4;
        if (string.IsNullOrWhiteSpace(format))
        {
            return true;
        }

        switch (format.Trim().TrimStart('.').ToUpperInvariant())
        {
            case "MP4":
                exportFormat = ExportFormat.Mp4;
                return true;
            case "WEBM":
                exportFormat = ExportFormat.WebM;
                return true;
            case "GIF":
                exportFormat = ExportFormat.Gif;
                return true;
            default:
                return false;
        }
    }

    private sealed class ConsoleProgress : IProgress<double>
    {
        private int _last = -1;

        public void Report(double value)
        {
            int percent = (int)Math.Round(value * 100);
            if (percent != _last)
            {
                _last = percent;
                Console.Write($"\rEncoding {percent}%   ");
            }
        }
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
