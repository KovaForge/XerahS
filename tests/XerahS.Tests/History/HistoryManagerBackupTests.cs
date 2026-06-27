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

using NUnit.Framework;
using XerahS.Common;
using XerahS.History;

namespace XerahS.Tests.History;

public class HistoryManagerBackupTests
{
    [Test]
    public void AppendHistoryItem_ReturnsFalse_WhenConfiguredZipBackupFails()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        Directory.CreateDirectory(Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM"), $"backup-{DateTime.Now:yyyy-MM-dd}.zip"));

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.False);
            Assert.That(File.Exists(historyFile), Is.True, "The history append itself completed; the false result surfaces the backup failure.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void AppendHistoryItem_ReturnsFalse_WhenConfiguredWeeklyBackupFails()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        Directory.CreateDirectory(Path.Combine(backupFolder, $"history-{DateTime.Now:yyyy-MM}-W{FileHelpers.WeekOfYear(DateTime.Now):00}.json"));

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateWeeklyBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.False);
            Assert.That(File.Exists(historyFile), Is.True, "The history append itself completed; the false result surfaces the backup failure.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void AppendHistoryItem_ReturnsTrue_WhenConfiguredWeeklyBackupAlreadyExists()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        File.WriteAllText(Path.Combine(backupFolder, $"history-{DateTime.Now:yyyy-MM}-W{FileHelpers.WeekOfYear(DateTime.Now):00}.json"), "existing backup");

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateWeeklyBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.True);
            Assert.That(File.Exists(historyFile), Is.True);
            Assert.That(manager.LastBackupFailureReason, Is.Null, "Pre-existing weekly backup is the happy path, so no diagnostic should be surfaced.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LastBackupFailureReason_SurfacesZipBackupFailureMessage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        // Pre-create the month folder + the .zip file as a directory so BackupFileZip's final move throws.
        Directory.CreateDirectory(Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM"), $"backup-{DateTime.Now:yyyy-MM-dd}.zip"));

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.False, "Backup step failed -> Append returns false so the caller does not appear to have lost data.");
            Assert.That(manager.LastBackupFailureReason, Is.Not.Null);
            Assert.That(manager.LastBackupFailureReason, Does.Contain(backupFolder),
                "The diagnostic must mention the configured backup folder so the user knows where to look.");
            Assert.That(manager.LastBackupFailureReason, Does.Contain("history file itself was updated"),
                "The diagnostic must tell the user the data write itself succeeded; otherwise they may try to redo work that was already saved.");
            Assert.That(File.Exists(historyFile), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LastBackupFailureReason_SurfacesWeeklyBackupFailureMessage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        // Pre-create the weekly backup file as a directory so BackupFileWeekly's File.Copy throws.
        Directory.CreateDirectory(Path.Combine(backupFolder, $"history-{DateTime.Now:yyyy-MM}-W{FileHelpers.WeekOfYear(DateTime.Now):00}.json"));

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateWeeklyBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.False);
            Assert.That(manager.LastBackupFailureReason, Is.Not.Null);
            Assert.That(manager.LastBackupFailureReason, Does.Contain(backupFolder));
            Assert.That(manager.LastBackupFailureReason, Does.Contain("history file itself was updated"));
            Assert.That(File.Exists(historyFile), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LastBackupFailureReason_IsClearedOnSubsequentSuccessfulAppend()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");
        string backupFolder = Path.Combine(directory, "backups");

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(backupFolder);
        Directory.CreateDirectory(Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM"), $"backup-{DateTime.Now:yyyy-MM-dd}.zip"));

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = backupFolder,
                CreateBackup = true
            };

            // First append: backup fails.
            manager.AppendHistoryItem(CreateHistoryItem());
            Assert.That(manager.LastBackupFailureReason, Is.Not.Null, "Pre-condition: first backup attempt failed and the diagnostic is set.");

            // Remove the blocking directory so the next backup attempt succeeds.
            Directory.Delete(Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM")), recursive: true);

            // Second append: backup succeeds. The diagnostic must be reset so the UI does not show a stale failure.
            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.True);
            Assert.That(manager.LastBackupFailureReason, Is.Null, "After a successful backup the diagnostic should be cleared, not retain a stale failure message.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LastBackupFailureReason_IsNullWhenBackupFolderNotConfigured()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-backup-{Guid.NewGuid():N}");
        string historyFile = Path.Combine(directory, "history.json");

        Directory.CreateDirectory(directory);

        try
        {
            var manager = new HistoryManagerJSON(historyFile)
            {
                BackupFolder = string.Empty,
                CreateBackup = true,
                CreateWeeklyBackup = true
            };

            bool result = manager.AppendHistoryItem(CreateHistoryItem());

            Assert.That(result, Is.True);
            Assert.That(manager.LastBackupFailureReason, Is.Null, "When no backup folder is configured, the backup step is skipped and the diagnostic must remain null.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HistoryItem CreateHistoryItem()
    {
        return new HistoryItem
        {
            FileName = "capture.png",
            FilePath = "/tmp/capture.png",
            DateTime = DateTime.UtcNow,
            Type = "Image",
            Host = "Local"
        };
    }
}
