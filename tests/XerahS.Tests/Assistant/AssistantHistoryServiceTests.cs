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
using Microsoft.Data.Sqlite;
using XerahS.Assistant.Services;
using XerahS.Core;
using XerahS.History;

namespace XerahS.Tests.Assistant;

[TestFixture]
[NonParallelizable]
public sealed class AssistantHistoryServiceTests
{
    private string? _originalPersonalFolder;

    [SetUp]
    public void SetUp()
    {
        _originalPersonalFolder = SettingsManager.PersonalFolder;
        SettingsManager.PersonalFolder = Path.Combine(Path.GetTempPath(), "xerahs-assistant-history-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(SettingsManager.HistoryFolder);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_originalPersonalFolder))
        {
            SettingsManager.PersonalFolder = _originalPersonalFolder;
        }
    }

    [Test]
    public async Task GetLatestScreenshotsAsync_WhenFileNameMissing_UsesCrossPlatformFileNameOnly()
    {
        const string windowsStylePath = @"C:\Users\alice\Pictures\capture.png";
        string historyPath = SettingsManager.GetHistoryFilePath();

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();

            CreateHistoryTable(connection);
            InsertHistoryItem(connection, windowsStylePath, new DateTime(2026, 4, 27, 15, 0, 0, DateTimeKind.Utc));
        }

        var service = new AssistantHistoryService();

        IReadOnlyList<AssistantHistoryItem> items = await service.GetLatestScreenshotsAsync(1, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(items[0].FileName, Is.EqualTo("capture.png"));
            Assert.That(items[0].FileName, Does.Not.Contain("alice"));
            Assert.That(items[0].FileName, Does.Not.Contain(@"C:\Users"));
        });
    }

    [Test]
    public async Task GetLatestScreenshotsAsync_WhenFirstHistoryPageContainsNoImages_ContinuesPaging()
    {
        string historyPath = SettingsManager.GetHistoryFilePath();

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();
            CreateHistoryTable(connection);

            DateTime newest = new(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 260; i++)
            {
                InsertHistoryItem(connection, $"/tmp/document-{i}.txt", newest.AddMinutes(-i));
            }

            InsertHistoryItem(connection, "/tmp/older-capture.png", newest.AddMinutes(-261));
        }

        var service = new AssistantHistoryService();

        IReadOnlyList<AssistantHistoryItem> items = await service.GetLatestScreenshotsAsync(1, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].FileName, Is.EqualTo("older-capture.png"));
    }

    [Test]
    public async Task GetCachedOcrTextAsync_WhenPathHasWhitespace_UsesTrimmedCanonicalHistoryPath()
    {
        string historyPath = SettingsManager.GetHistoryFilePath();
        string filePath = Path.Combine(SettingsManager.HistoryFolder, "capture.png");
        File.WriteAllText(filePath, "image placeholder");

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();
            CreateHistoryTable(connection);
            InsertHistoryItem(connection, filePath, new DateTime(2026, 4, 29, 13, 0, 0, DateTimeKind.Utc), "{\"OcrText\":\"cached text\"}");
        }

        var service = new AssistantHistoryService();

        string? ocrText = await service.GetCachedOcrTextAsync($"  {filePath}  ", CancellationToken.None);

        Assert.That(ocrText, Is.EqualTo("cached text"));
    }

    [Test]
    public async Task GetCachedOcrTextAsync_WhenHistoryFileWasDeleted_IgnoresStaleCachedText()
    {
        string historyPath = SettingsManager.GetHistoryFilePath();
        string filePath = Path.Combine(SettingsManager.HistoryFolder, "deleted-capture.png");

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();
            CreateHistoryTable(connection);
            InsertHistoryItem(connection, filePath, new DateTime(2026, 5, 2, 11, 0, 0, DateTimeKind.Utc), "{\"OcrText\":\"stale text\"}");
        }

        var service = new AssistantHistoryService();

        string? ocrText = await service.GetCachedOcrTextAsync(filePath, CancellationToken.None);

        Assert.That(ocrText, Is.Null);
    }

    [Test]
    public async Task GetLatestScreenshotsAsync_WhenHistoryFileWasDeleted_HidesStaleOcrText()
    {
        string historyPath = SettingsManager.GetHistoryFilePath();
        string filePath = Path.Combine(SettingsManager.HistoryFolder, "deleted-capture.png");

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();
            CreateHistoryTable(connection);
            InsertHistoryItem(connection, filePath, new DateTime(2026, 5, 2, 11, 5, 0, DateTimeKind.Utc), "{\"OcrText\":\"stale text\"}");
        }

        var service = new AssistantHistoryService();

        IReadOnlyList<AssistantHistoryItem> items = await service.GetLatestScreenshotsAsync(1, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(items[0].Exists, Is.False);
            Assert.That(items[0].OcrText, Is.Null);
        });
    }

    [Test]
    public async Task SearchScreenshotsAsync_FindsIndexedOcrText()
    {
        string historyPath = SettingsManager.GetHistoryFilePath();
        string filePath = Path.Combine(SettingsManager.HistoryFolder, "indexed-capture.png");
        File.WriteAllText(filePath, "image placeholder");

        using (var connection = new SqliteConnection($"Data Source={historyPath}"))
        {
            connection.Open();
            CreateHistoryTable(connection);
            InsertHistoryItem(connection, filePath, new DateTime(2026, 5, 9, 14, 0, 0, DateTimeKind.Utc));
        }

        new HistoryOcrIndexStore(historyPath).UpsertText(1, filePath, null, "Quarterly roadmap review", "test", "en");

        var service = new AssistantHistoryService();

        IReadOnlyList<AssistantHistoryItem> items = await service.SearchScreenshotsAsync("roadmap", 10, CancellationToken.None);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].OcrText, Is.EqualTo("Quarterly roadmap review"));
    }

    private static void CreateHistoryTable(SqliteConnection connection)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS History(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT,
                FilePath TEXT,
                DateTime TEXT,
                Type TEXT,
                Host TEXT,
                URL TEXT,
                ThumbnailURL TEXT,
                DeletionURL TEXT,
                ShortenedURL TEXT,
                Tags TEXT
            );
            """;
        create.ExecuteNonQuery();
    }

    private static void InsertHistoryItem(SqliteConnection connection, string filePath, DateTime dateTime, string tags = "{}")
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO History(FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags)
            VALUES('', $filePath, $dateTime, 'Image', '', '', '', '', '', $tags);
            """;
        insert.Parameters.AddWithValue("$filePath", filePath);
        insert.Parameters.AddWithValue("$dateTime", dateTime.ToString("O"));
        insert.Parameters.AddWithValue("$tags", tags);
        insert.ExecuteNonQuery();
    }

}
