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
using System.CommandLine;
using System.CommandLine.Invocation;
using XerahS.CLI;
using XerahS.Core;

namespace XerahS.CLI.Commands
{
    public class BackupSettingsCommand : Command
    {
        public BackupSettingsCommand() : base("backup-settings", "Force a backup of application settings")
        {
            this.SetAction((parseResult) =>
            {
                Environment.ExitCode = Execute();
            });
        }

        public static Command Create()
        {
            return new BackupSettingsCommand();
        }

        internal static int Execute(
            Action? loadInitialSettings = null,
            Action? saveAllSettings = null,
            Func<string>? getBackupFolder = null)
        {
            loadInitialSettings ??= SettingsManager.LoadInitialSettings;
            saveAllSettings ??= SettingsManager.SaveAllSettings;
            getBackupFolder ??= () => SettingsManager.BackupFolder;

            try
            {
                Console.WriteLine("Loading settings...");
                loadInitialSettings();

                Console.WriteLine("Backing up settings...");
                saveAllSettings();

                Console.WriteLine($"[SUCCESS] Settings backed up to: {getBackupFolder()}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Failed to backup settings: {ex.Message}");
                return 1;
            }
        }
    }
}
