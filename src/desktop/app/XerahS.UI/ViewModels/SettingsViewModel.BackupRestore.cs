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
using CommunityToolkit.Mvvm.Input;
using XerahS.Core.Managers;

namespace XerahS.UI.ViewModels;

public partial class SettingsViewModel
{
    private const string PlaintextBackupWarning =
        "This backup is not encrypted. It can contain plaintext passwords, Amazon S3 access keys, OAuth tokens, and other destination credentials. Store it as securely as a password vault.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupSettingsToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSettingsFromFileCommand))]
    private bool _isSettingsBackupBusy;

    [ObservableProperty]
    private string _settingsBackupStatusText =
        "Create a portable settings file or restore one created on another computer.";

    public Func<Task<string?>>? BackupSettingsFileRequester { get; set; }
    public Func<Task<string?>>? RestoreSettingsFileRequester { get; set; }
    public Func<string, string, Task<bool>>? SettingsBackupConfirmationRequester { get; set; }
    public Func<string, string, Task>? SettingsBackupMessageRequester { get; set; }
    public Func<string, string, Task>? SettingsBackupErrorRequester { get; set; }
    internal Func<string, PortableSettingsBackupResult> SettingsBackupWriter { get; set; } = PortableSettingsBackupService.Create;
    internal Func<string, PortableSettingsRestoreResult> SettingsBackupReader { get; set; } = PortableSettingsBackupService.Restore;

    private bool CanManageSettingsBackup() => !IsSettingsBackupBusy;

    [RelayCommand(CanExecute = nameof(CanManageSettingsBackup))]
    private async Task BackupSettingsToFile()
    {
        if (BackupSettingsFileRequester == null ||
            SettingsBackupConfirmationRequester == null ||
            !await SettingsBackupConfirmationRequester("Unencrypted Settings Backup", PlaintextBackupWarning + Environment.NewLine + Environment.NewLine + "Continue and choose a backup file?"))
        {
            return;
        }

        string? filePath = await BackupSettingsFileRequester();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        IsSettingsBackupBusy = true;
        SettingsBackupStatusText = "Creating portable settings backup...";
        try
        {
            // SettingsManager contains values owned by Avalonia's UI thread. Keep snapshotting
            // and serialization on that thread to avoid cross-thread access exceptions.
            PortableSettingsBackupResult result = SettingsBackupWriter(filePath);
            SettingsBackupStatusText = $"Backup created with {result.SecretCount} secret value(s).";
            string message = $"Portable settings backup created:{Environment.NewLine}{result.FilePath}{Environment.NewLine}{Environment.NewLine}{PlaintextBackupWarning}";
            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, result.Warnings);
            }

            if (SettingsBackupMessageRequester != null)
            {
                await SettingsBackupMessageRequester("Settings Backup Created", message);
            }
        }
        catch (Exception ex)
        {
            SettingsBackupStatusText = $"Backup failed: {ex.Message}";
            if (SettingsBackupErrorRequester != null)
            {
                await SettingsBackupErrorRequester("Settings Backup Failed", ex.Message);
            }
        }
        finally
        {
            IsSettingsBackupBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageSettingsBackup))]
    private async Task RestoreSettingsFromFile()
    {
        if (RestoreSettingsFileRequester == null || SettingsBackupConfirmationRequester == null)
        {
            return;
        }

        string? filePath = await RestoreSettingsFileRequester();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        string warning =
            "Restoring replaces the current application, workflow, and destination settings. Destination credentials from the backup will be encrypted by this computer after import." +
            Environment.NewLine + Environment.NewLine + PlaintextBackupWarning +
            Environment.NewLine + Environment.NewLine + "Continue with restore?";
        if (!await SettingsBackupConfirmationRequester("Restore Settings", warning))
        {
            return;
        }

        IsSettingsBackupBusy = true;
        SettingsBackupStatusText = "Validating and restoring settings...";
        try
        {
            // Restoring replaces live settings bound to the UI, so it must run on their owner thread.
            PortableSettingsRestoreResult result = SettingsBackupReader(filePath);
            SettingsBackupStatusText = $"Settings restored with {result.SecretCount} secret value(s). Restart XerahS.";
            string message = "Settings were restored successfully. Restart XerahS before using the restored workflows or destinations.";
            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, result.Warnings);
            }

            if (SettingsBackupMessageRequester != null)
            {
                await SettingsBackupMessageRequester("Settings Restored", message);
            }
        }
        catch (Exception ex)
        {
            SettingsBackupStatusText = $"Restore failed: {ex.Message}";
            if (SettingsBackupErrorRequester != null)
            {
                await SettingsBackupErrorRequester("Settings Restore Failed", ex.Message);
            }
        }
        finally
        {
            IsSettingsBackupBusy = false;
        }
    }
}
