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

using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services;

/// <summary>
/// XIP0088 Phase 5 glue between workflows, settings and <see cref="IHyprlandKeybindingService"/>:
/// builds the binding list, runs the consent flow from settings, and keeps
/// <c>~/.config/hypr/xerahs.lua</c> in step when hotkeys change.
/// </summary>
public static class HyprlandKeybindingCoordinator
{
    private static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(750);
    private static CancellationTokenSource? _pendingSync;
    private static WorkflowManager? _manager;

    /// <summary>The service when this is a Hyprland session, otherwise null.</summary>
    public static IHyprlandKeybindingService? Service =>
        PlatformServices.IsInitialized ? PlatformServices.HyprlandKeybindings : null;

    /// <summary>
    /// Returns the hotkey service workflows should use and restores the saved mode. The mode is
    /// only restored when the user config still loads the managed file; otherwise XerahS falls
    /// back to portal or evdev shortcuts so hotkeys keep working.
    /// </summary>
    public static IHotkeyService Attach(IHotkeyService hotkeyService)
    {
        IHyprlandKeybindingService? service = Service;
        if (service == null)
        {
            return hotkeyService;
        }

        bool wanted = SettingsManager.Settings.LinuxHyprlandKeybindings;
        service.IsEnabled = wanted && service.IsIncluded;
        if (wanted && !service.IsEnabled)
        {
            DebugHelper.WriteLine($"Hyprland keybindings: {service.IncludeTargetPath} no longer loads {service.ManagedFilePath}; using portal or evdev hotkeys.");
        }

        service.WorkflowHotkeysChanged -= OnWorkflowHotkeysChanged;
        service.WorkflowHotkeysChanged += OnWorkflowHotkeysChanged;
        return service.WrapWorkflowHotkeys(hotkeyService);
    }

    /// <summary>Called once the workflow manager exists so later hotkey edits rewrite the managed file.</summary>
    public static void Track(WorkflowManager manager)
    {
        _manager = manager;
        if (Service?.IsEnabled == true)
        {
            ScheduleSync();
        }
    }

    public static IReadOnlyList<HyprlandWorkflowBinding> BuildBindings(IEnumerable<WorkflowSettings> workflows) =>
        workflows
            .Where(workflow => workflow.Enabled && workflow.Job != WorkflowType.None && workflow.HotkeyInfo?.IsValid == true && !string.IsNullOrEmpty(workflow.Id))
            .Select(workflow => new HyprlandWorkflowBinding(workflow.Id, workflow.ToString(), workflow.HotkeyInfo))
            .ToList();

    public static Task<HyprlandBindingScan> ScanAsync()
    {
        IHyprlandKeybindingService service = Service ?? throw new InvalidOperationException("Not a Hyprland session.");
        return service.ScanAsync(BuildBindings(_manager?.Workflows ?? []));
    }

    /// <summary>The consent step. Saves the choice and re-registers hotkeys only when Hyprland accepted the config.</summary>
    public static async Task<HyprlandApplyResult> EnableAsync(IReadOnlyCollection<string> approvedUnbinds)
    {
        IHyprlandKeybindingService service = Service ?? throw new InvalidOperationException("Not a Hyprland session.");
        HyprlandApplyResult result = await service.EnableAsync(BuildBindings(_manager?.Workflows ?? []), approvedUnbinds);
        DebugHelper.WriteLine($"Hyprland keybindings: enable -> {result.Message}");
        if (!result.Succeeded)
        {
            return result;
        }

        SettingsManager.Settings.LinuxHyprlandKeybindings = true;
        SettingsManager.Settings.LinuxHyprlandApprovedUnbinds = approvedUnbinds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        SettingsManager.SaveApplicationConfig();
        ReregisterWorkflowHotkeys();
        return result;
    }

    public static async Task<HyprlandApplyResult> DisableAsync()
    {
        IHyprlandKeybindingService service = Service ?? throw new InvalidOperationException("Not a Hyprland session.");
        CancelPendingSync();
        HyprlandApplyResult result = await service.DisableAsync();
        DebugHelper.WriteLine($"Hyprland keybindings: disable -> {result.Message}");
        SettingsManager.Settings.LinuxHyprlandKeybindings = false;
        SettingsManager.SaveApplicationConfig();
        ReregisterWorkflowHotkeys();
        return result;
    }

    private static void ReregisterWorkflowHotkeys()
    {
        if (_manager != null)
        {
            _manager.UpdateHotkeys(_manager.Workflows);
        }
    }

    private static void OnWorkflowHotkeysChanged(object? sender, EventArgs e) => ScheduleSync();

    /// <summary>Hotkey edits arrive one registration at a time; write the file once they settle.</summary>
    private static void ScheduleSync()
    {
        CancellationTokenSource source = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _pendingSync, source);
        previous?.Cancel();
        previous?.Dispose();
        _ = SyncAfterDelayAsync(source.Token);
    }

    private static void CancelPendingSync()
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref _pendingSync, null);
        previous?.Cancel();
        previous?.Dispose();
    }

    private static async Task SyncAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SyncDelay, token).ConfigureAwait(false);
            IHyprlandKeybindingService? service = Service;
            if (service is not { IsEnabled: true } || _manager == null)
            {
                return;
            }

            // The workflow list belongs to the UI thread; the file write and hyprctl run off it.
            WorkflowManager manager = _manager;
            IReadOnlyList<HyprlandWorkflowBinding> bindings = await Dispatcher.UIThread.InvokeAsync(() => BuildBindings(manager.Workflows.ToList()));
            HyprlandApplyResult result = await service.SyncAsync(bindings, SettingsManager.Settings.LinuxHyprlandApprovedUnbinds, token).ConfigureAwait(false);
            DebugHelper.WriteLine($"Hyprland keybindings: sync -> {result.Message}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Hyprland keybindings: sync failed");
        }
    }
}
