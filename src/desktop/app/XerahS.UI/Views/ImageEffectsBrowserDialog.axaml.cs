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

using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using ShareX.ImageEditor.Presentation.Controls;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class ImageEffectsBrowserDialog : SurfaceWindow
{
    public ImageEffectsBrowserDialog()
    {
        InitializeComponent();
        WireBrowserEvents();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WireBrowserEvents()
    {
        var browser = this.FindControl<EffectBrowserPanel>("EffectBrowserPanel");
        if (browser == null)
        {
            return;
        }

        browser.EffectDialogRequested += OnEffectDialogRequested;
        browser.Rotate90CWRequested += (_, _) => TryAddAndClose(vm => vm.TryAddRotate90ClockwiseEffect());
        browser.Rotate90CCWRequested += (_, _) => TryAddAndClose(vm => vm.TryAddRotate90CounterClockwiseEffect());
        browser.Rotate180Requested += (_, _) => TryAddAndClose(vm => vm.TryAddRotate180Effect());
        browser.RotateCustomAngleRequested += (_, _) => TryAddAndClose(vm => vm.TryAddRotateCustomEffect());
        browser.FlipHorizontalRequested += (_, _) => TryAddAndClose(vm => vm.TryAddFlipHorizontalEffect());
        browser.FlipVerticalRequested += (_, _) => TryAddAndClose(vm => vm.TryAddFlipVerticalEffect());
        browser.InvertRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("invert"));
        browser.BlackAndWhiteRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("black_white"));
        browser.PolaroidRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("polaroid"));
        browser.EdgeDetectRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("edge_detect"));
        browser.EmbossRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("emboss"));
        browser.MeanRemovalRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("mean_removal"));
        browser.SmoothRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("smooth"));
        browser.AutoCropImageRequested += (_, _) => TryAddAndClose(vm => vm.TryAddEffectByBrowserId("auto_crop_image"));
    }

    private void OnEffectDialogRequested(object? sender, EffectDialogRequestedEventArgs e)
    {
        if (DataContext is not ImageEffectsViewModel vm)
        {
            return;
        }

        if (vm.TryAddEffectByBrowserId(e.EffectId))
        {
            Close();
        }
    }

    private void TryAddAndClose(Func<ImageEffectsViewModel, bool> addAction)
    {
        if (DataContext is not ImageEffectsViewModel vm)
        {
            return;
        }

        if (addAction(vm))
        {
            Close();
        }
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
