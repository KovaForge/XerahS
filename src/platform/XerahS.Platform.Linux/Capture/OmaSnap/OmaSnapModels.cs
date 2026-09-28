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

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>Output of <c>omasnap --host-capabilities</c> (XIP0088 Phase 1 contract).</summary>
internal sealed class OmaSnapCapabilities
{
    public const int MinimumHostMode = 1;

    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("hyprland")] public bool Hyprland { get; init; }
    [JsonPropertyName("extImageCopyCapture")] public bool ExtImageCopyCapture { get; init; }
    [JsonPropertyName("layerShell")] public bool LayerShell { get; init; }
    [JsonPropertyName("hostMode")] public int HostMode { get; init; }
    [JsonPropertyName("targets")] public string[] Targets { get; init; } = [];
    [JsonPropertyName("editor")] public string[] Editor { get; init; } = [];
    [JsonPropertyName("pin")] public bool Pin { get; init; }

    /// <summary>Raw probe JSON, kept for diagnostics.</summary>
    [JsonIgnore] public string? RawJson { get; init; }

    /// <summary>Why the engine cannot be used, or null when usable.</summary>
    [JsonIgnore] public string? FailureReason { get; init; }

    [JsonIgnore]
    public bool IsUsable =>
        FailureReason == null &&
        Ok && Hyprland && ExtImageCopyCapture && LayerShell && HostMode >= MinimumHostMode;

    public bool SupportsTarget(HostedCaptureTarget target) =>
        Targets.Length == 0 || Targets.Contains(OmaSnapArguments.TargetName(target), StringComparer.OrdinalIgnoreCase);

    public static OmaSnapCapabilities Unusable(string reason, string? rawJson = null) =>
        new() { FailureReason = reason, RawJson = rawJson };

    public string Describe()
    {
        if (IsUsable)
        {
            return $"OmaSnap {Version ?? "?"} · Hyprland · ready";
        }

        if (FailureReason != null)
        {
            return $"OmaSnap unavailable: {FailureReason}";
        }

        var missing = new List<string>();
        if (!Ok) missing.Add("probe reported not ok");
        if (!Hyprland) missing.Add("not Hyprland");
        if (!ExtImageCopyCapture) missing.Add("no ext-image-copy-capture");
        if (!LayerShell) missing.Add("no layer-shell");
        if (HostMode < MinimumHostMode) missing.Add($"host mode {HostMode} < {MinimumHostMode}");
        return $"OmaSnap {Version ?? "?"} unavailable: {string.Join(", ", missing)}";
    }

    public static OmaSnapCapabilities Parse(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<OmaSnapCapabilities>(json);
            if (parsed == null)
            {
                return Unusable("empty probe output", json);
            }

            return new OmaSnapCapabilities
            {
                SchemaVersion = parsed.SchemaVersion,
                Ok = parsed.Ok,
                Version = parsed.Version,
                Hyprland = parsed.Hyprland,
                ExtImageCopyCapture = parsed.ExtImageCopyCapture,
                LayerShell = parsed.LayerShell,
                HostMode = parsed.HostMode,
                Targets = parsed.Targets ?? [],
                Editor = parsed.Editor ?? [],
                Pin = parsed.Pin,
                RawJson = json
            };
        }
        catch (JsonException ex)
        {
            return Unusable($"probe output is not valid JSON ({ex.Message})", json);
        }
    }
}

internal sealed class OmaSnapRegionJson
{
    [JsonPropertyName("x")] public int X { get; init; }
    [JsonPropertyName("y")] public int Y { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
}

internal sealed class OmaSnapWindowJson
{
    [JsonPropertyName("class")] public string? Class { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("address")] public string? Address { get; init; }
}

/// <summary>Result object written by <c>omasnap --host … --result-json</c>.</summary>
internal sealed class OmaSnapResultJson
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("target")] public string? Target { get; init; }
    [JsonPropertyName("path")] public string? Path { get; init; }
    [JsonPropertyName("pixelWidth")] public int PixelWidth { get; init; }
    [JsonPropertyName("pixelHeight")] public int PixelHeight { get; init; }
    [JsonPropertyName("logicalWidth")] public int LogicalWidth { get; init; }
    [JsonPropertyName("logicalHeight")] public int LogicalHeight { get; init; }
    [JsonPropertyName("scale")] public double Scale { get; init; } = 1.0;
    [JsonPropertyName("monitor")] public string? Monitor { get; init; }
    [JsonPropertyName("region")] public OmaSnapRegionJson? Region { get; init; }
    [JsonPropertyName("window")] public OmaSnapWindowJson? Window { get; init; }
    [JsonPropertyName("annotated")] public bool Annotated { get; init; }
    [JsonPropertyName("documentPath")] public string? DocumentPath { get; init; }
    [JsonPropertyName("omasnapVersion")] public string? OmaSnapVersion { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

/// <summary>Exit codes defined by the host-mode contract.</summary>
internal static class OmaSnapExitCodes
{
    public const int Ok = 0;
    public const int Failure = 1;
    public const int Usage = 2;
    public const int Cancelled = 3;
}

/// <summary>Turns an OmaSnap process outcome into a <see cref="HostedCaptureResult"/>.</summary>
internal static class OmaSnapResultParser
{
    public static HostedCaptureResult Parse(string? json, int exitCode, string expectedOutputPath)
    {
        OmaSnapResultJson? parsed = null;
        string? parseError = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<OmaSnapResultJson>(json);
            }
            catch (JsonException ex)
            {
                parseError = ex.Message;
            }
        }

        string? status = parsed?.Status?.Trim().ToLowerInvariant();
        if (exitCode == OmaSnapExitCodes.Cancelled || status == "cancelled")
        {
            return HostedCaptureResult.Cancelled();
        }

        if (exitCode == OmaSnapExitCodes.Usage)
        {
            return HostedCaptureResult.Failed(parsed?.Error ?? "OmaSnap rejected the arguments (exit 2).");
        }

        if (parsed == null)
        {
            return HostedCaptureResult.Failed(parseError != null
                ? $"OmaSnap result is not valid JSON ({parseError})."
                : $"OmaSnap exited with code {exitCode} without a result.");
        }

        if (exitCode != OmaSnapExitCodes.Ok || status != "ok")
        {
            return HostedCaptureResult.Failed(parsed.Error ?? $"OmaSnap reported '{parsed.Status ?? "unknown"}' (exit {exitCode}).");
        }

        string path = string.IsNullOrWhiteSpace(parsed.Path) ? expectedOutputPath : parsed.Path;
        if (!string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(expectedOutputPath), StringComparison.Ordinal))
        {
            // Host mode must write exactly where the host asked; never read files from elsewhere.
            return HostedCaptureResult.Failed($"OmaSnap wrote to an unexpected path: {path}");
        }

        if (!File.Exists(path))
        {
            return HostedCaptureResult.Failed($"OmaSnap reported ok but {path} does not exist.");
        }

        return new HostedCaptureResult
        {
            Status = HostedCaptureStatus.Ok,
            ImagePath = path,
            PixelWidth = parsed.PixelWidth,
            PixelHeight = parsed.PixelHeight,
            Scale = parsed.Scale <= 0 ? 1.0 : parsed.Scale,
            Monitor = parsed.Monitor,
            Region = parsed.Region is { Width: > 0, Height: > 0 } r ? new Rectangle(r.X, r.Y, r.Width, r.Height) : null,
            WindowClass = parsed.Window?.Class,
            WindowTitle = parsed.Window?.Title,
            Annotated = parsed.Annotated,
            EngineVersion = parsed.OmaSnapVersion
        };
    }
}
