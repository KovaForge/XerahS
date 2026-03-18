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
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services;

/// <summary>
/// Windows implementation of shell integration services (file extension registration, etc.)
/// </summary>
public sealed class WindowsShellIntegrationService : IShellIntegrationService
{
    private const string UploadContextMenuDisplayName = "Upload with XerahS";
    private const string FilesContextMenuPath = @"Software\Classes\*\shell\XerahSUpload";
    private const string FilesContextMenuCommandPath = @"Software\Classes\*\shell\XerahSUpload\command";
    private const string DirectoryContextMenuPath = @"Software\Classes\Directory\shell\XerahSUpload";
    private const string DirectoryContextMenuCommandPath = @"Software\Classes\Directory\shell\XerahSUpload\command";
    private const string SendToScriptName = "XerahS.cmd";
    private const string SendToFlag = "--send-to";
    private const string SendToScriptMarker = "REM XerahS SendTo Integration";

    private const string ShellPluginExtensionPath = @"Software\Classes\.xsdp";
    private readonly string ShellPluginExtensionValue = $"{AppResources.AppName}.xsdp";
    private readonly string ShellPluginAssociatePath;
    private readonly string ShellPluginAssociateValue;
    private readonly string ShellPluginIconPath;
    private readonly string ShellPluginCommandPath;

    private readonly string ApplicationPath;
    private readonly string ShellPluginIconValue;
    private readonly string ShellPluginCommandValue;

    public WindowsShellIntegrationService()
    {
        ShellPluginAssociatePath = $@"Software\Classes\{ShellPluginExtensionValue}";
        ShellPluginAssociateValue = $"{AppResources.AppName} plugin";
        ShellPluginIconPath = $@"{ShellPluginAssociatePath}\DefaultIcon";
        ShellPluginCommandPath = $@"{ShellPluginAssociatePath}\shell\open\command";

        ApplicationPath = $"\"{Environment.ProcessPath}\"";
        ShellPluginIconValue = $"{ApplicationPath},0"; // Extract icon from .exe
        ShellPluginCommandValue = $"{ApplicationPath} -InstallPlugin \"%1\"";
    }

    public bool SupportsPluginExtensionRegistration => OperatingSystem.IsWindows();
    public bool SupportsContextMenuIntegration => OperatingSystem.IsWindows();
    public bool SupportsSendToIntegration => OperatingSystem.IsWindows();

    /// <summary>
    /// Check if .xsdp file association is registered
    /// </summary>
    public bool IsPluginExtensionRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return CheckRegistryValue(ShellPluginExtensionPath, null, ShellPluginExtensionValue) &&
                   CheckRegistryValue(ShellPluginCommandPath, null, ShellPluginCommandValue);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
            return false;
        }
    }

    /// <summary>
    /// Register or unregister .xsdp file association
    /// </summary>
    public void SetPluginExtensionRegistration(bool register)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (register)
            {
                UnregisterPluginExtension();
                RegisterPluginExtension();
            }
            else
            {
                UnregisterPluginExtension();
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    public bool IsContextMenuIntegrationEnabled()
    {
        if (!SupportsContextMenuIntegration)
            return false;

        try
        {
            string commandValue = $"{ApplicationPath} \"%1\"";
            return CheckRegistryValue(FilesContextMenuPath, null, UploadContextMenuDisplayName) &&
                   CheckRegistryValue(FilesContextMenuCommandPath, null, commandValue) &&
                   CheckRegistryValue(DirectoryContextMenuPath, null, UploadContextMenuDisplayName) &&
                   CheckRegistryValue(DirectoryContextMenuCommandPath, null, commandValue);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return false;
        }
    }

    public bool SetContextMenuIntegration(bool enable)
    {
        if (!SupportsContextMenuIntegration)
            return !enable;

        try
        {
            if (enable)
            {
                string commandValue = $"{ApplicationPath} \"%1\"";
                CreateRegistryKey(FilesContextMenuPath, UploadContextMenuDisplayName);
                CreateRegistryKey(FilesContextMenuPath, "Icon", $"{ApplicationPath},0");
                CreateRegistryKey(FilesContextMenuPath, "MUIVerb", UploadContextMenuDisplayName);
                CreateRegistryKey(FilesContextMenuPath, "Position", "Bottom");
                CreateRegistryKey(FilesContextMenuCommandPath, commandValue);

                CreateRegistryKey(DirectoryContextMenuPath, UploadContextMenuDisplayName);
                CreateRegistryKey(DirectoryContextMenuPath, "Icon", $"{ApplicationPath},0");
                CreateRegistryKey(DirectoryContextMenuPath, "MUIVerb", UploadContextMenuDisplayName);
                CreateRegistryKey(DirectoryContextMenuPath, "Position", "Bottom");
                CreateRegistryKey(DirectoryContextMenuCommandPath, commandValue);
            }
            else
            {
                RemoveRegistryKey(FilesContextMenuPath);
                RemoveRegistryKey(DirectoryContextMenuPath);
            }

            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            return IsContextMenuIntegrationEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return false;
        }
    }

    public bool IsSendToIntegrationEnabled()
    {
        if (!SupportsSendToIntegration)
            return false;

        try
        {
            string scriptPath = GetSendToScriptPath();
            if (!File.Exists(scriptPath))
            {
                return false;
            }

            string content = File.ReadAllText(scriptPath);
            return IsManagedSendToScript(content);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return false;
        }
    }

    public bool SetSendToIntegration(bool enable)
    {
        if (!SupportsSendToIntegration)
            return !enable;

        try
        {
            string scriptPath = GetSendToScriptPath();
            string? directoryPath = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (enable)
            {
                File.WriteAllText(scriptPath, BuildSendToScript());
            }
            else if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }

            return IsSendToIntegrationEnabled() == enable;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
            return false;
        }
    }

    private void RegisterPluginExtension()
    {
        CreateRegistryKey(ShellPluginExtensionPath, ShellPluginExtensionValue);
        CreateRegistryKey(ShellPluginAssociatePath, ShellPluginAssociateValue);
        CreateRegistryKey(ShellPluginIconPath, ShellPluginIconValue);
        CreateRegistryKey(ShellPluginCommandPath, ShellPluginCommandValue);

        // Notify Windows shell of file association change
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        DebugHelper.WriteLine($"Registered .xsdp file association for {AppResources.AppName}");
    }

    private void UnregisterPluginExtension()
    {
        RemoveRegistryKey(ShellPluginExtensionPath);
        RemoveRegistryKey(ShellPluginAssociatePath);

        DebugHelper.WriteLine($"Unregistered .xsdp file association for {AppResources.AppName}");
    }

    // Registry helper methods
    private static void CreateRegistryKey(string path, string value)
    {
        CreateRegistryKey(path, null, value);
    }

    private static void CreateRegistryKey(string path, string? name, string value)
    {
        try
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(path))
            {
                if (rk != null)
                {
                    rk.SetValue(name ?? string.Empty, value, RegistryValueKind.String);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static void RemoveRegistryKey(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static bool CheckRegistryValue(string path, string? name, string value)
    {
        try
        {
            using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(path))
            {
                if (rk != null)
                {
                    string? registryValue = rk.GetValue(name ?? string.Empty) as string;
                    return registryValue != null && registryValue.Equals(value, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }

        return false;
    }

    private static string GetSendToScriptPath()
    {
        string sendToPath = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
        return Path.Combine(sendToPath, SendToScriptName);
    }

    private string BuildSendToScript()
    {
        return
$"""
@echo off
{SendToScriptMarker}
"{Environment.ProcessPath}" {SendToFlag} %*
""";
    }

    private bool IsManagedSendToScript(string content)
    {
        string processPath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        return content.Contains(SendToScriptMarker, StringComparison.OrdinalIgnoreCase) &&
               content.Contains(processPath, StringComparison.OrdinalIgnoreCase) &&
               content.Contains(SendToFlag, StringComparison.OrdinalIgnoreCase);
    }

    // P/Invoke for shell notification
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
