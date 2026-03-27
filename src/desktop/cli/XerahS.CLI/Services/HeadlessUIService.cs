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

using ShareX.VideoEditor.Hosting;
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Services
{
    /// <summary>
    /// Minimal IUIService implementation for headless CLI execution.
    /// Image editor and after-capture UI remain unavailable, but the video editor can be launched for diagnostics/workflows.
    /// </summary>
    public class HeadlessUIService : IUIService
    {
        public Task HideMainWindowAsync()
        {
            // No window to hide in CLI mode
            return Task.CompletedTask;
        }

        public Task RestoreMainWindowAsync()
        {
            // No window to restore in CLI mode
            return Task.CompletedTask;
        }

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, bool taskMode = false)
        {
            Console.Error.WriteLine("[WARNING] Image editor not available in CLI mode.");
            Console.Error.WriteLine("Image dimensions: {0}x{1}", image.Width, image.Height);
            return Task.FromResult<SKBitmap?>(image);
        }

        public async Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath)
        {
            string detectedFfmpegPath = PathsManager.GetFFmpegPath();

            return await Task.Run(async () =>
            {
                try
                {
                    string resolvedVideoPath = FileHelpers.GetAbsolutePath(videoPath);
                    VideoEditorLaunchPolicy launchPolicy = VideoEditorLaunchPolicyResolver.GetCurrentPolicy();
                    if (!launchPolicy.AllowInteractiveLaunch)
                    {
                        Console.Error.WriteLine("[VideoEditor] The video editor is unavailable on this platform/session.");
                        return null;
                    }

                    var ffmpegResolution = VideoEditorFfmpegResolver.Resolve(ffmpegPath, detectedFfmpegPath);

                    LogVideoEditorFfmpegResolution(ffmpegPath, detectedFfmpegPath, ffmpegResolution);

                    string ffprobePath = string.Empty;
                    if (ffmpegResolution.IsAvailable)
                    {
                        try
                        {
                            ffprobePath = await VideoEditorFfprobeResolver.EnsureAvailableAsync(
                                ffmpegResolution.ConfiguredPath,
                                message =>
                                {
                                    string logMessage = $"[VideoEditor] {message}";
                                    Console.WriteLine(logMessage);
                                    DebugHelper.WriteLine(logMessage);
                                });
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[VideoEditor] FFprobe unavailable: {ex.Message}");
                            DebugHelper.WriteException(ex, "Failed to resolve FFprobe for video editor");
                        }
                    }

                    var options = new VideoEditorOptions
                    {
                        VideoPath = resolvedVideoPath,
                        FFmpegPath = ffmpegResolution.ConfiguredPath,
                        FFprobePath = ffprobePath,
                        Theme = ResolveTheme(),
                        EnableLinuxWaylandExplicitSyncMitigation = launchPolicy.EnableLinuxWaylandExplicitSyncMitigation
                    };

                    var events = new VideoEditorEvents
                    {
                        DiagnosticReported = diagnosticEvent =>
                        {
                            string message = $"[VideoEditor:{diagnosticEvent.Source}] {diagnosticEvent.Message}";

                            if (diagnosticEvent.Exception != null)
                            {
                                Console.Error.WriteLine(message);
                                Console.Error.WriteLine(diagnosticEvent.Exception);
                                DebugHelper.WriteException(diagnosticEvent.Exception, message);
                            }
                            else
                            {
                                Console.WriteLine(message);
                                DebugHelper.WriteLine(message);
                            }
                        },
                        ExportCompleted = outputPath =>
                        {
                            string message = $"[VideoEditor] Export completed: {outputPath}";
                            Console.WriteLine(message);
                            DebugHelper.WriteLine(message);
                        },
                        ExportFailed = ex =>
                        {
                            string message = $"[VideoEditor] Export failed: {ex.Message}";
                            Console.Error.WriteLine(message);
                            DebugHelper.WriteException(ex, message);
                        }
                    };

                    return VideoEditorHost.ShowEditorDialog(options, events);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[VideoEditor] Failed to open editor: {ex.Message}");
                    DebugHelper.WriteException(ex, "Failed to open video editor from CLI");
                    return null;
                }
            });
        }

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
        {
            // Return the tasks as-is without modification (no UI to change them)
            Console.WriteLine("[INFO] After-capture window not available in CLI mode. Using configured defaults.");
            return Task.FromResult((afterCapture, afterUpload, false));
        }

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info)
        {
            Console.WriteLine("[INFO] After-upload window not available in CLI mode.");
            return Task.CompletedTask;
        }

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection)
        {
            Console.WriteLine("[INFO] Send-to prompt not available in CLI mode. Falling back to upload.");

            return Task.FromResult(new SendToPromptResult
            {
                Action = SendToAction.UploadNow,
                IsFallback = true,
                Reason = "CLI mode cannot display the Send-to prompt."
            });
        }

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection)
        {
            Console.WriteLine($"[INFO] Send-to action '{action}' is not available in CLI mode.");
            return Task.CompletedTask;
        }

        private static void LogVideoEditorFfmpegResolution(
            string? hostPath,
            string? detectedPath,
            (string ConfiguredPath, bool IsAvailable, string Source) resolution)
        {
            string hostCandidate = string.IsNullOrWhiteSpace(hostPath) ? "(empty)" : hostPath;
            string detectedCandidate = string.IsNullOrWhiteSpace(detectedPath) ? "(empty)" : detectedPath;
            string configuredPath = string.IsNullOrWhiteSpace(resolution.ConfiguredPath)
                ? "(not set)"
                : resolution.ConfiguredPath;

            if (resolution.IsAvailable)
            {
                string message =
                    $"[VideoEditor] Using FFmpeg at: {configuredPath} (source: {resolution.Source}, hostCandidate: {hostCandidate}, detectedCandidate: {detectedCandidate})";
                Console.WriteLine(message);
                DebugHelper.WriteLine(message);
            }
            else
            {
                string message =
                    $"[VideoEditor] FFmpeg unavailable. Source={resolution.Source}, hostCandidate={hostCandidate}, detectedCandidate={detectedCandidate}, configuredPath={configuredPath}";
                Console.Error.WriteLine(message);
                DebugHelper.WriteLine(message);
            }
        }

        private static string ResolveTheme()
        {
            return SettingsManager.Settings?.ThemeMode switch
            {
                AppThemeMode.Light => "Light",
                AppThemeMode.System => "System",
                _ => "Dark"
            };
        }
    }
}
