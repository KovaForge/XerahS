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
using XerahS.Common;
using XerahS.Core.Managers;
using XerahS.Core.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.CLI.Commands;

public class BackupSettingsCommand : Command
{
    private const string SecurityWarning =
        "The backup is UNENCRYPTED and may contain plaintext passwords, S3 keys, and OAuth tokens. Protect it like a password vault.";

    public BackupSettingsCommand() : base("backup-settings", "Create a portable settings backup file")
    {
        var outputOption = new Option<string?>("--output")
        {
            Description = "Output .xerahsbackup file. Defaults to the current directory."
        };
        Add(outputOption);
        this.SetAction(parseResult =>
        {
            Environment.ExitCode = Execute(parseResult.GetValue(outputOption));
        });
    }

    public static Command Create() => new BackupSettingsCommand();

    internal static int Execute(
        string? outputFilePath = null,
        Action? initializeProviders = null,
        Func<string, PortableSettingsBackupResult>? createBackup = null)
    {
        initializeProviders ??= InitializeProviders;
        createBackup ??= PortableSettingsBackupService.Create;
        outputFilePath = string.IsNullOrWhiteSpace(outputFilePath)
            ? Path.Combine(Environment.CurrentDirectory, $"XerahS-Settings-{DateTime.Now:yyyyMMdd-HHmmss}.{PortableSettingsBackupService.FileExtension}")
            : Path.GetFullPath(outputFilePath);

        try
        {
            initializeProviders();
            Console.Error.WriteLine($"[WARNING] {SecurityWarning}");
            PortableSettingsBackupResult result = createBackup(outputFilePath);
            Console.WriteLine($"[SUCCESS] Portable settings backup created: {result.FilePath}");
            Console.WriteLine($"Included {result.FileCount} file(s) and {result.SecretCount} plaintext secret value(s).");
            foreach (string warning in result.Warnings)
            {
                Console.Error.WriteLine($"[WARNING] {warning}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to create settings backup: {ex.Message}");
            return 1;
        }
    }

    private static void InitializeProviders()
    {
        ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders();
        ProviderCatalog.LoadPlugins(PathsManager.GetPluginDirectories());
        InstanceManager.Instance.MigrateSecretsIfNeeded();
    }
}
