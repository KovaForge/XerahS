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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Data;
using Avalonia.Media;

namespace XerahS.UI.Onboarding;

/// <summary>
/// Converts step index to a brush for progress indicator:
/// - Completed (index &lt; current): accent color (filled)
/// - Current (index == current): accent ring
/// - Future (index &gt; current): border/subtle color
/// </summary>
public class StepIndexToBrushConverter : IMultiValueConverter
{
    public static readonly StepIndexToBrushConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Gray;

        if (values[0] is not int stepIndex) return Brushes.Gray;
        if (values[1] is not int currentIndex) return Brushes.Gray;

        // Try to get colors from theme resources
        var completedBrush = Application.Current?.FindResource("AccentFillColorDefaultBrush");
        var currentBrush = Application.Current?.FindResource("AccentFillColorSecondaryBrush");
        var futureBrush = Application.Current?.FindResource("ControlStrokeColorDefaultBrush");

        if (stepIndex < currentIndex)
        {
            // Completed step - filled accent
            return completedBrush as IBrush ?? new SolidColorBrush(Color.Parse("#0078D4"));
        }
        else if (stepIndex == currentIndex)
        {
            // Current step - ring style
            return currentBrush as IBrush ?? new SolidColorBrush(Color.Parse("#0078D4")) { Opacity = 0.3 };
        }
        else
        {
            // Future step
            return futureBrush as IBrush ?? new SolidColorBrush(Color.Parse("#3F3F3F"));
        }
    }
}

/// <summary>
/// Returns true if the step index is less than the provided threshold (used for connecting lines).
/// </summary>
public class LessThanConverter : IValueConverter
{
    public static readonly LessThanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int stepIndex && parameter is int threshold)
        {
            return stepIndex < threshold;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Subtracts one from the provided value (used for step count in progress).
/// </summary>
public class SubtractOneConverter : IValueConverter
{
    public static readonly SubtractOneConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return Math.Max(0, count - 1);
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if the string is not null or empty.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if the string equals the parameter (for radio button selection).
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string param)
        {
            return str.Equals(param, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string param)
        {
            return param;
        }
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Returns true if the list contains the specified item (for OCR language selection).
/// </summary>
public class ListContainsConverter : IMultiValueConverter
{
    public static readonly ListContainsConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;
        if (values[0] is not IEnumerable<string> list) return false;
        if (values[1] is not string item) return false;

        return list.Contains(item);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

/// <summary>
/// Converts bool to success/error brush for connection test results.
/// </summary>
public class BoolToSuccessErrorBrushConverter : IValueConverter
{
    public static readonly BoolToSuccessErrorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSuccess)
        {
            if (isSuccess)
            {
                return Application.Current?.FindResource("StatusSuccessForegroundBrush")
                    ?? new SolidColorBrush(Color.Parse("#183624"));
            }
            else
            {
                return Application.Current?.FindResource("StatusErrorForegroundBrush")
                    ?? new SolidColorBrush(Color.Parse("#7A1622"));
            }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to string based on parameter (format: "trueText|falseText").
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && parameter is string options)
        {
            var parts = options.Split('|');
            if (parts.Length >= 2)
            {
                return b ? parts[0] : parts[1];
            }
            return b.ToString();
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if the value equals the parameter (int or string comparison).
/// </summary>
public class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter == null) return false;

        // Try int comparison first
        if (value is int intVal && int.TryParse(parameter.ToString(), out int paramInt))
        {
            return intVal == paramInt;
        }

        // Fall back to string comparison
        return value?.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int result))
            {
                return result;
            }
            return parameter;
        }
        return BindingOperations.DoNothing;
    }
}