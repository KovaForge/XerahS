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

using Microsoft.Data.Sqlite;
using XerahS.Common;

namespace XerahS.History;

public sealed record HistoryOcrIndexEntry(
    long HistoryItemId,
    string FilePath,
    string? FileHash,
    string? OcrText,
    string? Engine,
    string? Language,
    double? Confidence,
    DateTime IndexedAt,
    string Status);

public sealed record HistoryOcrSearchMatch(long HistoryItemId, string OcrText, string FilePath);

public sealed class HistoryOcrIndexStore
{
    private const string IndexedStatus = "indexed";
    private readonly string _dbPath;

    public HistoryOcrIndexStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void EnsureDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);
    }

    public void UpsertText(
        long historyItemId,
        string filePath,
        string? fileHash,
        string ocrText,
        string? engine = null,
        string? language = null,
        double? confidence = null)
    {
        if (historyItemId <= 0 || string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(ocrText))
        {
            return;
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO HistoryOcrIndex
                (HistoryItemId, FilePath, FileHash, OcrText, Engine, Language, Confidence, IndexedAt, Status)
            VALUES
                (@HistoryItemId, @FilePath, @FileHash, @OcrText, @Engine, @Language, @Confidence, @IndexedAt, @Status)
            ON CONFLICT(HistoryItemId) DO UPDATE SET
                FilePath = excluded.FilePath,
                FileHash = excluded.FileHash,
                OcrText = excluded.OcrText,
                Engine = excluded.Engine,
                Language = excluded.Language,
                Confidence = excluded.Confidence,
                IndexedAt = excluded.IndexedAt,
                Status = excluded.Status;
            """;
        cmd.Parameters.AddWithValue("@HistoryItemId", historyItemId);
        cmd.Parameters.AddWithValue("@FilePath", filePath);
        cmd.Parameters.AddWithValue("@FileHash", (object?)fileHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OcrText", NormalizeOcrText(ocrText));
        cmd.Parameters.AddWithValue("@Engine", (object?)engine ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Language", (object?)language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Confidence", (object?)confidence ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IndexedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Status", IndexedStatus);
        cmd.ExecuteNonQuery();
    }

    public void MarkStatus(long historyItemId, string filePath, string status, string? fileHash = null)
    {
        if (historyItemId <= 0 || string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO HistoryOcrIndex
                (HistoryItemId, FilePath, FileHash, OcrText, Engine, Language, Confidence, IndexedAt, Status)
            VALUES
                (@HistoryItemId, @FilePath, @FileHash, NULL, NULL, NULL, NULL, @IndexedAt, @Status)
            ON CONFLICT(HistoryItemId) DO UPDATE SET
                FilePath = excluded.FilePath,
                FileHash = excluded.FileHash,
                IndexedAt = excluded.IndexedAt,
                Status = excluded.Status;
            """;
        cmd.Parameters.AddWithValue("@HistoryItemId", historyItemId);
        cmd.Parameters.AddWithValue("@FilePath", filePath);
        cmd.Parameters.AddWithValue("@FileHash", (object?)fileHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IndexedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.ExecuteNonQuery();
    }

    public string? GetText(long historyItemId)
    {
        if (historyItemId <= 0)
        {
            return null;
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT OcrText
            FROM HistoryOcrIndex
            WHERE HistoryItemId = @HistoryItemId AND Status = @Status AND OcrText IS NOT NULL
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@HistoryItemId", historyItemId);
        cmd.Parameters.AddWithValue("@Status", IndexedStatus);

        object? result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public Dictionary<long, string> GetTexts(IEnumerable<long> historyItemIds)
    {
        long[] ids = historyItemIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<long, string>();
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        string[] parameterNames = ids.Select((_, index) => $"@Id{index}").ToArray();
        cmd.CommandText = $"""
            SELECT HistoryItemId, OcrText
            FROM HistoryOcrIndex
            WHERE Status = @Status
              AND OcrText IS NOT NULL
              AND HistoryItemId IN ({string.Join(",", parameterNames)});
            """;
        cmd.Parameters.AddWithValue("@Status", IndexedStatus);
        for (int i = 0; i < ids.Length; i++)
        {
            cmd.Parameters.AddWithValue(parameterNames[i], ids[i]);
        }

        Dictionary<long, string> texts = new();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long id = reader.GetInt64(0);
            string text = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(text))
            {
                texts[id] = text;
            }
        }

        return texts;
    }

    public List<HistoryOcrSearchMatch> Search(string? query, int limit = 500)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<HistoryOcrSearchMatch>();
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT HistoryItemId, OcrText, FilePath
            FROM HistoryOcrIndex
            WHERE Status = @Status
              AND OcrText IS NOT NULL
              AND OcrText LIKE @Query ESCAPE '\'
            ORDER BY IndexedAt DESC
            LIMIT @Limit;
            """;
        cmd.Parameters.AddWithValue("@Status", IndexedStatus);
        cmd.Parameters.AddWithValue("@Query", $"%{EscapeLike(query.Trim())}%");
        cmd.Parameters.AddWithValue("@Limit", Math.Clamp(limit, 1, 5000));

        List<HistoryOcrSearchMatch> matches = new();
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string text = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(text))
            {
                matches.Add(new HistoryOcrSearchMatch(reader.GetInt64(0), text, reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }

        return matches;
    }

    public int CountIndexed()
    {
        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM HistoryOcrIndex WHERE Status = @Status AND OcrText IS NOT NULL;";
        cmd.Parameters.AddWithValue("@Status", IndexedStatus);
        object? result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    public void Delete(long historyItemId)
    {
        if (historyItemId <= 0)
        {
            return;
        }

        using SqliteConnection connection = OpenConnection();
        EnsureDatabase(connection);

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM HistoryOcrIndex WHERE HistoryItemId = @HistoryItemId;";
        cmd.Parameters.AddWithValue("@HistoryItemId", historyItemId);
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        FileHelpers.CreateDirectoryFromFilePath(_dbPath);
        SqliteConnection connection = new($"Data Source={_dbPath};Pooling=False");
        connection.Open();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static void EnsureDatabase(SqliteConnection connection)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS HistoryOcrIndex (
                HistoryItemId INTEGER PRIMARY KEY,
                FilePath TEXT NOT NULL,
                FileHash TEXT,
                OcrText TEXT,
                Engine TEXT,
                Language TEXT,
                Confidence REAL,
                IndexedAt TEXT NOT NULL,
                Status TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_HistoryOcrIndex_Status
                ON HistoryOcrIndex(Status);

            CREATE INDEX IF NOT EXISTS IX_HistoryOcrIndex_FilePath
                ON HistoryOcrIndex(FilePath);
            """;
        cmd.ExecuteNonQuery();
    }

    private static string NormalizeOcrText(string text)
    {
        return string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}
