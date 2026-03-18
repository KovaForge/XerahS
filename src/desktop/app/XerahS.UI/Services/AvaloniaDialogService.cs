using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace XerahS.UI.Services
{
    public class AvaloniaDialogService : IViewDialogService
    {
        public async Task ShowDialogAsync<TWindow>(object dataContext) where TWindow : class, new()
        {
            if (new TWindow() is not Window window)
            {
                throw new InvalidOperationException($"Type {typeof(TWindow).Name} must inherit from Avalonia.Controls.Window");
            }

            window.DataContext = dataContext;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                GetDialogOwner(desktop) is { } owner)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                var completionSource = new TaskCompletionSource();
                window.Closed += (_, _) => completionSource.TrySetResult();
                window.Show();
                await completionSource.Task;
            }
        }

        public async Task<TResult?> ShowDialogAsync<TWindow, TResult>(object dataContext) where TWindow : class, new()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                GetDialogOwner(desktop) is { } owner)
            {
                var window = new TWindow() as Window;
                if (window != null)
                {
                    window.DataContext = dataContext;
                    return await window.ShowDialog<TResult>(owner);
                }
            }
            return default;
        }

        private static Window? GetDialogOwner(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prefer the currently active visible window so modal dialogs are not hidden
            // behind another tool/settings window (most noticeable on Linux WMs).
            return desktop.Windows.FirstOrDefault(window => window.IsVisible && window.IsActive)
                ?? desktop.Windows.LastOrDefault(window => window.IsVisible)
                ?? desktop.MainWindow;
        }

        public async Task<string?> ShowFilePickerAsync(string title, IEnumerable<string>? filters = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };
                if (filters != null)
                {
                    options.FileTypeFilter = new[] { new FilePickerFileType("Files") { Patterns = filters.ToList() } };
                }
                var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(options);
                return files.FirstOrDefault()?.Path.LocalPath;
            }
            return null;
        }

        public async Task<string?> ShowSaveFilePickerAsync(string title, string suggestedFileName, string defaultExtension, IEnumerable<string>? filters = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = suggestedFileName,
                    DefaultExtension = defaultExtension
                };
                if (filters != null)
                {
                    options.FileTypeChoices = new[] { new FilePickerFileType("Files") { Patterns = filters.ToList() } };
                }
                var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(options);
                return file?.Path.LocalPath;
            }
            return null;
        }

        public async Task<string?> ShowFolderPickerAsync(string title)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
                var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(options);
                return folders.FirstOrDefault()?.Path.LocalPath;
            }
            return null;
        }

        public object? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public IEnumerable<object> GetOpenWindows()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows;
            }
            return Enumerable.Empty<object>();
        }
    }
}
