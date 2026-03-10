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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using ShareX.VideoEditor.Hosting;
using SkiaSharp;

namespace XerahS.UI.Services
{
    public class AvaloniaUIService : IUIService
    {
        private bool _wasMainWindowVisible;
        private Avalonia.Controls.WindowState _previousWindowState;

        public async Task HideMainWindowAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null && mainWindow.IsVisible)
                    {
                        _wasMainWindowVisible = true;
                        _previousWindowState = mainWindow.WindowState;

                        // Minimize the window so it doesn't appear in screenshots
                        mainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                        DebugHelper.WriteLine("AvaloniaUIService: Main window minimized before capture");
                    }
                    else
                    {
                        _wasMainWindowVisible = false;
                    }
                }
            });

            // Small delay to ensure window is fully minimized before capture starts
            await Task.Delay(150);
        }

        public async Task RestoreMainWindowAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_wasMainWindowVisible &&
                    Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null)
                    {
                        // Restore to previous state
                        mainWindow.WindowState = _previousWindowState;
                        DebugHelper.WriteLine("AvaloniaUIService: Main window restored after capture");
                    }
                }
                _wasMainWindowVisible = false;
            });
        }

        public async Task<SKBitmap?> ShowEditorAsync(SKBitmap image)
        {
            var tcs = new TaskCompletionSource<SKBitmap?>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Create independent Editor Window
                var editorWindow = new Views.EditorWindow();

                // Create independent ViewModel for this editor instance
                var editorViewModel = new MainViewModel();
                editorViewModel.ShowCaptureToolbar = false;
                editorViewModel.ApplicationName = AppResources.AppName;

                // Wire up UploadRequested to trigger host app upload workflow
                MainViewModelHelper.WireUploadRequested(editorViewModel);

                // Wire up CopyRequested to copy edited image (with annotations) to clipboard
                MainViewModelHelper.WireCopyRequested(editorViewModel, () =>
                {
                    var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");
                    return editorView?.GetSnapshot();
                });

                // Wire up SaveRequested / SaveAsRequested for standalone editor window
                Func<SkiaSharp.SKBitmap?> getSnapshot = () =>
                    editorWindow.FindControl<EditorView>("EditorViewControl")?.GetSnapshot();
                MainViewModelHelper.WireSaveRequested(editorViewModel, getSnapshot, () => editorWindow);
                MainViewModelHelper.WireSaveAsRequested(editorViewModel, getSnapshot, () => editorWindow);
                MainViewModelHelper.WirePinRequested(editorViewModel, getSnapshot);

                // Set DataContext BEFORE initializing preview so bindings update correctly
                editorWindow.DataContext = editorViewModel;

                // Initialize the preview image
                editorViewModel.UpdatePreview(image);

                // Handle window closing to capture result
                editorWindow.Closing += (s, e) =>
                {
                    try
                    {
                        var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");
                        if (editorView != null)
                        {
                            var snapshot = editorView.GetSnapshot();
                            tcs.TrySetResult(snapshot);
                        }
                        else
                        {
                            tcs.TrySetResult(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "Failed to get editor snapshot");
                        tcs.TrySetResult(null);
                    }
                };

                // Show the window
                editorWindow.Show();
            });

            return await tcs.Task;
        }

        public async Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath)
        {
            string detectedFfmpegPath = await Dispatcher.UIThread.InvokeAsync(PathsManager.GetFFmpegPath);

            return await Task.Run(async () =>
            {
                try
                {
                    Exception? startupFailure = null;
                    var ffmpegResolution = VideoEditorFfmpegResolver.Resolve(ffmpegPath, detectedFfmpegPath);
                    LogVideoEditorFfmpegResolution(ffmpegPath, detectedFfmpegPath, ffmpegResolution);

                    string ffprobePath = string.Empty;
                    if (ffmpegResolution.IsAvailable)
                    {
                        try
                        {
                            ffprobePath = await VideoEditorFfprobeResolver.EnsureAvailableAsync(
                                ffmpegResolution.ConfiguredPath,
                                message => DebugHelper.WriteLine($"[VideoEditor] {message}"));

                            if (!string.IsNullOrWhiteSpace(ffprobePath))
                            {
                                DebugHelper.WriteLine($"[VideoEditor] Using FFprobe at: {ffprobePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteException(ex, "Failed to resolve FFprobe for video editor");
                        }
                    }

                    var options = new VideoEditorOptions
                    {
                        VideoPath = videoPath,
                        FFmpegPath = ffmpegResolution.ConfiguredPath,
                        FFprobePath = ffprobePath,
                        Theme = ResolveTheme(),
                    };

                    var events = new VideoEditorEvents
                    {
                        DiagnosticReported = diagnosticEvent =>
                        {
                            string message = $"[VideoEditor:{diagnosticEvent.Source}] {diagnosticEvent.Message}";

                            if (diagnosticEvent.Exception != null)
                            {
                                DebugHelper.WriteException(diagnosticEvent.Exception, message);
                            }
                            else
                            {
                                DebugHelper.WriteLine(message);
                            }

                            if (startupFailure == null &&
                                diagnosticEvent.Source == nameof(VideoEditorHost) &&
                                diagnosticEvent.Exception != null)
                            {
                                startupFailure = diagnosticEvent.Exception;
                            }
                        }
                    };

                    string? result = VideoEditorHost.ShowEditorDialog(options, events);

                    if (startupFailure != null)
                    {
                        await ShowVideoEditorStartupErrorAsync(startupFailure.Message);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to open video editor");
                    await ShowVideoEditorStartupErrorAsync(ex.Message);
                    return null;
                }
            });
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
                DebugHelper.WriteLine(
                    $"[VideoEditor] Using FFmpeg at: {configuredPath} (source: {resolution.Source}, hostCandidate: {hostCandidate}, detectedCandidate: {detectedCandidate})");
            }
            else
            {
                DebugHelper.WriteLine(
                    $"[VideoEditor] FFmpeg unavailable. Source={resolution.Source}, hostCandidate={hostCandidate}, detectedCandidate={detectedCandidate}, configuredPath={configuredPath}");
            }
        }

        private static string ResolveTheme()
        {
            // Map the XerahS theme setting to the VideoEditorOptions theme string.
            return XerahS.Core.SettingsManager.Settings?.ThemeMode switch
            {
                XerahS.Core.AppThemeMode.Light  => "Light",
                XerahS.Core.AppThemeMode.System => "System",
                _                               => "Dark",
            };
        }

        private static async Task ShowVideoEditorStartupErrorAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var dialog = new Window
                {
                    Title = "Video Editor Unavailable",
                    Width = 680,
                    Height = 280,
                    MinWidth = 560,
                    MinHeight = 220,
                    CanResize = true,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var closeButton = new Button
                {
                    Content = "Close",
                    MinWidth = 100,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                closeButton.Click += (_, _) => dialog.Close();

                dialog.Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "The video editor could not start.",
                            FontWeight = FontWeight.SemiBold
                        },
                        new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = new TextBlock
                            {
                                Text = message,
                                TextWrapping = TextWrapping.Wrap
                            }
                        },
                        closeButton
                    }
                };

                Window? owner = TryGetDialogOwner();

                if (CanUseDialogOwner(owner))
                {
                    _ = dialog.ShowDialog(owner!);
                }
                else
                {
                    dialog.Show();
                }
            });
        }

        private static Window? TryGetDialogOwner()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }

        private static bool CanUseDialogOwner(Window? owner) =>
            owner != null &&
            owner.IsVisible &&
            owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
            owner.ShowInTaskbar;

        public async Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new AfterCaptureViewModel(image, afterCapture, afterUpload);
                var window = new Views.AfterCaptureWindow
                {
                    DataContext = viewModel
                };

                viewModel.RequestClose += () => window.Close();

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool canUseOwner = owner != null && owner.IsVisible &&
                                   owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   owner.ShowInTaskbar;

                if (canUseOwner)
                {
                    await window.ShowDialog(owner!);
                }
                else
                {
                    var closedTcs = new TaskCompletionSource<bool>();
                    window.Closed += (_, _) => closedTcs.TrySetResult(true);
                    window.Show();
                    await closedTcs.Task;
                }

                return (viewModel.AfterCaptureTasks, viewModel.AfterUploadTasks, viewModel.Cancelled);
            });
        }

        public async Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = new AfterUploadViewModel(info);
                var window = new Views.AfterUploadWindow
                {
                    DataContext = viewModel
                };

                viewModel.RequestClose += () => window.Close();
                window.Closed += (_, _) => viewModel.Dispose();

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool canUseOwner = owner != null && owner.IsVisible &&
                                   owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   owner.ShowInTaskbar;

                if (canUseOwner)
                {
                    window.Show(owner!);
                }
                else
                {
                    window.Show();
                }
            });
        }
    }
}
