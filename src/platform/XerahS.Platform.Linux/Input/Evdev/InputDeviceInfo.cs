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
/// Describes a single <c>/dev/input/event*</c> device discovered during enumeration,
/// including whether the current process can open it for reading.
/// </summary>
internal sealed class InputDeviceInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public bool IsKeyboard { get; init; }
    public bool IsMouse { get; init; }
    public bool IsVirtual { get; init; }

    /// <summary>True when the device could be opened for reading by this process.</summary>
    public bool CanRead { get; init; }

    /// <summary>errno captured when <see cref="CanRead"/> is false (0 otherwise).</summary>
    public int OpenErrno { get; init; }

    public string DeviceType =>
        (IsKeyboard, IsMouse) switch
        {
            (true, true) => "Keyboard+Mouse",
            (true, false) => "Keyboard",
            (false, true) => "Mouse",
            _ => "Other"
        };

    public override string ToString() => $"{Name} ({Path}) [{DeviceType}]";
}
