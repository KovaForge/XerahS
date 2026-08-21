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

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace XerahS.UI.Controls;

/// <summary>
/// ShareX-style settings panel search: hide non-matching section cards in real time.
/// </summary>
public sealed class SettingsSearch : AvaloniaObject
{
    public static readonly AttachedProperty<string?> PageIdProperty =
        AvaloniaProperty.RegisterAttached<SettingsSearch, Control, string?>("PageId");

    public static readonly AttachedProperty<bool> IsPageTitleProperty =
        AvaloniaProperty.RegisterAttached<SettingsSearch, Control, bool>("IsPageTitle");

    public static readonly AttachedProperty<bool> IsPanelProperty =
        AvaloniaProperty.RegisterAttached<SettingsSearch, Control, bool>("IsPanel");

    public static readonly AttachedProperty<bool> IsAvailabilityContainerProperty =
        AvaloniaProperty.RegisterAttached<SettingsSearch, Control, bool>("IsAvailabilityContainer");

    private static readonly string[] SearchablePropertyNames = ["Text", "Content", "Header", "Watermark", "PlaceholderText"];
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SearchableProperties = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SearchableItemProperties = new();

    private SettingsSearch()
    {
    }

    public static string? GetPageId(Control control) => control.GetValue(PageIdProperty);
    public static void SetPageId(Control control, string? value) => control.SetValue(PageIdProperty, value);

    public static bool GetIsPageTitle(Control control) => control.GetValue(IsPageTitleProperty);
    public static void SetIsPageTitle(Control control, bool value) => control.SetValue(IsPageTitleProperty, value);

    public static bool GetIsPanel(Control control) => control.GetValue(IsPanelProperty);
    public static void SetIsPanel(Control control, bool value) => control.SetValue(IsPanelProperty, value);

    public static bool GetIsAvailabilityContainer(Control control) => control.GetValue(IsAvailabilityContainerProperty);
    public static void SetIsAvailabilityContainer(Control control, bool value) => control.SetValue(IsAvailabilityContainerProperty, value);

    /// <summary>
    /// Filters top-level IsPanel cards under each PageId page (or TabItem content).
    /// Also toggles TabItem visibility when pages live in a TabControl.
    /// </summary>
    public static void Apply(Control root, string? query)
    {
        query ??= string.Empty;

        HashSet<Control> processedPages = [];

        foreach (Control page in root.GetLogicalDescendants().OfType<Control>().Where(x => !string.IsNullOrEmpty(GetPageId(x))))
        {
            if (!processedPages.Add(page))
            {
                continue;
            }

            bool pageVisible = ApplyToPage(page, query);
            if (FindOwningTabItem(page) is TabItem tabItem)
            {
                tabItem.IsVisible = pageVisible;
            }
        }

        foreach (TabControl tabControl in root.GetLogicalDescendants().OfType<TabControl>())
        {
            foreach (object? item in tabControl.Items)
            {
                if (item is not TabItem tabItem || tabItem.Content is not Control content)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(GetPageId(content)) || content.GetLogicalDescendants().OfType<Control>().Any(x => !string.IsNullOrEmpty(GetPageId(x))))
                {
                    continue;
                }

                bool pageVisible = ApplyToPage(content, query, tabItem.Header?.ToString());
                tabItem.IsVisible = pageVisible;
            }

            EnsureVisibleTabSelected(tabControl);
        }
    }

    /// <summary>
    /// When the selected tab is filtered out, select the first still-visible tab so its content shows immediately.
    /// </summary>
    private static void EnsureVisibleTabSelected(TabControl tabControl)
    {
        if (tabControl.Items == null)
        {
            return;
        }

        TabItem[] visibleTabs = tabControl.Items.OfType<TabItem>().Where(static tab => tab.IsVisible).ToArray();
        if (visibleTabs.Length == 0)
        {
            return;
        }

        if (tabControl.SelectedItem is TabItem selected && selected.IsVisible)
        {
            return;
        }

        tabControl.SelectedItem = visibleTabs[0];
    }

    private static bool ApplyToPage(Control page, string query, string? fallbackTitle = null)
    {
        string pageTitle = page.GetLogicalDescendants()
            .OfType<Control>()
            .FirstOrDefault(GetIsPageTitle) is { } titleControl
            ? GetDisplayedSearchText(titleControl)
            : fallbackTitle ?? string.Empty;

        Control[] panels = page.GetLogicalDescendants()
            .OfType<Control>()
            .Where(GetIsPanel)
            .Where(x => !HasPanelAncestor(x, page))
            .ToArray();

        if (panels.Length == 0)
        {
            return Matches(string.Join(' ', pageTitle, GetItemsSourceSearchText(page), GetDisplayedSearchText(page)), query);
        }

        bool anyVisible = false;

        foreach (Control panel in panels)
        {
            bool isAvailable = IsPanelAvailable(panel, page);
            string panelSearchText = GetDisplayedSearchText(panel);
            bool visible = isAvailable && Matches(string.Join(' ', pageTitle, panelSearchText), query);
            panel.IsVisible = visible;
            anyVisible |= visible;
        }

        return anyVisible || Matches(pageTitle, query);
    }

    private static TabItem? FindOwningTabItem(Control page)
    {
        ILogical? ancestor = page.GetLogicalParent();

        while (ancestor != null)
        {
            if (ancestor is TabItem tabItem)
            {
                return tabItem;
            }

            ancestor = ancestor.GetLogicalParent();
        }

        return null;
    }

    private static bool HasPanelAncestor(Control panel, Control page)
    {
        ILogical? ancestor = panel.GetLogicalParent();

        while (ancestor != null && ancestor != page)
        {
            if (ancestor is Control control && GetIsPanel(control))
            {
                return true;
            }

            ancestor = ancestor.GetLogicalParent();
        }

        return false;
    }

    private static bool IsPanelAvailable(Control panel, Control page)
    {
        ILogical? ancestor = panel.GetLogicalParent();

        while (ancestor != null && ancestor != page)
        {
            if (ancestor is Control control && GetIsAvailabilityContainer(control) && !control.IsVisible)
            {
                return false;
            }

            ancestor = ancestor.GetLogicalParent();
        }

        return true;
    }

    private static string GetDisplayedSearchText(Control root)
    {
        List<string> values = [];
        AddDisplayedText(root, values);
        AddItemsSourceText(root, values);

        foreach (Control control in root.GetLogicalDescendants().OfType<Control>().Where(x => IsSearchAvailable(x, root)))
        {
            AddDisplayedText(control, values);
            AddItemsSourceText(control, values);
        }

        return string.Join(' ', values);
    }

    private static void AddDisplayedText(Control control, List<string> values)
    {
        PropertyInfo[] properties = SearchableProperties.GetOrAdd(control.GetType(), static type => SearchablePropertyNames
            .Select(name => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public))
            .Where(property => property is { CanRead: true } && property.GetIndexParameters().Length == 0)
            .Cast<PropertyInfo>()
            .ToArray());

        foreach (PropertyInfo property in properties)
        {
            if (control is TextBox && (property.Name == "Text" || property.Name == "PlaceholderText"))
            {
                continue;
            }

            try
            {
                if (property.GetValue(control) is string text && !string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }
            catch (TargetInvocationException)
            {
                // A custom control property should not be able to break settings search.
            }
        }
    }

    private static string GetItemsSourceSearchText(Control root)
    {
        List<string> values = [];
        AddItemsSourceText(root, values);

        foreach (ItemsControl itemsControl in root.GetLogicalDescendants().OfType<ItemsControl>().Where(x => IsSearchAvailable(x, root)))
        {
            AddItemsSourceText(itemsControl, values);
        }

        return string.Join(' ', values);
    }

    private static bool IsSearchAvailable(Control control, Control root)
    {
        ILogical? current = control;

        while (current != null && current != root)
        {
            if (current is Control candidate && GetIsAvailabilityContainer(candidate) && !candidate.IsVisible)
            {
                return false;
            }

            current = current.GetLogicalParent();
        }

        return true;
    }

    private static void AddItemsSourceText(Control control, List<string> values)
    {
        if (control is ItemsControl { ItemsSource: IEnumerable items })
        {
            AddSearchableItemText(items, values, 0);
        }
    }

    private static void AddSearchableItemText(object? value, List<string> values, int depth)
    {
        if (value == null || depth > 4)
        {
            return;
        }

        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }

            return;
        }

        if (value is IEnumerable items and not string)
        {
            foreach (object? item in items)
            {
                AddSearchableItemText(item, values, depth + 1);
            }

            return;
        }

        PropertyInfo[] properties = SearchableItemProperties.GetOrAdd(value.GetType(), static type => type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 &&
                (property.PropertyType == typeof(string) || typeof(IEnumerable).IsAssignableFrom(property.PropertyType)))
            .ToArray());

        foreach (PropertyInfo property in properties)
        {
            try
            {
                AddSearchableItemText(property.GetValue(value), values, depth + 1);
            }
            catch (TargetInvocationException)
            {
                // A custom item property should not be able to break settings search.
            }
        }
    }

    internal static bool Matches(string searchText, string query)
    {
        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Length == 0 || terms.All(term => searchText.Contains(term, StringComparison.CurrentCultureIgnoreCase));
    }
}
