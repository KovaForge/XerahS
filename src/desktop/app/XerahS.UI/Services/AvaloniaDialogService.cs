using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Views.Dialogs;
using ShareX.ImageEditor.Presentation.ViewModels;

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

        public Task<bool> ShowPluginInstallerAsync(PluginInstallerViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>();
            WireModalAwaiter(
                mainVm,
                viewModel,
                tcs,
                assignCloseCallback: set =>
                {
                    viewModel.RequestClose = result => set(result ?? false);
                },
                dismissResult: false);

            ModalOpenService.Open(mainVm, viewModel, nameof(PluginInstallerViewModel));
            return tcs.Task;
        }

        public Task<bool> ShowCustomUploaderEditorAsync(CustomUploaderEditorViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>();
            WireModalAwaiter(
                mainVm,
                viewModel,
                tcs,
                assignCloseCallback: set =>
                {
                    viewModel.CloseRequested = set;
                },
                dismissResult: false);

            ModalOpenService.Open(mainVm, viewModel, nameof(CustomUploaderEditorViewModel));
            return tcs.Task;
        }

        public Task<bool> ShowWorkflowEditorAsync(WorkflowEditorViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>();
            WireModalAwaiter(
                mainVm,
                viewModel,
                tcs,
                assignCloseCallback: set =>
                {
                    viewModel.CloseRequested = set;
                },
                dismissResult: false);

            ModalOpenService.Open(mainVm, viewModel, nameof(WorkflowEditorViewModel));
            return tcs.Task;
        }

        public Task ShowImageEffectsBrowserAsync(ImageEffectsViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.CompletedTask;

            viewModel.CloseRequested = _ =>
            {
                if (mainVm.ModalContent == viewModel)
                    mainVm.CloseModalCommand.Execute(null);
            };

            ModalOpenService.Open(mainVm, viewModel, nameof(ImageEffectsViewModel));
            return Task.CompletedTask;
        }

        public Task ShowFFmpegOptionsAsync(FFmpegOptionsViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.CompletedTask;

            viewModel.CloseRequested = result =>
            {
                if (mainVm.ModalContent == viewModel)
                    mainVm.CloseModalCommand.Execute(null);
            };

            ModalOpenService.Open(mainVm, viewModel, nameof(FFmpegOptionsViewModel));
            return Task.CompletedTask;
        }

        public Task ShowProviderExplorerAsync(ProviderExplorerViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.CompletedTask;

            viewModel.CloseRequested = _ =>
            {
                if (mainVm.ModalContent == viewModel)
                    mainVm.CloseModalCommand.Execute(null);
            };

            ModalOpenService.Open(mainVm, viewModel, nameof(ProviderExplorerViewModel));
            return Task.CompletedTask;
        }

        public Task ShowQrCodeGeneratorAsync(QrCodeGeneratorViewModel viewModel)
        {
            var mainVm = MainViewModel.Current;
            if (mainVm == null) return Task.CompletedTask;

            viewModel.CloseRequested = _ =>
            {
                if (mainVm.ModalContent == viewModel)
                    mainVm.CloseModalCommand.Execute(null);
            };

            ModalOpenService.Open(mainVm, viewModel, nameof(QrCodeGeneratorViewModel));
            return Task.CompletedTask;
        }


        /// <summary>
        /// Completes <paramref name="tcs"/> when the modal closes via Save/Cancel
        /// (assigned close callback) OR when the overlay is dismissed (backdrop /
        /// Escape / CloseModal) without invoking the VM close callback.
        /// </summary>
        private static void WireModalAwaiter<T>(
            MainViewModel mainVm,
            object viewModel,
            TaskCompletionSource<T> tcs,
            Action<Action<T>> assignCloseCallback,
            T dismissResult)
        {
            void Complete(T result)
            {
                mainVm.PropertyChanged -= OnModalPropertyChanged;
                if (mainVm.ModalContent == viewModel)
                    mainVm.CloseModalCommand.Execute(null);
                tcs.TrySetResult(result);
            }

            void OnModalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(MainViewModel.IsModalOpen) &&
                    !mainVm.IsModalOpen &&
                    !tcs.Task.IsCompleted)
                {
                    // Backdrop / Escape / CloseModal cleared the overlay without the VM callback.
                    mainVm.PropertyChanged -= OnModalPropertyChanged;
                    tcs.TrySetResult(dismissResult);
                }
            }

            assignCloseCallback(Complete);
            mainVm.PropertyChanged += OnModalPropertyChanged;
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
                return files.FirstOrDefault()?.TryGetLocalPath();
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
                return file?.TryGetLocalPath();
            }
            return null;
        }

        public async Task<string?> ShowSecretInputAsync(string title, string label)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                GetDialogOwner(desktop) is not { } owner)
            {
                return null;
            }

            string? result = null;
            var dialog = new SurfaceWindow
            {
                Title = title,
                Width = 420,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var textBox = new TextBox
            {
                PasswordChar = '*',
                PlaceholderText = label
            };
            var panel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 14
            };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 14 });
            panel.Children.Add(textBox);

            var buttonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8
            };
            var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(20, 8) };
            var okButton = new Button { Content = "OK", Padding = new Thickness(20, 8), IsDefault = true };
            cancelButton.Click += (_, _) => dialog.Close();
            okButton.Click += (_, _) =>
            {
                result = textBox.Text;
                dialog.Close();
            };
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(okButton);
            panel.Children.Add(buttonRow);
            dialog.Content = panel;

            await dialog.ShowDialog(owner);
            return result;
        }

        public async Task<string?> ShowFolderPickerAsync(string title)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
                var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(options);
                return folders.FirstOrDefault()?.TryGetLocalPath();
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

        private static Window CreateDialog<TWindow>(object dataContext) where TWindow : Window, new()
        {
            var dialog = new TWindow
            {
                DataContext = dataContext
            };

            return dialog;
        }

        private static void WireCloseRequest(PluginInstallerViewModel viewModel, Window dialog)
        {
            viewModel.RequestClose = result =>
            {
                dialog.Close(result ?? false);
            };
        }

        private static void WireCloseRequest(CustomUploaderEditorViewModel viewModel, Window dialog)
        {
            viewModel.CloseRequested = result =>
            {
                dialog.Close(result);
            };
        }

        private static async Task<TResult?> ShowDialogAsync<TResult>(Window dialog)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                GetDialogOwner(desktop) is { } owner)
            {
                return await dialog.ShowDialog<TResult>(owner);
            }

            var tcs = new TaskCompletionSource<TResult?>();
            dialog.Closed += (_, _) => tcs.TrySetResult(default);
            dialog.Show();
            return await tcs.Task;
        }

        private static Task ShowDialogAsync(Window dialog)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                GetDialogOwner(desktop) is { } owner)
            {
                return dialog.ShowDialog(owner);
            }

            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.TrySetResult();
            dialog.Show();
            return tcs.Task;
        }
    }
}
