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

using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class ShareLogsWindow : SurfaceWindow
{
    private readonly ShareLogsViewModel _viewModel;

    public ShareLogsWindow() : this(new ShareLogsViewModel())
    {
    }

    public ShareLogsWindow(ShareLogsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.CopyToClipboard = async text =>
        {
            if (XerahS.Platform.Abstractions.PlatformServices.IsInitialized)
            {
                await XerahS.Platform.Abstractions.PlatformServices.Clipboard.SetTextAsync(text);
            }
        };
        _viewModel.PickSavePath = async suggestedName =>
        {
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save a copy of the report",
                SuggestedFileName = suggestedName,
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            return file?.TryGetLocalPath();
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await _viewModel.InitializeAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
