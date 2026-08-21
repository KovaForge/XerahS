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
using System.Runtime.Versioning;
using XerahS.Common;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class SettingsBaseBackupDiagnosticsTests
{
    private class TestSettings : SettingsBase<TestSettings>
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
            try
            {
                // chmod 0000 child files inside month folders can stop the recursive
                // delete from succeeding; restore perms so TearDown can clean up.
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    foreach (string innerFile in Directory.EnumerateFiles(_baseDir, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            TryRestoreUnixFileMode(innerFile);
                        }
                        catch
                        {
                            // Best-effort: TearDown is allowed to fail silently on Windows or sandboxed FSes.
                        }
                    }
                }
                Directory.Delete(_baseDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; the OS will reclaim the temp dir eventually.
            }
        }
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static void TryRestoreUnixFileMode(string path)
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
    }

    [Test]
    public void Save_InvalidBackupFolder_FiresSettingsBackupFailed_CreatePhase()
    {
        // An embedded NUL makes Path.Combine/Path.GetFileName throw ArgumentException
        // before Directory.CreateDirectory can run, so CreateBackupZip surfaces the
        // exception via SettingsBackupFailed with phase="create".
        var settingsFile = Path.Combine(_baseDir, "Settings.json");
        File.WriteAllText(settingsFile, "{}");
        var settings = new TestSettings
        {
            CreateBackup = true,
            BackupFolder = Path.Combine(_baseDir, "bad\0destination")
        };

        Exception? captured = null;
        string? capturedPhase = null;
        settings.SettingsBackupFailed += (_, phase, _, e) =>
        {
            capturedPhase = phase;
            captured = e;
        };

        Assert.DoesNotThrow(() => settings.Save(settingsFile),
            "Save() must not throw when backup creation fails; the failure is surfaced via SettingsBackupFailed.");

        Assert.That(captured, Is.Not.Null, "SettingsBackupFailed must fire when CreateBackupZip throws.");
        Assert.That(capturedPhase, Is.EqualTo("create"),
            "SettingsBackupFailed must be tagged with phase='create' for CreateBackupZip failures.");
    }

    [Test]
    public void Save_ValidBackupFolder_DoesNotFireSettingsBackupFailed()
    {
        var settingsFile = Path.Combine(_baseDir, "Settings.json");
        File.WriteAllText(settingsFile, "{}");
        var settingsDir = Path.Combine(_baseDir, "settings-dir");
        Directory.CreateDirectory(settingsDir);
        var settingsFile2 = Path.Combine(settingsDir, "Settings.json");
        File.WriteAllText(settingsFile2, "{}");

        var settings = new TestSettings
        {
            CreateBackup = true,
            BackupFolder = Path.Combine(_baseDir, "backups")
        };

        bool fired = false;
        settings.SettingsBackupFailed += (_, _, _, _) => fired = true;

        settings.Save(settingsFile2);

        Assert.That(fired, Is.False,
            "SettingsBackupFailed must not fire when the backup was created successfully.");
    }

    [Test]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void PruneOldBackups_PerFolderDeleteFailure_FiresSettingsBackupFailed_PruneFolderPhase()
    {
        // BCL behavior probe (verified on macOS): Directory.Delete(monthDir, recursive: true)
        // throws UnauthorizedAccessException when the month folder is chmod 0555 (read+execute
        // but no write) because the OS denies the unlink of the child file. The catch in
        // PruneOldBackups surfaces the exception as phase="pruneFolder".
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("chmod 0555 month-folder test is Unix-only; the Windows ACL path is exercised by the build itself.");
        }

        string backupFolder = Path.Combine(_baseDir, "backups");
        Directory.CreateDirectory(backupFolder);

        DateTime oldMonth = DateTime.Now.AddDays(-200);
        string oldMonthDir = Path.Combine(backupFolder, oldMonth.ToString("yyyy-MM"));
        Directory.CreateDirectory(oldMonthDir);
        string lockedFile = Path.Combine(oldMonthDir, "locked.zip");
        File.WriteAllBytes(lockedFile, new byte[] { 1, 2, 3 });
        // 0555 = read+execute, no write. macOS/Linux non-root users cannot unlink a
        // child of a directory they don't have write perms on, so the recursive
        // Directory.Delete in PruneOldBackups throws UnauthorizedAccessException,
        // which the inner catch surfaces as phase="pruneFolder".
        File.SetUnixFileMode(oldMonthDir, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        var settings = new TestSettings
        {
            BackupFolder = backupFolder,
            BackupRetentionDays = 90
        };

        var events = new List<(string phase, Exception ex)>();
        settings.SettingsBackupFailed += (_, phase, _, e) => events.Add((phase, e));

        settings.PruneOldBackups();

        // Restore perms so TearDown can clean up the test tree.
        TryRestoreUnixFileMode(oldMonthDir);

        Assert.That(events, Has.Count.GreaterThanOrEqualTo(1),
            "SettingsBackupFailed must fire when an individual month folder cannot be deleted.");
        Assert.That(events.Any(e => e.phase == "pruneFolder"), Is.True,
            $"Expected at least one 'pruneFolder' event but got phases: {string.Join(",", events.Select(e => e.phase))}");

        // The directory should still exist because the delete failed.
        Assert.That(Directory.Exists(oldMonthDir), Is.True,
            "Month folder should be preserved when its deletion fails.");
    }

}
