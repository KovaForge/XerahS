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
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Onboarding.ViewModels.Steps;

/// <summary>
/// Represents a secondary hotkey configuration.
/// </summary>
public record SecondaryHotkeyConfig(string Name, string Description, HotkeyInfo Hotkey);

/// <summary>
/// Step 3: Hotkey Configuration
/// </summary>
public partial class HotkeyStepViewModel : StepViewModelBase
{
    private bool _syncingHotkeyText;
    private bool _syncingHotkeyItems;

    [ObservableProperty]
    private HotkeyInfo? _primaryHotkey;

    [ObservableProperty]
    private string _primaryHotkeyText = string.Empty;

    [ObservableProperty]
    private string? _conflictMessage;

    [ObservableProperty]
    private bool _isRecordingHotkey;

    public ObservableCollection<HotkeyItemViewModel> PrimaryHotkeys { get; } = new();

    public ObservableCollection<HotkeyItemViewModel> SecondaryHotkeyItems { get; } = new();

    public ObservableCollection<SecondaryHotkeyConfig> SecondaryHotkeys { get; } = new();

    public bool HasConflict => !string.IsNullOrEmpty(ConflictMessage);

    /// <summary>
    /// Callback to trigger a test capture. Set by the wizard.
    /// </summary>
    public Func<Task>? TestCaptureCallback { get; set; }

    public HotkeyStepViewModel()
    {
        StepTitle = "Hotkeys";
        StepSubtitle = "Configure your capture shortcuts";
        StepDescription = "Set up keyboard shortcuts for quick screenshot capture.";
        CanSkip = true;

        InitializeDefaults();
        SetValidationState(true);
    }

    private void InitializeDefaults()
    {
        PrimaryHotkey = new HotkeyInfo(Key.PrintScreen, KeyModifiers.None);

        PrimaryHotkeys.Add(CreateHotkeyItem(
            WorkflowType.RectangleRegion,
            "Primary Capture",
            PrimaryHotkey));

        SecondaryHotkeys.Add(new SecondaryHotkeyConfig(
            "Region Capture",
            "Capture a selected region of the screen",
            new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control)));

        SecondaryHotkeys.Add(new SecondaryHotkeyConfig(
            "Window Capture",
            "Capture the active window",
            new HotkeyInfo(Key.PrintScreen, KeyModifiers.Alt)));

        SecondaryHotkeys.Add(new SecondaryHotkeyConfig(
            "Full Screen",
            "Capture the entire screen",
            new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift)));

        SecondaryHotkeyItems.Add(CreateHotkeyItem(
            WorkflowType.RectangleRegion,
            "Region Capture",
            SecondaryHotkeys[0].Hotkey));

        SecondaryHotkeyItems.Add(CreateHotkeyItem(
            WorkflowType.ActiveWindow,
            "Window Capture",
            SecondaryHotkeys[1].Hotkey));

        SecondaryHotkeyItems.Add(CreateHotkeyItem(
            WorkflowType.PrintScreen,
            "Full Screen",
            SecondaryHotkeys[2].Hotkey));
    }

    private static HotkeyItemViewModel CreateHotkeyItem(WorkflowType workflowType, string description, HotkeyInfo hotkey)
    {
        WorkflowSettings workflowSettings = new(workflowType, hotkey)
        {
            Name = description
        };
        workflowSettings.TaskSettings.Description = description;

        return new HotkeyItemViewModel(workflowSettings);
    }

    [RelayCommand]
    private async Task TestHotkeyAsync()
    {
        if (TestCaptureCallback != null)
        {
            await TestCaptureCallback();
        }
    }

    public void DetectConflict()
    {
        if (!_syncingHotkeyItems)
        {
            SyncFromHotkeyItems();
        }

        ConflictMessage = null;

        if (PrimaryHotkey == null)
        {
            ConflictMessage = "No hotkey configured.";
            return;
        }

        if (IsSystemHotkey(PrimaryHotkey))
        {
            ConflictMessage = "This hotkey may conflict with system shortcuts. Consider using a modifier key.";
            return;
        }

        List<HotkeyInfo> allHotkeys = [PrimaryHotkey];
        allHotkeys.AddRange(SecondaryHotkeys.Select(config => config.Hotkey));

        bool hasDuplicate = allHotkeys
            .GroupBy(hotkey => new { hotkey.Key, hotkey.Modifiers })
            .Any(group => group.Count() > 1);

        if (hasDuplicate)
        {
            ConflictMessage = "Duplicate hotkeys detected. Each action should have a unique shortcut.";
        }
    }

    private static bool IsSystemHotkey(HotkeyInfo hotkey)
    {
        var systemHotkeys = new[]
        {
            new { Key = Key.PrintScreen, Modifiers = KeyModifiers.None },
            new { Key = Key.Tab, Modifiers = KeyModifiers.Alt },
            new { Key = Key.F4, Modifiers = KeyModifiers.Alt },
            new { Key = Key.Space, Modifiers = KeyModifiers.Alt },
            new { Key = Key.Escape, Modifiers = KeyModifiers.None },
        };

        return systemHotkeys.Any(systemHotkey =>
            hotkey.Key == systemHotkey.Key && hotkey.Modifiers == systemHotkey.Modifiers);
    }

    public override void LoadFromState(OnboardingState state)
    {
        if (state.PrimaryCaptureHotkey != null)
        {
            PrimaryHotkey = state.PrimaryCaptureHotkey;
            SetHotkeyItem(PrimaryHotkeys.FirstOrDefault(), state.PrimaryCaptureHotkey);
        }

        if (state.AdditionalHotkeys.Count > 0)
        {
            for (int i = 0; i < Math.Min(state.AdditionalHotkeys.Count, SecondaryHotkeys.Count); i++)
            {
                HotkeyInfo saved = state.AdditionalHotkeys[i];
                SecondaryHotkeyConfig existing = SecondaryHotkeys[i];
                SecondaryHotkeys[i] = existing with { Hotkey = saved };
                SetHotkeyItem(SecondaryHotkeyItems.ElementAtOrDefault(i), saved);
            }
        }

        DetectConflict();
        SetValidationState(PrimaryHotkey?.IsValid == true, PrimaryHotkey?.IsValid == true ? null : "Choose a valid hotkey.");
    }

    public override void SaveToState(OnboardingState state)
    {
        SyncFromHotkeyItems();
        state.PrimaryCaptureHotkey = PrimaryHotkey;
        state.AdditionalHotkeys = SecondaryHotkeyItems.Select(item => CloneHotkey(item.Model.HotkeyInfo)).ToList();
    }

    public override bool Validate()
    {
        DetectConflict();
        bool isValid = PrimaryHotkey?.IsValid == true;
        SetValidationState(isValid, isValid ? null : "Choose a valid hotkey.");
        return isValid;
    }

    public void RefreshFromHotkeyItems()
    {
        SyncFromHotkeyItems();
        DetectConflict();
        SetValidationState(PrimaryHotkey?.IsValid == true, PrimaryHotkey?.IsValid == true ? null : "Choose a valid hotkey.");
    }

    private void SyncFromHotkeyItems()
    {
        if (_syncingHotkeyItems)
        {
            return;
        }

        _syncingHotkeyItems = true;

        try
        {
            HotkeyItemViewModel? primaryItem = PrimaryHotkeys.FirstOrDefault();
            if (primaryItem != null)
            {
                PrimaryHotkey = CloneHotkey(primaryItem.Model.HotkeyInfo);
                primaryItem.Refresh();
            }

            for (int i = 0; i < SecondaryHotkeyItems.Count && i < SecondaryHotkeys.Count; i++)
            {
                SecondaryHotkeyItems[i].Refresh();
                SecondaryHotkeys[i] = SecondaryHotkeys[i] with { Hotkey = CloneHotkey(SecondaryHotkeyItems[i].Model.HotkeyInfo) };
            }
        }
        finally
        {
            _syncingHotkeyItems = false;
        }
    }

    private static void SetHotkeyItem(HotkeyItemViewModel? item, HotkeyInfo hotkey)
    {
        if (item == null)
        {
            return;
        }

        item.Model.HotkeyInfo = CloneHotkey(hotkey);
        item.Refresh();
    }

    private static HotkeyInfo CloneHotkey(HotkeyInfo hotkey)
    {
        return new HotkeyInfo(hotkey.Key, hotkey.Modifiers);
    }

    partial void OnPrimaryHotkeyChanged(HotkeyInfo? value)
    {
        if (!_syncingHotkeyItems && value != null)
        {
            SetHotkeyItem(PrimaryHotkeys.FirstOrDefault(), value);
        }

        string displayValue = value?.ToString() ?? string.Empty;
        if (!string.Equals(PrimaryHotkeyText, displayValue, StringComparison.Ordinal))
        {
            _syncingHotkeyText = true;
            PrimaryHotkeyText = displayValue;
            _syncingHotkeyText = false;
        }

        if (!_syncingHotkeyItems)
        {
            DetectConflict();
            SetValidationState(value?.IsValid == true, value?.IsValid == true ? null : "Choose a valid hotkey.");
        }
    }

    partial void OnPrimaryHotkeyTextChanged(string value)
    {
        if (_syncingHotkeyText)
        {
            return;
        }

        HotkeyInfo? parsedHotkey = ParseHotkey(value);
        if (!Equals(PrimaryHotkey, parsedHotkey))
        {
            PrimaryHotkey = parsedHotkey;
        }
    }

    partial void OnConflictMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasConflict));
    }

    private static HotkeyInfo? ParseHotkey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] parts = value.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        KeyModifiers modifiers = KeyModifiers.None;
        Key key = Key.None;

        foreach (string part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "alt":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "shift":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= KeyModifiers.Meta;
                    break;
                default:
                    if (string.Equals(part, "Print Screen", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Key.PrintScreen;
                        break;
                    }

                    if (string.Equals(part, "Page Up", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Key.PageUp;
                        break;
                    }

                    if (string.Equals(part, "Page Down", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Key.PageDown;
                        break;
                    }

                    if (string.Equals(part, "Num Lock", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Key.NumLock;
                        break;
                    }

                    if (string.Equals(part, "Scroll Lock", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Key.Scroll;
                        break;
                    }

                    if (Enum.TryParse(part, true, out Key parsedKey))
                    {
                        key = parsedKey;
                    }
                    break;
            }
        }

        if (key == Key.None)
        {
            return null;
        }

        return new HotkeyInfo(key, modifiers);
    }
}
