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

using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Core;
using XerahS.Platform.Linux.Services;

namespace XerahS.UI.ViewModels;

public partial class SettingsViewModel
{
    [ObservableProperty]
    private bool _showLinuxClipboardCliWarning;

    [ObservableProperty]
    private string? _linuxClipboardCliWarningText;

    public bool PersistClipboardAfterExit
    {
        get => SettingsManager.Settings.PersistClipboardAfterExit ?? ResolveDefaultPersistClipboardAfterExit();
        set
        {
            bool current = SettingsManager.Settings.PersistClipboardAfterExit ?? ResolveDefaultPersistClipboardAfterExit();
            if (current == value && SettingsManager.Settings.PersistClipboardAfterExit.HasValue)
            {
                return;
            }

            SettingsManager.Settings.PersistClipboardAfterExit = value;
            OnPropertyChanged();
        }
    }

    private void RefreshLinuxClipboardDiagnostics()
    {
        if (!OperatingSystem.IsLinux())
        {
            ShowLinuxClipboardCliWarning = false;
            LinuxClipboardCliWarningText = null;
            return;
        }

        LinuxClipboardCliWarningText = LinuxClipboardCapabilities.UserFacingWarning;
        ShowLinuxClipboardCliWarning = !LinuxClipboardCapabilities.CliClipboardHealthy;
    }

    private static bool ResolveDefaultPersistClipboardAfterExit()
    {
        bool isWayland = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                         string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);
        return isWayland;
    }
}
