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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.CaptureCommandPalette;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.UI.CaptureCommandPalette;

public sealed class CaptureCommandPaletteCoordinator : IDisposable
{
    private readonly WorkflowManager _workflowManager;
    private readonly Func<WorkflowSettings, Task> _executeWorkflowAsync;
    private CaptureCommandPaletteWindow? _window;
    private HotkeyInfo? _registeredHotkey;
    private bool _disposed;

    public CaptureCommandPaletteCoordinator(
        WorkflowManager workflowManager,
        Func<WorkflowSettings, Task> executeWorkflowAsync)
    {
        _workflowManager = workflowManager;
        _executeWorkflowAsync = executeWorkflowAsync;
    }

    public void Start()
    {
        if (!PlatformServices.IsInitialized)
        {
            return;
        }

        SettingsManager.SettingsChanged += OnSettingsChanged;
        _workflowManager.WorkflowsChanged += OnWorkflowsChanged;

        if (SettingsManager.Settings.CaptureCommandPaletteEnabled)
        {
            RegisterHotkey();
        }
    }

    private void RegisterHotkey()
    {
        HotkeyInfo hotkey = SettingsManager.Settings.CaptureCommandPaletteHotkey;
        if (!hotkey.IsValid)
        {
            hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.NotConfigured;
            return;
        }

        try
        {
            bool registered = PlatformServices.Hotkey.RegisterHotkey(hotkey);
            if (!registered)
            {
                hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
                DebugHelper.WriteLine($"Capture command palette shortcut failed to register: {hotkey.GetDisplayString()}");
                return;
            }

            hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Registered;
            _registeredHotkey = hotkey;
            PlatformServices.Hotkey.HotkeyTriggered -= OnHotkeyTriggered;
            PlatformServices.Hotkey.HotkeyTriggered += OnHotkeyTriggered;
            DebugHelper.WriteLine($"Capture command palette shortcut registered: {hotkey.GetDisplayString()}");
        }
        catch (Exception ex)
        {
            hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
            DebugHelper.WriteException(ex, "Capture command palette shortcut registration failed.");
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed || !PlatformServices.IsInitialized)
        {
            return;
        }

        UnregisterHotkey();

        if (SettingsManager.Settings.CaptureCommandPaletteEnabled)
        {
            RegisterHotkey();
        }
        else
        {
            Dispatcher.UIThread.Post(() => _window?.Close());
        }
    }

    private void OnWorkflowsChanged(object? sender, EventArgs e)
    {
        if (_window?.DataContext is CaptureCommandPaletteViewModel viewModel)
        {
            Dispatcher.UIThread.Post(viewModel.ReloadItems);
        }
    }

    private void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (_registeredHotkey == null || e.HotkeyInfo.Id != _registeredHotkey.Id)
        {
            return;
        }

        Dispatcher.UIThread.Post(TogglePalette);
    }

    public void TogglePalette()
    {
        if (_window is { IsVisible: true })
        {
            _window.Close();
            return;
        }

        ShowPalette();
    }

    public void ShowPalette()
    {
        var viewModel = new CaptureCommandPaletteViewModel(
            () => CaptureCommandPaletteProvider.CreateItems(_workflowManager.Workflows),
            async item => await _executeWorkflowAsync(item.Workflow));

        _window = new CaptureCommandPaletteWindow();
        _window.Initialize(viewModel);
        _window.Closed += (_, _) => _window = null;
        PositionWindow(_window);
        _window.Show();
        _window.Activate();
    }

    private static void PositionWindow(CaptureCommandPaletteWindow window)
    {
        try
        {
            var screen = PlatformServices.Screen.GetScreenFromPoint(PlatformServices.Input.GetCursorPosition());
            var area = screen.WorkingArea.IsEmpty
                ? screen.Bounds
                : screen.WorkingArea;
            double scale = Math.Max(1, screen.ScaleFactor);
            int width = RoundEven((int)(620 * scale));
            int x = area.Left + (area.Width - width) / 2;
            int y = area.Top + RoundEven((int)(100 * scale));

            window.Position = new PixelPoint(x, y);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Capture command palette positioning failed.");

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
            DebugHelper.WriteException(ex, "Capture command palette shortcut unregister failed.");
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
        _workflowManager.WorkflowsChanged -= OnWorkflowsChanged;
        UnregisterHotkey();
        _window?.Close();
        _window = null;
    }
}
