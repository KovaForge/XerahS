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

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Media;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Video Trimmer (ShareX ec0d3f6): pick a start and end, preview the boundary frames, and export
/// either instantly (stream copy, cuts snap to keyframes) or precisely (H.264/AAC re-encode).
/// </summary>
public partial class VideoTrimmerViewModel : ViewModelBase, IDisposable
{
    private VideoTrimmerService? _service;
    private CancellationTokenSource? _trimCancellation;
    private CancellationTokenSource? _startPreviewCancellation;
    private CancellationTokenSource? _endPreviewCancellation;
    private bool _syncingText;

    [ObservableProperty] private string? _inputFilePath;
    [ObservableProperty] private string _outputFilePath = "";
    [ObservableProperty] private double _duration;
    [ObservableProperty] private double _startSeconds;
    [ObservableProperty] private double _endSeconds;
    [ObservableProperty] private string _startText = "00:00:00.000";
    [ObservableProperty] private string _endText = "00:00:00.000";
    [ObservableProperty] private bool _isPrecise;
    [ObservableProperty] private bool _hasInput;
    [ObservableProperty] private bool _isTrimming;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _statusText = "Select a video to trim.";
    [ObservableProperty] private Bitmap? _startPreview;
    [ObservableProperty] private Bitmap? _endPreview;

    public event EventHandler? FilePickerRequested;
    public event EventHandler? SavePickerRequested;

    public string SelectionText => HasInput ? $"Selection: {VideoTrimmerService.Timestamp(EndSeconds - StartSeconds)}" : string.Empty;

    public string ModeHint => IsPrecise
        ? "Precise re-encodes to H.264/AAC MP4 at the exact boundaries. Slower; subtitles are dropped."
        : "Fast copies the streams without re-encoding. Instant and lossless, but cuts snap to the nearest keyframe, so the result can start a little early.";

    [RelayCommand]
    private void BrowseInput() => FilePickerRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void BrowseOutput() => SavePickerRequested?.Invoke(this, EventArgs.Empty);

    public async Task LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        string ffmpegPath = PathsManager.GetFFmpegPath();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            StatusText = "FFmpeg not found. Install FFmpeg or set its path in Settings.";
            return;
        }

        _service = new VideoTrimmerService(ffmpegPath);
        StatusText = "Reading video...";
        double? duration = await _service.ProbeDurationAsync(filePath);
        if (duration is not > 0)
        {
            HasInput = false;
            StatusText = "Could not read the video's duration. Is this a video file?";
            return;
        }

        InputFilePath = filePath;
        Duration = duration.Value;
        HasInput = true;
        StartSeconds = 0;
        EndSeconds = Duration;
        OutputFilePath = VideoTrimmerService.GetDefaultOutputPath(filePath, IsPrecise);
        StatusText = $"{Path.GetFileName(filePath)} - {VideoTrimmerService.Timestamp(Duration)}";
    }

    partial void OnStartSecondsChanged(double value)
    {
        double clamped = Math.Clamp(value, 0, Math.Max(0, EndSeconds - 0.001));
        if (clamped != value && HasInput)
        {
            StartSeconds = clamped;
            return;
        }

        SetText(() => StartText = VideoTrimmerService.Timestamp(value));
        OnPropertyChanged(nameof(SelectionText));
        RefreshPreview(value, isStart: true);
    }

    partial void OnEndSecondsChanged(double value)
    {
        double clamped = Math.Clamp(value, Math.Min(Duration, StartSeconds + 0.001), Duration);
        if (clamped != value && HasInput)
        {
            EndSeconds = clamped;
            return;
        }

        SetText(() => EndText = VideoTrimmerService.Timestamp(value));
        OnPropertyChanged(nameof(SelectionText));
        // A frame exactly at EOF does not decode; preview slightly before it.
        RefreshPreview(Math.Max(0, value - 0.05), isStart: false);
    }

    partial void OnStartTextChanged(string value)
    {
        if (!_syncingText && VideoTrimmerService.TryParseTimestamp(value, out double seconds))
        {
            StartSeconds = seconds;
        }
    }

    partial void OnEndTextChanged(string value)
    {
        if (!_syncingText && VideoTrimmerService.TryParseTimestamp(value, out double seconds))
        {
            EndSeconds = seconds;
        }
    }

    partial void OnIsPreciseChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeHint));
        if (InputFilePath != null)
        {
            OutputFilePath = Path.ChangeExtension(OutputFilePath, VideoTrimmerService.GetOutputExtension(InputFilePath, value));
        }
    }

    [RelayCommand]
    private async Task TrimAsync()
    {
        if (_service == null || InputFilePath == null || IsTrimming) return;

        IsTrimming = true;
        ProgressPercent = 0;
        StatusText = "Trimming...";
        _trimCancellation = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(percent => ProgressPercent = percent);
            await _service.TrimAsync(InputFilePath, OutputFilePath, StartSeconds, EndSeconds, Duration, IsPrecise, progress, _trimCancellation.Token);
            ProgressPercent = 100;
            StatusText = $"Saved {Path.GetFileName(OutputFilePath)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Trim canceled.";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "VideoTrimmer");
            StatusText = $"Trim failed: {FirstLine(ex.Message)}";
        }
        finally
        {
            IsTrimming = false;
            _trimCancellation?.Dispose();
            _trimCancellation = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _trimCancellation?.Cancel();

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (File.Exists(OutputFilePath))
        {
            FileHelpers.OpenFolderWithFile(OutputFilePath);
        }
    }

    private void SetText(Action set)
    {
        _syncingText = true;
        try { set(); }
        finally { _syncingText = false; }
    }

    private async void RefreshPreview(double position, bool isStart)
    {
        if (_service == null || InputFilePath == null) return;

        ref CancellationTokenSource? slot = ref isStart ? ref _startPreviewCancellation : ref _endPreviewCancellation;
        slot?.Cancel();
        var cancellation = new CancellationTokenSource();
        slot = cancellation;

        try
        {
            // Debounce slider drags so only the settled position spawns FFmpeg.
            await Task.Delay(200, cancellation.Token);
            byte[]? png = await _service.GetFrameAsync(InputFilePath, position, token: cancellation.Token);
            if (png == null || cancellation.IsCancellationRequested) return;

            using var stream = new MemoryStream(png);
            var bitmap = new Bitmap(stream);
            if (isStart)
            {
                var old = StartPreview;
                StartPreview = bitmap;
                old?.Dispose();
            }
            else
            {
                var old = EndPreview;
                EndPreview = bitmap;
                old?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "VideoTrimmer preview");
        }
    }

    private static string FirstLine(string text)
    {
        string trimmed = text.Trim();
        int newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }

    public void Dispose()
    {
        _trimCancellation?.Cancel();
        _startPreviewCancellation?.Cancel();
        _endPreviewCancellation?.Cancel();
        StartPreview?.Dispose();
        EndPreview?.Dispose();
    }
}
