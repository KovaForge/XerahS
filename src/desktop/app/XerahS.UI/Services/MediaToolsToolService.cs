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

using Avalonia.Controls;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Media;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class MediaToolsToolService
{
    private static ImageCombinerWindow? _combinerWindow;
    private static ImageSplitterWindow? _splitterWindow;
    private static ImageThumbnailerWindow? _thumbnailerWindow;
    private static VideoConverterWindow? _converterWindow;
    private static VideoTrimmerWindow? _trimmerWindow;
    private static AnimatedGifMakerWindow? _gifMakerWindow;
    private static readonly Dictionary<ImageBatchOperation, ImageBatchToolWindow> _imageBatchWindows = new();
    private static VideoThumbnailerWindow? _videoThumbnailerWindow;
    private static ImageAnalyzerWindow? _analyzerWindow;

    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        switch (job)
        {
            case WorkflowType.ImageCombiner:
                ShowWindow(_combinerWindow, owner, () =>
                {
                    var vm = new ImageCombinerViewModel();
                    var w = new ImageCombinerWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _combinerWindow = w, "ImageCombiner");
                break;

            case WorkflowType.ImageSplitter:
                ShowWindow(_splitterWindow, owner, () =>
                {
                    var vm = new ImageSplitterViewModel();
                    var w = new ImageSplitterWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _splitterWindow = w, "ImageSplitter");
                break;

            case WorkflowType.ImageThumbnailer:
                ShowWindow(_thumbnailerWindow, owner, () =>
                {
                    var vm = new ImageThumbnailerViewModel();
                    var w = new ImageThumbnailerWindow(vm);
                    w.Initialize(vm);
                    return w;
                }, w => _thumbnailerWindow = w, "ImageThumbnailer");
                break;

            case WorkflowType.VideoConverter:
                ShowWindow(_converterWindow, owner, () =>
                {
                    var vm = new VideoConverterViewModel();
                    var w = new VideoConverterWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _converterWindow = w, "VideoConverter");
                break;

            case WorkflowType.VideoTrimmer:
                OpenVideoTrimmer(null, owner);
                break;

            case WorkflowType.ImageResizer:
                OpenImageBatchTool(ImageBatchOperation.Resize, null, owner);
                break;

            case WorkflowType.ImageConverter:
                OpenImageBatchTool(ImageBatchOperation.Convert, null, owner);
                break;

            case WorkflowType.ImageWatermark:
                OpenImageBatchTool(ImageBatchOperation.Watermark, null, owner);
                break;

            case WorkflowType.AnimatedGifMaker:
                ShowWindow(_gifMakerWindow, owner, () =>
                {
                    var vm = new AnimatedGifMakerViewModel();
                    var w = new AnimatedGifMakerWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _gifMakerWindow = w, "AnimatedGifMaker");
                break;

            case WorkflowType.VideoThumbnailer:
                ShowWindow(_videoThumbnailerWindow, owner, () =>
                {
                    var vm = new VideoThumbnailerViewModel();
                    var w = new VideoThumbnailerWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _videoThumbnailerWindow = w, "VideoThumbnailer");
                break;

            case WorkflowType.AnalyzeImage:
                ShowWindow(_analyzerWindow, owner, () =>
                {
                    var vm = new ImageAnalyzerViewModel();
                    var w = new ImageAnalyzerWindow();
                    w.Initialize(vm);
                    return w;
                }, w => _analyzerWindow = w, "ImageAnalyzer");
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>Opens the Image Resizer / Converter / Watermark tool, optionally with images added.</summary>
    public static void OpenImageBatchTool(ImageBatchOperation operation, IEnumerable<string>? filePaths, Window? owner)
    {
        _imageBatchWindows.TryGetValue(operation, out var current);
        ShowWindow(current, owner, () =>
        {
            var vm = new ImageBatchToolViewModel(operation);
            var w = new ImageBatchToolWindow();
            w.Initialize(vm);
            return w;
        }, w =>
        {
            if (w == null) _imageBatchWindows.Remove(operation);
            else _imageBatchWindows[operation] = w;
        }, operation.ToString());

        if (filePaths != null && _imageBatchWindows.TryGetValue(operation, out var window) && window.ViewModel is { } viewModel)
        {
            viewModel.AddFilePaths(filePaths);
        }
    }

    /// <summary>Opens the Video Trimmer, optionally with a video already loaded (History "Trim video...").</summary>
    public static void OpenVideoTrimmer(string? filePath, Window? owner)
    {
        ShowWindow(_trimmerWindow, owner, () =>
        {
            var vm = new VideoTrimmerViewModel();
            var w = new VideoTrimmerWindow();
            w.Initialize(vm);
            return w;
        }, w => _trimmerWindow = w, "VideoTrimmer");

        if (!string.IsNullOrWhiteSpace(filePath) && _trimmerWindow?.ViewModel is { } viewModel)
        {
            _ = viewModel.LoadAsync(filePath);
        }
    }

    private static void ShowWindow<T>(T? current, Window? owner, Func<T> createWindow, Action<T?> setWindow, string toolName) where T : Window
    {
        if (current != null)
        {
            try
            {
                if (owner != null)
                {
                    current.Show(owner);
                }
                else
                {
                    current.Show();
                }
                current.Activate();
                return;
            }
            catch
            {
                setWindow(null);
            }
        }

        var window = createWindow();
        setWindow(window);

        window.Closed += (_, _) =>
        {
            setWindow(null);
        };

        if (owner != null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        DebugHelper.WriteLine($"{toolName}: Window shown.");
    }
}
