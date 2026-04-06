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

#endregion

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.Platform.Abstractions;
using XerahS.UI.Onboarding.Controls;
using XerahS.UI.Onboarding.ViewModels.Steps;
using Key = Avalonia.Input.Key;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace XerahS.UI.Onboarding.Steps;

/// <summary>
/// Step 3: Hotkey Configuration view.
/// </summary>
public partial class HotkeyStepView : UserControl
{
    public HotkeyStepView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        WireHotkeyRecorder();
    }

    private void WireHotkeyRecorder()
    {
        if (PrimaryHotkeyRecorder == null) return;
        if (DataContext is not HotkeyStepViewModel vm) return;

        // Wire the recorder's HotkeyChanged event to sync Value → ViewModel
        PrimaryHotkeyRecorder.HotkeyChanged -= OnRecorderHotkeyChanged;
        PrimaryHotkeyRecorder.HotkeyChanged += OnRecorderHotkeyChanged;

        // Sync ViewModel.PrimaryHotkey → Recorder.Value
        if (vm.PrimaryHotkey != null)
        {
            PrimaryHotkeyRecorder.Value = vm.PrimaryHotkey.ToString();
        }
    }

    private void OnRecorderHotkeyChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not HotkeyRecorder recorder) return;
        if (DataContext is not HotkeyStepViewModel vm) return;

        var hotkeyString = recorder.Value;
        if (string.IsNullOrEmpty(hotkeyString))
        {
            vm.PrimaryHotkey = null;
            return;
        }

        // Parse the hotkey string back to HotkeyInfo
        var hotkey = ParseHotkeyString(hotkeyString);
        vm.PrimaryHotkey = hotkey;
    }

    private static HotkeyInfo? ParseHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrEmpty(hotkeyString)) return null;

        var parts = hotkeyString.Split(" + ");
        if (parts.Length == 0) return null;

        var modifiers = KeyModifiers.None;
        Key key = Key.None;

        foreach (var part in parts)
        {
            var normalized = part.Trim();
            switch (normalized.ToLowerInvariant())
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
                    // Try to parse as a key
                    if (Enum.TryParse<Key>(normalized, true, out var parsedKey))
                    {
                        key = parsedKey;
                    }
                    break;
            }
        }

        if (key == Key.None) return null;
        return new HotkeyInfo(key, modifiers);
    }
}
