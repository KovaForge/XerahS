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
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using XerahS.Common;
using XerahS.Common.Converters;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.History;
using XerahS.Media;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace XerahS.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase, IDisposable
    {
        private static readonly HashSet<string> CombinableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".webp",
            ".ico",
            ".tif",
            ".tiff"
        };

        // Converter for view toggle button text
        public static IValueConverter ViewToggleConverter { get; } = new FuncValueConverter<bool, string>(
            isGrid => isGrid ? "📋 List View" : "🔲 Grid View");

        // Converter to load thumbnail from file path (resource-efficient)
        public static IValueConverter ThumbnailConverter { get; } = new FuncValueConverter<string?, Bitmap?>(
            filePath =>
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                try
                {
                    // Check if it's an image file
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".bmp" && ext != ".webp")
                        return null;

                    // Load with decode size for memory efficiency (thumbnail size)
                    using var stream = File.OpenRead(filePath);
                    return Bitmap.DecodeToWidth(stream, 180); // Decode to thumbnail width
                }
                catch
                {
                    return null;
                }
            });

        [ObservableProperty]
        private ObservableCollection<HistoryItem> _historyItems;

        [ObservableProperty]
        private bool _isGridView = true;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isLoadingThumbnails = false;

        [ObservableProperty]
        private bool _isCombiningSelection = false;

        public ObservableCollection<HistoryItem> SelectedHistoryItems { get; } = new();

        public bool CanCombineSelectedImages => GetSelectedCombinableHistoryItems().Count >= 2;

        public bool ShowCombineActions => CanCombineSelectedImages;

        public int SelectedImageCount => GetSelectedCombinableHistoryItems().Count;

        public string CombineSelectionSummary => SelectedImageCount == 1
            ? "1 image selected"
            : $"{SelectedImageCount} images selected";



        // Pagination Properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalPages = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalItems = 0;

        [ObservableProperty]
        private int _pageSize = 50;

        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrevious => CurrentPage > 1;

        public string PageInfo => $"Page {CurrentPage} of {Math.Max(1, TotalPages)} ({TotalItems} items)"; // Prevent "Page 1 of 0" looking weird

        private readonly HistoryManagerSQLite _historyManager;
        private CancellationTokenSource? _thumbnailCancellationTokenSource;
        private readonly IViewDialogService _dialogService;

        public HistoryViewModel()
        {
            _dialogService = PlatformServices.RootProvider?.GetService(typeof(IViewDialogService)) as IViewDialogService ?? new AvaloniaDialogService();
            HistoryItems = new ObservableCollection<HistoryItem>();
            SelectedHistoryItems.CollectionChanged += (_, _) => NotifySelectionStateChanged();

            // Create history manager with centralized path
            var historyPath = SettingsManager.GetHistoryFilePath();
            DebugHelper.WriteLine($"HistoryViewModel - History file path: {historyPath}");

            _historyManager = new HistoryManagerSQLite(historyPath);

            // Configure backup settings similar to JSON files
            _historyManager.BackupFolder = SettingsManager.HistoryBackupFolder;
            _historyManager.CreateBackup = true;
            _historyManager.CreateWeeklyBackup = true;

            // Start loading history asynchronously WITHOUT blocking UI
            // Use fire-and-forget to let view display immediately
            _ = BeginHistoryLoadAsync();
        }

        /// <summary>
        /// Starts history loading asynchronously without blocking the UI thread.
        /// This allows the empty panel to display immediately.
        /// </summary>
        private async Task BeginHistoryLoadAsync()
        {
            // Small delay to allow UI to render the empty history view first
            await Task.Delay(100);
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsLoading) return;

            IsLoading = true;


            try
            {
                var historyPath = SettingsManager.GetHistoryFilePath();
                DebugHelper.WriteLine($"History.xml location: {historyPath} (exists={File.Exists(historyPath)})");

                // calculating offset
                int offset = (CurrentPage - 1) * PageSize;

                // Load total count first
                TotalItems = await _historyManager.GetTotalCountAsync();
                TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
                if (TotalPages == 0) TotalPages = 1; // Ensure at least 1 page even if empty

                // Adjust CurrentPage if out of bounds (e.g. after deletion)
                if (CurrentPage > TotalPages) CurrentPage = TotalPages;
                if (CurrentPage < 1) CurrentPage = 1;

                // Load paged history on background thread
                var items = await _historyManager.GetHistoryItemsAsync(offset, PageSize);

                // Clear and populate on UI thread
                ClearSelectedHistoryItems();
                HistoryItems.Clear();
                foreach (var item in items)
                {
                    HistoryItems.Add(item);
                }

                DebugHelper.WriteLine($"History loaded: {items.Count} items (Page {CurrentPage}/{TotalPages})");

                // Start loading thumbnails in background after history is displayed
                if (HistoryItems.Count > 0)
                {
                    _ = LoadThumbnailsInBackgroundAsync();
                }
            }

            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load history");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task NextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task PreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                await LoadHistoryAsync();
            }
        }

        /// <summary>
        /// Loads thumbnails asynchronously on a background thread.
        /// This allows history items to display immediately while thumbnails load gradually.
        /// </summary>
        private async Task LoadThumbnailsInBackgroundAsync()
        {
            // Cancel any previous thumbnail loading
            _thumbnailCancellationTokenSource?.Cancel();
            _thumbnailCancellationTokenSource = new CancellationTokenSource();

            IsLoadingThumbnails = true;
            try
            {
                await Task.Run(() =>
                {
                    int loadedCount = 0;
                    foreach (var item in HistoryItems)
                    {
                        // Check cancellation token
                        _thumbnailCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // Pre-load thumbnail by accessing the converter
                        // This forces the thumbnail to be cached for faster display
                        if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                        {
                            try
                            {
                                var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
                                {
                                    using var stream = File.OpenRead(item.FilePath);
                                    _ = Bitmap.DecodeToWidth(stream, 180);
                                    loadedCount++;
                                }
                            }
                            catch
                            {
                                // Silently skip thumbnails that fail to load
                            }
                        }

                        // Add small delay to prevent CPU saturation
                        if (loadedCount % 5 == 0)
                        {
                            System.Threading.Thread.Sleep(50);
                        }
                    }

                    DebugHelper.WriteLine($"Thumbnails pre-loaded: {loadedCount} images");
                }, _thumbnailCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                DebugHelper.WriteLine("Thumbnail loading was cancelled");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Error while loading thumbnails");
            }
            finally
            {
                IsLoadingThumbnails = false;
            }
        }

        [RelayCommand]
        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        [RelayCommand]
        private async Task RefreshHistory()
        {
            // Cancel any ongoing thumbnail loading
            _thumbnailCancellationTokenSource?.Cancel();
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private Task CombineHorizontalAsync()
        {
            return CombineSelectedImagesAsync(ImageCombinerOrientation.Horizontal);
        }

        [RelayCommand]
        private Task CombineVerticalAsync()
        {
            return CombineSelectedImagesAsync(ImageCombinerOrientation.Vertical);
        }

        [RelayCommand]
        private async Task EditImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                // Load the image from file directly as SKBitmap
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
                var skBitmap = SKBitmap.Decode(fs);
                if (skBitmap == null) return;

                // Open in Editor using the platform service
                await XerahS.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(skBitmap);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open image in editor: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFile(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                XerahS.Platform.Abstractions.PlatformServices.System.OpenFile(item.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open file: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFolder(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;

            XerahS.Platform.Abstractions.PlatformServices.System.ShowFileInExplorer(item.FilePath);
        }

        [RelayCommand]
        private async Task UploadItem(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            {
                return;
            }

            if (!File.Exists(item.FilePath))
            {
                DebugHelper.WriteLine($"HistoryViewModel - Upload skipped, file not found: {item.FilePath}");
                return;
            }

            try
            {
                var settings = GetUploadTaskSettings();
                settings.Job = WorkflowType.FileUpload;
                await TaskManager.Instance.StartFileTask(settings, item.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "HistoryViewModel - UploadItem failed");
            }
        }

        [RelayCommand]
        private async Task CopyFilePath(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;

            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(item.FilePath);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy file path: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CopyURL(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(item.URL);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy URL: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CopyMarkdownImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            var markdownImage = $"[img]{item.URL}[/img]";
            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(markdownImage);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy markdown image: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyImageToClipboard(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath)) return;

            try
            {
                using var bitmap = SKBitmap.Decode(item.FilePath);
                if (bitmap != null)
                {
                    PlatformServices.Clipboard.SetImage(bitmap);
                    DebugHelper.WriteLine($"Copied image to clipboard: {item.FilePath}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy image to clipboard from history");
            }
        }

        [RelayCommand]
        private async Task CopyErrors(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.Errors)) return;

            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(item.Errors);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy errors: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenURL(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            try
            {
                XerahS.Platform.Abstractions.PlatformServices.System.OpenUrl(item.URL);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteItem(HistoryItem? item)
        {
            if (item == null) return;

            // Show confirmation dialog
            var confirmDelete = await ShowDeleteConfirmationDialog(item.FileName);
            if (!confirmDelete) return;

            // Remove from the observable collection (UI update)
            HistoryItems.Remove(item);

            // Persist deletion to database
            _historyManager.Delete(item);
            DebugHelper.WriteLine($"Deleted history item: {item.FileName}");
        }

        private async Task<bool> ShowDeleteConfirmationDialog(string fileName)
        {
            var result = false;

            var confirmDialog = new Avalonia.Controls.Window
            {
                Title = "Confirm Delete",
                Width = 400,
                Height = 180,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var messageText = new Avalonia.Controls.TextBlock
            {
                Text = $"Are you sure you want to delete '{fileName}' from history?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 360,
                FontSize = 14
            };

            var warningText = new Avalonia.Controls.TextBlock
            {
                Text = "This action cannot be undone.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 360,
                FontSize = 12,
                Foreground = Avalonia.Media.Brushes.Orange,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };

            var buttonPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            var deleteButton = new Avalonia.Controls.Button
            {
                Content = "Delete",
                Padding = new Avalonia.Thickness(24, 8),
                Background = Avalonia.Media.Brushes.Red,
                Foreground = Avalonia.Media.Brushes.White
            };

            var cancelButton = new Avalonia.Controls.Button
            {
                Content = "Cancel",
                Padding = new Avalonia.Thickness(24, 8),
                IsDefault = true
            };

            deleteButton.Click += (s, e) =>
            {
                result = true;
                confirmDialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                result = false;
                confirmDialog.Close();
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(deleteButton);

            panel.Children.Add(messageText);
            panel.Children.Add(warningText);
            panel.Children.Add(buttonPanel);

            confirmDialog.Content = panel;

            // Get the main window as the owner
            if (_dialogService.GetMainWindow() is Avalonia.Controls.Window mainWindow)
            {
                await confirmDialog.ShowDialog(mainWindow);
            }
            else
            {
                // Fallback: show as independent window
                confirmDialog.Show();
                // Wait for close via event
                var closeTcs = new TaskCompletionSource<bool>();
                confirmDialog.Closed += (s, e) => closeTcs.TrySetResult(true);
                await closeTcs.Task;
            }

            return result;
        }

        private TaskSettings GetUploadTaskSettings()
        {
            var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
            if (uploadWorkflow?.TaskSettings != null)
            {
                var settings = CloneTaskSettings(uploadWorkflow.TaskSettings);
                settings.WorkflowId = uploadWorkflow.Id;
                return settings;
            }

            return CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
        }

        private static TaskSettings CloneTaskSettings(TaskSettings source)
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter(),
                    new SkColorJsonConverter()
                }
            };

            var json = JsonConvert.SerializeObject(source, jsonSettings);
            return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
        }

        private async Task CombineSelectedImagesAsync(ImageCombinerOrientation orientation)
        {
            if (IsCombiningSelection)
            {
                return;
            }

            var selectedItems = GetSelectedCombinableHistoryItems();
            if (selectedItems.Count < 2)
            {
                return;
            }

            IsCombiningSelection = true;

            try
            {
                var combinedHistoryItem = await Task.Run(() => CreateCombinedHistoryItem(selectedItems, orientation));
                if (combinedHistoryItem == null)
                {
                    return;
                }

                if (!_historyManager.AppendHistoryItem(combinedHistoryItem))
                {
                    DebugHelper.WriteLine($"HistoryViewModel - Failed to append combined history item: {combinedHistoryItem.FilePath}");
                    return;
                }

                ClearSelectedHistoryItems();
                CurrentPage = 1;
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "HistoryViewModel - CombineSelectedImages failed");
            }
            finally
            {
                IsCombiningSelection = false;
            }
        }

        private static HistoryItem? CreateCombinedHistoryItem(IReadOnlyList<HistoryItem> selectedItems, ImageCombinerOrientation orientation)
        {
            var filePaths = selectedItems
                .Select(item => item.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            if (filePaths.Count < 2)
            {
                return null;
            }

            var bitmaps = new List<SKBitmap>();

            try
            {
                foreach (var filePath in filePaths)
                {
                    var bitmap = ImageHelpers.LoadBitmap(filePath);
                    if (bitmap != null)
                    {
                        bitmaps.Add(bitmap);
                    }
                }

                if (bitmaps.Count < 2)
                {
                    return null;
                }

                var combiner = new ImageCombiner();
                using var combinedBitmap = combiner.Combine(bitmaps, orientation);
                if (combinedBitmap == null)
                {
                    return null;
                }

                var outputFolder = TaskHelpers.GetScreenshotsFolder();
                FileHelpers.CreateDirectory(outputFolder);

                var outputPath = FileHelpers.GetUniqueFilePath(
                    Path.Combine(outputFolder, $"Combined_{DateTime.Now:yyyyMMdd_HHmmss}.png"));

                ImageHelpers.SaveBitmap(combinedBitmap, outputPath);

                return new HistoryItem
                {
                    FilePath = outputPath,
                    FileName = Path.GetFileName(outputPath),
                    DateTime = DateTime.Now,
                    Type = "Image"
                };
            }
            finally
            {
                foreach (var bitmap in bitmaps)
                {
                    bitmap.Dispose();
                }
            }
        }

        public void SetSelectedHistoryItems(IEnumerable<HistoryItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var selectedSet = new HashSet<HistoryItem>(items);

            SelectedHistoryItems.Clear();

            foreach (var item in HistoryItems)
            {
                if (selectedSet.Contains(item))
                {
                    SelectedHistoryItems.Add(item);
                }
            }
        }

        public void ClearSelectedHistoryItems()
        {
            if (SelectedHistoryItems.Count == 0)
            {
                return;
            }

            SelectedHistoryItems.Clear();
        }

        private void NotifySelectionStateChanged()
        {
            OnPropertyChanged(nameof(CanCombineSelectedImages));
            OnPropertyChanged(nameof(ShowCombineActions));
            OnPropertyChanged(nameof(SelectedImageCount));
            OnPropertyChanged(nameof(CombineSelectionSummary));
        }

        private List<HistoryItem> GetSelectedCombinableHistoryItems()
        {
            return SelectedHistoryItems
                .Where(CanCombineHistoryItem)
                .ToList();
        }

        private static bool CanCombineHistoryItem(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
            {
                return false;
            }

            var extension = Path.GetExtension(item.FilePath);
            return !string.IsNullOrWhiteSpace(extension) && CombinableImageExtensions.Contains(extension);
        }

        public void Dispose()
        {
            _thumbnailCancellationTokenSource?.Cancel();
            _thumbnailCancellationTokenSource?.Dispose();
            _historyManager?.Dispose();
        }
    }
}
