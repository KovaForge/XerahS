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
using Tmds.DBus;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Detection;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal sealed class GnomeShellWindowPointQueryHelper : IWaylandWindowPointQueryHelper
{
    private const string GnomeShellBusName = "org.gnome.Shell";
    private static readonly ObjectPath GnomeShellObjectPath = new("/org/gnome/Shell");

    public WindowPointQueryCapability Capability { get; } =
        DesktopCaptureInterfaceChecker.HasInterface(GnomeShellBusName, "/org/gnome/Shell", GnomeShellBusName)
            ? new WindowPointQueryCapability(WindowPointQuerySupportLevel.Full, null)
            : new WindowPointQueryCapability(
                WindowPointQuerySupportLevel.Unsupported,
                "Wayland session: GNOME Shell window helper is unavailable.");

    public WindowInfo? GetWindowAtPoint(Point logicalPoint)
    {
        if (!Capability.IsEnabled)
            return null;

        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();
            var proxy = connection.CreateProxy<IGnomeShellEval>(GnomeShellBusName, GnomeShellObjectPath);
            var (success, result) = proxy.EvalAsync(CreateEvalScript(logicalPoint)).GetAwaiter().GetResult();
            if (!success)
                return null;

            return ParseEvalResult(result);
        }
        catch
        {
            return null;
        }
    }

    internal static string CreateEvalScript(Point logicalPoint)
    {
        return
            $"(() => {{ const pointX = {logicalPoint.X}; const pointY = {logicalPoint.Y}; const overlayTitle = \"{PlatformWindowTitles.RegionCaptureOverlay}\"; const actors = global.get_window_actors(); for (let index = actors.length - 1; index >= 0; index--) {{ const actor = actors[index]; const window = actor ? actor.meta_window : null; if (!window) continue; const title = window.get_title() || \"\"; if (title === overlayTitle) continue; const rect = window.get_frame_rect(); if (!rect || rect.width <= 1 || rect.height <= 1) continue; if (pointX < rect.x || pointX >= rect.x + rect.width || pointY < rect.y || pointY >= rect.y + rect.height) continue; return JSON.stringify({{ stableSequence: window.get_stable_sequence(), title: title, className: window.get_wm_class() || \"\", x: rect.x, y: rect.y, width: rect.width, height: rect.height }}); }} return \"\"; }})()";
    }

    internal static WindowInfo? ParseEvalResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(result);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            string title = GetString(root, "title");
            if (string.Equals(title, PlatformWindowTitles.RegionCaptureOverlay, StringComparison.Ordinal))
                return null;

            int stableSequence = GetInt32(root, "stableSequence");
            var bounds = new Rectangle(
                GetInt32(root, "x"),
                GetInt32(root, "y"),
                GetInt32(root, "width"),
                GetInt32(root, "height"));

            return new WindowInfo
            {
                Handle = stableSequence != 0
                    ? (nint)stableSequence
                    : (nint)HashCode.Combine(title, GetString(root, "className"), bounds.X, bounds.Y, bounds.Width, bounds.Height),
                Title = title,
                ClassName = GetString(root, "className"),
                Bounds = bounds,
                IsVisible = true
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out int value)
            ? value
            : 0;
    }
}

[DBusInterface("org.gnome.Shell")]
internal interface IGnomeShellEval : IDBusObject
{
    Task<(bool success, string result)> EvalAsync(string script);
}
