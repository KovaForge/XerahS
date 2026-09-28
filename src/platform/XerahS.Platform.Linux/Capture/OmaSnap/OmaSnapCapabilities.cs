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

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>Parsed output of <c>omasnap --host-capabilities</c> (schemaVersion 1).</summary>
public sealed record OmaSnapCapabilities(
    bool Ok,
    string? Version,
    bool Hyprland,
    bool Wayland,
    bool ExtImageCopyCapture,
    bool LayerShell,
    int HostMode,
    string BinaryPath)
{
    /// <summary>Host mode protocol this XerahS build speaks.</summary>
    public const int RequiredHostMode = 1;

    /// <summary>The probe passed and the binary speaks a host mode XerahS understands.</summary>
    public bool IsUsable => Ok && HostMode >= RequiredHostMode;

    public string Summary
    {
        get
        {
            string version = string.IsNullOrWhiteSpace(Version) ? "OmaSnap" : $"OmaSnap {Version}";
            if (IsUsable)
            {
                return $"{version} · Hyprland · ready";
            }

            if (HostMode < RequiredHostMode)
            {
                return $"{version} · no host mode (needs {RequiredHostMode}+)";
            }

            var missing = new List<string>();
            if (!Hyprland) missing.Add("Hyprland");
            if (!Wayland) missing.Add("Wayland");
            if (!ExtImageCopyCapture) missing.Add("ext-image-copy-capture");
            if (!LayerShell) missing.Add("layer-shell");
            return missing.Count == 0 ? $"{version} · unavailable" : $"{version} · missing {string.Join(", ", missing)}";
        }
    }

    /// <summary>Parses the probe JSON; null when it is not a schemaVersion 1 object.</summary>
    public static OmaSnapCapabilities? Parse(string? json, string binaryPath)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json.Trim());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                schema.GetInt32() != 1)
            {
                return null;
            }

            return new OmaSnapCapabilities(
                Ok: GetBool(root, "ok"),
                Version: root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String ? version.GetString() : null,
                Hyprland: GetBool(root, "hyprland"),
                Wayland: GetBool(root, "wayland"),
                ExtImageCopyCapture: GetBool(root, "extImageCopyCapture"),
                LayerShell: GetBool(root, "layerShell"),
                HostMode: root.TryGetProperty("hostMode", out var hostMode) && hostMode.ValueKind == JsonValueKind.Number ? hostMode.GetInt32() : 0,
                BinaryPath: binaryPath);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool GetBool(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }
}
