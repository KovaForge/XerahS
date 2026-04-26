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

using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.History;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Context for the shared history-item context menu (ContextFlyout).
/// Exposes commands and the current item so the same MenuFlyout can be used in History and Toast.
/// </summary>
public interface IHistoryItemMenuContext
{
    ICommand EditImageCommand { get; }
    ICommand EditAnnotationsCommand { get; }
    ICommand OpenFileCommand { get; }
    ICommand UploadItemCommand { get; }
    ICommand OpenFolderCommand { get; }
    ICommand CopyFilePathCommand { get; }
    ICommand CopyURLCommand { get; }
    ICommand CopyMarkdownImageCommand { get; }
    ICommand CopyImageToClipboardCommand { get; }
    ICommand CopyErrorsCommand { get; }
    ICommand OpenURLCommand { get; }
    ICommand DeleteItemCommand { get; }

    /// <summary>Current item used for visibility (URL, HasErrors).</summary>
    IHistoryItemMenuTarget? Item { get; }

    /// <summary>Underlying item for display (e.g. HistoryItem in History view). Null for Toast.</summary>
    object? DisplayItem { get; }
}

/// <summary>
/// Minimal item shape for context menu visibility (URL, HasErrors).
/// </summary>
public interface IHistoryItemMenuTarget
{
    string? URL { get; }
    bool HasErrors { get; }
    bool HasEditableAnnotations { get; }
    bool HasImageFile { get; }
    bool HasFilePath { get; }
    bool HasExistingFile { get; }
}

/// <summary>
/// Adapter so <see cref="HistoryItem"/> can be used as <see cref="IHistoryItemMenuTarget"/> without coupling History to UI.
/// </summary>
public sealed class HistoryItemMenuTargetAdapter : IHistoryItemMenuTarget
{
    private readonly HistoryItem _item;

    public HistoryItemMenuTargetAdapter(HistoryItem item)
    {
        _item = item;
    }

    public string? URL => _item.URL;
    public bool HasErrors => _item.HasErrors;
    public bool HasEditableAnnotations => _item.HasEditableAnnotations;
    public bool HasImageFile => !string.IsNullOrWhiteSpace(_item.FilePath) && FileHelpers.IsImageFile(_item.FilePath);
    public bool HasFilePath => !string.IsNullOrWhiteSpace(_item.FilePath);
    public bool HasExistingFile => !string.IsNullOrWhiteSpace(_item.FilePath) && File.Exists(_item.FilePath);
}

/// <summary>
/// Context for the shared context menu when used from History view (per-item).
/// </summary>
public sealed class HistoryItemMenuContext : IHistoryItemMenuContext
{
    private readonly HistoryViewModel _vm;
    private readonly HistoryItem _item;

    public HistoryItemMenuContext(HistoryViewModel vm, HistoryItem item)
    {
        _vm = vm;
        _item = item;
        Item = new HistoryItemMenuTargetAdapter(item);
    }

    public IHistoryItemMenuTarget? Item { get; }
    public object? DisplayItem => _item;

    public ICommand EditImageCommand => new RelayCommand(() => _vm.EditImageCommand.Execute(_item));
    public ICommand EditAnnotationsCommand => new RelayCommand(() => _vm.EditAnnotationsCommand.Execute(_item));
    public ICommand OpenFileCommand => new RelayCommand(() => _vm.OpenFileCommand.Execute(_item));
    public ICommand UploadItemCommand => new RelayCommand(() => _vm.UploadItemCommand.Execute(_item));
    public ICommand OpenFolderCommand => new RelayCommand(() => _vm.OpenFolderCommand.Execute(_item));
    public ICommand CopyFilePathCommand => new RelayCommand(() => _vm.CopyFilePathCommand.Execute(_item));
    public ICommand CopyURLCommand => new RelayCommand(() => _vm.CopyURLCommand.Execute(_item));
    public ICommand CopyMarkdownImageCommand => new RelayCommand(() => _vm.CopyMarkdownImageCommand.Execute(_item));
    public ICommand CopyImageToClipboardCommand => new RelayCommand(() => _vm.CopyImageToClipboardCommand.Execute(_item));
    public ICommand CopyErrorsCommand => new RelayCommand(() => _vm.CopyErrorsCommand.Execute(_item));
    public ICommand OpenURLCommand => new RelayCommand(() => _vm.OpenURLCommand.Execute(_item));
    public ICommand DeleteItemCommand => new RelayCommand(() => _vm.DeleteItemCommand.Execute(_item));
}

/// <summary>
/// Adapter so <see cref="ToastViewModel"/> can be used as <see cref="IHistoryItemMenuTarget"/> for visibility bindings.
/// </summary>
public sealed class ToastItemMenuTargetAdapter : IHistoryItemMenuTarget
{
    private readonly ToastViewModel _vm;

    public ToastItemMenuTargetAdapter(ToastViewModel vm)
    {
        _vm = vm;
    }

    public string? URL => _vm.Url;
    public bool HasErrors => _vm.HasErrors;
    public bool HasEditableAnnotations => false;
    public bool HasImageFile => _vm.CanCopyImage;
    public bool HasFilePath => !string.IsNullOrWhiteSpace(_vm.FilePath);
    public bool HasExistingFile => _vm.HasExistingFile;
}


/// <summary>
/// Context for the shared context menu when used from Toast window.
/// </summary>
public sealed class ToastMenuContext : IHistoryItemMenuContext
{
    public ToastMenuContext(ToastViewModel vm)
    {
        ViewModel = vm;
        Item = new ToastItemMenuTargetAdapter(vm);
    }

    /// <summary>Toast ViewModel for view bindings (Title, Text, Image, etc.).</summary>
    public ToastViewModel ViewModel { get; }

    public IHistoryItemMenuTarget? Item { get; }
    public object? DisplayItem => null;

    public ICommand EditImageCommand => ViewModel.EditImageCommand;
    public ICommand EditAnnotationsCommand => ViewModel.EditImageCommand;
    public ICommand OpenFileCommand => ViewModel.OpenFileCommand;
    public ICommand UploadItemCommand => ViewModel.UploadItemCommand;
    public ICommand OpenFolderCommand => ViewModel.OpenFolderCommand;
    public ICommand CopyFilePathCommand => ViewModel.CopyFilePathCommand;
    public ICommand CopyURLCommand => ViewModel.CopyUrlCommand;
    public ICommand CopyMarkdownImageCommand => ViewModel.CopyMarkdownImageCommand;
    public ICommand CopyImageToClipboardCommand => ViewModel.CopyImageToClipboardCommand;
    public ICommand CopyErrorsCommand => ViewModel.CopyErrorsCommand;
    public ICommand OpenURLCommand => ViewModel.OpenURLCommand;
    public ICommand DeleteItemCommand => ViewModel.DeleteItemCommand;
}
