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

using Avalonia.Input;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Hyprland;

/// <summary>
/// Converts XerahS hotkeys to Hyprland key syntax (<c>"SUPER + SHIFT + PRINT"</c>) and to the
/// modmask and key name that <c>hyprctl binds -j</c> reports. Hyprland resolves key names as
/// case-insensitive XKB keysyms, so names are written upper case the way Omarchy writes them.
/// </summary>
public static class HyprlandKeyMap
{
    public const int ShiftMask = 1;
    public const int ControlMask = 4;
    public const int AltMask = 8;
    public const int SuperMask = 64;

    private static readonly Dictionary<Key, string> KeyNames = new()
    {
        { Key.PrintScreen, "PRINT" },
        { Key.Space, "SPACE" },
        { Key.Enter, "RETURN" },
        { Key.Tab, "TAB" },
        { Key.Escape, "ESCAPE" },
        { Key.Back, "BACKSPACE" },
        { Key.Delete, "DELETE" },
        { Key.Insert, "INSERT" },
        { Key.Home, "HOME" },
        { Key.End, "END" },
        { Key.PageUp, "PAGE_UP" },
        { Key.PageDown, "PAGE_DOWN" },
        { Key.Left, "LEFT" },
        { Key.Right, "RIGHT" },
        { Key.Up, "UP" },
        { Key.Down, "DOWN" },
        { Key.Pause, "PAUSE" },
        { Key.Scroll, "SCROLL_LOCK" },
        { Key.CapsLock, "CAPS_LOCK" },
        { Key.NumLock, "NUM_LOCK" },
        { Key.Apps, "MENU" },
        { Key.OemPlus, "EQUAL" },
        { Key.OemMinus, "MINUS" },
        { Key.OemComma, "COMMA" },
        { Key.OemPeriod, "PERIOD" },
        { Key.Oem1, "SEMICOLON" },
        { Key.Oem2, "SLASH" },
        { Key.Oem3, "GRAVE" },
        { Key.Oem4, "BRACKETLEFT" },
        { Key.Oem5, "BACKSLASH" },
        { Key.Oem6, "BRACKETRIGHT" },
        { Key.Oem7, "APOSTROPHE" },
        { Key.Oem102, "LESS" },
        { Key.Divide, "KP_DIVIDE" },
        { Key.Multiply, "KP_MULTIPLY" },
        { Key.Add, "KP_ADD" },
        { Key.Subtract, "KP_SUBTRACT" },
        { Key.Decimal, "KP_DECIMAL" }
    };

    /// <summary>XKB aliases that name the same key, so conflicts are found however a bind spells it.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "PRIOR", "PAGE_UP" },
        { "NEXT", "PAGE_DOWN" },
        { "ENTER", "RETURN" },
        { "ESC", "ESCAPE" },
        { "SYS_REQ", "PRINT" },
        { "SYSREQ", "PRINT" },
        { "KP_PRIOR", "KP_PAGE_UP" },
        { "KP_NEXT", "KP_PAGE_DOWN" }
    };

    /// <summary>
    /// Formats a hotkey as Hyprland keys. False for keys Hyprland cannot name from an XerahS hotkey
    /// (for example mouse buttons or media keys Avalonia does not report), which are skipped.
    /// </summary>
    public static bool TryFormat(HotkeyInfo hotkey, out string keys)
    {
        keys = string.Empty;
        if (!hotkey.IsValid || !TryGetKeyName(hotkey.Key, out string keyName))
        {
            return false;
        }

        var parts = new List<string>(5);
        if (hotkey.HasMeta)
        {
            parts.Add("SUPER");
        }

        if (hotkey.HasControl)
        {
            parts.Add("CTRL");
        }

        if (hotkey.HasAlt)
        {
            parts.Add("ALT");
        }

        if (hotkey.HasShift)
        {
            parts.Add("SHIFT");
        }

        parts.Add(keyName);
        keys = string.Join(" + ", parts);
        return true;
    }

    public static bool TryGetKeyName(Key key, out string name)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            name = ((char)('A' + (key - Key.A))).ToString();
            return true;
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            name = (key - Key.D0).ToString();
            return true;
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            name = "KP_" + (key - Key.NumPad0);
            return true;
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            name = "F" + (key - Key.F1 + 1);
            return true;
        }

        return KeyNames.TryGetValue(key, out name!);
    }

    public static int ToModmask(HotkeyInfo hotkey)
    {
        int mask = 0;
        if (hotkey.HasShift)
        {
            mask |= ShiftMask;
        }

        if (hotkey.HasControl)
        {
            mask |= ControlMask;
        }

        if (hotkey.HasAlt)
        {
            mask |= AltMask;
        }

        if (hotkey.HasMeta)
        {
            mask |= SuperMask;
        }

        return mask;
    }

    /// <summary>Canonical key name for comparisons: upper case with XKB aliases folded.</summary>
    public static string NormalizeKeyName(string key)
    {
        string upper = key.Trim().ToUpperInvariant();
        return Aliases.TryGetValue(upper, out string? canonical) ? canonical : upper;
    }

    /// <summary>Only the modifiers XerahS can express; lock and Mod2 (Num Lock) bits are ignored.</summary>
    public static int NormalizeModmask(int modmask) => modmask & (ShiftMask | ControlMask | AltMask | SuperMask);
}
