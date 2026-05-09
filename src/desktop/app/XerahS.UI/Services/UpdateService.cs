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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

/// <summary>
/// Singleton service that coordinates the auto-update flow.
/// </summary>
public class UpdateService : IDisposable
{
    private static UpdateService? _instance;
    private static readonly object _lock = new();
    private static readonly TimeSpan DialogOwnerRetryInterval = TimeSpan.FromMilliseconds(200);
    private const int DialogOwnerRetryCount = 25;
    private const string DefaultReleaseOwner = "ShareX";
    private const string DefaultPreReleaseOwner = "KovaForge";
    private const string DefaultRepo = "XerahS";

    public static UpdateService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new UpdateService();
                }
            }
            return _instance;
        }
    }

    private GitHubUpdateManager? _updateManager;
    private DispatcherTimer? _pendingUpdateDialogTimer;
    private UpdateChecker? _pendingUpdateChecker;
    private bool _disposed;

    public bool IsUpdateDialogOpen { get; private set; }

    private UpdateService()
    {
    }

    /// <summary>
    /// Initialize the update service and start periodic update checks if enabled.
    /// </summary>
    public void Initialize()
    {
        if (_updateManager != null)
        {
            DebugHelper.WriteLine("UpdateService already initialized.");
            return;
        }

        var settings = SettingsManager.Settings;
        bool includePreRelease = settings.UpdateChannel == UpdateChannel.PreRelease;
        var updateRepository = ResolveUpdateRepository(settings);

        _updateManager = new GitHubUpdateManager(updateRepository.Owner, updateRepository.Repo)
        {
            IsPortable = IsPortableBuild(),
            IncludePreRelease = includePreRelease,
            AllowAutoUpdate = settings.AutoCheckUpdate
        };

        // Wire up the callback for showing the update dialog
        _updateManager.ShowUpdateDialogCallback = ShowUpdateDialogAsync;

        if (settings.AutoCheckUpdate)
        {
            _updateManager.ConfigureAutoUpdate();
            DebugHelper.WriteLine("UpdateService: Auto-update enabled and configured.");
        }
        else
        {
            DebugHelper.WriteLine("UpdateService: Auto-update is disabled.");
        }
    }

    public static (string Owner, string Repo) ResolveUpdateRepository(ApplicationConfig settings)
    {
        if (settings.UpdateChannel != UpdateChannel.PreRelease)
        {
            return (DefaultReleaseOwner, DefaultRepo);
        }

        return settings.PreReleaseUpdateSource switch
        {
            PreReleaseUpdateSource.ShareX => (DefaultReleaseOwner, DefaultRepo),
            PreReleaseUpdateSource.Custom => ResolveCustomPreReleaseRepository(settings.CustomPreReleaseUpdateSource),
            _ => (DefaultPreReleaseOwner, DefaultRepo)
        };
    }

    public static (string Owner, string Repo) ResolveCustomPreReleaseRepository(string? source)
    {
        string normalized = NormalizeCustomPreReleaseSource(source);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (DefaultPreReleaseOwner, DefaultRepo);
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? (parts[0], parts[1])
            : (parts[0], DefaultRepo);
    }

    private static string NormalizeCustomPreReleaseSource(string? source)
    {
        string normalized = source?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = uri.AbsolutePath;
        }

        return normalized.Trim().Trim('/');
    }

    /// <summary>
    /// Shows the update dialog to the user when an update is available.
    /// </summary>
    /// <param name="updateChecker">The update checker with version information.</param>
    /// <returns>True if user accepted the update, false otherwise.</returns>
    public async Task<bool> ShowUpdateDialogAsync(UpdateChecker updateChecker)
    {
        if (IsUpdateDialogOpen)
        {
            DebugHelper.WriteLine("Update dialog is already open.");
            return false;
        }

        if (updateChecker.Status != UpdateStatus.UpdateAvailable)
        {
            return false;
        }

        IsUpdateDialogOpen = true;

        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = await WaitForDialogOwnerAsync();
                if (!CanUseDialogOwner(owner))
                {
                    // Defer this prompt if the main window is not ready/visible yet.
                    // Returning true prevents auto-update from being disabled as if user declined.
                    DeferUpdateDialog(updateChecker);
                    return true;
                }

                var dialog = new UpdateMessageBox(updateChecker);
                bool? result;
                try
                {
                    result = await dialog.ShowDialog<bool?>(owner!);
                }
                catch (InvalidOperationException ex)
                {
                    DebugHelper.WriteException(ex, "Failed to show update dialog");
                    return true;
                }

                if (result == true)
                {
                    await HandleUpdateAcceptedAsync(updateChecker);
                    return true;
                }
                else
                {
                    // User clicked No - disable auto-update for this session
                    if (_updateManager != null)
                    {
                        _updateManager.AutoUpdateEnabled = false;
                    }
                    DebugHelper.WriteLine("User declined update. Auto-update disabled until restart.");
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Update dialog flow failed");
            return true;
        }
        finally
        {
            IsUpdateDialogOpen = false;
        }
    }

    private async Task HandleUpdateAcceptedAsync(UpdateChecker updateChecker)
    {
        if (updateChecker.IsPortable)
        {
            // For portable builds, open the download URL in browser
            if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
            {
                URLHelpers.OpenURL(updateChecker.DownloadURL);
                DebugHelper.WriteLine($"Portable build: Opened download URL in browser: {updateChecker.DownloadURL}");
            }
        }
        else
        {
            // For installer builds, show the downloader window
            await ShowDownloaderWindowAsync(updateChecker);
        }
    }

    private async Task ShowDownloaderWindowAsync(UpdateChecker updateChecker)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = await WaitForDialogOwnerAsync();
            if (!CanUseDialogOwner(owner))
            {
                DebugHelper.WriteLine("Cannot show downloader: Main window is not visible.");
                // Fallback to opening URL in browser
                if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
                {
                    URLHelpers.OpenURL(updateChecker.DownloadURL);
                }
                return;
            }

            var dialog = new DownloaderWindow(updateChecker);
            bool? result;
            try
            {
                result = await dialog.ShowDialog<bool?>(owner!);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "Failed to show downloader window");
                if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
                {
                    URLHelpers.OpenURL(updateChecker.DownloadURL);
                }
                return;
            }

            if (result == true)
            {
                // Installer was launched successfully - shut down the application
                DebugHelper.WriteLine("Installer launched. Shutting down application...");
                ShutdownApplication();
            }
        });
    }

    private void DeferUpdateDialog(UpdateChecker updateChecker)
    {
        _pendingUpdateChecker = updateChecker;

        if (_pendingUpdateDialogTimer == null)
        {
            _pendingUpdateDialogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _pendingUpdateDialogTimer.Tick += PendingUpdateDialogTimer_Tick;
        }

        if (!_pendingUpdateDialogTimer.IsEnabled)
        {
            _pendingUpdateDialogTimer.Start();
        }

        DebugHelper.WriteLine("Update dialog deferred until main window is visible.");
    }

    private async void PendingUpdateDialogTimer_Tick(object? sender, EventArgs e)
    {
        if (IsUpdateDialogOpen || _pendingUpdateChecker == null)
        {
            return;
        }

        var owner = GetMainWindow();
        if (!CanUseDialogOwner(owner))
        {
            return;
        }

        var updateChecker = _pendingUpdateChecker;
        _pendingUpdateChecker = null;
        _pendingUpdateDialogTimer?.Stop();

        await ShowUpdateDialogAsync(updateChecker);
    }

    private static void ShutdownApplication()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            App.IsExiting = true;
            desktop.Shutdown();
        }
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private static bool CanUseDialogOwner(Window? owner)
    {
        return owner != null &&
               owner.IsVisible &&
               owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
               owner.ShowInTaskbar;
    }

    private static async Task<Window?> WaitForDialogOwnerAsync()
    {
        for (int i = 0; i < DialogOwnerRetryCount; i++)
        {
            var owner = GetMainWindow();
            if (CanUseDialogOwner(owner))
            {
                return owner;
            }

            await Task.Delay(DialogOwnerRetryInterval);
        }

        return null;
    }

    private static bool IsPortableBuild()
    {
        // Check for portable marker file
        var portableMarker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.txt");
        return File.Exists(portableMarker);
    }

    /// <summary>
    /// Manually trigger an update check.
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_updateManager == null)
        {
            DebugHelper.WriteLine("UpdateService not initialized. Call Initialize() first.");
            return;
        }

        var updateChecker = _updateManager.CreateUpdateChecker();
        await updateChecker.CheckUpdateAsync();

        if (updateChecker.Status == UpdateStatus.UpdateAvailable)
        {
            await ShowUpdateDialogAsync(updateChecker);
        }
        else if (updateChecker.Status == UpdateStatus.UpToDate)
        {
            DebugHelper.WriteLine($"Application is up to date. Current version: {updateChecker.CurrentVersion}");
        }
        else
        {
            DebugHelper.WriteLine($"Update check failed. Status: {updateChecker.Status}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_pendingUpdateDialogTimer != null)
            {
                _pendingUpdateDialogTimer.Stop();
                _pendingUpdateDialogTimer.Tick -= PendingUpdateDialogTimer_Tick;
                _pendingUpdateDialogTimer = null;
            }

            _pendingUpdateChecker = null;
            _updateManager?.Dispose();
            _updateManager = null;
            _disposed = true;
        }
    }
}
