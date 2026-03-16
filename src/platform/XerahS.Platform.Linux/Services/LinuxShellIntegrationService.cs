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

using System.Text;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

public sealed class LinuxShellIntegrationService : IShellIntegrationService
{
    private const string PluginMimeType = "application/x-xerahs-plugin";
    private const string PluginDesktopEntryFileName = "xerahs-xsdp.desktop";
    private const string PluginMimeXmlFileName = "xerahs-xsdp.xml";

    private readonly string _processPath;
    private readonly string _desktopEntryPath;
    private readonly string _mimeXmlPath;
    private readonly string _mimeAppsPath;
    private readonly string[] _contextMenuScriptPaths;
    private readonly string[] _sendToEntryPaths;

    public LinuxShellIntegrationService()
    {
        _processPath = Environment.ProcessPath ?? string.Empty;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
                          Path.Combine(home, ".local", "share");
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                            Path.Combine(home, ".config");

        _desktopEntryPath = Path.Combine(dataHome, "applications", PluginDesktopEntryFileName);
        _mimeXmlPath = Path.Combine(dataHome, "mime", "packages", PluginMimeXmlFileName);
        _mimeAppsPath = Path.Combine(configHome, "mimeapps.list");

        _contextMenuScriptPaths =
        [
            Path.Combine(dataHome, "nautilus", "scripts", "Upload with XerahS"),
            Path.Combine(dataHome, "nemo", "scripts", "Upload with XerahS"),
            Path.Combine(dataHome, "caja", "scripts", "Upload with XerahS")
        ];

        _sendToEntryPaths =
        [
            Path.Combine(dataHome, "kio", "servicemenus", "XerahS.desktop"),
            Path.Combine(dataHome, "Thunar", "sendto", "XerahS.desktop")
        ];
    }

    public bool SupportsPluginExtensionRegistration => OperatingSystem.IsLinux();
    public bool SupportsContextMenuIntegration => OperatingSystem.IsLinux();
    public bool SupportsSendToIntegration => OperatingSystem.IsLinux();

    public bool IsPluginExtensionRegistered()
    {
        if (!SupportsPluginExtensionRegistration)
        {
            return false;
        }

        return File.Exists(_desktopEntryPath) &&
               File.Exists(_mimeXmlPath) &&
               MimeAppsContainsPluginAssociation();
    }

    public void SetPluginExtensionRegistration(bool register)
    {
        if (!SupportsPluginExtensionRegistration)
        {
            return;
        }

        try
        {
            if (register)
            {
                if (string.IsNullOrWhiteSpace(_processPath))
                {
                    return;
                }

                EnsureParentDirectory(_desktopEntryPath);
                EnsureParentDirectory(_mimeXmlPath);
                EnsureParentDirectory(_mimeAppsPath);

                File.WriteAllText(_desktopEntryPath, BuildPluginDesktopEntry(), Encoding.UTF8);
                File.WriteAllText(_mimeXmlPath, BuildPluginMimeXml(), Encoding.UTF8);
                UpdateMimeAppsPluginAssociation(enable: true);
            }
            else
            {
                DeleteIfExists(_desktopEntryPath);
                DeleteIfExists(_mimeXmlPath);
                UpdateMimeAppsPluginAssociation(enable: false);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxShellIntegrationService: Failed to update plugin association.");
        }
    }

    public bool IsContextMenuIntegrationEnabled()
    {
        if (!SupportsContextMenuIntegration)
        {
            return false;
        }

        return _contextMenuScriptPaths.Any(File.Exists);
    }

    public bool SetContextMenuIntegration(bool enable)
    {
        if (!SupportsContextMenuIntegration)
        {
            return !enable;
        }

        try
        {
            if (enable)
            {
                if (string.IsNullOrWhiteSpace(_processPath))
                {
                    return false;
                }

                string content = BuildContextMenuScript();
                foreach (string path in _contextMenuScriptPaths)
                {
                    EnsureParentDirectory(path);
                    File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    TryMakeExecutable(path);
                }
            }
            else
            {
                foreach (string path in _contextMenuScriptPaths)
                {
                    DeleteIfExists(path);
                }
            }

            return IsContextMenuIntegrationEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxShellIntegrationService: Failed to update context menu integration.");
            return false;
        }
    }

    public bool IsSendToIntegrationEnabled()
    {
        if (!SupportsSendToIntegration)
        {
            return false;
        }

        return _sendToEntryPaths.Any(File.Exists);
    }

    public bool SetSendToIntegration(bool enable)
    {
        if (!SupportsSendToIntegration)
        {
            return !enable;
        }

        try
        {
            if (enable)
            {
                if (string.IsNullOrWhiteSpace(_processPath))
                {
                    return false;
                }

                foreach (string path in _sendToEntryPaths)
                {
                    EnsureParentDirectory(path);
                    File.WriteAllText(path, BuildSendToDesktopEntry(), Encoding.UTF8);
                }
            }
            else
            {
                foreach (string path in _sendToEntryPaths)
                {
                    DeleteIfExists(path);
                }
            }

            return IsSendToIntegrationEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "LinuxShellIntegrationService: Failed to update send-to integration.");
            return false;
        }
    }

    private bool MimeAppsContainsPluginAssociation()
    {
        if (!File.Exists(_mimeAppsPath))
        {
            return false;
        }

        string[] lines = File.ReadAllLines(_mimeAppsPath);
        string expected = $"{PluginMimeType}={PluginDesktopEntryFileName};";
        return lines.Any(line => string.Equals(line.Trim(), expected, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateMimeAppsPluginAssociation(bool enable)
    {
        List<string> lines = File.Exists(_mimeAppsPath)
            ? File.ReadAllLines(_mimeAppsPath).ToList()
            : [];

        const string defaultApplicationsSection = "[Default Applications]";
        string targetLinePrefix = $"{PluginMimeType}=";
        string replacementLine = $"{PluginMimeType}={PluginDesktopEntryFileName};";

        int sectionIndex = lines.FindIndex(line => string.Equals(line.Trim(), defaultApplicationsSection, StringComparison.OrdinalIgnoreCase));
        if (sectionIndex < 0 && enable)
        {
            lines.Add(defaultApplicationsSection);
            lines.Add(replacementLine);
        }
        else if (sectionIndex >= 0)
        {
            int insertIndex = sectionIndex + 1;
            int existingIndex = -1;
            for (int i = sectionIndex + 1; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith(']'))
                {
                    break;
                }

                if (trimmed.StartsWith(targetLinePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }

                insertIndex = i + 1;
            }

            if (enable)
            {
                if (existingIndex >= 0)
                {
                    lines[existingIndex] = replacementLine;
                }
                else
                {
                    lines.Insert(insertIndex, replacementLine);
                }
            }
            else if (existingIndex >= 0)
            {
                lines.RemoveAt(existingIndex);
            }
        }

        EnsureParentDirectory(_mimeAppsPath);
        File.WriteAllLines(_mimeAppsPath, lines);
    }

    private string BuildPluginDesktopEntry()
    {
        return
$"""
[Desktop Entry]
Type=Application
Name=XerahS Plugin Installer
Exec="{_processPath}" "%f"
NoDisplay=true
MimeType={PluginMimeType};
""";
    }

    private static string BuildPluginMimeXml()
    {
        return
"""
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
  <mime-type type="application/x-xerahs-plugin">
    <comment>XerahS plugin package</comment>
    <glob pattern="*.xsdp"/>
  </mime-type>
</mime-info>
""";
    }

    private string BuildContextMenuScript()
    {
        return
$"""
#!/usr/bin/env bash
"{_processPath}" "$@"
""";
    }

    private string BuildSendToDesktopEntry()
    {
        return
$"""
[Desktop Entry]
Type=Application
Name=Send to XerahS
Exec="{_processPath}" %F
Icon=xerahs
MimeType=all/allfiles;
NoDisplay=false
""";
    }

    private static void EnsureParentDirectory(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryMakeExecutable(string path)
    {
        try
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }
        catch
        {
            // Ignore chmod failures: script may still be usable depending on file manager behavior.
        }
    }
}
