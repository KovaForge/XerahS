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

using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

/// <summary>
/// ViewModel for the toast notification window
/// </summary>
public partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly ToastConfig _config;
    private readonly IDesktopTaskManager? _taskManager;
    private readonly DispatcherTimer _durationTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly int _fadeInterval = 50;
    private double _opacity = 1.0;
    private double _opacityDecrement;
    private bool _isDurationEnd;
    private bool _isMouseInside;
    private bool _isMenuOpen;
    private bool _disposed;

    public event EventHandler? CloseRequested;
    public event EventHandler<double>? OpacityChanged;

    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _url;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _hasUrl;

    [ObservableProperty]
    private string? _headerText;

    [ObservableProperty]
    private bool _hasHeaderText;

    [ObservableProperty]
    private string? _errorDetails;

    [ObservableProperty]
    private bool _hasErrors;

    // Commands for context menu (shared with History - same MenuFlyout)
    public ICommand EditImageCommand { get; }
    public ICommand CopyImageToClipboardCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand UploadItemCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CopyFilePathCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand CopyMarkdownImageCommand { get; }
    public ICommand CopyErrorsCommand { get; }
    public ICommand OpenURLCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public bool CanCopyImage => !string.IsNullOrWhiteSpace(_config.FilePath) && File.Exists(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath);
    internal string? FilePath => _config.FilePath;
    internal bool HasExistingFile => !string.IsNullOrWhiteSpace(_config.FilePath) && File.Exists(_config.FilePath);

    public ToastViewModel(ToastConfig config, IDesktopTaskManager? taskManager = null)
    {
        _config = config;
        _taskManager = taskManager;

        // Try to load image from path
        if (!string.IsNullOrEmpty(config.ImagePath) && File.Exists(config.ImagePath))
        {
            try
            {
                Image = new Bitmap(config.ImagePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load toast image");
            }
        }

        // Initialize display properties
        Text = config.Text;
        Title = config.Title;
        Url = config.URL;
        HasImage = Image != null;
        HasUrl = !string.IsNullOrEmpty(config.URL);
        HeaderText = !string.IsNullOrWhiteSpace(config.URL) ? config.URL : config.FilePath;
        HasHeaderText = !string.IsNullOrWhiteSpace(HeaderText);
        ErrorDetails = config.ErrorDetails;
        HasErrors = !string.IsNullOrWhiteSpace(config.ErrorDetails);

        // Initialize context menu commands (same set as History for shared ContextFlyout)
        EditImageCommand = new RelayCommand(AnnotateMedia);
        CopyImageToClipboardCommand = new RelayCommand(CopyImageToClipboard);
        OpenFileCommand = new RelayCommand(OpenFile);
        UploadItemCommand = new AsyncRelayCommand(UploadFileAsync);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        CopyFilePathCommand = new RelayCommand(CopyFilePath);
        CopyUrlCommand = new RelayCommand(CopyUrl);
        CopyMarkdownImageCommand = new RelayCommand(CopyMarkdownImage);
        CopyErrorsCommand = new RelayCommand(CopyErrors);
        OpenURLCommand = new RelayCommand(OpenUrl);
        DeleteItemCommand = new RelayCommand(DeleteFile);

        // Calculate fade decrement
        if (config.FadeDuration > 0)
        {
            _opacityDecrement = (double)_fadeInterval / (config.FadeDuration * 1000);
        }

        // Setup duration timer
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(config.Duration)
        };
        _durationTimer.Tick += OnDurationTick;

        // Setup fade timer
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_fadeInterval)
        };
        _fadeTimer.Tick += OnFadeTick;

        // Start duration timer if auto-hide is enabled. A zero display duration means the
        // toast should begin fading immediately instead of staying visible forever.
        switch (GetAutoHideStartMode(config))
        {
            case ToastAutoHideStartMode.WaitForDuration:
                _durationTimer.Start();
                break;
            case ToastAutoHideStartMode.StartFade:
                _isDurationEnd = true;
                CheckFade();
                break;
        }
    }

    internal static ToastAutoHideStartMode GetAutoHideStartMode(ToastConfig config)
    {
        if (!config.AutoHide)
        {
            return ToastAutoHideStartMode.None;
        }

        return config.Duration > 0
            ? ToastAutoHideStartMode.WaitForDuration
            : ToastAutoHideStartMode.StartFade;
    }

    internal enum ToastAutoHideStartMode
    {
        None,
        WaitForDuration,
        StartFade
    }

    public void OnMenuOpened()
    {
        _isMenuOpen = true;
        _fadeTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnMenuClosed()
    {
        _isMenuOpen = false;
        CheckFade();
    }

    public void OnMouseEnter()
    {
        _isMouseInside = true;
        _fadeTimer.Stop();

        // Reset opacity
        _opacity = 1.0;
        OpacityChanged?.Invoke(this, _opacity);
    }

    public void OnMouseLeave()
    {
        _isMouseInside = false;
        CheckFade();
    }

    public void ExecuteLeftClick()
    {
        ExecuteAction(_config.LeftClickAction);
    }

    public void ExecuteRightClick()
    {
        // Right click opens context menu, handled by view
    }

    public void ExecuteMiddleClick()
    {
        ExecuteAction(_config.MiddleClickAction);
    }

    private void OnDurationTick(object? sender, EventArgs e)
    {
        _durationTimer.Stop();
        _isDurationEnd = true;

        if (!_isMouseInside)
        {
            CheckFade();
        }
    }

    private void CheckFade()
    {
        if (_isDurationEnd && _config.AutoHide && !_isMouseInside && !_isMenuOpen)
        {
            StartFade();
        }
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        if (_opacity > _opacityDecrement)
        {
            _opacity -= _opacityDecrement;
            OpacityChanged?.Invoke(this, _opacity);
        }
        else
        {
            _fadeTimer.Stop();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StartFade()
    {
        if (_config.FadeDuration <= 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _opacity = 1.0;
            OpacityChanged?.Invoke(this, _opacity);
            _fadeTimer.Start();
        }
    }

    private void ExecuteAction(ToastClickAction action)
    {
        _durationTimer.Stop();
        _fadeTimer.Stop();

        switch (action)
        {
            case ToastClickAction.OpenFile:
                OpenFile();
                break;

            case ToastClickAction.OpenFolder:
                OpenFolder();
                break;

            case ToastClickAction.OpenUrl:
                OpenUrl();
                break;

            case ToastClickAction.CopyImageToClipboard:
                CopyImageToClipboard();
                break;

            case ToastClickAction.CopyFile:
                CopyFile();
                break;

            case ToastClickAction.CopyFilePath:
                CopyFilePath();
                break;

            case ToastClickAction.CopyUrl:
                CopyUrl();
                break;

            case ToastClickAction.AnnotateMedia:
                AnnotateMedia();
                break;

            case ToastClickAction.Upload:
                _ = UploadFileAsync();
                break;

            case ToastClickAction.PinToScreen:
                PinToScreen();
                break;

            case ToastClickAction.DeleteFile:
                DeleteFile();
                break;

            case ToastClickAction.CloseNotification:
            default:
                break;
        }

        // Close after action (unless it's a no-op close action that already closes)
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_config.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open file from toast");
            }
        }
    }

    private void OpenFolder()
    {
        if (!string.IsNullOrEmpty(_config.FilePath))
        {
            try
            {
                FileHelpers.OpenFolderWithFile(_config.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open folder from toast");
            }
        }
    }

    private void OpenUrl()
    {
        if (!string.IsNullOrEmpty(_config.URL))
        {
            try
            {
                URLHelpers.OpenURL(_config.URL);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to open URL from toast");
            }
        }
    }

    private void CopyImageToClipboard()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    PlatformServices.Clipboard.SetImage(bitmap);
                    DebugHelper.WriteLine($"Copied image to clipboard: {_config.FilePath}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy image to clipboard from toast");
            }
        }
    }

    private void CopyFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                PlatformServices.Clipboard.SetFileDropList(new[] { _config.FilePath });
                DebugHelper.WriteLine($"Copied file to clipboard: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy file to clipboard from toast");
            }
        }
    }

    private void CopyFilePath()
    {
        if (!string.IsNullOrEmpty(_config.FilePath))
        {
            try
            {
                PlatformServices.Clipboard.SetText(_config.FilePath);
                DebugHelper.WriteLine($"Copied file path to clipboard: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy file path from toast");
            }
        }
    }

    private void CopyUrl()
    {
        if (!string.IsNullOrEmpty(_config.URL))
        {
            try
            {
                PlatformServices.Clipboard.SetText(_config.URL);
                DebugHelper.WriteLine($"Copied URL to clipboard: {_config.URL}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy URL from toast");
            }
        }
    }

    internal static string BuildMarkdownImage(string url, string? altText = null)
    {
        string escapedAltText = string.IsNullOrWhiteSpace(altText)
            ? "Image"
            : altText.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");

        string markdownUrl = url.IndexOfAny([' ', '(', ')']) >= 0
            ? $"<{url}>"
            : url;

        return $"![{escapedAltText}]({markdownUrl})";
    }

    private void CopyMarkdownImage()
    {
        if (string.IsNullOrEmpty(_config.URL)) return;

        var markdownImage = BuildMarkdownImage(_config.URL, _config.Title);
        try
        {
            PlatformServices.Clipboard.SetText(markdownImage);
            DebugHelper.WriteLine("Copied markdown image to clipboard from toast.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy markdown image from toast");
        }
    }

    private void CopyErrors()
    {
        if (string.IsNullOrWhiteSpace(ErrorDetails))
        {
            return;
        }

        try
        {
            PlatformServices.Clipboard.SetText(ErrorDetails);
            DebugHelper.WriteLine("Copied task errors to clipboard from toast.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy errors from toast");
        }
    }

    private async void AnnotateMedia()
    {
        if (string.IsNullOrEmpty(_config.FilePath)) return;

        try
        {
            if (FileHelpers.IsImageFile(_config.FilePath))
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    await PlatformServices.UI.ShowEditorAsync(bitmap, sourceFilePath: _config.FilePath);
                }
            }
            else
            {
                string ffmpegPath = XerahS.Common.PathsManager.GetFFmpegPath();
                await PlatformServices.UI.ShowVideoEditorAsync(
                    _config.FilePath,
                    string.IsNullOrEmpty(ffmpegPath) ? null : ffmpegPath);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to annotate media from toast");
        }
    }

    private async Task UploadFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.FilePath))
        {
            return;
        }

        if (!File.Exists(_config.FilePath))
        {
            DebugHelper.WriteLine($"Toast upload skipped, file not found: {_config.FilePath}");
            return;
        }

        var taskManager = _taskManager
            ?? PlatformServices.RootProvider?.GetService(typeof(IDesktopTaskManager)) as IDesktopTaskManager;
        if (taskManager == null)
        {
            DebugHelper.WriteLine("Toast upload skipped, desktop task manager is not available.");
            return;
        }

        try
        {
            var settings = GetUploadTaskSettings();
            settings.Job = WorkflowType.FileUpload;
            await taskManager.StartFileTask(settings, _config.FilePath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to upload file from toast");
        }
    }

    private static TaskSettings GetUploadTaskSettings()
    {
        var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
        if (uploadWorkflow?.TaskSettings != null)
        {
            var settings = WatchFolderManager.CloneTaskSettings(uploadWorkflow.TaskSettings);
            settings.WorkflowId = uploadWorkflow.Id;
            return settings;
        }

        return WatchFolderManager.CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
    }

    private void PinToScreen()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && FileHelpers.IsImageFile(_config.FilePath))
        {
            try
            {
                using var bitmap = SKBitmap.Decode(_config.FilePath);
                if (bitmap != null)
                {
                    var options = SettingsManager.DefaultTaskSettings?.ToolsSettings?.PinToScreenOptions
                        ?? new PinToScreenOptions();
                    Services.PinToScreenManager.PinImage(bitmap, null, options);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to pin image from toast");
            }
        }
    }

    private void DeleteFile()
    {
        if (!string.IsNullOrEmpty(_config.FilePath) && File.Exists(_config.FilePath))
        {
            try
            {
                // TODO: Add confirmation dialog
                File.Delete(_config.FilePath);
                DebugHelper.WriteLine($"Deleted file: {_config.FilePath}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to delete file from toast");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _durationTimer.Stop();
            _fadeTimer.Stop();
            _disposed = true;
        }
    }
}
