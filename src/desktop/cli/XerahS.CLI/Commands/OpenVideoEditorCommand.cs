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

        cmd.Add(videoOption);
        cmd.Add(ffmpegOption);

        cmd.SetAction(parseResult =>
        {
            string videoPath = parseResult.GetValue(videoOption)!;
            string? ffmpegPath = parseResult.GetValue(ffmpegOption);

            Environment.ExitCode = RunAsync(videoPath, ffmpegPath).GetAwaiter().GetResult();
        });

        return cmd;
    }

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
}
