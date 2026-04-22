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
using XerahS.CLI.Commands;

namespace XerahS.Tests.Tools;

[TestFixture]
public class UploadCommandPathSanitizationTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("folder/..")]
    [TestCase("folder/.")]
    public void SanitizeUploadFileName_WhenNameDoesNotResolveToARealFileName_UsesFallback(string? requestedName)
    {
        string result = UploadCommand.SanitizeUploadFileName(requestedName, "upload.txt");

        Assert.That(result, Is.EqualTo("upload.txt"));
    }

    [Test]
    public void SanitizeUploadFileName_WhenPathContainsDirectories_ReturnsLeafFileName()
    {
        string result = UploadCommand.SanitizeUploadFileName("nested/path/report.png", "upload.txt");

        Assert.That(result, Is.EqualTo("report.png"));
    }

    [Test]
    public void CreateTemporaryUploadFilePath_WhenNameResolvesToParentSegment_KeepsFileInsideUniqueTempDirectory()
    {
        string tempPath = UploadCommand.CreateTemporaryUploadFilePath("..", "upload.txt");

        try
        {
            string? directory = Path.GetDirectoryName(tempPath);

            Assert.Multiple(() =>
            {
                Assert.That(Path.GetFileName(tempPath), Is.EqualTo("upload.txt"));
                Assert.That(directory, Is.Not.Null.And.Contains(Path.Combine(Path.GetTempPath(), "xerahs-upload")));
            });
        }
        finally
        {
            UploadCommand.CleanupTemporaryUploadDirectories([Path.GetDirectoryName(tempPath)]);
        }
    }

    [Test]
    public void CleanupTemporaryUploadDirectories_WhenGivenMultipleDirectories_RemovesEachDirectoryOnce()
    {
        string firstDirectory = UploadCommand.CreateTemporaryUploadDirectory();
        string secondDirectory = UploadCommand.CreateTemporaryUploadDirectory();
        File.WriteAllText(Path.Combine(firstDirectory, "upload.txt"), "first");
        File.WriteAllText(Path.Combine(secondDirectory, "upload.txt"), "second");

        UploadCommand.CleanupTemporaryUploadDirectories([firstDirectory, secondDirectory, firstDirectory, null, string.Empty]);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(firstDirectory), Is.False);
            Assert.That(Directory.Exists(secondDirectory), Is.False);
        });
    }
}
