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

using System.Text.Json;

namespace XerahS.Platform.Linux.Hyprland;

/// <summary>One keyboard bind from <c>hyprctl binds -j</c>.</summary>
public sealed record HyprlandExistingBind(int Modmask, string Key, string Description, string Dispatcher, string Argument)
{
    /// <summary>What the bind does, for the conflict list.</summary>
    public string Summary =>
        !string.IsNullOrWhiteSpace(Description) ? Description :
        string.IsNullOrWhiteSpace(Argument) ? Dispatcher : $"{Dispatcher} {Argument}";

    /// <summary>Binds from an earlier XerahS managed file are ours, not conflicts.</summary>
    public bool IsXerahS =>
        Description.StartsWith(HyprlandManagedConfig.DescriptionPrefix, StringComparison.Ordinal) ||
        Argument.Contains("omaxerahs", StringComparison.Ordinal);
}

public static class HyprlandBindsParser
{
    /// <summary>
    /// Parses <c>hyprctl binds -j</c>. Mouse binds, keycode binds and binds inside a submap
    /// cannot collide with a global XerahS key and are left out.
    /// </summary>
    public static IReadOnlyList<HyprlandExistingBind> Parse(string json)
    {
        var binds = new List<HyprlandExistingBind>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return binds;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("hyprctl binds -j did not return an array.");
        }

        foreach (JsonElement bind in document.RootElement.EnumerateArray())
        {
            if (bind.ValueKind != JsonValueKind.Object ||
                GetBool(bind, "mouse") ||
                !string.IsNullOrEmpty(GetString(bind, "submap")))
            {
                continue;
            }

            string key = GetString(bind, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            int modmask = bind.TryGetProperty("modmask", out JsonElement mask) && mask.TryGetInt32(out int value) ? value : 0;
            binds.Add(new HyprlandExistingBind(
                modmask,
                key,
                GetString(bind, "description"),
                GetString(bind, "dispatcher"),
                GetString(bind, "arg")));
        }

        return binds;
    }

    /// <summary>The first non-XerahS bind on the same keys, if any.</summary>
    public static HyprlandExistingBind? FindConflict(IEnumerable<HyprlandExistingBind> binds, int modmask, string keyName)
    {
        int wantedMask = HyprlandKeyMap.NormalizeModmask(modmask);
        string wantedKey = HyprlandKeyMap.NormalizeKeyName(keyName);
        return binds.FirstOrDefault(bind =>
            !bind.IsXerahS &&
            HyprlandKeyMap.NormalizeModmask(bind.Modmask) == wantedMask &&
            HyprlandKeyMap.NormalizeKeyName(bind.Key) == wantedKey);
    }

    /// <summary>
    /// Errors from <c>hyprctl -j configerrors</c> (a JSON array, <c>[""]</c> when clean) or the
    /// plain text form.
    /// </summary>
    public static IReadOnlyList<string> ParseConfigErrors(string output)
    {
        string trimmed = output.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        if (trimmed.StartsWith('['))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                return document.RootElement.EnumerateArray()
                    .Where(element => element.ValueKind == JsonValueKind.String)
                    .Select(element => element.GetString()!.Trim())
                    .Where(error => error.Length > 0)
                    .ToArray();
            }
            catch (JsonException)
            {
            }
        }

        return trimmed.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.Equals("no errors", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
