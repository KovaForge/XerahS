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
}
