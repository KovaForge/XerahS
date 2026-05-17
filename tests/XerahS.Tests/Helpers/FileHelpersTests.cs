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
}
