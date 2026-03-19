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
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using XerahS.RegionCapture.UI;
using XerahS.RegionCapture.UI.Controls;
using XerahS.RegionCapture.ViewModels;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
[NonParallelizable]
public class RegionCaptureUiSmokeTests
{
    [AvaloniaTest]
    public void AnnotationToolbar_Loads_With_ImageEditor_Resources()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Rectangle
        };

        var toolbar = new AnnotationToolbar
        {
            DataContext = viewModel
        };

        var window = new Window
        {
            Width = 1200,
            Height = 300,
            Content = toolbar
        };

        try
        {
            window.Show();

            Assert.That(toolbar.FindControl<Button>("ShadowToggleButton"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("StrokeColorPicker"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("TextColorPicker"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("CornerRadiusPicker"), Is.Not.Null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void OverlayWindow_Loads_AnnotationToolbar_Surface()
    {
        var overlayWindow = new OverlayWindow();

        try
        {
            overlayWindow.Show();

            Assert.That(overlayWindow.FindControl<AnnotationToolbar>("AnnotationToolbarControl"), Is.Not.Null);
            Assert.That(overlayWindow.FindControl<Canvas>("AnnotationCanvas"), Is.Not.Null);
            Assert.That(overlayWindow.FindControl<Panel>("RootPanel"), Is.Not.Null);
        }
        finally
        {
            overlayWindow.Close();
        }
    }
}
