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

using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    /// <summary>
    /// Hyprland-managed keybindings (XIP0088 Phase 5). Consent is two explicit steps: "Review changes"
    /// shows exactly what will be written and which keys conflict, then "Apply" changes the config
    /// (with a timestamped backup, validation and automatic rollback).
    /// </summary>
    public partial class SettingsViewModel
    {
        private readonly HyprlandKeybindingCoordinator _hyprlandKeybindings = new();
        private HyprlandKeybindingPlan? _hyprlandPlan;

        public bool IsHyprlandKeybindingsSupported => _hyprlandKeybindings.IsSupported;

        [ObservableProperty]
        private bool _isHyprlandKeybindingsEnabled = SettingsManager.Settings?.LinuxHyprlandKeybindings == true;

        [ObservableProperty]
        private string _hyprlandKeybindingPlanText = string.Empty;

        [ObservableProperty]
        private string _hyprlandKeybindingStatusText = string.Empty;

        [ObservableProperty]
        private bool _hasHyprlandKeybindingPlan;

        [ObservableProperty]
        private bool _hasHyprlandKeybindingConflicts;

        /// <summary>User approval to unbind keys Omarchy or the user already bound.</summary>
        [ObservableProperty]
        private bool _unbindConflictingHyprlandKeys;

        [RelayCommand]
        private async Task ReviewHyprlandKeybindingsAsync()
        {
            _hyprlandPlan = await _hyprlandKeybindings.PlanAsync(SettingsManager.WorkflowsConfig.Hotkeys ?? []);
            HasHyprlandKeybindingPlan = _hyprlandPlan.Supported;
            HasHyprlandKeybindingConflicts = _hyprlandPlan.Conflicts.Count > 0;
            HyprlandKeybindingPlanText = DescribePlan(_hyprlandPlan);
            HyprlandKeybindingStatusText = string.Empty;
        }

        [RelayCommand]
        private async Task ApplyHyprlandKeybindingsAsync()
        {
            if (_hyprlandPlan is not { Supported: true } plan)
            {
                HyprlandKeybindingStatusText = "Review the changes first.";
                return;
            }

            IReadOnlyCollection<string> approved = UnbindConflictingHyprlandKeys
                ? plan.Conflicts.Select(c => c.Entry.Keys).ToArray()
                : [];
            CompositorKeybindingResult result = await _hyprlandKeybindings.EnableAsync(plan, approved);
            HyprlandKeybindingStatusText = FormatResult(result);
            if (result.Success)
            {
                IsHyprlandKeybindingsEnabled = true;
                HasHyprlandKeybindingPlan = false;
                ReRegisterHotkeys();
            }
        }

        [RelayCommand]
        private async Task DisableHyprlandKeybindingsAsync()
        {
            CompositorKeybindingResult result = await _hyprlandKeybindings.DisableAsync();
            HyprlandKeybindingStatusText = FormatResult(result);
            if (result.Success)
            {
                IsHyprlandKeybindingsEnabled = false;
                ReRegisterHotkeys();
            }
        }

        internal static string DescribePlan(HyprlandKeybindingPlan plan)
        {
            if (!plan.Supported)
            {
                return plan.Problem ?? "Hyprland keybindings are not available.";
            }

            var text = new StringBuilder();
            text.AppendLine($"XerahS will write {plan.ManagedFilePath}, back up ~/.config/hypr/bindings.lua to bindings.lua.bak.<time>, add require(\"xerahs\") to it, run hyprctl reload and check hyprctl configerrors (rolled back on errors). Portal/evdev hotkeys stop while this is on.");
            text.AppendLine();
            if (plan.Entries.Count == 0)
            {
                text.AppendLine("No enabled workflow has a hotkey.");
            }

            foreach (HyprlandBindingEntry entry in plan.Entries)
            {
                text.AppendLine($"  {entry.Keys}  →  {entry.WorkflowName}");
            }

            foreach (HyprlandBindingConflict conflict in plan.Conflicts)
            {
                text.AppendLine($"Conflict: {conflict.Entry.Keys} is already bound to {conflict.Existing.Description ?? conflict.Existing.Dispatcher} {conflict.Existing.Argument}".TrimEnd());
            }

            foreach (string problem in plan.Unsupported)
            {
                text.AppendLine($"Skipped: {problem}");
            }

            return text.ToString().TrimEnd();
        }

        private static string FormatResult(CompositorKeybindingResult result)
        {
            string errors = result.ConfigErrors is { Count: > 0 } list ? " " + string.Join(" ", list) : string.Empty;
            string backup = result.BackupPath != null ? $" Backup: {result.BackupPath}." : string.Empty;
            return result.Message + backup + errors;
        }

        private static void ReRegisterHotkeys()
        {
            if (Avalonia.Application.Current is App app && app.WorkflowManager != null)
            {
                app.WorkflowManager.UpdateHotkeys(SettingsManager.WorkflowsConfig.Hotkeys);
            }
        }
    }
}
