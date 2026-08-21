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

using Avalonia.Controls;
using Avalonia.LogicalTree;
using System.Text;

namespace XerahS.UI.Services.SettingsSearch;

/// <summary>
/// Indexes checkbox labels from settings views via the logical tree
/// (works for non-selected TabItem content without forcing tab switches).
/// </summary>
public static class SettingsVisualTreeIndexer
{
    public static IReadOnlyList<SettingsSearchEntry> IndexApplicationSettings(Control root)
    {
        List<SettingsSearchEntry> entries = new(128);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (TabControl tabControl in root.GetLogicalDescendants().OfType<TabControl>())
        {
            foreach (object? item in tabControl.Items)
            {
                if (item is not TabItem tabItem)
                {
                    continue;
                }

                string tabHeader = GetHeaderText(tabItem.Header);
                if (string.IsNullOrWhiteSpace(tabHeader))
                {
                    continue;
                }

                Control content = tabItem.Content as Control ?? tabItem;
                foreach (CheckBox checkBox in content.GetLogicalDescendants().OfType<CheckBox>())
                {
                    TryAddCheckboxEntry(
                        entries,
                        seen,
                        checkBox,
                        SettingsSearchArea.Application,
                        $"Application Settings → {tabHeader}",
                        SettingsSearchCatalog.AppNavigationTag,
                        appTab: tabHeader);
                }
            }
        }

        return entries;
    }

    public static IReadOnlyList<SettingsSearchEntry> IndexDestinationSettings(
        Control root,
        IEnumerable<(string CategoryName, IEnumerable<string> InstanceNames)>? categories = null)
    {
        List<SettingsSearchEntry> entries = new(64);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        if (categories != null)
        {
            foreach ((string categoryName, IEnumerable<string> instanceNames) in categories)
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    continue;
                }

                string categoryId = $"dest-cat-{categoryName}";
                if (seen.Add(categoryId))
                {
                    entries.Add(new SettingsSearchEntry(
                        categoryId,
                        categoryName,
                        SettingsSearchArea.Destination,
                        $"Destination Settings → {categoryName}",
                        SettingsSearchCatalog.DestNavigationTag,
                        SettingsSearchSource.VisualTree,
                        destinationCategory: categoryName,
                        keywords: [categoryName]));
                }

                foreach (string instanceName in instanceNames)
                {
                    if (string.IsNullOrWhiteSpace(instanceName))
                    {
                        continue;
                    }

                    string instanceId = $"dest-inst-{categoryName}-{instanceName}";
                    if (!seen.Add(instanceId))
                    {
                        continue;
                    }

                    entries.Add(new SettingsSearchEntry(
                        instanceId,
                        instanceName,
                        SettingsSearchArea.Destination,
                        $"Destination Settings → {categoryName}",
                        SettingsSearchCatalog.DestNavigationTag,
                        SettingsSearchSource.VisualTree,
                        destinationCategory: categoryName,
                        destinationInstance: instanceName,
                        keywords: [instanceName, categoryName]));
                }
            }
        }

        foreach (CheckBox checkBox in root.GetLogicalDescendants().OfType<CheckBox>())
        {
            TryAddCheckboxEntry(
                entries,
                seen,
                checkBox,
                SettingsSearchArea.Destination,
                "Destination Settings",
                SettingsSearchCatalog.DestNavigationTag);
        }

        return entries;
    }

    private static void TryAddCheckboxEntry(
        List<SettingsSearchEntry> entries,
        HashSet<string> seen,
        CheckBox checkBox,
        SettingsSearchArea area,
        string pathLabel,
        string navigationTag,
        string? appTab = null,
        string? destinationCategory = null,
        string? destinationInstance = null)
    {
        string? label = ExtractCheckboxLabel(checkBox);
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        string id = $"cb-{area}-{appTab}-{destinationCategory}-{label}";
        if (!seen.Add(id))
        {
            return;
        }

        entries.Add(new SettingsSearchEntry(
            id,
            label,
            area,
            pathLabel,
            navigationTag,
            SettingsSearchSource.VisualTree,
            appTab: appTab,
            destinationCategory: destinationCategory,
            destinationInstance: destinationInstance,
            keywords: [label]));
    }

    public static string? ExtractCheckboxLabel(CheckBox checkBox)
    {
        if (checkBox.Content is string contentText && !string.IsNullOrWhiteSpace(contentText))
        {
            return CollapseWhitespace(contentText);
        }

        if (checkBox.Content is Control contentControl)
        {
            string nested = CollectText(contentControl);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        string? automationName = Avalonia.Automation.AutomationProperties.GetName(checkBox);
        return string.IsNullOrWhiteSpace(automationName) ? null : CollapseWhitespace(automationName);
    }

    private static string CollectText(Control root)
    {
        StringBuilder builder = new(64);
        foreach (TextBlock textBlock in root.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (string.IsNullOrWhiteSpace(textBlock.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(textBlock.Text.Trim());
        }

        return CollapseWhitespace(builder.ToString());
    }

    private static string GetHeaderText(object? header)
    {
        return header switch
        {
            string text => text.Trim(),
            TextBlock textBlock => textBlock.Text?.Trim() ?? string.Empty,
            _ => header?.ToString()?.Trim() ?? string.Empty
        };
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
