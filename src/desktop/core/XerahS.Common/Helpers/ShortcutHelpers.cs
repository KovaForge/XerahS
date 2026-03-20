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

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XerahS.Common.Helpers
{
    [SupportedOSPlatform("windows")]
    public static class ShortcutHelpers
    {
        public static bool SetShortcut(
            bool create,
            Environment.SpecialFolder specialFolder,
            string shortcutName,
            string targetPath,
            string arguments = "",
            string? iconLocation = null,
            string? description = null)
        {
            string shortcutPath = GetShortcutPath(specialFolder, shortcutName);
            if (string.IsNullOrEmpty(shortcutPath))
            {
                return false;
            }

            return SetShortcut(create, shortcutPath, targetPath, arguments, iconLocation, description);
        }

        public static bool SetShortcut(
            bool create,
            string shortcutPath,
            string targetPath,
            string arguments = "",
            string? iconLocation = null,
            string? description = null)
        {
            if (string.IsNullOrEmpty(shortcutPath) || string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            try
            {
                if (create)
                {
                    return CreateShortcut(shortcutPath, targetPath, arguments, iconLocation, description);
                }
                else
                {
                    return DeleteShortcut(shortcutPath);
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
                e.ShowError();
            }

            return false;
        }

        public static bool CheckShortcut(
            Environment.SpecialFolder specialFolder,
            string shortcutName,
            string targetPath,
            string arguments = "")
        {
            string shortcutPath = GetShortcutPath(specialFolder, shortcutName);
            return CheckShortcut(shortcutPath, targetPath, arguments);
        }

        public static bool CheckShortcut(string shortcutPath, string targetPath, string arguments = "")
        {
            if (!string.IsNullOrEmpty(shortcutPath) && !string.IsNullOrEmpty(targetPath) && File.Exists(shortcutPath))
            {
                try
                {
                    ShortcutInfo? shortcut = GetShortcutInfo(shortcutPath);
                    return shortcut != null &&
                           shortcut.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(shortcut.Arguments.Trim(), arguments.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception e)
                {
                    DebugHelper.WriteException(e);
                }
            }

            return false;
        }

        private static string GetShortcutPath(Environment.SpecialFolder specialFolder, string shortcutName)
        {
            string folderPath = Environment.GetFolderPath(specialFolder);

            if (!shortcutName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                shortcutName += ".lnk";
            }

            return Path.Combine(folderPath, shortcutName);
        }

        private static bool CreateShortcut(
            string shortcutPath,
            string targetPath,
            string arguments = "",
            string? iconLocation = null,
            string? description = null)
        {
            // TODO: [Avalonia] Shortcuts (.lnk) are Windows specific. 
            // Consider alternatives for Linux/macOS (.desktop files / aliases).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!string.IsNullOrEmpty(shortcutPath) && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    DeleteShortcut(shortcutPath);

                    object? shellObject = null;
                    object? shortcutObject = null;

                    try
                    {
                        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            shellObject = Activator.CreateInstance(shellType);
                            dynamic? shell = shellObject;
                            shortcutObject = shell?.CreateShortcut(shortcutPath);
                            dynamic? shortcut = shortcutObject;

                            if (shortcut != null)
                            {
                                shortcut.TargetPath = targetPath;
                                shortcut.Arguments = arguments;
                                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;

                                if (!string.IsNullOrWhiteSpace(iconLocation))
                                {
                                    shortcut.IconLocation = iconLocation;
                                }

                                if (!string.IsNullOrWhiteSpace(description))
                                {
                                    shortcut.Description = description;
                                }

                                shortcut.Save();
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex);
                    }
                    finally
                    {
                        ReleaseComObject(shortcutObject);
                        ReleaseComObject(shellObject);
                    }
                }
            }

            return false;
        }

        public static ShortcutInfo? GetShortcutInfo(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath) || !File.Exists(shortcutPath))
            {
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                object? shellObject = null;
                object? shortcutObject = null;

                try
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        shellObject = Activator.CreateInstance(shellType);
                        dynamic? shell = shellObject;
                        shortcutObject = shell?.CreateShortcut(shortcutPath);
                        dynamic? shortcut = shortcutObject;
                        if (shortcut != null)
                        {
                            return new ShortcutInfo(
                                (string?)shortcut.TargetPath ?? string.Empty,
                                (string?)shortcut.Arguments ?? string.Empty);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex);
                }
                finally
                {
                    ReleaseComObject(shortcutObject);
                    ReleaseComObject(shellObject);
                }
            }

            return null;
        }

        private static bool DeleteShortcut(string shortcutPath)
        {
            if (!string.IsNullOrEmpty(shortcutPath) && File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                return true;
            }

            return false;
        }

        private static void ReleaseComObject(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }

        public sealed record ShortcutInfo(string TargetPath, string Arguments);
    }
}
