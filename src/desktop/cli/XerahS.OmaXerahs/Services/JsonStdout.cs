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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XerahS.OmaXerahs.Models;

namespace XerahS.OmaXerahs.Services;

internal static class JsonStdout
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    internal static bool Enabled { get; set; }

    internal static Option<bool> CreateJsonOption()
    {
        return new Option<bool>("--json")
        {
            Description = "Write exactly one JSON object to stdout (default true when stdout is not a TTY).",
            DefaultValueFactory = static _ => Console.IsOutputRedirected
        };
    }

    internal static bool ShouldEnable(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--json", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return Console.IsOutputRedirected;
    }

    internal static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);
    }

    internal static bool IsSingleJsonObject(string json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON payload is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "JSON payload root is not an object.";
                return false;
            }

            var bytes = Encoding.UTF8.GetBytes(json.Trim());
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                error = "JSON payload is not a single object.";
                return false;
            }

            if (!reader.TrySkip())
            {
                error = "JSON payload is incomplete.";
                return false;
            }

            if (reader.Read())
            {
                error = "JSON payload contains trailing tokens.";
                return false;
            }
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }

        return true;
    }

    internal static void Write(object value)
    {
        Console.Out.WriteLine(Serialize(value));
    }

    internal static void WriteFailure(string code, string message)
    {
        Write(CliFailureResponse.Create(code, message));
        if (!Enabled)
        {
            Console.Error.WriteLine(message);
        }
    }

    internal static int WriteFailureAndExit(string code, string message)
    {
        WriteFailure(code, message);
        return 1;
    }
}
