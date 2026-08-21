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
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace XerahS.UI.Controls
{
    public partial class PropertyGrid : UserControl
    {
        public static readonly StyledProperty<object?> SelectedObjectProperty =
            AvaloniaProperty.Register<PropertyGrid, object?>(nameof(SelectedObject));

        public object? SelectedObject
        {
            get => GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }

        public event EventHandler? PropertyValueChanged;

        public PropertyGrid()
        {
            InitializeComponent();
            this.GetObservable(SelectedObjectProperty).Subscribe(new SimpleObserver<object?>(OnSelectedObjectChanged));
        }

        private class SimpleObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public SimpleObserver(Action<T> onNext) => _onNext = onNext;
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }

        private void OnSelectedObjectChanged(object? obj)
        {
            PropertiesPanel.Children.Clear();

            if (obj == null) return;

            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.CanRead)
                .Where(p =>
                {
                    var attr = p.GetCustomAttribute<BrowsableAttribute>();
                    return attr == null || attr.Browsable;
                });

            var grouped = properties
                .GroupBy(GetCategory)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in grouped)
            {
                var header = new TextBlock
                {
                    Text = group.Key,
                    Classes = { "propertyCategoryHeader" }
                };
                PropertiesPanel.Children.Add(header);

                foreach (var prop in group.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    var row = CreatePropertyRow(obj, prop);
                    if (row != null)
                    {
                        PropertiesPanel.Children.Add(row);
                    }
                }
            }
        }

        private Control? CreatePropertyRow(object obj, PropertyInfo prop)
        {
            var editor = CreateEditor(obj, prop);
            if (editor == null)
            {
                return null;
            }

            if (editor is EffectSlider sliderEditor)
            {
                sliderEditor.Label = GetDisplayName(prop);
                return sliderEditor;
            }

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("180, *"),
                Classes = { "propertyRow" }
            };

            // Label
            var label = new TextBlock
            {
                Text = GetDisplayName(prop),
                Classes = { "propertyName" }
            };
            ToolTip.SetTip(label, GetDescription(prop));
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);

            return grid;
        }

        private Control? CreateEditor(object obj, PropertyInfo prop)
        {
            var type = prop.PropertyType;
            var binding = new Binding(prop.Name)
            {
                Source = obj,
                Mode = BindingMode.TwoWay
            };

            if (type == typeof(bool))
            {
                var checkBox = new CheckBox();
                checkBox.Bind(CheckBox.IsCheckedProperty, binding);
                checkBox.IsCheckedChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return checkBox;
            }
            if (type.IsEnum)
            {
                var comboBox = new ComboBox();
                comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                comboBox.ItemsSource = Enum.GetValues(type);
                comboBox.Bind(ComboBox.SelectedItemProperty, binding);
                comboBox.SelectionChanged += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return comboBox;
            }
            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            {
                var slider = CreateNumericSlider(prop, 1);
                slider.Bind(RangeBase.ValueProperty, binding);
                slider.PropertyChanged += (s, e) =>
                {
                    if (e.Property == RangeBase.ValueProperty)
                    {
                        PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
                return slider;
            }
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                var slider = CreateNumericSlider(prop, 0.1);
                slider.ValueStringFormat = "{}{0:0.##}";
                slider.Bind(RangeBase.ValueProperty, binding);
                slider.PropertyChanged += (s, e) =>
                {
                    if (e.Property == RangeBase.ValueProperty)
                    {
                        PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
                return slider;
            }
            if (type == typeof(string))
            {
                var textBox = new TextBox();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }
            if (type == typeof(System.Drawing.Color))
            {
                var textBox = new TextBox();
                binding.Converter = new ColorStringConverter();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }
            if (type == typeof(SKColor))
            {
                var textBox = new TextBox();
                binding.Converter = new SKColorStringConverter();
                textBox.Bind(TextBox.TextProperty, binding);
                textBox.LostFocus += (s, e) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
                return textBox;
            }

            // Fallback for complex types?
            if (type.IsClass && type != typeof(string))
            {
                // Nested expandable? Too complex for now.
                return new TextBlock { Text = $"({type.Name})", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
            }

            return new TextBox { Text = $"(Unsupported {type.Name})", IsReadOnly = true };
        }

        private static EffectSlider CreateNumericSlider(PropertyInfo prop, double fallbackStep)
        {
            var (minimum, maximum, step) = GetNumericBounds(prop, fallbackStep);

            return new EffectSlider
            {
                Minimum = minimum,
                Maximum = maximum,
                TickFrequency = step,
                SmallChange = step,
                LargeChange = Math.Max(step * 5, step),
                IsSnapToTickEnabled = false,
                ValueStringFormat = "{}{0:0.##}"
            };
        }

        private static (double Minimum, double Maximum, double Step) GetNumericBounds(PropertyInfo prop, double fallbackStep)
        {
            var range = prop.GetCustomAttribute<RangeAttribute>();
            if (range != null &&
                double.TryParse(range.Minimum?.ToString(), out var rangeMin) &&
                double.TryParse(range.Maximum?.ToString(), out var rangeMax))
            {
                return (rangeMin, rangeMax, fallbackStep);
            }

            string name = prop.Name;
            if (name.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Strength", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Percentage", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 100, 1);
            }

            if (name.Contains("Angle", StringComparison.OrdinalIgnoreCase))
            {
                return (-180, 180, 1);
            }

            if (name.Contains("Hue", StringComparison.OrdinalIgnoreCase))
            {
                return (-180, 180, 1);
            }

            if (name.Contains("Gamma", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 5, 0.05);
            }

            if (name.Contains("Radius", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Size", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Depth", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Range", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Width", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Height", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 200, 1);
            }

            if (name.Contains("Offset", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Shift", StringComparison.OrdinalIgnoreCase))
            {
                return (-200, 200, 1);
            }

            if (name.Contains("Threshold", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 255, 1);
            }

            return (0, 100, fallbackStep);
        }

        private string GetDisplayName(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DisplayNameAttribute>();
            return attr?.DisplayName ?? prop.Name;
        }

        private string GetDescription(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? "";
        }

        private string GetCategory(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<CategoryAttribute>();
            return string.IsNullOrWhiteSpace(attr?.Category) ? "Miscellaneous" : attr.Category;
        }

        private class ColorStringConverter : Avalonia.Data.Converters.IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is System.Drawing.Color c)
                {
                    if (c.IsNamedColor) return c.Name;
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                return value?.ToString();
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is string s)
                {
                    try
                    {
                        if (s.StartsWith("#"))
                        {
                            return System.Drawing.ColorTranslator.FromHtml(s);
                        }
                        return System.Drawing.Color.FromName(s);
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
                return BindingOperations.DoNothing;
            }
        }

        private class SKColorStringConverter : Avalonia.Data.Converters.IValueConverter
        {
            public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is SKColor c)
                {
                    return c.ToString();
                }
                return value?.ToString();
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            {
                if (value is string s)
                {
                    if (SKColor.TryParse(s, out var color))
                    {
                        return color;
                    }
                }
                return BindingOperations.DoNothing;
            }
        }
    }
}
