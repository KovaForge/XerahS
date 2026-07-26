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

namespace XerahS.Platform.Linux.Input.Evdev;

/// <summary>
/// Subset of Linux <c>input-event-codes.h</c> constants required for global hotkey
/// detection via raw evdev devices. Values intentionally match the kernel ABI.
/// </summary>
internal static class InputEventCodes
{
    // Event types (EV_*)
    public const ushort EV_SYN = 0x00;
    public const ushort EV_KEY = 0x01;
    public const ushort EV_REL = 0x02;
    public const ushort EV_ABS = 0x03;

    // Synchronisation events (SYN_*)
    public const ushort SYN_REPORT = 0;
    public const ushort SYN_DROPPED = 3;

    // Relative axes (REL_*)
    public const ushort REL_X = 0x00;
    public const ushort REL_Y = 0x01;

    // Absolute axes (ABS_*)
    public const ushort ABS_X = 0x00;
    public const ushort ABS_Y = 0x01;
    public const ushort ABS_MT_POSITION_X = 0x35;
    public const ushort ABS_MT_POSITION_Y = 0x36;

    // Mouse / pointer buttons (BTN_*)
    public const ushort BTN_LEFT = 0x110;
    public const ushort BTN_RIGHT = 0x111;
    public const ushort BTN_MIDDLE = 0x112;
    public const ushort BTN_TASK = 0x117;
    public const ushort BTN_TOUCH = 0x14a;

    // Modifier keys (KEY_*). These are tracked to build hotkey combinations.
    public const ushort KEY_LEFTCTRL = 29;
    public const ushort KEY_RIGHTCTRL = 97;
    public const ushort KEY_LEFTSHIFT = 42;
    public const ushort KEY_RIGHTSHIFT = 54;
    public const ushort KEY_LEFTALT = 56;
    public const ushort KEY_RIGHTALT = 100;
    public const ushort KEY_LEFTMETA = 125;
    public const ushort KEY_RIGHTMETA = 126;

    // Keys frequently referenced directly.
    public const ushort KEY_ESC = 1;
    public const ushort KEY_ENTER = 28;
    public const ushort KEY_SYSRQ = 99; // PrintScreen

    /// <summary>
    /// The highest key code the kernel can report (KEY_MAX is 0x2ff = 767).
    /// </summary>
    public const int KeyMax = 767;

    public static bool IsMouseButton(ushort code) => code >= BTN_LEFT && code <= BTN_TASK;

    public static bool IsModifierKey(ushort code) =>
        code == KEY_LEFTCTRL || code == KEY_RIGHTCTRL ||
        code == KEY_LEFTSHIFT || code == KEY_RIGHTSHIFT ||
        code == KEY_LEFTALT || code == KEY_RIGHTALT ||
        code == KEY_LEFTMETA || code == KEY_RIGHTMETA;
}
