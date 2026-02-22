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
using Avalonia.Media;
using System;
using XerahS.RegionCapture.UI.Controls;

namespace XerahS.RegionCapture.UI;

public partial class OverlayWindow
{
    private void WireUpToolbarEvents()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar == null)
        {
            return;
        }

        toolbar.ColorChanged += OnToolbarColorChanged;
        toolbar.FillColorChanged += OnToolbarFillColorChanged;
        toolbar.WidthChanged += OnToolbarWidthChanged;
        toolbar.FontSizeChanged += OnToolbarFontSizeChanged;
        toolbar.StrengthChanged += OnToolbarStrengthChanged;
        toolbar.ShadowButtonClick += OnToolbarShadowButtonClicked;
    }

    private void OnToolbarColorChanged(object? sender, IBrush color)
    {
        if (color is SolidColorBrush solidBrush)
        {
            _viewModel.SelectedColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
        }
    }

    private void OnToolbarFillColorChanged(object? sender, IBrush color)
    {
        if (color is SolidColorBrush solidBrush)
        {
            _viewModel.FillColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
        }
    }

    private void OnToolbarWidthChanged(object? sender, int width)
    {
        _viewModel.StrokeWidth = width;
    }

    private void OnToolbarFontSizeChanged(object? sender, float fontSize)
    {
        _viewModel.FontSize = fontSize;
    }

    private void OnToolbarStrengthChanged(object? sender, float strength)
    {
        _viewModel.EffectStrength = strength;
    }

    private void OnToolbarShadowButtonClicked(object? sender, EventArgs e)
    {
        _viewModel.ShadowEnabled = !_viewModel.ShadowEnabled;
    }

    /// <summary>
    /// XIP-0023: Toggles the visibility of the annotation toolbar in the overlay.
    /// </summary>
    private void ToggleAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = !toolbar.IsVisible;
        }
    }

    /// <summary>
    /// XIP-0023: Shows the annotation toolbar.
    /// </summary>
    public void ShowAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = true;
        }
    }

    /// <summary>
    /// XIP-0023: Hides the annotation toolbar.
    /// </summary>
    public void HideAnnotationToolbar()
    {
        var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
        if (toolbar != null)
        {
            toolbar.IsVisible = false;
        }
    }
}
