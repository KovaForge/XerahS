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

namespace XerahS.UI.Converters;

/// <summary>
/// Converts a UploaderOption's Id to bool based on whether it matches
/// the selected uploader from the parent DataContext.
/// </summary>
public class UploaderIdEqualityConverter : IMultiValueConverter
{
    public static readonly UploaderIdEqualityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] = UploaderOption.Id (string)
        // values[1] = SelectedUploaderId from VM (string)
        if (values.Count < 2)
            return false;

        if (values[0] is not string id || string.IsNullOrEmpty(id))
            return false;

        if (values[1] is not string selectedId)
            return false;

        return id == selectedId;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This is called when the RadioButton is clicked
        // parameter contains the uploader id
        if (value is true && parameter is string id)
            return id;

        return BindingOperations.DoNothing;
    }
}
