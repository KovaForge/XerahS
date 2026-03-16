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
using Avalonia.Data.Converters;
using XerahS.History;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Converters;

/// <summary>
/// Converts (HistoryItem, HistoryViewModel) to <see cref="HistoryItemMenuContext"/> for the shared context menu.
/// Use with MultiBinding: first binding = item, second binding = HistoryViewModel (e.g. Root.DataContext).
/// Required because ConverterParameter does not resolve bindings in Avalonia.
/// </summary>
public sealed class HistoryItemMenuContextConverter : IMultiValueConverter
{
    public static readonly HistoryItemMenuContextConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Count == 0)
            return null;

        if (values[0] is IHistoryItemMenuContext menuContext)
        {
            return menuContext;
        }

        if (values.Count < 2)
            return null;

        if (values[0] is HistoryItem item && values[1] is HistoryViewModel vm)
        {
            return new HistoryItemMenuContext(vm, item);
        }

        return null;
    }
}
