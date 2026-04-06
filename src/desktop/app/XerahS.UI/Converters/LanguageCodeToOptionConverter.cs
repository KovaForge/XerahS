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
using Avalonia.Data;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.UI.Converters;

/// <summary>
/// Converts between a language code string (ViewModel.SelectedLanguage) and
/// a LanguageOption object (for ComboBox.SelectedItem binding).
/// </summary>
public class LanguageCodeToOptionConverter : IMultiValueConverter
{
    public static readonly LanguageCodeToOptionConverter Instance = new();

    /// <summary>
    /// Converts ViewModel code + AvailableLanguages to a LanguageOption for display.
    /// </summary>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] = SelectedLanguage code (string)
        // values[1] = AvailableLanguages (IList&lt;LanguageOption&gt;)
        if (values.Count < 2)
            return null;

        if (values[0] is not string code || string.IsNullOrEmpty(code))
            return null;

        if (values[1] is not IEnumerable<LanguageOption> languages)
            return null;

        return languages.FirstOrDefault(l => l.Code == code);
    }

    /// <summary>
    /// Converts a LanguageOption (from ComboBox selection) back to a language code string.
    /// Returns the actual object from AvailableLanguages to maintain reference equality.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[1] = AvailableLanguages (from original MultiBinding)
        // value = SelectedLanguage (the code string, not the LanguageOption)
        // We need to find the option in the collection by code
        if (parameter is IEnumerable<LanguageOption> languages)
        {
            if (value is string code)
            {
                return languages.FirstOrDefault(l => l.Code == code);
            }
        }

        // Fallback: return value as-is (shouldn't reach here normally)
        return value;
    }
}
