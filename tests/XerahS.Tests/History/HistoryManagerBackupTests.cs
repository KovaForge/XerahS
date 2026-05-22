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
