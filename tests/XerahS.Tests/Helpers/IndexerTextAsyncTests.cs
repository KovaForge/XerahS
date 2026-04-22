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
using XerahS.Indexer;

namespace XerahS.Tests.Helpers;

public class IndexerTextAsyncTests
{
    [Test]
    public async Task IndexToFileAsync_WithLeafOutputPath_WritesIntoCurrentDirectory()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-indexer-input-{Guid.NewGuid():N}");
        string originalCurrentDirectory = Environment.CurrentDirectory;
        string fileName = $"xerahs-index-{Guid.NewGuid():N}.txt";
        string outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, fileName);

        Directory.CreateDirectory(rootDirectory);
        File.WriteAllText(Path.Combine(rootDirectory, "capture.txt"), "hello");

        try
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.WorkDirectory;

            IndexResult result = await IndexerAsync.IndexToFileAsync(
                rootDirectory,
                fileName,
                new IndexerSettings
                {
                    Output = IndexerOutput.Txt
                });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.ErrorMessage);
                Assert.That(result.OutputFilePath, Is.EqualTo(fileName));
                Assert.That(File.Exists(outputPath), Is.True);
            });
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
