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

using System.Reflection;
using Avalonia.Controls;
using XerahS.Common;

namespace XerahS.UI.Helpers;

/// <summary>
/// Token picker for filename / subfolder name-pattern text boxes (ShareX CodeMenu).
/// </summary>
public static class NamePatternMenu
{
    public readonly record struct Group(string? Category, IReadOnlyList<Item> Items);

    public readonly record struct Item(string Pattern, string Header);

    public static IReadOnlyList<CodeMenuEntryFilename> GetEntries(params CodeMenuEntryFilename[] ignored)
    {
        HashSet<CodeMenuEntryFilename> ignoredSet = ignored.ToHashSet();
        return typeof(CodeMenuEntryFilename)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(CodeMenuEntryFilename))
            .Select(field => field.GetValue(null))
            .OfType<CodeMenuEntryFilename>()
            .Where(entry => !ignoredSet.Contains(entry))
            .ToArray();
    }

    public static IReadOnlyList<Group> BuildGroups(params CodeMenuEntryFilename[] ignored)
    {
        return GetEntries(ignored)
            .GroupBy(entry => entry.Category)
            .Select(group => new Group(
                group.Key,
                group.Select(entry =>
                {
                    string pattern = entry.ToPrefixString();
                    return new Item(pattern, $"{pattern} - {entry.Description}");
                }).ToArray()))
            .ToArray();
    }

    public static (string Text, int Caret) InsertAtSelection(string? text, int selectionStart, int selectionEnd, string insertion)
    {
        string value = text ?? string.Empty;
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, value.Length);
        return (value.Insert(start, insertion), start + insertion.Length);
    }

    public static void Attach(TextBox textBox, params CodeMenuEntryFilename[] ignored)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        List<MenuItem> rootItems = [];
        foreach (Group group in BuildGroups(ignored))
        {
            List<MenuItem> items = group.Items.Select(entry =>
            {
                MenuItem item = new()
                {
                    Header = entry.Header,
                    Focusable = false
                };
                item.Click += (_, _) => InsertInto(textBox, entry.Pattern);
                return item;
            }).ToList();

            if (string.IsNullOrWhiteSpace(group.Category))
            {
                rootItems.AddRange(items);
            }
            else
            {
                rootItems.Add(new MenuItem
                {
                    Header = group.Category,
                    ItemsSource = items,
                    Focusable = false
                });
            }
        }

        ContextMenu menu = new()
        {
            Focusable = false,
            Placement = PlacementMode.Bottom,
            ItemsSource = rootItems
        };
        textBox.ContextMenu = menu;
        textBox.GotFocus += (_, _) =>
        {
            if (!menu.IsOpen)
            {
                menu.Open(textBox);
            }
        };
    }

    private static void InsertInto(TextBox textBox, string insertion)
    {
        (string text, int caret) = InsertAtSelection(
            textBox.Text,
            textBox.SelectionStart,
            textBox.SelectionEnd,
            insertion);
        textBox.Text = text;
        textBox.CaretIndex = caret;
        textBox.Focus();
    }
}
