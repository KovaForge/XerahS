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

using System.Security;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.MacOS.Services;

public sealed class MacOSStartupService : IStartupService
{
    private readonly string _launchAgentsFolder;
    private readonly string _plistPath;

    public MacOSStartupService()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        _launchAgentsFolder = Path.Combine(home, "Library", "LaunchAgents");
        _plistPath = Path.Combine(_launchAgentsFolder, "com.xerahs.app.startup.plist");
    }

    public bool IsRunAtStartupEnabled()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            if (!File.Exists(_plistPath))
            {
                return false;
            }

            string content = File.ReadAllText(_plistPath);
            string processPath = Environment.ProcessPath ?? string.Empty;
            return content.Contains(processPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "MacOSStartupService: Failed to query startup state.");
            return false;
        }
    }

    public bool SetRunAtStartup(bool enable)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return !enable;
        }

        try
        {
            if (enable)
            {
                string? processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    return false;
                }

                Directory.CreateDirectory(_launchAgentsFolder);
                File.WriteAllText(_plistPath, BuildPlist(processPath));
            }
            else if (File.Exists(_plistPath))
            {
                File.Delete(_plistPath);
            }

            return IsRunAtStartupEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "MacOSStartupService: Failed to update startup state.");
            return false;
        }
    }

    private static string BuildPlist(string processPath)
    {
        string escapedProcessPath = SecurityElement.Escape(processPath) ?? processPath;
        return
$"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>com.xerahs.app.startup</string>
  <key>ProgramArguments</key>
  <array>
    <string>{escapedProcessPath}</string>
    <string>-silent</string>
  </array>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <false/>
</dict>
</plist>
""";
    }
}
