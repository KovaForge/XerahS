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
/// Desktop facts platform-neutral code needs (XIP0088). The Linux implementation is the single
/// source of truth for Omarchy and Hyprland detection.
/// </summary>
public interface IDesktopEnvironmentProfile
{
    /// <summary>Omarchy is installed: <c>OMARCHY_PATH</c> is set or <c>/usr/share/omarchy</c> exists.</summary>
    bool IsOmarchy { get; }

    /// <summary>A Hyprland session: <c>HYPRLAND_INSTANCE_SIGNATURE</c> is set.</summary>
    bool IsHyprland { get; }

    bool IsWayland { get; }

    /// <summary>Hyprland and the OmaSnap host-mode probe succeeded.</summary>
    bool IsOmarchyLike { get; }

    /// <summary>One line for diagnostics.</summary>
    string Describe();
}
