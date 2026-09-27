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
using XerahS.Core;
using XerahS.Media;

namespace XerahS.UI.ViewModels;

/// <summary>Animated GIF Maker (ShareX 09-22): turn two or more images into one GIF.</summary>
public partial class AnimatedGifMakerViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cancellation;

    public AnimatedGifMakerViewModel()
    {
        Frames.CollectionChanged += (_, _) =>
        {
            CreateCommand.NotifyCanExecuteChanged();
            StatusText = Frames.Count < 2 ? "Add at least two images." : $"{Frames.Count} frames";
            if (string.IsNullOrWhiteSpace(OutputFilePath) && Frames.Count > 0)
            {
                OutputFilePath = Path.Combine(Path.GetDirectoryName(Frames[0]) ?? TaskHelpers.GetScreenshotsFolder(),
                    Path.GetFileNameWithoutExtension(Frames[0]) + "-animated.gif");
            }
        };
    }

    public ObservableCollection<string> Frames { get; } = new();

    [ObservableProperty] private string? _selectedFrame;
    [ObservableProperty] private Bitmap? _selectedPreview;
    [ObservableProperty] private int _delayMilliseconds = 500;
    [ObservableProperty] private bool _loop = true;
    [ObservableProperty] private int _repeatCount;
    [ObservableProperty] private int _maxDimension;
    [ObservableProperty] private string _outputFilePath = string.Empty;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _statusText = "Add at least two images.";

    public event EventHandler? AddFramesRequested;
    public event EventHandler? SavePickerRequested;

    [RelayCommand] private void AddFrames() => AddFramesRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void BrowseOutput() => SavePickerRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void ClearFrames() => Frames.Clear();

    [RelayCommand]
    private void RemoveFrame()
    {
        if (SelectedFrame != null) Frames.Remove(SelectedFrame);
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int offset)
    {
        if (SelectedFrame == null) return;
        int index = Frames.IndexOf(SelectedFrame);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= Frames.Count) return;
        string frame = SelectedFrame;
        Frames.Move(index, target);
        SelectedFrame = frame;
    }

    public void AddFramePaths(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(p => File.Exists(p) && FileHelpers.IsImageFile(p)))
        {
            Frames.Add(path);
        }
    }

    partial void OnSelectedFrameChanged(string? value)
    {
        var old = SelectedPreview;
        SelectedPreview = null;
        old?.Dispose();
        if (value == null || !File.Exists(value)) return;
        try
        {
            using FileStream stream = File.OpenRead(value);
            SelectedPreview = Bitmap.DecodeToWidth(stream, 480);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AnimatedGifMaker preview");
        }
    }

    private bool CanCreate() => Frames.Count >= 2 && !IsCreating;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        string ffmpegPath = PathsManager.GetFFmpegPath();
        if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            StatusText = "FFmpeg not found. Install FFmpeg or set its path in Settings.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFilePath))
        {
            StatusText = "Choose where to save the GIF.";
            return;
        }

        IsCreating = true;
        CreateCommand.NotifyCanExecuteChanged();
        ProgressPercent = 0;
        StatusText = "Creating GIF...";
        _cancellation = new CancellationTokenSource();
        try
        {
            var options = new AnimatedGifOptions
            {
                DelayMilliseconds = DelayMilliseconds,
                Loop = Loop,
                RepeatCount = RepeatCount,
                MaxDimension = MaxDimension
            };
            await new AnimatedGifMakerService(ffmpegPath).CreateAsync(Frames.ToArray(), OutputFilePath, options,
                new Progress<double>(p => ProgressPercent = p), _cancellation.Token);
            StatusText = $"Saved {Path.GetFileName(OutputFilePath)} ({new FileInfo(OutputFilePath).Length / 1024.0:0} KB)";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "AnimatedGifMaker");
            StatusText = "Could not create the GIF: " + ex.Message.Split('\n')[0];
        }
        finally
        {
            IsCreating = false;
            CreateCommand.NotifyCanExecuteChanged();
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    [RelayCommand] private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (File.Exists(OutputFilePath)) FileHelpers.OpenFolderWithFile(OutputFilePath);
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        SelectedPreview?.Dispose();
    }
}
