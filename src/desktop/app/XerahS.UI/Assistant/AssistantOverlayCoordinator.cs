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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.UI.Assistant;

public sealed class AssistantOverlayCoordinator : IDisposable
{
    private readonly IDesktopTaskManager _taskManager;
    private AssistantOverlayWindow? _window;
    private HotkeyInfo? _registeredHotkey;
    private bool _disposed;

    public AssistantOverlayCoordinator(IDesktopTaskManager taskManager)
    {
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
    }

    public void Start()
    {
        if (!PlatformServices.IsInitialized || !SettingsManager.Settings.AssistantEnabled)
        {
            return;
        }

        RegisterHotkey();
        SettingsManager.SettingsChanged += OnSettingsChanged;
    }

    private void RegisterHotkey()
    {
        var hotkey = SettingsManager.Settings.AssistantHotkey;
        if (!hotkey.IsValid)
        {
            return;
        }

        try
        {
            bool registered = PlatformServices.Hotkey.RegisterHotkey(hotkey);
            if (!registered)
            {
                hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
                DebugHelper.WriteLine($"Assistant shortcut failed to register: {hotkey.GetDisplayString()}");
                return;
            }

            hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Registered;
            _registeredHotkey = hotkey;
            PlatformServices.Hotkey.HotkeyTriggered -= OnHotkeyTriggered;
            PlatformServices.Hotkey.HotkeyTriggered += OnHotkeyTriggered;
            DebugHelper.WriteLine($"Assistant shortcut registered: {hotkey.GetDisplayString()}");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Assistant shortcut registration failed.");
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed || !PlatformServices.IsInitialized)
        {
            return;
        }

        UnregisterHotkey();

        if (SettingsManager.Settings.AssistantEnabled)
        {
            RegisterHotkey();
        }
    }

    private void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (_registeredHotkey == null || e.HotkeyInfo.Id != _registeredHotkey.Id)
        {
            return;
        }

        Dispatcher.UIThread.Post(ShowOverlay);
    }

    public void ShowOverlay()
    {
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        var viewModel = new AssistantViewModel(new XerahS.Assistant.Services.AssistantService(_taskManager));
        _window = new AssistantOverlayWindow();
        _window.Initialize(viewModel);
        _window.Closed += (_, _) => _window = null;
        PositionWindow(_window);
        _window.Show();
        _window.Activate();
    }

    private static void PositionWindow(AssistantOverlayWindow window)
    {
        try
        {
            var screen = PlatformServices.Screen.GetScreenFromPoint(PlatformServices.Input.GetCursorPosition());
            var area = screen.WorkingArea.IsEmpty
                ? screen.Bounds
                : screen.WorkingArea;
            double scale = Math.Max(1, screen.ScaleFactor);
            int width = RoundEven((int)(600 * scale));
            int x = area.Left + (area.Width - width) / 2;
            int y = area.Top + RoundEven((int)(100 * scale));

            window.Position = new PixelPoint(x, y);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Assistant overlay positioning failed.");

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                window.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
            }
        }
    }

    private static int RoundEven(int value) => value % 2 == 0 ? value : value + 1;

    private void UnregisterHotkey()
    {
        if (_registeredHotkey == null || !PlatformServices.IsInitialized)
        {
            return;
        }

        try
        {
            PlatformServices.Hotkey.HotkeyTriggered -= OnHotkeyTriggered;
            PlatformServices.Hotkey.UnregisterHotkey(_registeredHotkey);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Assistant shortcut unregister failed.");
        }
        finally
        {
            _registeredHotkey = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SettingsManager.SettingsChanged -= OnSettingsChanged;
        UnregisterHotkey();
        _window?.Close();
        _window = null;
    }
}
