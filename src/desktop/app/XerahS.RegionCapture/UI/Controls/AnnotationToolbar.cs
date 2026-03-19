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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Theming;

namespace XerahS.RegionCapture.UI.Controls;

/// <summary>
/// Minimal toolbar surface used by RegionCapture overlay.
/// This control remains XerahS-specific, but it consumes ShareX.ImageEditor's
/// shared icon and control resources so icon/font updates do not drift.
/// </summary>
public partial class AnnotationToolbar : UserControl
{
    private const double AccentForegroundDarkSwitchRatio = 1.75;

    private readonly SolidColorBrush? _activeBrush;
    private readonly SolidColorBrush? _activeForegroundBrush;
    private IPlatformSettings? _platformSettings;

    public event EventHandler<IBrush>? ColorChanged;
    public event EventHandler<IBrush>? FillColorChanged;
    public event EventHandler<IBrush>? TextColorChanged;
    public event EventHandler<int>? WidthChanged;
    public event EventHandler<int>? CornerRadiusChanged;
    public event EventHandler<float>? FontSizeChanged;
    public event EventHandler<float>? StrengthChanged;

    // Compatibility helpers for future UI wiring.
    public void RaiseColorChanged(IBrush brush) => ColorChanged?.Invoke(this, brush);
    public void RaiseFillColorChanged(IBrush brush) => FillColorChanged?.Invoke(this, brush);
    public void RaiseTextColorChanged(IBrush brush) => TextColorChanged?.Invoke(this, brush);
    public void RaiseWidthChanged(int width) => WidthChanged?.Invoke(this, width);
    public void RaiseCornerRadiusChanged(int cornerRadius) => CornerRadiusChanged?.Invoke(this, cornerRadius);
    public void RaiseFontSizeChanged(float fontSize) => FontSizeChanged?.Invoke(this, fontSize);
    public void RaiseStrengthChanged(float strength) => StrengthChanged?.Invoke(this, strength);

    public AnnotationToolbar()
    {
        InitializeComponent();
        _activeBrush = Resources["AnnotationToolbarActiveBrush"] as SolidColorBrush;
        _activeForegroundBrush = Resources["AnnotationToolbarActiveForegroundBrush"] as SolidColorBrush;
        WireCompatibilityEvents();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WireCompatibilityEvents()
    {
        if (this.FindControl<ColorPickerDropdown>("StrokeColorPicker") is ColorPickerDropdown strokePicker)
        {
            strokePicker.ColorChanged += (_, brush) => RaiseColorChanged(brush);
        }

        if (this.FindControl<ColorPickerDropdown>("FillColorPicker") is ColorPickerDropdown fillPicker)
        {
            fillPicker.ColorChanged += (_, brush) => RaiseFillColorChanged(brush);
        }

        if (this.FindControl<ColorPickerDropdown>("TextColorPicker") is ColorPickerDropdown textColorPicker)
        {
            textColorPicker.ColorChanged += (_, brush) => RaiseTextColorChanged(brush);
        }

        if (this.FindControl<WidthPickerDropdown>("StrokeWidthPicker") is WidthPickerDropdown widthPicker)
        {
            widthPicker.WidthChanged += (_, width) => RaiseWidthChanged(width);
        }

        if (this.FindControl<CornerRadiusPickerDropdown>("CornerRadiusPicker") is CornerRadiusPickerDropdown cornerRadiusPicker)
        {
            cornerRadiusPicker.CornerRadiusChanged += (_, cornerRadius) => RaiseCornerRadiusChanged(cornerRadius);
        }

        if (this.FindControl<FontSizePickerDropdown>("FontSizePicker") is FontSizePickerDropdown fontSizePicker)
        {
            fontSizePicker.FontSizeChanged += (_, fontSize) => RaiseFontSizeChanged(fontSize);
        }

        if (this.FindControl<StrengthSlider>("EffectStrengthSlider") is StrengthSlider strengthSlider)
        {
            strengthSlider.StrengthChanged += (_, strength) => RaiseStrengthChanged(strength);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged += OnThemeChanged;
        RefreshPlatformColorTracking();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        SetPlatformSettings(null);
    }

    private void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant theme)
    {
        Dispatcher.UIThread.Post(() => UpdateAccentBrushes());
    }

    private void RefreshPlatformColorTracking()
    {
        SetPlatformSettings(TopLevel.GetTopLevel(this)?.PlatformSettings ?? Application.Current?.PlatformSettings);
        UpdateAccentBrushes(_platformSettings?.GetColorValues());
    }

    private void SetPlatformSettings(IPlatformSettings? platformSettings)
    {
        if (ReferenceEquals(_platformSettings, platformSettings))
        {
            return;
        }

        if (_platformSettings != null)
        {
            _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
        }

        _platformSettings = platformSettings;

        if (_platformSettings != null)
        {
            _platformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
        }
    }

    private void OnPlatformColorValuesChanged(object? sender, PlatformColorValues colorValues)
    {
        Dispatcher.UIThread.Post(() => UpdateAccentBrushes(colorValues));
    }

    private void UpdateAccentBrushes(PlatformColorValues? colorValues = null)
    {
        if (_activeBrush == null || _activeForegroundBrush == null)
        {
            return;
        }

        Color accentColor = colorValues?.AccentColor1 ?? default;
        if (accentColor.A == 0 &&
            Application.Current?.TryGetResource("SystemAccentColor", ActualThemeVariant, out object? resourceValue) == true)
        {
            accentColor = resourceValue switch
            {
                Color color => color,
                SolidColorBrush brush => brush.Color,
                _ => default
            };
        }

        if (accentColor.A == 0)
        {
            return;
        }

        _activeBrush.Color = accentColor;
        _activeForegroundBrush.Color = GetAccentForegroundColor(accentColor);
    }

    private Color GetAccentForegroundColor(Color accentColor)
    {
        Color lightForeground = GetThemeColor(
            ThemeManager.ShareXDark,
            "ShareX.Color.Text",
            Color.Parse("#D8DADB"));

        Color darkForeground = GetThemeColor(
            ThemeManager.ShareXLight,
            "ShareX.Color.Text",
            Color.Parse("#4E4E4E"));

        double lightContrast = GetContrastRatio(lightForeground, accentColor);
        double darkContrast = GetContrastRatio(darkForeground, accentColor);

        return darkContrast >= lightContrast * AccentForegroundDarkSwitchRatio
            ? darkForeground
            : lightForeground;
    }

    private Color GetThemeColor(Avalonia.Styling.ThemeVariant theme, string resourceKey, Color fallback)
    {
        if (!Resources.TryGetResource(resourceKey, theme, out object? resourceValue))
        {
            return fallback;
        }

        return resourceValue switch
        {
            Color color => color,
            SolidColorBrush brush => brush.Color,
            _ => fallback
        };
    }

    private static double GetContrastRatio(Color firstColor, Color secondColor)
    {
        double firstLuminance = GetRelativeLuminance(firstColor);
        double secondLuminance = GetRelativeLuminance(secondColor);

        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        double red = LinearizeColorChannel(color.R);
        double green = LinearizeColorChannel(color.G);
        double blue = LinearizeColorChannel(color.B);

        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double LinearizeColorChannel(byte channel)
    {
        double normalized = channel / 255.0;

        return normalized <= 0.03928
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}
