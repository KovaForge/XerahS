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
using System.Linq;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Helpers;

internal static class LinuxRegionSelectorPreferenceSupport
{
    private static readonly LinuxInteractiveRegionSelectorPreference[] FallbackVisiblePreferences =
    [
        LinuxInteractiveRegionSelectorPreference.Automatic,
        LinuxInteractiveRegionSelectorPreference.XerahSOverlay
    ];

    public static IReadOnlyList<LinuxInteractiveRegionSelectorPreference> GetVisiblePreferences()
    {
        if (!OperatingSystem.IsLinux())
        {
            return System.Enum.GetValues<LinuxInteractiveRegionSelectorPreference>();
        }

        return GetVisiblePreferences(TryGetDiagnostics());
    }

    public static IReadOnlyList<LinuxInteractiveRegionSelectorPreference> GetVisiblePreferences(
        LinuxRegionSelectorDiagnostics? diagnostics)
    {
        if (!OperatingSystem.IsLinux())
        {
            return System.Enum.GetValues<LinuxInteractiveRegionSelectorPreference>();
        }

        if (diagnostics?.AvailablePreferences is { Count: > 0 } availablePreferences)
        {
            return availablePreferences;
        }

        return FallbackVisiblePreferences;
    }

    public static LinuxInteractiveRegionSelectorPreference NormalizeForCurrentSession(
        LinuxInteractiveRegionSelectorPreference preference)
    {
        if (!OperatingSystem.IsLinux() || preference == LinuxInteractiveRegionSelectorPreference.Automatic)
        {
            return preference;
        }

        var diagnostics = TryGetDiagnostics();
        if (diagnostics?.AvailablePreferences is { Count: > 0 } availablePreferences &&
            !availablePreferences.Contains(preference))
        {
            return LinuxInteractiveRegionSelectorPreference.Automatic;
        }

        return preference;
    }

    public static LinuxRegionSelectorDiagnostics? TryGetDiagnostics()
    {
        if (!PlatformServices.IsInitialized || PlatformServices.ScreenCapture is not ILinuxRegionSelectorDiagnosticsProvider provider)
        {
            return null;
        }

        return provider.GetLinuxRegionSelectorDiagnostics();
    }
}
