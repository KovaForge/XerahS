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

    /// <summary>
    /// Returns the list of region selector options to show in the UI. On Linux this always
    /// includes Automatic first and the XerahS overlay crosshair when the platform reports
    /// it as available, so the dropdown never omits those options.
    /// </summary>
    public static IReadOnlyList<LinuxInteractiveRegionSelectorPreference> GetVisiblePreferences(
        LinuxRegionSelectorDiagnostics? diagnostics)
    {
        if (!OperatingSystem.IsLinux())
        {
            return System.Enum.GetValues<LinuxInteractiveRegionSelectorPreference>();
        }

        if (diagnostics?.AvailablePreferences is { Count: > 0 } availablePreferences)
        {
            return BuildVisiblePreferencesWithAutomaticAndOverlay(diagnostics, availablePreferences);
        }

        return FallbackVisiblePreferences;
    }

    /// <summary>
    /// Ensures Automatic is first and XerahS overlay is included when the platform
    /// lists it or when automatic preference is overlay (session supports it), so the
    /// region selector dropdown always shows them when supported.
    /// </summary>
    private static IReadOnlyList<LinuxInteractiveRegionSelectorPreference> BuildVisiblePreferencesWithAutomaticAndOverlay(
        LinuxRegionSelectorDiagnostics diagnostics,
        IReadOnlyList<LinuxInteractiveRegionSelectorPreference> availablePreferences)
    {
        var result = new List<LinuxInteractiveRegionSelectorPreference>(availablePreferences.Count + 1);

        result.Add(LinuxInteractiveRegionSelectorPreference.Automatic);

        bool includeOverlay = availablePreferences.Contains(LinuxInteractiveRegionSelectorPreference.XerahSOverlay) ||
                              diagnostics.AutomaticPreference == LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
        if (includeOverlay)
        {
            result.Add(LinuxInteractiveRegionSelectorPreference.XerahSOverlay);
        }

        foreach (var p in availablePreferences)
        {
            if (p != LinuxInteractiveRegionSelectorPreference.Automatic &&
                p != LinuxInteractiveRegionSelectorPreference.XerahSOverlay)
            {
                result.Add(p);
            }
        }

        return result;
    }

    public static LinuxInteractiveRegionSelectorPreference NormalizeForCurrentSession(
        LinuxInteractiveRegionSelectorPreference preference)
    {
        if (!OperatingSystem.IsLinux() || preference == LinuxInteractiveRegionSelectorPreference.Automatic)
        {
            return preference;
        }

        return NormalizeForCurrentSession(preference, TryGetDiagnostics());
    }

    internal static LinuxInteractiveRegionSelectorPreference NormalizeForCurrentSession(
        LinuxInteractiveRegionSelectorPreference preference,
        LinuxRegionSelectorDiagnostics? diagnostics)
    {
        if (preference == LinuxInteractiveRegionSelectorPreference.Automatic)
        {
            return preference;
        }

        if (diagnostics?.AvailablePreferences is not { Count: > 0 } availablePreferences)
        {
            return preference;
        }

        bool overlayAvailableViaAutomaticPreference =
            preference == LinuxInteractiveRegionSelectorPreference.XerahSOverlay &&
            diagnostics.AutomaticPreference == LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
        if (!availablePreferences.Contains(preference) && !overlayAvailableViaAutomaticPreference)
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
