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

using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using XerahS.Common;
using XerahS.Core;

namespace XerahS.UI.Assistant;

public sealed record AssistantAliasDefinition(string Alias, string Command);

public sealed class AssistantLocalMemoryStore
{
    private static readonly Regex AliasRegex = new(
        @"^\s*(?:alias|remember|save\s+alias)\s+""?(?<alias>[^""=]+?)""?\s+(?:as|=)\s+""?(?<command>.+?)""?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> BuiltInAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["copy last five paths"] = "Give me the local file path of my last 5 screenshots",
        ["bug report shot"] = "Upload the latest screenshot"
    };

    private readonly string _databasePath;
    private bool _initialized;

    public AssistantLocalMemoryStore()
        : this(Path.Combine(SettingsManager.SettingsFolder, "assistant", "history.db"))
    {
    }

    public AssistantLocalMemoryStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    public bool TryParseAliasDefinition(string prompt, out AssistantAliasDefinition definition)
    {
        definition = default!;
        var match = AliasRegex.Match(prompt);
        if (!match.Success)
        {
            return false;
        }

        string alias = Normalize(match.Groups["alias"].Value);
        string command = match.Groups["command"].Value.Trim();
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        definition = new AssistantAliasDefinition(alias, command);
        return true;
    }

    public bool TryResolveAlias(string prompt, out string command)
    {
        command = string.Empty;
        string alias = Normalize(prompt);
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        if (BuiltInAliases.TryGetValue(alias, out command!))
        {
            return true;
        }

        EnsureInitialized();
        using var connection = OpenConnection();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT command FROM aliases WHERE alias = $alias LIMIT 1;";
        select.Parameters.AddWithValue("$alias", alias);
        object? value = select.ExecuteScalar();
        command = value as string ?? string.Empty;
        return !string.IsNullOrWhiteSpace(command);
    }

    public void SaveAlias(AssistantAliasDefinition definition)
    {
        EnsureInitialized();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO aliases(alias, command, pinned, updated_at)
            VALUES($alias, $command, 1, $updatedAt)
            ON CONFLICT(alias) DO UPDATE SET
                command = excluded.command,
                pinned = 1,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$alias", Normalize(definition.Alias));
        command.Parameters.AddWithValue("$command", definition.Command.Trim());
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void RecordExecution(AssistantDeterministicIntent intent, string actionSummary, bool pinned = false)
    {
        if (!SettingsManager.Settings.AssistantPromptHistoryEnabled || intent.Kind == AssistantDeterministicIntentKind.Unknown)
        {
            return;
        }

        EnsureInitialized();
        using var connection = OpenConnection();
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO recent_commands(intent_kind, action_summary, pinned, created_at)
            VALUES($intentKind, $actionSummary, $pinned, $createdAt);
            """;
        insert.Parameters.AddWithValue("$intentKind", intent.Kind.ToString());
        insert.Parameters.AddWithValue("$actionSummary", actionSummary);
        insert.Parameters.AddWithValue("$pinned", pinned ? 1 : 0);
        insert.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();

        using var prune = connection.CreateCommand();
        prune.CommandText = """
            DELETE FROM recent_commands
            WHERE pinned = 0
              AND (
                    created_at < $expiresAt
                    OR id NOT IN (
                        SELECT id FROM recent_commands
                        ORDER BY pinned DESC, datetime(created_at) DESC
                        LIMIT 100
                    )
              );
            """;
        prune.Parameters.AddWithValue("$expiresAt", DateTimeOffset.UtcNow.AddDays(-30).ToString("O"));
        prune.ExecuteNonQuery();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            FileHelpers.CreateDirectory(directory);
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS aliases(
                alias TEXT PRIMARY KEY,
                command TEXT NOT NULL,
                pinned INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS recent_commands(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                intent_kind TEXT NOT NULL,
                action_summary TEXT NOT NULL,
                pinned INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        _initialized = true;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString());
        connection.Open();
        return connection;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
}
