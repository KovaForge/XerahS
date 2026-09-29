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

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Media;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Shared view model for the Image Resizer, Image Converter and Image Watermark tools
/// (ShareX 09-20). The operation decides which options are shown and the default file name.
/// </summary>
public partial class ImageBatchToolViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _runCancellation;

    public ImageBatchToolViewModel(ImageBatchOperation operation)
    {
        Operation = operation;
        _fileNamePattern = ImageBatchService.GetDefaultFileNamePattern(operation);
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            ProcessCommand.NotifyCanExecuteChanged();
            if (SelectedFile == null && Files.Count > 0) SelectedFile = Files[0];
        };
    }

    public ImageBatchOperation Operation { get; }
    public string Title => Operation switch
    {
        ImageBatchOperation.Resize => "Image Resizer",
        ImageBatchOperation.Watermark => "Image Watermark",
        _ => "Image Converter"
    };

    public bool IsResize => Operation == ImageBatchOperation.Resize;
    public bool IsWatermark => Operation == ImageBatchOperation.Watermark;
    public bool IsTextWatermark => IsWatermark && WatermarkType == ImageWatermarkType.Text;
    public bool IsImageWatermark => IsWatermark && WatermarkType == ImageWatermarkType.Image;
    public bool UsesQuality => Format != ImageBatchOutputFormat.Png;
    public bool UsesBackground => Format == ImageBatchOutputFormat.Jpeg;
    public bool HasFiles => Files.Count > 0;

    public ObservableCollection<string> Files { get; } = new();
    public ImageResizeMode[] ResizeModes { get; } = Enum.GetValues<ImageResizeMode>();
    public ImageBatchOutputFormat[] Formats { get; } = Enum.GetValues<ImageBatchOutputFormat>();
    public ImageWatermarkType[] WatermarkTypes { get; } = Enum.GetValues<ImageWatermarkType>();
    public ImageWatermarkPosition[] Positions { get; } = Enum.GetValues<ImageWatermarkPosition>();

    [ObservableProperty] private string? _selectedFile;
    [ObservableProperty] private int _width = 1280;
    [ObservableProperty] private int _height = 720;
    [ObservableProperty] private ImageResizeMode _resizeMode = ImageResizeMode.Fit;
    [ObservableProperty] private ImageBatchOutputFormat _format = ImageBatchOutputFormat.Png;
    [ObservableProperty] private int _quality = 90;
    [ObservableProperty] private string _backgroundColor = "#FFFFFF";
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private string _fileNamePattern;
    [ObservableProperty] private ImageWatermarkType _watermarkType = ImageWatermarkType.Text;
    [ObservableProperty] private string _watermarkText = "© XerahS";
    [ObservableProperty] private string _watermarkImagePath = string.Empty;
    [ObservableProperty] private ImageWatermarkPosition _watermarkPosition = ImageWatermarkPosition.BottomRight;
    [ObservableProperty] private int _watermarkMargin = 16;
    [ObservableProperty] private int _watermarkOpacity = 60;
    [ObservableProperty] private int _watermarkTextSize = 32;
    [ObservableProperty] private string _watermarkTextColor = "#FFFFFF";
    [ObservableProperty] private int _watermarkImageScale = 20;
    [ObservableProperty] private int _watermarkRotation;
    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private string _previewInfo = string.Empty;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _statusText = "Add images to get started.";

    public event EventHandler? AddFilesRequested;
    public event EventHandler? OutputFolderRequested;
    public event EventHandler? WatermarkImageRequested;

    [RelayCommand] private void AddFiles() => AddFilesRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void BrowseOutputFolder() => OutputFolderRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void BrowseWatermarkImage() => WatermarkImageRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedFile != null) Files.Remove(SelectedFile);
    }

    [RelayCommand]
    private void ClearFiles() => Files.Clear();

    public void AddFilePaths(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (File.Exists(path) && FileHelpers.IsImageFile(path) && !Files.Contains(path))
            {
                Files.Add(path);
            }
        }

        StatusText = HasFiles ? $"{Files.Count} image(s)" : "No supported images were added.";
    }

    public ImageBatchOptions BuildOptions()
    {
        ImageBatchService.TryParseColor(BackgroundColor, out var background);
        ImageBatchService.TryParseColor(WatermarkTextColor, out var textColor);
        return new ImageBatchOptions
        {
            Operation = Operation,
            Width = Math.Max(1, Width),
            Height = Math.Max(1, Height),
            ResizeMode = ResizeMode,
            Format = Format,
            Quality = Quality,
            BackgroundColor = background,
            OutputFolder = OutputFolder,
            FileNamePattern = FileNamePattern,
            Watermark = new ImageWatermarkOptions
            {
                Type = WatermarkType,
                Text = WatermarkText,
                ImagePath = WatermarkImagePath,
                Position = WatermarkPosition,
                Margin = WatermarkMargin,
                Opacity = WatermarkOpacity,
                TextSize = WatermarkTextSize,
                TextColor = textColor,
                ImageScale = WatermarkImageScale,
                Rotation = WatermarkRotation
            }
        };
    }

    private bool CanProcess() => HasFiles && !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanProcess))]
    private async Task ProcessAsync()
    {
        IsProcessing = true;
        ProcessCommand.NotifyCanExecuteChanged();
        ProgressPercent = 0;
        _runCancellation = new CancellationTokenSource();
        CancellationToken token = _runCancellation.Token;
        ImageBatchOptions options = BuildOptions();
        string[] files = Files.ToArray();
        int done = 0, failed = 0;
        string? lastError = null;

        try
        {
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();
                StatusText = $"Processing {Path.GetFileName(file)}...";
                try
                {
                    await Task.Run(() => ImageBatchService.ProcessFile(file, options), token);
                    done++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    lastError = ex.Message;
                    DebugHelper.WriteException(ex, $"{Title}: {file}");
                }

                ProgressPercent = (done + failed) * 100.0 / files.Length;
            }

            StatusText = failed == 0
                ? $"Saved {done} image(s)."
                : $"Saved {done}, failed {failed}: {lastError}";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Canceled after {done} image(s).";
        }
        finally
        {
            IsProcessing = false;
            ProcessCommand.NotifyCanExecuteChanged();
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _runCancellation?.Cancel();

    partial void OnSelectedFileChanged(string? value) => RefreshPreview();
    partial void OnWidthChanged(int value) => RefreshPreview();
    partial void OnHeightChanged(int value) => RefreshPreview();
    partial void OnResizeModeChanged(ImageResizeMode value) => RefreshPreview();
    partial void OnQualityChanged(int value) => RefreshPreview();
    partial void OnBackgroundColorChanged(string value) => RefreshPreview();
    partial void OnWatermarkTextChanged(string value) => RefreshPreview();
    partial void OnWatermarkImagePathChanged(string value) => RefreshPreview();
    partial void OnWatermarkPositionChanged(ImageWatermarkPosition value) => RefreshPreview();
    partial void OnWatermarkMarginChanged(int value) => RefreshPreview();
    partial void OnWatermarkOpacityChanged(int value) => RefreshPreview();
    partial void OnWatermarkTextSizeChanged(int value) => RefreshPreview();
    partial void OnWatermarkTextColorChanged(string value) => RefreshPreview();
    partial void OnWatermarkImageScaleChanged(int value) => RefreshPreview();
    partial void OnWatermarkRotationChanged(int value) => RefreshPreview();

    partial void OnFormatChanged(ImageBatchOutputFormat value)
    {
        OnPropertyChanged(nameof(UsesQuality));
        OnPropertyChanged(nameof(UsesBackground));
        RefreshPreview();
    }

    partial void OnWatermarkTypeChanged(ImageWatermarkType value)
    {
        OnPropertyChanged(nameof(IsTextWatermark));
        OnPropertyChanged(nameof(IsImageWatermark));
        RefreshPreview();
    }

    private async void RefreshPreview()
    {
        _previewCancellation?.Cancel();
        string? file = SelectedFile;
        if (file == null)
        {
            SetPreview(null, string.Empty);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        ImageBatchOptions options = BuildOptions();
        try
        {
            await Task.Delay(150, cancellation.Token);
            byte[] data = await Task.Run(() => ImageBatchService.CreatePreview(file, options), cancellation.Token);
            if (cancellation.IsCancellationRequested) return;

            using var stream = new MemoryStream(data);
            var bitmap = new Bitmap(stream);
            string size = options.Operation == ImageBatchOperation.Resize ? $"{options.Width} x {options.Height}" : string.Empty;
            SetPreview(bitmap, $"{Path.GetFileName(file)}  {size}  ~{data.Length / 1024.0:0} KB preview".Trim());
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetPreview(null, ex.Message);
        }
    }

    private void SetPreview(Bitmap? bitmap, string info)
    {
        var old = Preview;
        Preview = bitmap;
        PreviewInfo = info;
        old?.Dispose();
    }

    public void Dispose()
    {
        _previewCancellation?.Cancel();
        _runCancellation?.Cancel();
        Preview?.Dispose();
    }
}
