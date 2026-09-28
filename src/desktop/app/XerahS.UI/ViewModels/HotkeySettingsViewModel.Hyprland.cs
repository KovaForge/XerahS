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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;

namespace XerahS.UI.ViewModels;

/// <summary>A key Hyprland already binds; the user ticks it to let XerahS take it over.</summary>
public partial class HyprlandConflictItemViewModel : ObservableObject
{
    public HyprlandConflictItemViewModel(HyprlandBindingConflict conflict, bool isApproved)
    {
        Keys = conflict.Keys;
        Text = $"{conflict.Keys}: {conflict.WorkflowDescription} (now: {conflict.ExistingDescription})";
        _isApproved = isApproved;
    }

    public string Keys { get; }

    public string Text { get; }

    [ObservableProperty]
    private bool _isApproved;
}

/// <summary>XIP0088 Phase 5: Settings > Hotkeys > Use Hyprland keybindings.</summary>
public partial class HotkeySettingsViewModel
{
    public ObservableCollection<HyprlandConflictItemViewModel> HyprlandConflicts { get; } = new();

    [ObservableProperty]
    private bool _isHyprlandKeybindingsAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHyprlandKeybindingsOff))]
    private bool _isHyprlandKeybindingsEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckHyprlandKeybindingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableHyprlandKeybindingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableHyprlandKeybindingsCommand))]
    private bool _isHyprlandBusy;

    [ObservableProperty]
    private string? _hyprlandKeybindingsStatus;

    [ObservableProperty]
    private string? _hyprlandConsentText;

    public bool IsHyprlandKeybindingsOff => !IsHyprlandKeybindingsEnabled;

    private void RefreshHyprlandKeybindings()
    {
        IHyprlandKeybindingService? service = HyprlandKeybindingCoordinator.Service;
        IsHyprlandKeybindingsAvailable = OperatingSystem.IsLinux() && service != null;
        if (service == null)
        {
            return;
        }

        IsHyprlandKeybindingsEnabled = service.IsEnabled;
        HyprlandConsentText =
            $"Turning this on writes {service.ManagedFilePath}, saves a timestamped copy of {Path.GetFileName(service.IncludeTargetPath)}, " +
            $"adds one line to {Path.GetFileName(service.IncludeTargetPath)} that loads it, and reloads Hyprland. " +
            "If Hyprland reports config errors, XerahS puts your config back. Portal and evdev hotkeys stop while this is on.";
        HyprlandKeybindingsStatus ??= service.IsEnabled
            ? $"On. Hyprland runs your workflow hotkeys from {service.ManagedFilePath}."
            : "Off. XerahS registers hotkeys itself.";
    }

    private bool CanChangeHyprlandKeybindings() => !IsHyprlandBusy;

    [RelayCommand(CanExecute = nameof(CanChangeHyprlandKeybindings))]
    private async Task CheckHyprlandKeybindings()
    {
        IsHyprlandBusy = true;
        try
        {
            HyprlandBindingScan scan = await HyprlandKeybindingCoordinator.ScanAsync();
            var approved = new HashSet<string>(SettingsManager.Settings.LinuxHyprlandApprovedUnbinds, StringComparer.OrdinalIgnoreCase);
            HyprlandConflicts.Clear();
            foreach (HyprlandBindingConflict conflict in scan.Conflicts)
            {
                HyprlandConflicts.Add(new HyprlandConflictItemViewModel(conflict, approved.Contains(conflict.Keys)));
            }

            string skipped = scan.Skipped.Count == 0 ? string.Empty : " Skipped: " + string.Join("; ", scan.Skipped) + ".";
            HyprlandKeybindingsStatus = scan.Error ?? (scan.Conflicts.Count == 0
                ? $"{scan.Bindings.Count} hotkey(s) checked. No key is bound by Omarchy or your config.{skipped}"
                : $"{scan.Conflicts.Count} key(s) are already bound. Tick the ones XerahS may take over; unticked ones keep their current binding.{skipped}");
        }
        finally
        {
            IsHyprlandBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeHyprlandKeybindings))]
    private async Task EnableHyprlandKeybindings()
    {
        IsHyprlandBusy = true;
        try
        {
            if (HyprlandConflicts.Count == 0)
            {
                // Always show conflicts before the first apply.
                HyprlandBindingScan scan = await HyprlandKeybindingCoordinator.ScanAsync();
                if (scan.Conflicts.Count > 0)
                {
                    IsHyprlandBusy = false;
                    await CheckHyprlandKeybindings();
                    return;
                }
            }

            string[] approved = HyprlandConflicts.Where(item => item.IsApproved).Select(item => item.Keys).ToArray();
            HyprlandApplyResult result = await HyprlandKeybindingCoordinator.EnableAsync(approved);
            HyprlandKeybindingsStatus = result.Message;
            if (result.Succeeded)
            {
                HyprlandConflicts.Clear();
            }
        }
        finally
        {
            IsHyprlandBusy = false;
            RefreshHyprlandKeybindings();
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeHyprlandKeybindings))]
    private async Task DisableHyprlandKeybindings()
    {
        IsHyprlandBusy = true;
        try
        {
            HyprlandApplyResult result = await HyprlandKeybindingCoordinator.DisableAsync();
            HyprlandKeybindingsStatus = result.Message;
        }
        finally
        {
            IsHyprlandBusy = false;
            RefreshHyprlandKeybindings();
        }
    }
}
