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
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;
using XerahS.UI.Services;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.UI.ViewModels;

public enum MediaBrowserSortColumn
{
    Name,
    Size,
    Modified,
}

/// <summary>A path segment in the location bar.</summary>
public sealed record MediaBrowserCrumb(string Label, string Path);

/// <summary>A file the view hands over for upload (from the picker or a drop).</summary>
public sealed record MediaBrowserUploadSource(string FileName, Func<Task<Stream>> OpenReadAsync);

/// <summary>
/// The Media Browser: one window over every browsable destination. Ported from ShareX's remote
/// storage browser (capability-gated actions, upload/rename/delete, instant search and column
/// sorting, busy/cancel, status counts) and kept XerahS's strengths: thumbnail grid, paging and
/// back/forward history. Adds an inline image preview.
/// </summary>
public sealed partial class MediaBrowserViewModel : ViewModelBase, IDisposable
{
    private const int ThumbnailWidth = 180;
    private const int ThumbnailConcurrency = 4;
    private const long MaxThumbnailBytes = 10 * 1024 * 1024;
    private const long MaxPreviewBytes = 64 * 1024 * 1024;

    private readonly IDialogService _dialogs;
    private readonly List<MediaBrowserItemViewModel> _loaded = [];
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private string? _continuationToken;
    private CancellationTokenSource? _operationCts;
    private CancellationTokenSource? _thumbnailCts;
    private CancellationTokenSource? _previewCts;
    private int _loadGeneration;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpload), nameof(CanCreateFolder), nameof(CanRename), nameof(CanDelete),
        nameof(CanDownload), nameof(CanUseUrl), nameof(LocationText))]
    private MediaBrowserSource _selectedSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanRename), nameof(CanDelete), nameof(CanDownload),
        nameof(CanUseUrl), nameof(CanPreview), nameof(CanOpen))]
    private MediaBrowserItemViewModel? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoUp), nameof(LocationText))]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private MediaBrowserTypeFilter _typeFilter = MediaBrowserTypeFilter.All;

    [ObservableProperty]
    private bool _isGridView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(CanUpload), nameof(CanCreateFolder), nameof(CanRename), nameof(CanDelete),
        nameof(CanDownload), nameof(CanGoUp), nameof(CanGoBack), nameof(CanGoForward), nameof(CanOpen))]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasMoreItems;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewOpen))]
    private MediaBrowserItemViewModel? _previewItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private bool _isPreviewLoading;

    [ObservableProperty]
    private MediaBrowserSortColumn _sortColumn = MediaBrowserSortColumn.Name;

    [ObservableProperty]
    private bool _sortDescending;

    public MediaBrowserViewModel(IReadOnlyList<MediaBrowserSource> sources, IDialogService dialogs, MediaBrowserSource? initialSource = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one browsable destination is required.", nameof(sources));
        }

        Sources = sources;
        _dialogs = dialogs;
        _selectedSource = initialSource != null && sources.Contains(initialSource) ? initialSource : sources[0];
        IsGridView = _selectedSource.Has(ExplorerCapabilities.Thumbnails);
    }

    public IReadOnlyList<MediaBrowserSource> Sources { get; }
    public ObservableCollection<MediaBrowserItemViewModel> Items { get; } = [];
    public ObservableCollection<MediaBrowserCrumb> Breadcrumbs { get; } = [];
    public IReadOnlyList<MediaBrowserTypeFilter> TypeFilters { get; } = Enum.GetValues<MediaBrowserTypeFilter>();

    /// <summary>Closes the modal host.</summary>
    public Action<bool>? CloseRequested { get; set; }

    public bool HasMultipleSources => Sources.Count > 1;
    public bool IsEmpty => !IsBusy && !HasItems;
    public bool HasSelection => SelectedItem != null;
    public bool HasError => ErrorText.Length > 0;
    public bool IsPreviewOpen => PreviewItem != null;
    public bool HasPreviewImage => PreviewImage != null;

    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchText) && TypeFilter == MediaBrowserTypeFilter.All
        ? "This folder is empty"
        : "No items match the filter";

    public string LocationText => SelectedSource.DisplayName + " › /" + CurrentPath.Trim('/');

    public bool CanGoUp => !IsBusy && CurrentPath.Trim('/').Length > 0;
    public bool CanGoBack => !IsBusy && _historyIndex > 0;
    public bool CanGoForward => !IsBusy && _historyIndex < _history.Count - 1;
    public bool CanUpload => !IsBusy && SelectedSource.Has(ExplorerCapabilities.Upload);
    public bool CanCreateFolder => !IsBusy && SelectedSource.Explorer.SupportsFolders && SelectedSource.Has(ExplorerCapabilities.CreateFolder);
    public bool CanRename => !IsBusy && HasSelection && SelectedSource.Has(ExplorerCapabilities.Rename);
    public bool CanDelete => !IsBusy && HasSelection && SelectedSource.Has(ExplorerCapabilities.Delete);
    public bool CanDownload => !IsBusy && SelectedItem is { IsFolder: false } && SelectedSource.Has(ExplorerCapabilities.Download);
    public bool CanUseUrl => SelectedItem is { HasUrl: true } && SelectedSource.Has(ExplorerCapabilities.Url);
    public bool CanPreview => SelectedItem is { IsImage: true } && SelectedSource.Has(ExplorerCapabilities.Download);
    public bool CanOpen => !IsBusy && SelectedItem != null;

    public bool IsNameSorted => SortColumn == MediaBrowserSortColumn.Name;
    public bool IsSizeSorted => SortColumn == MediaBrowserSortColumn.Size;
    public bool IsModifiedSorted => SortColumn == MediaBrowserSortColumn.Modified;
    public string SortGlyph => SortDescending ? ShareX.ImageEditor.Presentation.Theming.LucideIcons.arrow_down : ShareX.ImageEditor.Presentation.Theming.LucideIcons.arrow_up;

    public Task InitializeAsync() => NavigateAsync(string.Empty, pushHistory: true);

    // =====================================================================================
    // Navigation
    // =====================================================================================

    partial void OnSelectedSourceChanged(MediaBrowserSource value)
    {
        _history.Clear();
        _historyIndex = -1;
        IsGridView = value.Has(ExplorerCapabilities.Thumbnails);
        ClosePreview();
        _ = NavigateAsync(string.Empty, pushHistory: true);
    }

    [RelayCommand]
    private Task NavigateTo(string? path) => NavigateAsync(path ?? string.Empty, pushHistory: true);

    [RelayCommand]
    private Task GoUp() => CanGoUp ? NavigateAsync(ParentOf(CurrentPath), pushHistory: true) : Task.CompletedTask;

    [RelayCommand]
    private Task GoBack()
    {
        if (!CanGoBack) return Task.CompletedTask;
        _historyIndex--;
        return NavigateAsync(_history[_historyIndex], pushHistory: false);
    }

    [RelayCommand]
    private Task GoForward()
    {
        if (!CanGoForward) return Task.CompletedTask;
        _historyIndex++;
        return NavigateAsync(_history[_historyIndex], pushHistory: false);
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(CurrentPath, append: false);

    [RelayCommand]
    private Task LoadMore() => HasMoreItems ? LoadAsync(CurrentPath, append: true) : Task.CompletedTask;

    [RelayCommand]
    private void ToggleView() => IsGridView = !IsGridView;

    /// <summary>Folders open; images preview; other files open with the default app.</summary>
    [RelayCommand]
    private async Task Open(MediaBrowserItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item == null || IsBusy)
        {
            return;
        }

        if (item.IsFolder)
        {
            await NavigateAsync(item.Item.Path, pushHistory: true);
        }
        else if (item.IsImage && SelectedSource.Has(ExplorerCapabilities.Download))
        {
            await ShowPreviewAsync(item);
        }
        else if (SelectedSource.Has(ExplorerCapabilities.Download))
        {
            await OpenWithDefaultAppAsync(item);
        }
    }

    internal async Task NavigateAsync(string path, bool pushHistory)
    {
        if (pushHistory)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            }

            if (_history.Count == 0 || _history[^1] != path)
            {
                _history.Add(path);
            }

            _historyIndex = _history.Count - 1;
        }

        bool folderChanged = !string.Equals(path, CurrentPath, StringComparison.Ordinal);
        CurrentPath = path;
        UpdateBreadcrumbs();
        if (folderChanged)
        {
            SearchText = string.Empty;
        }

        await LoadAsync(path, append: false);
    }

    // =====================================================================================
    // Loading, filtering and sorting
    // =====================================================================================

    private async Task LoadAsync(string path, bool append)
    {
        int generation = ++_loadGeneration;
        MediaBrowserSource source = SelectedSource;
        string? token = append ? _continuationToken : null;

        await RunAsync("Loading…", async cancellation =>
        {
            ExplorerPage page = await source.Explorer.ListAsync(new ExplorerQuery
            {
                SettingsJson = source.Instance.SettingsJson,
                FolderPath = path,
                PageSize = 200,
                ContinuationToken = token,
            }, cancellation);

            if (generation != _loadGeneration || !ReferenceEquals(source, SelectedSource))
            {
                return; // superseded by a newer navigation
            }

            if (!append)
            {
                _loaded.Clear();
            }

            _loaded.AddRange(page.Items.Where(item => !string.IsNullOrEmpty(item.Name)).Select(item => new MediaBrowserItemViewModel(item)));
            _continuationToken = page.ContinuationToken;
            HasMoreItems = !string.IsNullOrEmpty(page.ContinuationToken);
            ApplyView();
            UpdateStatus();
        }, clearError: true);

        if (generation == _loadGeneration)
        {
            StartThumbnails();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyView();
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnTypeFilterChanged(MediaBrowserTypeFilter value)
    {
        ApplyView();
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnIsGridViewChanged(bool value) => StartThumbnails();

    [RelayCommand]
    private void SortBy(MediaBrowserSortColumn column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = column == MediaBrowserSortColumn.Modified; // newest first feels natural
        }

        ApplyView();
        OnPropertyChanged(nameof(IsNameSorted));
        OnPropertyChanged(nameof(IsSizeSorted));
        OnPropertyChanged(nameof(IsModifiedSorted));
        OnPropertyChanged(nameof(SortGlyph));
    }

    private void ApplyView()
    {
        MediaBrowserItemViewModel? selected = SelectedItem;
        string search = SearchText.Trim();
        IEnumerable<MediaBrowserItemViewModel> visible = _loaded.Where(item => item.Matches(TypeFilter));
        if (search.Length > 0)
        {
            visible = visible.Where(item => item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }

        Items.Clear();
        foreach (MediaBrowserItemViewModel item in Sort(visible))
        {
            Items.Add(item);
        }

        HasItems = Items.Count > 0;
        SelectedItem = selected != null && Items.Contains(selected) ? selected : null;
    }

    internal IEnumerable<MediaBrowserItemViewModel> Sort(IEnumerable<MediaBrowserItemViewModel> items)
    {
        IOrderedEnumerable<MediaBrowserItemViewModel> sorted = items.OrderByDescending(item => item.IsFolder);
        sorted = SortColumn switch
        {
            MediaBrowserSortColumn.Size => SortDescending
                ? sorted.ThenByDescending(item => item.Item.SizeBytes)
                : sorted.ThenBy(item => item.Item.SizeBytes),
            MediaBrowserSortColumn.Modified => SortDescending
                ? sorted.ThenByDescending(item => item.Modified ?? DateTime.MinValue)
                : sorted.ThenBy(item => item.Modified ?? DateTime.MaxValue),
            _ => SortDescending
                ? sorted.ThenByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                : sorted.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        return sorted.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    private void UpdateStatus()
    {
        int files = _loaded.Count(item => !item.IsFolder);
        int folders = _loaded.Count - files;
        long bytes = _loaded.Where(item => !item.IsFolder).Sum(item => item.Item.SizeBytes);
        string more = HasMoreItems ? " (more available)" : string.Empty;
        StatusText = $"{files} file{(files == 1 ? "" : "s")}, {folders} folder{(folders == 1 ? "" : "s")} · {MediaBrowserItemViewModel.FormatBytes(bytes)}{more}";
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new MediaBrowserCrumb(SelectedSource.Instance.DisplayName is { Length: > 0 } name ? name : SelectedSource.ProviderName, string.Empty));
        string trailing = CurrentPath.EndsWith('/') ? "/" : string.Empty;
        string accumulated = string.Empty;
        foreach (string part in CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            accumulated = accumulated.Length == 0 ? part : accumulated + "/" + part;
            Breadcrumbs.Add(new MediaBrowserCrumb(part, accumulated + trailing));
        }
    }

    /// <summary>Parent folder path, keeping the provider's trailing-slash convention.</summary>
    internal static string ParentOf(string path)
    {
        string trimmed = path.TrimEnd('/');
        int separator = trimmed.LastIndexOf('/');
        if (separator < 0)
        {
            return string.Empty;
        }

        return trimmed[..separator] + (path.EndsWith('/') ? "/" : string.Empty);
    }

    // =====================================================================================
    // Operations
    // =====================================================================================

    public async Task UploadAsync(IReadOnlyList<MediaBrowserUploadSource> files)
    {
        if (files.Count == 0 || !CanUpload)
        {
            return;
        }

        MediaBrowserSource source = SelectedSource;
        string folder = CurrentPath;
        bool uploaded = await RunAsync("Uploading…", async cancellation =>
        {
            for (int i = 0; i < files.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                BusyText = files.Count == 1 ? $"Uploading {files[i].FileName}…" : $"Uploading {i + 1} of {files.Count}: {files[i].FileName}";
                await using Stream stream = await files[i].OpenReadAsync();
                if (!await source.Explorer.UploadAsync(source.Context, folder, files[i].FileName, stream, cancellation))
                {
                    throw new InvalidOperationException($"{source.ProviderName} did not accept {files[i].FileName}.");
                }
            }
        });

        if (uploaded)
        {
            await LoadAsync(folder, append: false);
            StatusText = files.Count == 1 ? $"Uploaded {files[0].FileName}" : $"Uploaded {files.Count} files";
        }
    }

    [RelayCommand]
    private async Task CreateFolder()
    {
        if (!CanCreateFolder)
        {
            return;
        }

        string? name = (await _dialogs.ShowInputAsync("New folder", "Folder name", "New folder"))?.Trim();
        if (string.IsNullOrEmpty(name) || !await ValidateNewNameAsync(name, except: null))
        {
            return;
        }

        MediaBrowserSource source = SelectedSource;
        string folder = CurrentPath;
        if (await RunAsync("Creating folder…", async cancellation =>
            {
                if (!await source.Explorer.CreateFolderAsync(source.Context, folder, name, cancellation))
                {
                    throw new InvalidOperationException($"{source.ProviderName} could not create the folder.");
                }
            }))
        {
            await LoadAsync(folder, append: false);
            StatusText = $"Created {name}";
        }
    }

    [RelayCommand]
    private async Task Rename(MediaBrowserItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item == null || !SelectedSource.Has(ExplorerCapabilities.Rename) || IsBusy)
        {
            return;
        }

        string? name = (await _dialogs.ShowInputAsync("Rename", "New name", item.Name))?.Trim();
        if (string.IsNullOrEmpty(name) || name == item.Name || !await ValidateNewNameAsync(name, except: item))
        {
            return;
        }

        MediaBrowserSource source = SelectedSource;
        string folder = CurrentPath;
        string oldName = item.Name;
        if (await RunAsync($"Renaming {oldName}…", async cancellation =>
            {
                if (!await source.Explorer.RenameAsync(source.Context, item.Item, name, cancellation))
                {
                    throw new InvalidOperationException($"{source.ProviderName} could not rename {oldName}.");
                }
            }))
        {
            await LoadAsync(folder, append: false);
            StatusText = $"Renamed {oldName} to {name}";
        }
    }

    [RelayCommand]
    private async Task Delete(MediaBrowserItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item == null || !SelectedSource.Has(ExplorerCapabilities.Delete) || IsBusy)
        {
            return;
        }

        string what = item.IsFolder ? "folder" : "file";
        if (!await _dialogs.ShowConfirmationAsync($"Delete {what}", $"Delete '{item.Name}' from {SelectedSource.DisplayName}?\n\nThis permanently removes it from the remote storage."))
        {
            return;
        }

        MediaBrowserSource source = SelectedSource;
        if (item.IsFolder)
        {
            bool? hasChildren = null;
            await RunAsync("Checking folder…", async cancellation =>
                hasChildren = await source.Explorer.HasChildrenAsync(source.Context, item.Item, cancellation));

            if (hasChildren != false &&
                !await _dialogs.ShowConfirmationAsync("Delete non-empty folder",
                    hasChildren == true
                        ? $"'{item.Name}' is not empty. Delete it and everything inside it?"
                        : $"'{item.Name}' may not be empty. Delete it and everything inside it?"))
            {
                return;
            }
        }

        string folder = CurrentPath;
        string name = item.Name;
        if (await RunAsync($"Deleting {name}…", async cancellation =>
            {
                if (!await source.Explorer.DeleteAsync(source.Context, item.Item, cancellation))
                {
                    throw new InvalidOperationException($"{source.ProviderName} could not delete {name}.");
                }
            }))
        {
            if (ReferenceEquals(PreviewItem, item))
            {
                ClosePreview();
            }

            await LoadAsync(folder, append: false);
            StatusText = $"Deleted {name}";
        }
    }

    /// <summary>Downloads into <paramref name="destination"/> (the view supplies a save-picker stream).</summary>
    public async Task<bool> DownloadAsync(MediaBrowserItemViewModel item, Stream destination)
    {
        MediaBrowserSource source = SelectedSource;
        bool ok = await RunAsync($"Downloading {item.Name}…", async cancellation =>
        {
            await using Stream content = await source.Explorer.GetContentAsync(item.Item, cancellation)
                ?? throw new InvalidOperationException($"{source.ProviderName} returned no content for {item.Name}.");
            await content.CopyToAsync(destination, cancellation);
        });

        if (ok)
        {
            StatusText = $"Downloaded {item.Name}";
        }

        return ok;
    }

    private async Task OpenWithDefaultAppAsync(MediaBrowserItemViewModel item)
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS", "media-browser");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N")[..8] + "-" + SafeFileName(item.Name));
        bool ok;
        await using (FileStream file = File.Create(path))
        {
            ok = await DownloadAsync(item, file);
        }

        if (ok && PlatformServices.IsInitialized)
        {
            PlatformServices.System.OpenFile(path);
        }
        else if (!ok)
        {
            TryDelete(path);
        }
    }

    [RelayCommand]
    private async Task CopyUrl(MediaBrowserItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item?.Item.Url is { Length: > 0 } url && PlatformServices.IsInitialized)
        {
            await PlatformServices.Clipboard.SetTextAsync(url);
            StatusText = "URL copied";
        }
    }

    [RelayCommand]
    private void OpenUrl(MediaBrowserItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item?.Item.Url is { Length: > 0 } url && PlatformServices.IsInitialized)
        {
            PlatformServices.System.OpenUrl(url);
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(true);

    [RelayCommand]
    private void CancelOperation() => _operationCts?.Cancel();

    [RelayCommand]
    private void DismissError() => ErrorText = string.Empty;

    private async Task<bool> ValidateNewNameAsync(string name, MediaBrowserItemViewModel? except)
    {
        if (name is "." or ".." || name.Contains('/') || name.Contains('\\'))
        {
            await _dialogs.ShowErrorAsync("Invalid name", $"'{name}' is not a valid name.");
            return false;
        }

        if (_loaded.Any(item => !ReferenceEquals(item, except) && item.Name.Equals(name, StringComparison.Ordinal)))
        {
            await _dialogs.ShowErrorAsync("Name already exists", $"'{name}' already exists in this folder.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Runs one operation at a time with a busy overlay and cancellation. Failures surface in the
    /// error banner with the provider's message; returns true on success.
    /// </summary>
    private async Task<bool> RunAsync(string busyText, Func<CancellationToken, Task> operation, bool clearError = false)
    {
        if (_disposed)
        {
            return false;
        }

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        var cts = new CancellationTokenSource();
        _operationCts = cts;
        BusyText = busyText;
        IsBusy = true;
        if (clearError)
        {
            ErrorText = string.Empty;
        }

        try
        {
            await operation(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Media Browser: {busyText.TrimEnd('…')} failed");
            ErrorText = ex.Message;
            return false;
        }
        finally
        {
            if (ReferenceEquals(_operationCts, cts))
            {
                IsBusy = false;
                BusyText = string.Empty;
            }
        }
    }

    // =====================================================================================
    // Thumbnails and preview
    // =====================================================================================

    private void StartThumbnails()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        if (!SelectedSource.Has(ExplorerCapabilities.Thumbnails))
        {
            return;
        }

        List<MediaBrowserItemViewModel> pending = _loaded
            .Where(item => item.IsImage && item.Thumbnail == null && item.Item.SizeBytes <= MaxThumbnailBytes)
            .ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _thumbnailCts = cts;
        IUploaderExplorer explorer = SelectedSource.Explorer;
        _ = LoadThumbnailsAsync(explorer, pending, cts.Token);
    }

    private static async Task LoadThumbnailsAsync(IUploaderExplorer explorer, List<MediaBrowserItemViewModel> items, CancellationToken cancellation)
    {
        using var gate = new SemaphoreSlim(ThumbnailConcurrency);
        IEnumerable<Task> jobs = items.Select(async item =>
        {
            await gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                byte[]? bytes = await explorer.GetThumbnailAsync(item.Item, ThumbnailWidth, cancellation).ConfigureAwait(false);
                if (bytes is { Length: > 0 } && !cancellation.IsCancellationRequested)
                {
                    using var stream = new MemoryStream(bytes);
                    Bitmap bitmap = Bitmap.DecodeToWidth(stream, ThumbnailWidth);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = bitmap);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Media Browser: thumbnail for {item.Name} failed: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        });

        try
        {
            await Task.WhenAll(jobs).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private Task Preview(MediaBrowserItemViewModel? item) => ShowPreviewAsync(item ?? SelectedItem);

    private async Task ShowPreviewAsync(MediaBrowserItemViewModel? item)
    {
        if (item is not { IsImage: true })
        {
            return;
        }

        _previewCts?.Cancel();
        _previewCts?.Dispose();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        PreviewItem = item;
        PreviewImage = item.Thumbnail; // instant low-res while the full image loads
        IsPreviewLoading = true;

        try
        {
            if (item.Item.SizeBytes > MaxPreviewBytes)
            {
                return;
            }

            await using Stream? content = await SelectedSource.Explorer.GetContentAsync(item.Item, cts.Token);
            if (content == null || cts.IsCancellationRequested)
            {
                return;
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cts.Token);
            buffer.Position = 0;
            Bitmap full = Bitmap.DecodeToWidth(buffer, 1600);
            if (!cts.IsCancellationRequested && ReferenceEquals(PreviewItem, item))
            {
                PreviewImage = full;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Media Browser: preview of {item.Name} failed: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_previewCts, cts))
            {
                IsPreviewLoading = false;
            }
        }
    }

    [RelayCommand]
    private void ClosePreview()
    {
        _previewCts?.Cancel();
        PreviewItem = null;
        PreviewImage = null;
        IsPreviewLoading = false;
    }

    partial void OnSelectedItemChanged(MediaBrowserItemViewModel? value)
    {
        // Keep an open preview in step with the selection, like a file manager's preview pane.
        if (IsPreviewOpen && value is { IsImage: true } && !ReferenceEquals(value, PreviewItem))
        {
            _ = ShowPreviewAsync(value);
        }
    }

    internal static string SafeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "download" : safe;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _previewCts?.Cancel();
        _previewCts?.Dispose();
    }
}
