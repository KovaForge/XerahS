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

public partial class AnimatedGifMakerWindow : SurfaceWindow
{
    private AnimatedGifMakerViewModel? _viewModel;

    public AnimatedGifMakerWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public AnimatedGifMakerViewModel? ViewModel => _viewModel;

    public void Initialize(AnimatedGifMakerViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.AddFramesRequested += OnAddFramesRequested;
        viewModel.SavePickerRequested += OnSavePickerRequested;
    }

    private async void OnAddFramesRequested(object? sender, EventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add frames",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif" } },
                FilePickerFileTypes.All
            }
        });

        // Pickers do not guarantee order; sort by name so numbered frames come out in sequence.
        _viewModel?.AddFramePaths(files.Select(f => f.TryGetLocalPath()).OfType<string>()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
    }

    private async void OnSavePickerRequested(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save GIF as",
            SuggestedFileName = Path.GetFileName(_viewModel.OutputFilePath),
            DefaultExtension = "gif"
        });
        string? path = file?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) _viewModel.OutputFilePath = path;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        _viewModel?.AddFramePaths(UploadContentWindow.GetDroppedStorageItems(e.DataTransfer)
            .Select(item => item.TryGetLocalPath()).OfType<string>());
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_viewModel != null)
        {
            _viewModel.AddFramesRequested -= OnAddFramesRequested;
            _viewModel.SavePickerRequested -= OnSavePickerRequested;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
