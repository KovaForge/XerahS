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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Describes how global hotkeys are delivered on the current platform/session (XIP0079 P1).
/// </summary>
public enum HotkeyBackendState
{
    /// <summary>Platform-native global hotkeys with no portal involvement (Windows, macOS Carbon, X11 XGrabKey).</summary>
    Native,

    /// <summary>XDG GlobalShortcuts portal session is bound; hotkeys fire while unfocused.</summary>
    PortalBound,

    /// <summary>Portal session is being established or rebound.</summary>
    PortalPending,

    /// <summary>Portal unavailable; X11 grab fallback only fires while XerahS is focused.</summary>
    X11FallbackFocusOnly,

    /// <summary>No working global hotkey backend.</summary>
    Unavailable
}

/// <summary>
/// User-visible hotkey delivery diagnostics for settings UI and troubleshooting reports.
/// </summary>
public sealed record HotkeyDiagnostics(
    HotkeyBackendState State,
    string BackendName,
    string? UserFacingWarning);
