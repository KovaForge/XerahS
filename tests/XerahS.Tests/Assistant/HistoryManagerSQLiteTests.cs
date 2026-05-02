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
using XerahS.History;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class HistoryManagerSQLiteTests
{
    [Test]
    public void ContainsFilePath_FindsEntriesBeyondFirstPage()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                for (int i = 0; i < 1005; i++)
                {
                    manager.AppendHistoryItem(new HistoryItem
                    {
                        FileName = $"shot-{i}.png",
                        FilePath = Path.Combine(tempDirectory, $"shot-{i}.png"),
                        DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i),
                        Type = "Image"
                    });
                }

                string lastFilePath = Path.Combine(tempDirectory, "shot-1004.png");
                Assert.That(manager.ContainsFilePath(lastFilePath, pageSize: 200), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }


    [Test]
    public void ContainsFilePath_MatchesSymbolicLinkEquivalentPath()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");
            string targetPath = Path.Combine(tempDirectory, "target.png");
            string linkPath = Path.Combine(tempDirectory, "linked.png");
            File.WriteAllText(targetPath, "image placeholder");

            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Ignore($"Symbolic links are not available in this test environment: {ex.Message}");
            }

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "target.png",
                    FilePath = targetPath,
                    DateTime = new DateTime(2026, 5, 2, 7, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });

                Assert.That(manager.ContainsFilePath(linkPath), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void ContainsFilePath_UsesHostPathCasingSemantics()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                string originalPath = Path.Combine(tempDirectory, "shot.png");
                string differentCasePath = Path.Combine(tempDirectory, "SHOT.png");

                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot.png",
                    FilePath = originalPath,
                    DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });

                bool exists = manager.ContainsFilePath(differentCasePath);

                if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                {
                    Assert.That(exists, Is.True);
                }
                else
                {
                    Assert.That(exists, Is.False);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void GetLatestByFilePath_ReturnsNewestMatchingHistoryItem()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");
            string filePath = Path.Combine(tempDirectory, "shot.png");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot-old.png",
                    FilePath = filePath,
                    DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image",
                    Tags = new Dictionary<string, string?> { ["OcrText"] = "old" }
                });

                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot-new.png",
                    FilePath = filePath,
                    DateTime = new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc),
                    Type = "Image",
                    Tags = new Dictionary<string, string?> { ["OcrText"] = "new" }
                });

                HistoryItem? match = manager.GetLatestByFilePath(filePath);

                Assert.That(match, Is.Not.Null);
                Assert.That(match!.FileName, Is.EqualTo("shot-new.png"));
                Assert.That(match.Tags["OcrText"], Is.EqualTo("new"));
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
