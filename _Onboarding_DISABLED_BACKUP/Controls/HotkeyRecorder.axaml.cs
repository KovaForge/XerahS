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

namespace XerahS.UI.Onboarding.Controls;

/// <summary>
/// A control for recording keyboard hotkey combinations.
/// </summary>
public partial class HotkeyRecorder : UserControl
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<HotkeyRecorder, string?>(nameof(Value));

    public static readonly StyledProperty<bool> HasConflictProperty =
        AvaloniaProperty.Register<HotkeyRecorder, bool>(nameof(HasConflict));

    public static readonly RoutedEvent<RoutedEventArgs> HotkeyChangedEvent =
        RoutedEvent.Register<HotkeyRecorder, RoutedEventArgs>(nameof(HotkeyChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// The recorded hotkey as a formatted string (e.g., "Ctrl+Shift+F1").
    /// </summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Whether there is a conflict with this hotkey.
    /// </summary>
    public bool HasConflict
    {
        get => GetValue(HasConflictProperty);
        set => SetValue(HasConflictProperty, value);
    }

    /// <summary>
    /// Fires when the hotkey changes.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? HotkeyChanged;

    private bool _isRecording;

    public HotkeyRecorder()
    {
        InitializeComponent();
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (!_isRecording)
        {
            StartRecording();
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            _isRecording = false;
            UpdateVisualState(false, false);
        }
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (!_isRecording)
            return;

        e.Handled = true;

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        if (IsModifierKey(key))
        {
            return;
        }

        if (key == Key.Escape)
        {
            _isRecording = false;
            UpdateVisualState(false, false);
            return;
        }

        var hotkeyString = BuildHotkeyString(key, modifiers);
        SetRecordedHotkey(hotkeyString);
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin ||
               key == Key.LeftAlt || key == Key.RightAlt;
    }

    private string BuildHotkeyString(Key key, KeyModifiers modifiers)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");

        var keyName = GetKeyDisplayName(key);
        if (!string.IsNullOrEmpty(keyName))
            parts.Add(keyName);

        return string.Join(" + ", parts);
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.PrintScreen => "Print Screen",
            Key.Pause => "Pause",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "Page Up",
            Key.PageDown => "Page Down",
            Key.Left => "←",
            Key.Up => "↑",
            Key.Right => "→",
            Key.Down => "↓",
            Key.Delete => "Delete",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Return => "Enter",
            Key.Space => "Space",
            Key.Escape => "Escape",
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",
            Key.NumLock => "Num Lock",
            Key.Scroll => "Scroll Lock",
            Key.Sleep => "Sleep",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemQuestion => "/",
            Key.OemTilde => "`",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemBackslash => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            _ => key.ToString()
        };
    }

    private void SetRecordedHotkey(string hotkey)
    {
        Value = hotkey;

        UpdateVisualState(true, false);

        _isRecording = false;

        var args = new RoutedEventArgs(HotkeyChangedEvent);
        HotkeyChanged?.Invoke(this, args);
        RaiseEvent(args);
    }

    private void UpdateVisualState(bool recorded, bool conflict)
    {
        var border = this.FindControl<Border>("RecorderBorder");
        var placeholder = this.FindControl<TextBlock>("PlaceholderText");
        var recordedText = this.FindControl<TextBlock>("RecordedText");
        var clearBtn = this.FindControl<Button>("ClearButton");

        if (border != null)
        {
            border.Classes.Set("wiz-hotkey-recorder-recording", !recorded && _isRecording);
            border.Classes.Set("wiz-hotkey-recorder-recorded", recorded && !conflict);
            border.Classes.Set("wiz-hotkey-recorder-conflict", conflict);
        }

        if (placeholder != null)
        {
            placeholder.IsVisible = !recorded && !_isRecording;
        }

        if (recordedText != null)
        {
            recordedText.Text = Value;
            recordedText.IsVisible = recorded;
        }

        if (clearBtn != null)
        {
            clearBtn.IsVisible = recorded;
        }
    }

    private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ClearHotkey();
    }

    /// <summary>
    /// Clears the recorded hotkey and returns to idle state.
    /// </summary>
    public void ClearHotkey()
    {
        Value = null;
        HasConflict = false;
        _isRecording = false;
        UpdateVisualState(false, false);
    }

    /// <summary>
    /// Starts recording a new hotkey.
    /// </summary>
    public void StartRecording()
    {
        _isRecording = true;

        var border = this.FindControl<Border>("RecorderBorder");
        var placeholder = this.FindControl<TextBlock>("PlaceholderText");
        var recordedText = this.FindControl<TextBlock>("RecordedText");
        var clearBtn = this.FindControl<Button>("ClearButton");

        if (border != null)
        {
            border.Classes.Set("wiz-hotkey-recorder-recording", true);
            border.Classes.Set("wiz-hotkey-recorder-recorded", false);
            border.Classes.Set("wiz-hotkey-recorder-conflict", false);
        }

        if (placeholder != null)
        {
            placeholder.Text = "Press keys...";
            placeholder.IsVisible = true;
        }

        if (recordedText != null)
            recordedText.IsVisible = false;

        if (clearBtn != null)
            clearBtn.IsVisible = false;
    }

    /// <summary>
    /// Sets the conflict state with a message.
    /// </summary>
    public void SetConflict(string message)
    {
        HasConflict = true;
        UpdateVisualState(false, true);
    }
}
