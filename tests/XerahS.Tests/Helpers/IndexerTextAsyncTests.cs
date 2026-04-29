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
    public async Task IndexToFileAsync_EmptyFolder_ReportsOneFolder()
    {
        // Regression test: totalFoldersProcessed must count the root folder,
        // not only subdirectories found during enumeration.
        string rootDir = Path.Combine(Path.GetTempPath(), $"xerahs-indexer-root-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-output-{Guid.NewGuid():N}.txt");

        try
        {
            Directory.CreateDirectory(rootDir);
            File.WriteAllText(Path.Combine(rootDir, "file.txt"), "hello");

            IndexResult result = await IndexerAsync.IndexToFileAsync(
                rootDir,
                outputPath,
                new IndexerSettings { Output = IndexerOutput.Txt });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.ErrorMessage);
                Assert.That(result.TotalFolders, Is.EqualTo(1), "Root folder must be counted");
                Assert.That(result.TotalFiles, Is.EqualTo(1));
            });
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, recursive: true);
        }
    }

    [Test]
    public async Task IndexToFileAsync_NestedFolders_ReportsCorrectTotalFolders()
    {
        // Regression test: totalFoldersProcessed must count every visited folder,
        // including the root and all nested subdirectories.
        string rootDir = Path.Combine(Path.GetTempPath(), $"xerahs-indexer-nested-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-output-{Guid.NewGuid():N}.txt");

        try
        {
            Directory.CreateDirectory(Path.Combine(rootDir, "sub1", "sub2"));
            Directory.CreateDirectory(Path.Combine(rootDir, "sub1", "sub3"));
            Directory.CreateDirectory(Path.Combine(rootDir, "sub4"));
            File.WriteAllText(Path.Combine(rootDir, "file0.txt"), "root file");
            File.WriteAllText(Path.Combine(rootDir, "sub1", "file1.txt"), "sub1 file");
            File.WriteAllText(Path.Combine(rootDir, "sub1", "sub2", "file2.txt"), "sub2 file");
            File.WriteAllText(Path.Combine(rootDir, "sub1", "sub3", "file3.txt"), "sub3 file");
            File.WriteAllText(Path.Combine(rootDir, "sub4", "file4.txt"), "sub4 file");

            IndexResult result = await IndexerAsync.IndexToFileAsync(
                rootDir,
                outputPath,
                new IndexerSettings { Output = IndexerOutput.Txt });

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.ErrorMessage);
                // root + sub1 + sub2 + sub3 + sub4 = 5 folders
                Assert.That(result.TotalFolders, Is.EqualTo(5), "All visited folders must be counted");
                Assert.That(result.TotalFiles, Is.EqualTo(5));
            });
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, recursive: true);
        }
    }

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

    [Test]
    public void Index_SyncText_NormalizesConfiguredExtensionFilters()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-indexer-filter-sync-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(rootDirectory);
            File.WriteAllText(Path.Combine(rootDirectory, "keep.txt"), "hello");
            File.WriteAllText(Path.Combine(rootDirectory, "skip.log"), "log");

            string output = global::XerahS.Indexer.Indexer.Index(
                rootDirectory,
                new IndexerSettings
                {
                    Output = IndexerOutput.Txt,
                    IncludedFileExtensions = [null!, " .TXT "],
                    ExcludedFileExtensions = [" .log "]
                });

            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Contain("keep.txt"));
                Assert.That(output, Does.Not.Contain("skip.log"));
            });
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task IndexToFileAsync_NormalizesConfiguredExtensionFilters()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-indexer-filter-async-{Guid.NewGuid():N}");
        string outputPath = Path.Combine(Path.GetTempPath(), $"xerahs-index-output-{Guid.NewGuid():N}.txt");

        try
        {
            Directory.CreateDirectory(rootDirectory);
            File.WriteAllText(Path.Combine(rootDirectory, "keep.txt"), "hello");
            File.WriteAllText(Path.Combine(rootDirectory, "skip.log"), "log");

            IndexResult result = await IndexerAsync.IndexToFileAsync(
                rootDirectory,
                outputPath,
                new IndexerSettings
                {
                    Output = IndexerOutput.Txt,
                    IncludedFileExtensions = [null!, " .TXT "],
                    ExcludedFileExtensions = [" .log "]
                });

            string output = await File.ReadAllTextAsync(outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.ErrorMessage);
                Assert.That(result.TotalFiles, Is.EqualTo(1));
                Assert.That(output, Does.Contain("keep.txt"));
                Assert.That(output, Does.Not.Contain("skip.log"));
            });
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
