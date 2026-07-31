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

using System.Runtime.InteropServices;
using Avalonia.Input;

namespace XerahS.Platform.MacOS.Native
{
    /// <summary>
    /// P/Invoke bindings for the Carbon hot-key API (XIP0078 P4).
    /// Carbon RegisterEventHotKey registers global hotkeys with no TCC permission and natively
    /// suppresses the registered combo - unlike CGEventTap-based hooks which require Accessibility.
    /// Carbon is deprecated-but-stable; it is the same API mainstream macOS hotkey utilities ship on.
    /// </summary>
    internal static class CarbonHotkeys
    {
        private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";

        internal const int NoErr = 0;

        /// <summary>Another application already owns this hotkey combo.</summary>
        internal const int EventHotKeyExistsErr = -9878;

        internal const int EventNotHandledErr = -9874;

        internal const uint EventClassKeyboard = 0x6B657962; // 'keyb'
        internal const uint EventHotKeyPressed = 5;          // kEventHotKeyPressed
        internal const uint ParamDirectObject = 0x2D2D2D2D;  // kEventParamDirectObject '----'
        internal const uint TypeEventHotKeyID = 0x686B6964;  // typeEventHotKeyID 'hkid'

        // Carbon modifier masks (Events.h)
        internal const uint CmdKey = 0x0100;
        internal const uint ShiftKey = 0x0200;
        internal const uint OptionKey = 0x0800;
        internal const uint ControlKey = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct EventHotKeyID
        {
            public uint Signature;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EventTypeSpec
        {
            public uint EventClass;
            public uint EventKind;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int EventHandlerProc(IntPtr handlerCallRef, IntPtr eventRef, IntPtr userData);

        [DllImport(CarbonLib)]
        internal static extern IntPtr GetApplicationEventTarget();

        [DllImport(CarbonLib)]
        internal static extern int InstallEventHandler(
            IntPtr target,
            EventHandlerProc handler,
            nuint numTypes,
            ref EventTypeSpec typeList,
            IntPtr userData,
            out IntPtr handlerRef);

        [DllImport(CarbonLib)]
        internal static extern int RemoveEventHandler(IntPtr handlerRef);

        [DllImport(CarbonLib)]
        internal static extern int RegisterEventHotKey(
            uint keyCode,
            uint modifiers,
            EventHotKeyID hotKeyId,
            IntPtr target,
            uint options,
            out IntPtr hotKeyRef);

        [DllImport(CarbonLib)]
        internal static extern int UnregisterEventHotKey(IntPtr hotKeyRef);

        [DllImport(CarbonLib)]
        internal static extern int GetEventParameter(
            IntPtr eventRef,
            uint name,
            uint desiredType,
            IntPtr actualType,
            nuint bufferSize,
            IntPtr actualSize,
            out EventHotKeyID data);

        /// <summary>
        /// Maps Avalonia modifier flags to the Carbon modifier mask.
        /// </summary>
        internal static uint MapModifiers(KeyModifiers modifiers)
        {
            uint carbonModifiers = 0;
            if (modifiers.HasFlag(KeyModifiers.Control)) carbonModifiers |= ControlKey;
            if (modifiers.HasFlag(KeyModifiers.Alt)) carbonModifiers |= OptionKey;
            if (modifiers.HasFlag(KeyModifiers.Shift)) carbonModifiers |= ShiftKey;
            if (modifiers.HasFlag(KeyModifiers.Meta)) carbonModifiers |= CmdKey;
            return carbonModifiers;
        }

        /// <summary>
        /// Maps an Avalonia key to a Carbon virtual key code (kVK_* from HIToolbox/Events.h).
        /// Returns false for keys with no ANSI-layout Carbon equivalent (e.g. PrintScreen);
        /// those combos fall back to the SharpHook path.
        /// </summary>
        internal static bool TryMapKey(Key key, out uint keyCode)
        {
            uint? mapped = key switch
            {
                Key.A => 0x00,
                Key.S => 0x01,
                Key.D => 0x02,
                Key.F => 0x03,
                Key.H => 0x04,
                Key.G => 0x05,
                Key.Z => 0x06,
                Key.X => 0x07,
                Key.C => 0x08,
                Key.V => 0x09,
                Key.B => 0x0B,
                Key.Q => 0x0C,
                Key.W => 0x0D,
                Key.E => 0x0E,
                Key.R => 0x0F,
                Key.Y => 0x10,
                Key.T => 0x11,
                Key.D1 => 0x12,
                Key.D2 => 0x13,
                Key.D3 => 0x14,
                Key.D4 => 0x15,
                Key.D6 => 0x16,
                Key.D5 => 0x17,
                Key.OemPlus => 0x18,       // kVK_ANSI_Equal
                Key.D9 => 0x19,
                Key.D7 => 0x1A,
                Key.OemMinus => 0x1B,      // kVK_ANSI_Minus
                Key.D8 => 0x1C,
                Key.D0 => 0x1D,
                Key.Oem6 => 0x1E,          // kVK_ANSI_RightBracket
                Key.O => 0x1F,
                Key.U => 0x20,
                Key.Oem4 => 0x21,          // kVK_ANSI_LeftBracket
                Key.I => 0x22,
                Key.P => 0x23,
                Key.Return => 0x24,
                Key.L => 0x25,
                Key.J => 0x26,
                Key.Oem7 => 0x27,          // kVK_ANSI_Quote
                Key.K => 0x28,
                Key.Oem1 => 0x29,          // kVK_ANSI_Semicolon
                Key.Oem5 => 0x2A,          // kVK_ANSI_Backslash
                Key.OemComma => 0x2B,
                Key.Oem2 => 0x2C,          // kVK_ANSI_Slash
                Key.N => 0x2D,
                Key.M => 0x2E,
                Key.OemPeriod => 0x2F,
                Key.Tab => 0x30,
                Key.Space => 0x31,
                Key.Oem3 => 0x32,          // kVK_ANSI_Grave
                Key.Back => 0x33,          // kVK_Delete (backspace)
                Key.Escape => 0x35,
                Key.Decimal => 0x41,       // kVK_ANSI_KeypadDecimal
                Key.Multiply => 0x43,
                Key.Add => 0x45,
                Key.Clear => 0x47,
                Key.Divide => 0x4B,
                Key.Subtract => 0x4E,
                Key.NumPad0 => 0x52,
                Key.NumPad1 => 0x53,
                Key.NumPad2 => 0x54,
                Key.NumPad3 => 0x55,
                Key.NumPad4 => 0x56,
                Key.NumPad5 => 0x57,
                Key.NumPad6 => 0x58,
                Key.NumPad7 => 0x59,
                Key.NumPad8 => 0x5B,
                Key.NumPad9 => 0x5C,
                Key.F1 => 0x7A,
                Key.F2 => 0x78,
                Key.F3 => 0x63,
                Key.F4 => 0x76,
                Key.F5 => 0x60,
                Key.F6 => 0x61,
                Key.F7 => 0x62,
                Key.F8 => 0x64,
                Key.F9 => 0x65,
                Key.F10 => 0x6D,
                Key.F11 => 0x67,
                Key.F12 => 0x6F,
                Key.F13 => 0x69,
                Key.F14 => 0x6B,
                Key.F15 => 0x71,
                Key.F16 => 0x6A,
                Key.F17 => 0x40,
                Key.F18 => 0x4F,
                Key.F19 => 0x50,
                Key.F20 => 0x5A,
                Key.Insert => 0x72,        // kVK_Help (Insert position on full-size keyboards)
                Key.Delete => 0x75,        // kVK_ForwardDelete
                Key.Home => 0x73,
                Key.End => 0x77,
                Key.PageUp => 0x74,
                Key.PageDown => 0x79,
                Key.Left => 0x7B,
                Key.Right => 0x7C,
                Key.Down => 0x7D,
                Key.Up => 0x7E,
                _ => null
            };

            keyCode = mapped ?? 0;
            return mapped.HasValue;
        }
    }
}
