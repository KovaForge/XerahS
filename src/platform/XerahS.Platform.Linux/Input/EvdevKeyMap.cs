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

using System.Collections.Generic;
using Avalonia.Input;
using XerahS.Platform.Linux.Input.Evdev;

namespace XerahS.Platform.Linux.Input;

/// <summary>
/// Maps Avalonia <see cref="Key"/> values to Linux evdev key codes
/// (<c>input-event-codes.h</c> KEY_* constants) so the hotkey matcher can compare
/// configured hotkeys against raw kernel input events.
/// </summary>
internal static class EvdevKeyMap
{
    /// <summary>
    /// Modifier flag to the set of evdev key codes that satisfy it (left or right variant).
    /// </summary>
    public static readonly IReadOnlyDictionary<KeyModifiers, ushort[]> ModifierCodes =
        new Dictionary<KeyModifiers, ushort[]>
        {
            [KeyModifiers.Control] = new[] { InputEventCodes.KEY_LEFTCTRL, InputEventCodes.KEY_RIGHTCTRL },
            [KeyModifiers.Shift] = new[] { InputEventCodes.KEY_LEFTSHIFT, InputEventCodes.KEY_RIGHTSHIFT },
            [KeyModifiers.Alt] = new[] { InputEventCodes.KEY_LEFTALT, InputEventCodes.KEY_RIGHTALT },
            [KeyModifiers.Meta] = new[] { InputEventCodes.KEY_LEFTMETA, InputEventCodes.KEY_RIGHTMETA },
        };

    /// <summary>
    /// Returns the modifier flag a given evdev key code contributes to, or
    /// <see cref="KeyModifiers.None"/> if the code is not a modifier.
    /// </summary>
    public static KeyModifiers GetModifierFlag(ushort code) => code switch
    {
        InputEventCodes.KEY_LEFTCTRL or InputEventCodes.KEY_RIGHTCTRL => KeyModifiers.Control,
        InputEventCodes.KEY_LEFTSHIFT or InputEventCodes.KEY_RIGHTSHIFT => KeyModifiers.Shift,
        InputEventCodes.KEY_LEFTALT or InputEventCodes.KEY_RIGHTALT => KeyModifiers.Alt,
        InputEventCodes.KEY_LEFTMETA or InputEventCodes.KEY_RIGHTMETA => KeyModifiers.Meta,
        _ => KeyModifiers.None
    };

    /// <summary>
    /// Attempts to map an Avalonia <see cref="Key"/> to its evdev key code.
    /// </summary>
    public static bool TryGetEvdevCode(Key key, out ushort code)
    {
        if (KeyToCode.TryGetValue(key, out code))
        {
            return true;
        }

        code = 0;
        return false;
    }

    private static readonly IReadOnlyDictionary<Key, ushort> KeyToCode = BuildMap();

    private static Dictionary<Key, ushort> BuildMap()
    {
        var map = new Dictionary<Key, ushort>
        {
            // Letters
            [Key.A] = 30, [Key.B] = 48, [Key.C] = 46, [Key.D] = 32, [Key.E] = 18,
            [Key.F] = 33, [Key.G] = 34, [Key.H] = 35, [Key.I] = 23, [Key.J] = 36,
            [Key.K] = 37, [Key.L] = 38, [Key.M] = 50, [Key.N] = 49, [Key.O] = 24,
            [Key.P] = 25, [Key.Q] = 16, [Key.R] = 19, [Key.S] = 31, [Key.T] = 20,
            [Key.U] = 22, [Key.V] = 47, [Key.W] = 17, [Key.X] = 45, [Key.Y] = 21,
            [Key.Z] = 44,

            // Top-row digits
            [Key.D1] = 2, [Key.D2] = 3, [Key.D3] = 4, [Key.D4] = 5, [Key.D5] = 6,
            [Key.D6] = 7, [Key.D7] = 8, [Key.D8] = 9, [Key.D9] = 10, [Key.D0] = 11,

            // Function keys
            [Key.F1] = 59, [Key.F2] = 60, [Key.F3] = 61, [Key.F4] = 62, [Key.F5] = 63,
            [Key.F6] = 64, [Key.F7] = 65, [Key.F8] = 66, [Key.F9] = 67, [Key.F10] = 68,
            [Key.F11] = 87, [Key.F12] = 88, [Key.F13] = 183, [Key.F14] = 184, [Key.F15] = 185,
            [Key.F16] = 186, [Key.F17] = 187, [Key.F18] = 188, [Key.F19] = 189, [Key.F20] = 190,
            [Key.F21] = 191, [Key.F22] = 192, [Key.F23] = 193, [Key.F24] = 194,

            // Numpad digits
            [Key.NumPad0] = 82, [Key.NumPad1] = 79, [Key.NumPad2] = 80, [Key.NumPad3] = 81,
            [Key.NumPad4] = 75, [Key.NumPad5] = 76, [Key.NumPad6] = 77, [Key.NumPad7] = 71,
            [Key.NumPad8] = 72, [Key.NumPad9] = 73,

            // Numpad operators
            [Key.Add] = 78, [Key.Subtract] = 74, [Key.Multiply] = 55, [Key.Divide] = 98,
            [Key.Decimal] = 83,

            // Editing / navigation
            [Key.Escape] = InputEventCodes.KEY_ESC,
            [Key.Tab] = 15,
            [Key.Space] = 57,
            [Key.Enter] = InputEventCodes.KEY_ENTER, // Key.Return shares this value in Avalonia
            [Key.Back] = 14,
            [Key.CapsLock] = 58,
            [Key.NumLock] = 69,
            [Key.Scroll] = 70,
            [Key.Pause] = 119,
            [Key.Insert] = 110,
            [Key.Delete] = 111,
            [Key.Home] = 102,
            [Key.End] = 107,
            [Key.PageUp] = 104,
            [Key.PageDown] = 109,
            [Key.Left] = 105,
            [Key.Right] = 106,
            [Key.Up] = 103,
            [Key.Down] = 108,
            [Key.Apps] = 127, // Menu / compose

            // PrintScreen reports as SYSRQ on Linux
            [Key.PrintScreen] = InputEventCodes.KEY_SYSRQ,
            [Key.Snapshot] = InputEventCodes.KEY_SYSRQ,

            // OEM / punctuation
            [Key.OemPlus] = 13,   // =
            [Key.OemMinus] = 12,  // -
            [Key.OemComma] = 51,  // ,
            [Key.OemPeriod] = 52, // .
            [Key.Oem1] = 39,      // ;
            [Key.Oem2] = 53,      // /
            [Key.Oem3] = 41,      // `
            [Key.Oem4] = 26,      // [
            [Key.Oem5] = 43,      // \
            [Key.Oem6] = 27,      // ]
            [Key.Oem7] = 40,      // '
            [Key.Oem102] = 86,    // 102nd key (ISO backslash)
        };

        return map;
    }
}
