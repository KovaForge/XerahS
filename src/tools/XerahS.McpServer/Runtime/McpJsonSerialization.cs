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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XerahS.McpServer.Runtime;

internal static class McpJsonSerialization
{
    public static JsonObject CreateResource(string uri, JsonObject payload)
    {
        return new JsonObject
        {
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["uri"] = uri,
                    ["mimeType"] = "application/json",
                    ["text"] = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
                })
        };
    }

    public static JsonArray FlagsToNames<TEnum>(TEnum flags)
        where TEnum : struct, Enum
    {
        var value = (Enum)(object)flags;
        JsonNode[] names = Enum.GetValues<TEnum>()
            .Where(flag => Convert.ToUInt64(flag, CultureInfo.InvariantCulture) != 0)
            .Where(flag => value.HasFlag((Enum)(object)flag))
            .Select(flag => JsonValue.Create(flag.ToString())!)
            .Cast<JsonNode>()
            .ToArray();

        return new JsonArray(names);
    }

    public static JsonObject SerializeRectangle(System.Drawing.Rectangle rectangle)
    {
        return new JsonObject
        {
            ["x"] = rectangle.X,
            ["y"] = rectangle.Y,
            ["width"] = rectangle.Width,
            ["height"] = rectangle.Height
        };
    }

    public static JsonArray ToJsonArray(IEnumerable<string?> values)
    {
        JsonNode[] nodes = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => JsonValue.Create(value)!)
            .Cast<JsonNode>()
            .ToArray();

        return new JsonArray(nodes);
    }

    public static string GuessMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".txt" or ".log" or ".md" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
