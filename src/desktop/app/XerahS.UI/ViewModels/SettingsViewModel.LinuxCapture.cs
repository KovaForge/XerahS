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

using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.UI.Helpers;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        [ObservableProperty]
        private LinuxInteractiveRegionSelectorPreference _linuxRegionSelectorPreference;

        [ObservableProperty]
        private LinuxRecordingBackendPreference _linuxRecordingBackendPreference;

        [ObservableProperty]
        private string _linuxRegionSelectorCurrentSessionText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorPortalBackendText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorAvailableText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorAutomaticText = string.Empty;

        public bool IsLinuxPlatform => OperatingSystem.IsLinux();

        public IReadOnlyList<LinuxInteractiveRegionSelectorPreference> LinuxRegionSelectorPreferences =>
            LinuxRegionSelectorPreferenceSupport.GetVisiblePreferences();

        public LinuxRecordingBackendPreference[] LinuxRecordingBackendPreferences =>
            Enum.GetValues<LinuxRecordingBackendPreference>();

        private void RefreshLinuxRegionSelectorDiagnostics()
        {
            if (!OperatingSystem.IsLinux())
            {
                LinuxRegionSelectorCurrentSessionText = string.Empty;
                LinuxRegionSelectorPortalBackendText = string.Empty;
                LinuxRegionSelectorAvailableText = string.Empty;
                LinuxRegionSelectorAutomaticText = string.Empty;
                OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
                return;
            }

            var diagnostics = LinuxRegionSelectorPreferenceSupport.TryGetDiagnostics();
            if (diagnostics == null)
            {
                LinuxRegionSelectorCurrentSessionText = "Current session: Linux diagnostics unavailable";
                LinuxRegionSelectorPortalBackendText = "Portal backend: unavailable";
                LinuxRegionSelectorAvailableText = "Available selectors: Automatic (recommended), XerahS overlay crosshair";
                LinuxRegionSelectorAutomaticText = "Automatic will prefer the best available selector at runtime.";
                OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
                return;
            }

            string currentSession = diagnostics.SessionType;
            if (!string.IsNullOrWhiteSpace(diagnostics.Desktop) && diagnostics.Desktop != "Unknown")
            {
                currentSession += $" / {diagnostics.Desktop}";
            }

            if (!string.IsNullOrWhiteSpace(diagnostics.Compositor) && diagnostics.Compositor != "Unknown")
            {
                currentSession += $" / {diagnostics.Compositor}";
            }

            LinuxRegionSelectorCurrentSessionText = $"Current session: {currentSession}";
            LinuxRegionSelectorPortalBackendText = $"Portal backend: {FormatDiagnosticsValue(diagnostics.PortalBackendSummary)}";
            LinuxRegionSelectorAvailableText = $"Available selectors: {string.Join(", ", diagnostics.AvailablePreferences.Select(GetPreferenceDescription))}";
            LinuxRegionSelectorAutomaticText = $"Automatic will prefer: {GetPreferenceDescription(diagnostics.AutomaticPreference)}";
            OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
        }

        private static string GetPreferenceDescription(LinuxInteractiveRegionSelectorPreference preference)
        {
            return preference.GetLocalizedDescription();
        }

        private static string FormatDiagnosticsValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
        }
    }
}
