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

using Microsoft.Win32;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services;

public sealed class WindowsStartupService : IStartupService
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsRunAtStartupEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath);
            string? value = key?.GetValue(AppResources.AppName) as string;
            return string.Equals(value, GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WindowsStartupService: Failed to query startup state.");
            return false;
        }
    }

    public bool SetRunAtStartup(bool enable)
    {
        if (!OperatingSystem.IsWindows())
        {
            return !enable;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (key == null)
            {
                return false;
            }

            if (enable)
            {
                key.SetValue(AppResources.AppName, GetStartupCommand(), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(AppResources.AppName, false);
            }

            return IsRunAtStartupEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WindowsStartupService: Failed to update startup state.");
            return false;
        }
    }

    private static string GetStartupCommand()
    {
        string processPath = Environment.ProcessPath ?? string.Empty;
        return $"\"{processPath}\" -silent";
    }
}
