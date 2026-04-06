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
using XerahS.Platform.Abstractions;

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
    [ObservableProperty]
    private HotkeyInfo? _primaryHotkey;

    [ObservableProperty]
    private string? _conflictMessage;

    [ObservableProperty]
    private bool _isRecordingHotkey;

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
    }

    private void InitializeDefaults()
    {
        // Default primary hotkey: PrintScreen
        PrimaryHotkey = new HotkeyInfo(Key.PrintScreen, KeyModifiers.None);

        // Default secondary hotkeys
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
        // Check for conflicts with system hotkeys
        ConflictMessage = null;

        if (PrimaryHotkey == null)
        {
            ConflictMessage = "No hotkey configured.";
            return;
        }

        // Check if this is a common system hotkey
        if (IsSystemHotkey(PrimaryHotkey))
        {
            ConflictMessage = "This hotkey may conflict with system shortcuts. Consider using a modifier key (Ctrl, Alt, Shift).";
            return;
        }

        // Check for duplicates within secondary hotkeys
        var allHotkeys = new List<HotkeyInfo> { PrimaryHotkey };
        allHotkeys.AddRange(SecondaryHotkeys.Select(s => s.Hotkey));

        var duplicates = allHotkeys
            .GroupBy(h => new { h.Key, h.Modifiers })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            ConflictMessage = "Duplicate hotkeys detected. Each action should have a unique shortcut.";
        }
    }

    private static bool IsSystemHotkey(HotkeyInfo hotkey)
    {
        // Common system hotkeys to warn about
        var systemHotkeys = new[]
        {
            new { Key = Key.PrintScreen, Modifiers = KeyModifiers.None },
            new { Key = Key.Tab, Modifiers = KeyModifiers.Alt },
            new { Key = Key.F4, Modifiers = KeyModifiers.Alt },
            new { Key = Key.Space, Modifiers = KeyModifiers.Alt },
            new { Key = Key.Escape, Modifiers = KeyModifiers.None },
        };

        return systemHotkeys.Any(sh =>
            hotkey.Key == sh.Key && hotkey.Modifiers == sh.Modifiers);
    }

    public override void LoadFromState(OnboardingState state)
    {
        if (state.PrimaryCaptureHotkey != null)
        {
            PrimaryHotkey = state.PrimaryCaptureHotkey;
        }

        if (state.AdditionalHotkeys.Count > 0)
        {
            // Map saved hotkeys to secondary hotkeys
            for (int i = 0; i < Math.Min(state.AdditionalHotkeys.Count, SecondaryHotkeys.Count); i++)
            {
                var saved = state.AdditionalHotkeys[i];
                var existing = SecondaryHotkeys[i];
                SecondaryHotkeys[i] = existing with { Hotkey = saved };
            }
        }

        DetectConflict();
    }

    public override void SaveToState(OnboardingState state)
    {
        state.PrimaryCaptureHotkey = PrimaryHotkey;
        state.AdditionalHotkeys = SecondaryHotkeys.Select(s => s.Hotkey).ToList();
    }

    public override bool Validate()
    {
        DetectConflict();
        // Allow proceeding even with conflicts, just warn
        return PrimaryHotkey != null && PrimaryHotkey.IsValid;
    }

    partial void OnPrimaryHotkeyChanged(HotkeyInfo? value)
    {
        DetectConflict();
    }
}
