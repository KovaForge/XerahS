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

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool HasConflict
    {
        get => GetValue(HasConflictProperty);
        set => SetValue(HasConflictProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? HotkeyChanged;

    private bool _isRecording;

    public HotkeyRecorder()
    {
        InitializeComponent();
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        PointerPressed += OnPointerPressed;
        ValueProperty.Changed.AddClassHandler<HotkeyRecorder>(OnValueChanged);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (!_isRecording)
        {
            StartRecording();
        }
    }

    private void OnLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_isRecording)
        {
            _isRecording = false;
            UpdateVisualState(false, false);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecording)
        {
            return;
        }

        e.Handled = true;

        Key key = e.Key;
        KeyModifiers modifiers = e.KeyModifiers;

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

        string hotkeyString = BuildHotkeyString(key, modifiers);
        SetRecordedHotkey(hotkeyString);
    }

    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin;
    }

    private static string BuildHotkeyString(Key key, KeyModifiers modifiers)
    {
        List<string> parts = [];

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        string keyName = GetKeyDisplayName(key);
        if (!string.IsNullOrEmpty(keyName))
        {
            parts.Add(keyName);
        }

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
            Key.Left => "Left",
            Key.Up => "Up",
            Key.Right => "Right",
            Key.Down => "Down",
            Key.Delete => "Delete",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Return => "Enter",
            Key.Space => "Space",
            Key.Escape => "Escape",
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

        RoutedEventArgs args = new(HotkeyChangedEvent);
        HotkeyChanged?.Invoke(this, args);
        RaiseEvent(args);
    }

    private void UpdateVisualState(bool recorded, bool conflict)
    {
        Border? border = this.FindControl<Border>("RecorderBorder");
        TextBlock? placeholder = this.FindControl<TextBlock>("PlaceholderText");
        TextBlock? recordedText = this.FindControl<TextBlock>("RecordedText");
        Button? clearButton = this.FindControl<Button>("ClearButton");

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

        if (clearButton != null)
        {
            clearButton.IsVisible = recorded;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        ClearHotkey();
    }

    public void ClearHotkey()
    {
        Value = null;
        HasConflict = false;
        _isRecording = false;
        UpdateVisualState(false, false);
    }

    public void StartRecording()
    {
        _isRecording = true;

        Border? border = this.FindControl<Border>("RecorderBorder");
        TextBlock? placeholder = this.FindControl<TextBlock>("PlaceholderText");
        TextBlock? recordedText = this.FindControl<TextBlock>("RecordedText");
        Button? clearButton = this.FindControl<Button>("ClearButton");

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
        {
            recordedText.IsVisible = false;
        }

        if (clearButton != null)
        {
            clearButton.IsVisible = false;
        }
    }

    public void SetConflict(string message)
    {
        HasConflict = true;
        UpdateVisualState(false, true);
    }

    private void OnValueChanged(HotkeyRecorder recorder, AvaloniaPropertyChangedEventArgs e)
    {
        if (recorder._isRecording)
        {
            return;
        }

        bool hasValue = !string.IsNullOrWhiteSpace(recorder.Value);
        recorder.UpdateVisualState(hasValue, recorder.HasConflict);
    }
}
