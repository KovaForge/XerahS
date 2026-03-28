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
using System.Globalization;
using System.Text.Json;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal sealed class HyprlandWindowPointQueryHelper : IWaylandWindowPointQueryHelper
{
    public WindowPointQueryCapability Capability { get; } =
        WaylandWindowPointQueryCommandRunner.CommandExists("hyprctl")
            ? new WindowPointQueryCapability(WindowPointQuerySupportLevel.Full, null)
            : new WindowPointQueryCapability(
                WindowPointQuerySupportLevel.Unsupported,
                "Wayland session: install hyprctl for Hyprland window snapping.");

    public WindowInfo? GetWindowAtPoint(Point logicalPoint)
    {
        if (!Capability.IsEnabled)
            return null;

        CommandRunResult result = WaylandWindowPointQueryCommandRunner.Run("hyprctl", "-j clients");
        if (!result.Success)
        {
            result = WaylandWindowPointQueryCommandRunner.Run("hyprctl", "clients -j");
        }

        if (!result.Success)
            return null;

        return SelectWindowFromClientsJson(result.StandardOutput, logicalPoint);
    }

    internal static WindowInfo? SelectWindowFromClientsJson(string json, Point logicalPoint)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            HyprlandCandidate? bestCandidate = null;
            int index = 0;

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                index++;

                if (!TryCreateCandidate(element, logicalPoint, index, out HyprlandCandidate candidate))
                    continue;

                if (bestCandidate == null || candidate.CompareTo(bestCandidate.Value) < 0)
                {
                    bestCandidate = candidate;
                }
            }

            return bestCandidate?.Window;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryCreateCandidate(
        JsonElement element,
        Point logicalPoint,
        int index,
        out HyprlandCandidate candidate)
    {
        candidate = default;

        if (GetBoolean(element, "mapped") == false || GetBoolean(element, "hidden") == true)
            return false;

        if (!TryGetPointArray(element, "at", out Point position) ||
            !TryGetPointArray(element, "size", out Point size))
        {
            return false;
        }

        if (size.X <= 1 || size.Y <= 1)
            return false;

        var bounds = new Rectangle(position.X, position.Y, size.X, size.Y);
        if (!bounds.Contains(logicalPoint))
            return false;

        string title = GetString(element, "title");
        if (string.Equals(title, PlatformWindowTitles.RegionCaptureOverlay, StringComparison.Ordinal))
            return false;

        string className = GetString(element, "class");
        string address = GetString(element, "address");
        int focusHistoryId = GetInt32(element, "focusHistoryID", int.MaxValue);

        candidate = new HyprlandCandidate(
            Window: new WindowInfo
            {
                Handle = CreatePseudoHandle(address, title, className, bounds),
                Title = title,
                ClassName = className,
                Bounds = bounds,
                IsVisible = true
            },
            FocusHistoryId: focusHistoryId,
            Area: (long)bounds.Width * bounds.Height,
            Index: index);
        return true;
    }

    private static int CompareCandidates(HyprlandCandidate left, HyprlandCandidate right)
    {
        int focusComparison = left.FocusHistoryId.CompareTo(right.FocusHistoryId);
        if (focusComparison != 0)
            return focusComparison;

        int areaComparison = left.Area.CompareTo(right.Area);
        if (areaComparison != 0)
            return areaComparison;

        return right.Index.CompareTo(left.Index);
    }

    private static nint CreatePseudoHandle(string address, string title, string className, Rectangle bounds)
    {
        if (!string.IsNullOrWhiteSpace(address) &&
            address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(address.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long numericAddress))
        {
            return (nint)numericAddress;
        }

        return (nint)HashCode.Combine(title, className, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static bool TryGetPointArray(JsonElement element, string propertyName, out Point point)
    {
        point = default;

        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        using JsonElement.ArrayEnumerator enumerator = property.EnumerateArray();
        if (!enumerator.MoveNext())
            return false;

        int x = enumerator.Current.GetInt32();
        if (!enumerator.MoveNext())
            return false;

        int y = enumerator.Current.GetInt32();
        point = new Point(x, y);
        return true;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int GetInt32(JsonElement element, string propertyName, int defaultValue)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
               property.TryGetInt32(out int value)
            ? value
            : defaultValue;
    }

    private readonly record struct HyprlandCandidate(
        WindowInfo Window,
        int FocusHistoryId,
        long Area,
        int Index) : IComparable<HyprlandCandidate>
    {
        public int CompareTo(HyprlandCandidate other)
        {
            return CompareCandidates(this, other);
        }
    }
}
