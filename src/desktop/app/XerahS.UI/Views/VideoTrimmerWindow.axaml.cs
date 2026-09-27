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
using Avalonia.Input;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class VideoTrimmerWindow : SurfaceWindow
{
    private VideoTrimmerViewModel? _viewModel;

    public VideoTrimmerWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public VideoTrimmerViewModel? ViewModel => _viewModel;

    public void Initialize(VideoTrimmerViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.FilePickerRequested += OnFilePickerRequested;
        viewModel.SavePickerRequested += OnSavePickerRequested;
    }

    private async void OnFilePickerRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select video to trim",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Video files") { Patterns = new[] { "*.mp4", "*.mkv", "*.webm", "*.mov", "*.avi", "*.m4v", "*.wmv", "*.flv" } },
                FilePickerFileTypes.All
            }
        });

        string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (_viewModel != null && !string.IsNullOrEmpty(path))
        {
            await _viewModel.LoadAsync(path);
        }
    }

    private async void OnSavePickerRequested(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save trimmed video as",
            SuggestedFileName = Path.GetFileName(_viewModel.OutputFilePath),
            DefaultExtension = Path.GetExtension(_viewModel.OutputFilePath).TrimStart('.')
        });

        string? path = file?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            _viewModel.OutputFilePath = path;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var file = UploadContentWindow.GetDroppedStorageItems(e.DataTransfer).FirstOrDefault()?.TryGetLocalPath();
        if (_viewModel != null && !string.IsNullOrEmpty(file))
        {
            await _viewModel.LoadAsync(file);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_viewModel != null)
        {
            _viewModel.FilePickerRequested -= OnFilePickerRequested;
            _viewModel.SavePickerRequested -= OnSavePickerRequested;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
