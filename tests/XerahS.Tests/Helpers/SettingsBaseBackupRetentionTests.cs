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

namespace XerahS.Tests.Helpers;

[TestFixture]
public class SettingsBaseBackupRetentionTests
{
    private sealed class TestSettings : SettingsBase<TestSettings>
    {
    }

    private string _baseDir = null!;

    [SetUp]
    public void SetUp()
    {
        _baseDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_baseDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_baseDir))
        {
            Directory.Delete(_baseDir, recursive: true);
        }
    }

    [Test]
    public void PruneOldBackups_RemovesMonthFolder_OlderThanRetention()
    {
        string backupFolder = Path.Combine(_baseDir, "backups");
        Directory.CreateDirectory(backupFolder);

        // Create a month folder dated 120 days ago.
        DateTime oldMonth = DateTime.Now.AddDays(-120);
        string oldMonthDir = Path.Combine(backupFolder, oldMonth.ToString("yyyy-MM"));
        Directory.CreateDirectory(oldMonthDir);
        File.WriteAllText(Path.Combine(oldMonthDir, "dummy.zip"), "stale");

        // Create a month folder for the current month (should survive).
        string currentMonthDir = Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM"));
        Directory.CreateDirectory(currentMonthDir);
        File.WriteAllText(Path.Combine(currentMonthDir, "recent.zip"), "fresh");

        var settings = new TestSettings
        {
            BackupFolder = backupFolder,
            BackupRetentionDays = 90
        };

        settings.PruneOldBackups();

        Assert.That(Directory.Exists(oldMonthDir), Is.False,
            "Month folder older than retention should be deleted.");
        Assert.That(Directory.Exists(currentMonthDir), Is.True,
            "Current month folder should not be deleted.");
    }

    [Test]
    public void PruneOldBackups_KeepsMonthFolder_WithinRetention()
    {
        string backupFolder = Path.Combine(_baseDir, "backups");
        Directory.CreateDirectory(backupFolder);

        // Create a month folder dated 30 days ago (within 90-day retention).
        DateTime recentMonth = DateTime.Now.AddDays(-30);
        string recentMonthDir = Path.Combine(backupFolder, recentMonth.ToString("yyyy-MM"));
        Directory.CreateDirectory(recentMonthDir);
        File.WriteAllText(Path.Combine(recentMonthDir, "recent.zip"), "data");

        var settings = new TestSettings
        {
            BackupFolder = backupFolder,
            BackupRetentionDays = 90
        };

        settings.PruneOldBackups();

        Assert.That(Directory.Exists(recentMonthDir), Is.True,
            "Month folder within retention should not be deleted.");
    }

    [Test]
    public void PruneOldBackups_NoOps_WhenBackupRetentionDaysIsZero()
    {
        string backupFolder = Path.Combine(_baseDir, "backups");
        Directory.CreateDirectory(backupFolder);

        DateTime oldMonth = DateTime.Now.AddDays(-200);
        string oldMonthDir = Path.Combine(backupFolder, oldMonth.ToString("yyyy-MM"));
        Directory.CreateDirectory(oldMonthDir);

        var settings = new TestSettings
        {
            BackupFolder = backupFolder,
            BackupRetentionDays = 0 // disabled
        };

        settings.PruneOldBackups();

        Assert.That(Directory.Exists(oldMonthDir), Is.True,
            "Backup folder should not be pruned when retention is zero.");
    }

    [Test]
    public void PruneOldBackups_NoOps_WhenBackupFolderIsNull()
    {
        var settings = new TestSettings
        {
            BackupFolder = null,
            BackupRetentionDays = 90
        };

        Assert.DoesNotThrow(() => settings.PruneOldBackups());
    }

    [Test]
    public void PruneOldBackups_NoOps_WhenBackupFolderDoesNotExist()
    {
        string nonExistent = Path.Combine(_baseDir, "does-not-exist");

        var settings = new TestSettings
        {
            BackupFolder = nonExistent,
            BackupRetentionDays = 90
        };

        Assert.DoesNotThrow(() => settings.PruneOldBackups());
    }

    [Test]
    public void PruneOldBackups_IgnoresNonMonthFolders()
    {
        string backupFolder = Path.Combine(_baseDir, "backups");
        Directory.CreateDirectory(backupFolder);

        // Create a folder that doesn't match yyyy-MM naming.
        string nonMonthDir = Path.Combine(backupFolder, "not-a-month-folder");
        Directory.CreateDirectory(nonMonthDir);
        File.WriteAllText(Path.Combine(nonMonthDir, "file.txt"), "keep");

        var settings = new TestSettings
        {
            BackupFolder = backupFolder,
            BackupRetentionDays = 90
        };

        settings.PruneOldBackups();

        Assert.That(Directory.Exists(nonMonthDir), Is.True,
            "Non-month folders should not be deleted.");
    }
}
