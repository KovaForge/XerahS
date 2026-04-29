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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal sealed class SwayWindowPointQueryHelper : IWaylandWindowPointQueryHelper
{
    public WindowPointQueryCapability Capability { get; } =
        WaylandWindowPointQueryCommandRunner.CommandExists("swaymsg")
            ? new WindowPointQueryCapability(WindowPointQuerySupportLevel.Full, null)
            : new WindowPointQueryCapability(
                WindowPointQuerySupportLevel.Unsupported,
                "Wayland session: install swaymsg for Sway window snapping.");

    public WindowInfo? GetWindowAtPoint(Point logicalPoint)
    {
        if (!Capability.IsEnabled)
            return null;

        CommandRunResult result = WaylandWindowPointQueryCommandRunner.Run("swaymsg", "-t get_tree -r");
        if (!result.Success)
        {
            result = WaylandWindowPointQueryCommandRunner.Run("swaymsg", "-t get_tree");
        }

        return result.Success
            ? SelectWindowFromTreeJson(result.StandardOutput, logicalPoint)
            : null;
    }

    internal static WindowInfo? SelectWindowFromTreeJson(string json, Point logicalPoint)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return FindWindow(document.RootElement, logicalPoint);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WindowInfo? FindWindow(JsonElement node, Point logicalPoint)
    {
        foreach (JsonElement child in EnumerateChildrenTopmostFirst(node))
        {
            WindowInfo? window = FindWindow(child, logicalPoint);
            if (window != null)
                return window;
        }

        return TryCreateWindow(node, logicalPoint, out WindowInfo? candidate)
            ? candidate
            : null;
    }

    private static IEnumerable<JsonElement> EnumerateChildrenTopmostFirst(JsonElement node)
    {
        var children = new List<JsonElement>();
        children.AddRange(GetChildElements(node, "floating_nodes"));
        children.AddRange(GetChildElements(node, "nodes"));

        if (children.Count <= 1)
            return children;

        var focusOrder = GetFocusOrder(node);
        if (focusOrder.Count == 0)
        {
            children.Reverse();
            return children;
        }

        children.Sort((left, right) =>
        {
            int leftOrder = GetFocusIndex(left, focusOrder);
            int rightOrder = GetFocusIndex(right, focusOrder);
            return leftOrder.CompareTo(rightOrder);
        });

        return children;
    }

    private static List<long> GetFocusOrder(JsonElement node)
    {
        var focusOrder = new List<long>();
        if (!node.TryGetProperty("focus", out JsonElement focus) || focus.ValueKind != JsonValueKind.Array)
            return focusOrder;

        foreach (JsonElement element in focus.EnumerateArray())
        {
            if (element.TryGetInt64(out long id))
            {
                focusOrder.Add(id);
            }
        }

        return focusOrder;
    }

    private static int GetFocusIndex(JsonElement node, IReadOnlyList<long> focusOrder)
    {
        long id = GetInt64(node, "id");
        for (int i = 0; i < focusOrder.Count; i++)
        {
            if (focusOrder[i] == id)
                return i;
        }

        return int.MaxValue;
    }

    private static IEnumerable<JsonElement> GetChildElements(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (JsonElement child in property.EnumerateArray())
        {
            yield return child;
        }
    }

    private static bool TryCreateWindow(JsonElement node, Point logicalPoint, out WindowInfo? window)
    {
        window = null;

        string type = GetString(node, "type");
        if (type is not ("con" or "floating_con"))
            return false;

        if (GetBoolean(node, "visible") == false)
            return false;

        if (!TryGetRectangle(node, "rect", out Rectangle rect) || rect.Width <= 1 || rect.Height <= 1 || !rect.Contains(logicalPoint))
            return false;

        string title = GetString(node, "name");
        if (string.Equals(title, PlatformWindowTitles.RegionCaptureOverlay, StringComparison.Ordinal))
            return false;

        string className = GetString(node, "app_id");
        if (string.IsNullOrWhiteSpace(className) &&
            node.TryGetProperty("window_properties", out JsonElement properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            className = GetString(properties, "class");
        }

        window = new WindowInfo
        {
            Handle = (nint)GetInt64(node, "id"),
            Title = title,
            ClassName = className,
            Bounds = rect,
            IsVisible = true
        };
        return true;
    }

    private static bool TryGetRectangle(JsonElement node, string propertyName, out Rectangle rect)
    {
        rect = Rectangle.Empty;

        if (!node.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Object)
            return false;

        if (!TryGetInt32(property, "x", out int x) ||
            !TryGetInt32(property, "y", out int y) ||
            !TryGetInt32(property, "width", out int width) ||
            !TryGetInt32(property, "height", out int height) ||
            width <= 0 ||
            height <= 0 ||
            (long)x + width > int.MaxValue ||
            (long)y + height > int.MaxValue)
        {
            return false;
        }

        rect = new Rectangle(x, y, width, height);
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

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out value);
    }

    private static long GetInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt64(out long value)
            ? value
            : 0;
    }
}
