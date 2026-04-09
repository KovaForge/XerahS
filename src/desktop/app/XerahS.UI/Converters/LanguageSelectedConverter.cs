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

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Data;

namespace XerahS.UI.Converters;

/// <summary>
/// Converts whether a language tag is in the SelectedLanguages collection.
/// Used for OCR language checkbox binding.
/// </summary>
public class LanguageSelectedConverter : IMultiValueConverter
{
    public static readonly LanguageSelectedConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] = LanguageTag (string) from the OcrLanguageOption
        // values[1] = SelectedLanguages (ObservableCollection<string>) from VM
        if (values.Count < 2)
            return false;

        if (values[0] is not string tag || string.IsNullOrEmpty(tag))
            return false;

        if (values[1] is not ObservableCollection<string> selected)
            return false;

        return selected.Contains(tag);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // The checkbox click is handled in code-behind via ToggleLanguageCommand
        return BindingOperations.DoNothing;
    }
}
