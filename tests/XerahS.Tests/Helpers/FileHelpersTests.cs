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

public class FileHelpersTests
{
    [Test]
    public void GetFileNameExtension_WhenDirectoryContainsDotsAndFileHasNoExtension_ReturnsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), "session.v1", "capture");

        string extension = FileHelpers.GetFileNameExtension(path);

        Assert.That(extension, Is.Empty);
    }

    [Test]
    public void GetFileNameExtension_WhenFileIsDotPrefixedWithoutRealExtension_ReturnsEmpty()
    {
        string extension = FileHelpers.GetFileNameExtension(".gitignore");

        Assert.That(extension, Is.Empty);
    }

    [Test]
    public void ChangeFileNameExtension_WhenDirectoryContainsDotsAndFileHasNoExtension_AppendsExtensionToFileName()
    {
        string path = Path.Combine(Path.GetTempPath(), "session.v1", "capture");

        string changedPath = FileHelpers.ChangeFileNameExtension(path, "png");

        Assert.That(changedPath, Is.EqualTo(Path.Combine(Path.GetTempPath(), "session.v1", "capture.png")));
    }

    [Test]
    public void AppendTextToFileName_WhenDirectoryContainsDotsAndFileHasNoExtension_AppendsTextToFileName()
    {
        string path = Path.Combine(Path.GetTempPath(), "session.v1", "capture");

        string changedPath = FileHelpers.AppendTextToFileName(path, "-edited");

        Assert.That(changedPath, Is.EqualTo(Path.Combine(Path.GetTempPath(), "session.v1", "capture-edited")));
    }

    [Test]
    public void AppendTextToFileName_WhenFileIsDotPrefixedWithoutRealExtension_AppendsTextAfterFileName()
    {
        string changedPath = FileHelpers.AppendTextToFileName(".gitignore", "-backup");

        Assert.That(changedPath, Is.EqualTo(".gitignore-backup"));
    }

    [Test]
    public void GetUniqueFilePath_FirstCollision_UsesOneSuffix()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "capture.png");
        File.WriteAllText(filePath, "existing");

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "capture (1).png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }


    [Test]
    public void GetUniqueFilePath_WhenDirectoryUsesRequestedFileName_AddsSuffix()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string directoryCollisionPath = Path.Combine(directory, "capture.png");
        Directory.CreateDirectory(directoryCollisionPath);

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(directoryCollisionPath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "capture (1).png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void GetUniqueFilePath_WhenNextNumberedCandidateIsDirectory_SkipsIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "capture.png");
        File.WriteAllText(filePath, "existing");
        Directory.CreateDirectory(Path.Combine(directory, "capture (1).png"));

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "capture (2).png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void GetUniqueFilePath_ExistingNumberedFile_IncrementsFromCurrentSuffix()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "capture (3).png");
        File.WriteAllText(filePath, "existing");

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "capture (4).png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void GetUniqueFilePath_DotPrefixedFileWithoutRealExtension_AppendsSuffixAfterFileName()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, ".gitignore");
        File.WriteAllText(filePath, "existing");

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, ".gitignore (1)")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void IsFileLocked_MissingFile_ReturnsFalse()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "xerahs-nonexistent-file-" + Guid.NewGuid().ToString("N") + ".tmp");

        bool locked = FileHelpers.IsFileLocked(missingPath);

        Assert.That(locked, Is.False);
    }

    [Test]
    public void IsFileLocked_MissingDirectory_ReturnsFalse()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "xerahs-nonexistent-dir-" + Guid.NewGuid().ToString("N"), "file.tmp");

        bool locked = FileHelpers.IsFileLocked(missingPath);

        Assert.That(locked, Is.False);
    }

    [Test]
    public void IsFileLocked_NullOrEmptyPath_ReturnsFalse()
    {
        Assert.That(FileHelpers.IsFileLocked(null!), Is.False);
        Assert.That(FileHelpers.IsFileLocked(""), Is.False);
        Assert.That(FileHelpers.IsFileLocked("  "), Is.False);
    }

    [Test]
    public void IsFileLocked_ExistingUnlockedFile_ReturnsFalse()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            bool locked = FileHelpers.IsFileLocked(tempFile);

            Assert.That(locked, Is.False);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void IsFileLocked_ExistingLockedFile_ReturnsTrue()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.None);

            bool locked = FileHelpers.IsFileLocked(tempFile);

            Assert.That(locked, Is.True);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void GetUniqueFilePath_KnownDoubleExtension_PreservesCompoundExtension()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "archive.tar.gz");
        File.WriteAllText(filePath, "existing");

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "archive (1).tar.gz")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void GetUniqueFilePath_NumberedSuffixBeyondIntRange_DoesNotThrow()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "capture (2147483648).png");
        File.WriteAllText(filePath, "existing");

        try
        {
            string uniquePath = FileHelpers.GetUniqueFilePath(filePath);

            Assert.That(uniquePath, Is.EqualTo(Path.Combine(directory, "capture (2147483649).png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsPath_WhenDestinationDoesNotExist()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        File.WriteAllText(sourceFile, "{}");
        string backupFolder = Path.Combine(directory, "backups");

        try
        {
            string? result = FileHelpers.BackupFileWeekly(sourceFile, backupFolder);

            Assert.That(result, Is.Not.Null);
            Assert.That(File.Exists(result), Is.True);
            Assert.That(Path.GetFileName(result), Does.StartWith("history-"));
            Assert.That(Path.GetExtension(result), Is.EqualTo(".json"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsNullAndNoThrow_WhenConcurrentBackupCreatesSameName()
    {
        // Simulates TOCTOU: another process creates the backup between File.Exists and File.Copy
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        File.WriteAllText(sourceFile, "{}");
        string backupFolder = Path.Combine(directory, "backups");
        Directory.CreateDirectory(backupFolder);

        // Pre-create the backup file to simulate a race
        string fileName = Path.GetFileNameWithoutExtension(sourceFile);
        string ext = Path.GetExtension(sourceFile);
        string preExistingBackup = Path.Combine(backupFolder, $"{fileName}-{DateTime.Now:yyyy-MM}-W{FileHelpers.WeekOfYear(DateTime.Now):00}{ext}");
        File.WriteAllText(preExistingBackup, "already-existed");

        try
        {
            // Should return null and not throw, rather than propagating IOException
            string? result = FileHelpers.BackupFileWeekly(sourceFile, backupFolder);

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsNull_WhenBackupFolderPathIsFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        string backupFolderAsFile = Path.Combine(directory, "backups");
        File.WriteAllText(sourceFile, "{}");
        File.WriteAllText(backupFolderAsFile, "not-a-directory");

        try
        {
            string? result = FileHelpers.BackupFileWeekly(sourceFile, backupFolderAsFile);

            Assert.That(result, Is.Null);
            Assert.That(File.Exists(backupFolderAsFile), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsNull_WhenBackupFolderPathIsInvalid()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        File.WriteAllText(sourceFile, "{}");

        try
        {
            string? result = FileHelpers.BackupFileWeekly(sourceFile, "bad\0destination");

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CopyFile_OverwriteFalse_ReturnsNull_WhenDestinationExists()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "source.txt");
        string destFolder = Path.Combine(directory, "dest");
        Directory.CreateDirectory(destFolder);
        File.WriteAllText(sourceFile, "source");
        File.WriteAllText(Path.Combine(destFolder, "source.txt"), "existing");

        try
        {
            string? result = FileHelpers.CopyFile(sourceFile, destFolder, overwrite: false);

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CopyFile_ReturnsPath_WhenCopySucceeds()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "source.txt");
        string destFolder = Path.Combine(directory, "dest");
        File.WriteAllText(sourceFile, "source");

        try
        {
            string? result = FileHelpers.CopyFile(sourceFile, destFolder);

            Assert.That(result, Is.Not.Null);
            Assert.That(File.Exists(result), Is.True);
            Assert.That(File.ReadAllText(result), Is.EqualTo("source"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CopyFile_ReturnsNull_WhenDestinationPathIsInvalid()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "source.txt");
        File.WriteAllText(sourceFile, "source");

        try
        {
            string? result = FileHelpers.CopyFile(sourceFile, "bad\0destination");

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CopyFile_ReturnsNull_WhenSourceMissing()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string missingFile = Path.Combine(directory, "missing.txt");

        try
        {
            string? result = FileHelpers.CopyFile(missingFile, Path.Combine(directory, "dest"));

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_ReturnsPath_WhenBackupSucceeds()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        string backupFolder = Path.Combine(directory, "backups");
        File.WriteAllText(sourceFile, "database-content");

        try
        {
            string? result = FileHelpers.BackupFileZip(sourceFile, backupFolder);

            Assert.That(result, Is.Not.Null);
            Assert.That(File.Exists(result), Is.True);
            Assert.That(Path.GetExtension(result), Is.EqualTo(".zip"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_ReturnsNull_WhenSourceMissing()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string missingFile = Path.Combine(directory, "missing.db");

        try
        {
            string? result = FileHelpers.BackupFileZip(missingFile, Path.Combine(directory, "backups"));

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_ReplacesExistingBackup_WithoutCorrupting()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        string backupFolder = Path.Combine(directory, "backups");
        File.WriteAllText(sourceFile, "v1-content");

        try
        {
            // First backup
            string? first = FileHelpers.BackupFileZip(sourceFile, backupFolder);
            Assert.That(first, Is.Not.Null);
            Assert.That(File.Exists(first), Is.True);

            // Second backup with different content
            File.WriteAllText(sourceFile, "v2-content-longer");
            string? second = FileHelpers.BackupFileZip(sourceFile, backupFolder);
            Assert.That(second, Is.Not.Null);
            Assert.That(File.Exists(second), Is.True);

            // Both should point to the same filename (same day)
            Assert.That(second, Is.EqualTo(first));

            // The backup should contain the updated content, not stale v1
            using (var archive = System.IO.Compression.ZipFile.OpenRead(second))
            {
                var entry = archive.GetEntry("data.db");
                Assert.That(entry, Is.Not.Null);
                using (var reader = new StreamReader(entry!.Open()))
                {
                    string content = reader.ReadToEnd();
                    Assert.That(content, Is.EqualTo("v2-content-longer"));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_DeletesTempFile_WhenFinalMoveFails()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        string backupFolder = Path.Combine(directory, "backups");
        string monthFolder = Path.Combine(backupFolder, DateTime.Now.ToString("yyyy-MM"));
        string zipPathAsDirectory = Path.Combine(monthFolder, $"backup-{DateTime.Now:yyyy-MM-dd}.zip");
        File.WriteAllText(sourceFile, "database-content");
        Directory.CreateDirectory(zipPathAsDirectory);

        try
        {
            string? result = FileHelpers.BackupFileZip(sourceFile, backupFolder);

            Assert.That(result, Is.Null);
            Assert.That(Directory.EnumerateFiles(monthFolder, "*.tmp"), Is.Empty);
            Assert.That(Directory.Exists(zipPathAsDirectory), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_DoesNotThrow_WhenWalShmEphemeral()
    {
        // WAL/SHM files may disappear between Exists and OpenRead (TOCTOU).
        // The backup should succeed even when they can't be read.
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        string backupFolder = Path.Combine(directory, "backups");
        File.WriteAllText(sourceFile, "content");

        // Create a WAL file that we lock to trigger IOException on read
        string walFile = sourceFile + "-wal";
        File.WriteAllText(walFile, "wal-content");

        try
        {
            // Open and lock the WAL file so OpenRead in backup fails
            using (var lockStream = new FileStream(walFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                string? result = FileHelpers.BackupFileZip(sourceFile, backupFolder);

                // Backup should still succeed for the main file; WAL is skipped
                Assert.That(result, Is.Not.Null);
                Assert.That(File.Exists(result), Is.True);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsNull_WhenDestinationIsEmpty()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        File.WriteAllText(sourceFile, "{}");

        try
        {
            string? result = FileHelpers.BackupFileWeekly(sourceFile, "");

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileWeekly_ReturnsNull_WhenDestinationIsWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "history.json");
        File.WriteAllText(sourceFile, "{}");

        try
        {
            string? result = FileHelpers.BackupFileWeekly(sourceFile, "   ");

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_ReturnsNull_WhenDestinationIsEmpty()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        File.WriteAllText(sourceFile, "database-content");

        try
        {
            // Guard against silent CWD pollution: an empty destination would otherwise
            // create a "yyyy-MM" folder in the current working directory and write
            // the backup there.
            string? cwdBefore = Directory.GetCurrentDirectory();
            string? result = FileHelpers.BackupFileZip(sourceFile, "");

            Assert.That(result, Is.Null);
            Assert.That(Directory.GetCurrentDirectory(), Is.EqualTo(cwdBefore));
            Assert.That(Directory.Exists(Path.Combine(cwdBefore, "2026-06")), Is.False,
                "Empty destination must not create a yyyy-MM folder in the current working directory.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void BackupFileZip_ReturnsNull_WhenDestinationIsWhitespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-filehelpers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string sourceFile = Path.Combine(directory, "data.db");
        File.WriteAllText(sourceFile, "database-content");

        try
        {
            string? result = FileHelpers.BackupFileZip(sourceFile, "   ");

            Assert.That(result, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
