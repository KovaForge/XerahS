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
using XerahS.Core.Managers;

namespace XerahS.CLI.Commands;

public sealed class RestoreSettingsCommand : Command
{
    public RestoreSettingsCommand() : base("restore-settings", "Restore a portable settings backup file")
    {
        var inputOption = new Option<string?>("--input")
        {
            Description = $"Input .{PortableSettingsBackupService.FileExtension} file."
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Acknowledge replacement of current settings and plaintext secrets."
        };
        Add(inputOption);
        Add(forceOption);
        this.SetAction(parseResult =>
        {
            Environment.ExitCode = Execute(parseResult.GetValue(inputOption), parseResult.GetValue(forceOption));
        });
    }

    public static Command Create() => new RestoreSettingsCommand();

    internal static int Execute(
        string? inputFilePath,
        bool force,
        Func<string, PortableSettingsRestoreResult>? restoreBackup = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath))
        {
            Console.Error.WriteLine("[ERROR] --input is required.");
            return 2;
        }

        if (!force)
        {
            Console.Error.WriteLine("[ERROR] Restore replaces current settings and imports plaintext credentials. Re-run with --force to continue.");
            return 2;
        }

        restoreBackup ??= PortableSettingsBackupService.Restore;
        try
        {
            PortableSettingsRestoreResult result = restoreBackup(Path.GetFullPath(inputFilePath));
            Console.WriteLine($"[SUCCESS] Restored settings from: {result.FilePath}");
            Console.WriteLine($"Restored {result.FileCount} file(s) and re-encrypted {result.SecretCount} secret value(s) for this computer.");
            Console.WriteLine("Restart XerahS before using restored workflows or destinations.");
            foreach (string warning in result.Warnings)
            {
                Console.Error.WriteLine($"[WARNING] {warning}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to restore settings: {ex.Message}");
            return 1;
        }
    }
}
