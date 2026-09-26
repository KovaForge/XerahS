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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ShareX.ImageEditor.Presentation.Theming;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

/// <summary>
/// Media Browser view: pickers, drag-and-drop upload, context menus and keyboard shortcuts.
/// Everything else lives in <see cref="MediaBrowserViewModel"/>.
/// </summary>
public partial class MediaBrowserView : UserControl
{
    private bool _initialized;

    public MediaBrowserView()
    {
        AvaloniaXamlLoader.Load(this);

        Control dropZone = this.FindControl<Control>("DropZone")!;
        dropZone.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        dropZone.AddHandler(DragDrop.DropEvent, OnDrop);

        foreach (string name in new[] { "RowsList", "TilesList" })
        {
            ListBox list = this.FindControl<ListBox>(name)!;
            list.ContextMenu = CreateItemMenu();
            list.DoubleTapped += OnItemDoubleTapped;
            list.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
        }

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => (DataContext as MediaBrowserViewModel)?.Dispose();
    }

    private MediaBrowserViewModel? ViewModel => DataContext as MediaBrowserViewModel;

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized && ViewModel is { } vm)
        {
            _initialized = true;
            Focus();
            await vm.InitializeAsync();
        }
    }

    // ---- upload / download ----

    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { CanUpload: true } vm || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Upload files to " + vm.SelectedSource.DisplayName,
            AllowMultiple = true,
        });

        await vm.UploadAsync(files.Select(ToUploadSource).ToList());
    }

    private async void OnDownloadClick(object? sender, RoutedEventArgs e) => await DownloadSelectedAsync();

    private async Task DownloadSelectedAsync()
    {
        if (ViewModel is not { CanDownload: true } vm || vm.SelectedItem is not { } item ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        string extension = Path.GetExtension(item.Name).TrimStart('.');
        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save " + item.Name,
            SuggestedFileName = MediaBrowserViewModel.SafeFileName(item.Name),
            DefaultExtension = extension.Length > 0 ? extension : null,
            ShowOverwritePrompt = true,
        });

        if (file == null)
        {
            return;
        }

        bool ok;
        await using (Stream stream = await file.OpenWriteAsync())
        {
            stream.SetLength(0);
            ok = await vm.DownloadAsync(item, stream);
        }

        if (!ok)
        {
            try
            {
                await file.DeleteAsync(); // never leave a truncated file behind
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        }
    }

    private static MediaBrowserUploadSource ToUploadSource(IStorageFile file) => new(file.Name, file.OpenReadAsync);

    // ---- drag and drop ----

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool canUpload = ViewModel?.CanUpload == true && e.DataTransfer.Formats.Contains(DataFormat.File);
        e.DragEffects = canUpload ? DragDropEffects.Copy : DragDropEffects.None;
        if (ViewModel != null)
        {
            ViewModel.IsDragOver = canUpload;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDragOver = false;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        vm.IsDragOver = false;
        e.Handled = true;
        if (!vm.CanUpload)
        {
            return;
        }

        var files = new List<MediaBrowserUploadSource>();
        foreach (IDataTransferItem item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(DataFormat.File) is IStorageFile file)
            {
                files.Add(ToUploadSource(file));
            }
        }

        await vm.UploadAsync(files);
    }

    // ---- selection, context menu, keyboard ----

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Right-click selects the row under the pointer before the context menu opens.
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed && ViewModel is { } vm &&
            (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is MediaBrowserItemViewModel item)
        {
            vm.SelectedItem = item;
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is MediaBrowserItemViewModel item)
        {
            ViewModel?.OpenCommand.Execute(item);
        }
    }

    private ContextMenu CreateItemMenu()
    {
        MenuItem open = MenuEntry("Open", LucideIcons.folder_open, () => ViewModel?.OpenCommand.Execute(null), "Enter");
        MenuItem preview = MenuEntry("Preview", LucideIcons.eye, () => ViewModel?.PreviewCommand.Execute(null), "Space");
        MenuItem download = MenuEntry("Download…", LucideIcons.download, () => _ = DownloadSelectedAsync());
        MenuItem copyUrl = MenuEntry("Copy URL", LucideIcons.link, () => ViewModel?.CopyUrlCommand.Execute(null));
        MenuItem openUrl = MenuEntry("Open URL", LucideIcons.external_link, () => ViewModel?.OpenUrlCommand.Execute(null));
        MenuItem rename = MenuEntry("Rename…", LucideIcons.pencil, () => ViewModel?.RenameCommand.Execute(null), "F2");
        MenuItem delete = MenuEntry("Delete…", LucideIcons.trash_2, () => ViewModel?.DeleteCommand.Execute(null), "Del");
        MenuItem newFolder = MenuEntry("New folder…", LucideIcons.folder_plus, () => ViewModel?.CreateFolderCommand.Execute(null));
        MenuItem upload = MenuEntry("Upload files…", LucideIcons.upload, () => OnUploadClick(null, new RoutedEventArgs()));
        var urlSeparator = new Separator();
        var editSeparator = new Separator();
        var folderSeparator = new Separator();

        var menu = new ContextMenu
        {
            Items = { open, preview, download, urlSeparator, copyUrl, openUrl, editSeparator, rename, delete, folderSeparator, newFolder, upload },
        };

        menu.Opening += (_, _) =>
        {
            MediaBrowserViewModel? vm = ViewModel;
            bool selected = vm?.HasSelection == true && !vm.IsBusy;
            open.IsVisible = selected;
            preview.IsVisible = vm?.CanPreview == true;
            download.IsVisible = vm?.CanDownload == true;
            copyUrl.IsVisible = openUrl.IsVisible = vm?.CanUseUrl == true;
            rename.IsVisible = vm?.CanRename == true;
            delete.IsVisible = vm?.CanDelete == true;
            newFolder.IsVisible = vm?.CanCreateFolder == true;
            upload.IsVisible = vm?.CanUpload == true;
            urlSeparator.IsVisible = copyUrl.IsVisible && (open.IsVisible || download.IsVisible);
            editSeparator.IsVisible = (rename.IsVisible || delete.IsVisible) && selected;
            folderSeparator.IsVisible = (newFolder.IsVisible || upload.IsVisible) && (selected || copyUrl.IsVisible);
        };

        return menu;
    }

    private static MenuItem MenuEntry(string header, string glyph, Action action, string? gesture = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = new TextBlock { Classes = { "glyph" }, Text = glyph },
            InputGesture = gesture is null ? null : KeyGesture.Parse(gesture == "Del" ? "Delete" : gesture),
        };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        bool inTextBox = e.Source is TextBox;
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        switch (e.Key)
        {
            case Key.F5:
                vm.RefreshCommand.Execute(null);
                break;
            case Key.F when ctrl:
                this.FindControl<TextBox>("SearchBox")?.Focus();
                break;
            case Key.Left when alt:
                vm.GoBackCommand.Execute(null);
                break;
            case Key.Right when alt:
                vm.GoForwardCommand.Execute(null);
                break;
            case Key.Escape when vm.IsPreviewOpen:
                vm.ClosePreviewCommand.Execute(null);
                break;
            case Key.Escape when inTextBox && vm.SearchText.Length > 0:
                vm.SearchText = string.Empty;
                break;
            case Key.Enter when !inTextBox && vm.SelectedItem != null:
                vm.OpenCommand.Execute(null);
                break;
            case Key.Space when !inTextBox && vm.CanPreview:
                if (vm.IsPreviewOpen) vm.ClosePreviewCommand.Execute(null); else vm.PreviewCommand.Execute(null);
                break;
            case Key.Back when !inTextBox:
                vm.GoUpCommand.Execute(null);
                break;
            case Key.F2 when !inTextBox && vm.CanRename:
                vm.RenameCommand.Execute(null);
                break;
            case Key.Delete when !inTextBox && vm.CanDelete:
                vm.DeleteCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
