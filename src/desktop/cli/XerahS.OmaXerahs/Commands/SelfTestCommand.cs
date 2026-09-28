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

using System.CommandLine;
using Microsoft.Data.Sqlite;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

/// <summary>
/// Hidden packaging smoke test. Release builds run "omaxerahs selftest" against the published
/// single-file binary to prove native dependencies (the e_sqlite3 provider used by upload
/// history) load outside the main app.
/// </summary>
internal static class SelfTestCommand
{
    internal static Command Create()
    {
        var command = new Command("selftest", "Verify that bundled native libraries load (packaging check).")
        {
            Hidden = true
        };
        command.SetAction(_ =>
        {
            JsonStdout.Enabled = true;
            return Run();
        });
        return command;
    }

    internal static int Run()
    {
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var sqliteCommand = connection.CreateCommand();
            sqliteCommand.CommandText = "select sqlite_version()";
            string version = Convert.ToString(sqliteCommand.ExecuteScalar()) ?? string.Empty;
            JsonStdout.Write(new SelfTestResponse { Sqlite = version });
            return 0;
        }
        catch (Exception ex)
        {
            return JsonStdout.WriteFailureAndExit(CliErrorCodes.Incompatible, $"SQLite could not load: {ex.GetBaseException().Message}");
        }
    }
}

internal sealed class SelfTestResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string Sqlite { get; init; } = string.Empty;
}
