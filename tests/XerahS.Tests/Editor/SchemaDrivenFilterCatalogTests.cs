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
using Avalonia.Interactivity;
using Avalonia.Media;
using NUnit.Framework;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Filters;
using ShareX.ImageEditor.Presentation.Views.Dialogs;
using SkiaSharp;

namespace XerahS.Tests.Editor;

[TestFixture]
public class SchemaDrivenFilterCatalogTests
{
    [AvaloniaTest]
    public void EffectDialogRegistry_Creates_SchemaDriven_Dialogs_For_Catalog_Filters()
    {
        Assert.Multiple(() =>
        {
            foreach (FilterDefinition definition in FilterCatalog.Definitions)
            {
                Assert.That(EffectDialogRegistry.TryCreate(definition.Id, out UserControl? dialog), Is.True, definition.Id);
                Assert.That(dialog, Is.TypeOf<SchemaDrivenFilterDialog>(), definition.Id);
                Assert.That(((SchemaDrivenFilterDialog)dialog!).Title, Is.EqualTo(definition.Name), definition.Id);
            }
        });
    }

    [AvaloniaTest]
    public void EffectBrowserPanel_Uses_FilterCatalog_Metadata_For_Catalog_Filters()
    {
        var panel = new EffectBrowserPanel();
        var filtersCategory = panel.Categories.Single(category => category.Name == "Filters");
        Dictionary<string, EffectItem> effectsById = filtersCategory.AllEffects
            .ToDictionary(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase);

        Assert.Multiple(() =>
        {
            foreach (FilterDefinition definition in FilterCatalog.Definitions)
            {
                Assert.That(effectsById.TryGetValue(definition.Id, out EffectItem? effectItem), Is.True, definition.Id);
                Assert.That(effectItem!.Name, Is.EqualTo(definition.BrowserLabel), definition.Id);
                Assert.That(effectItem.Icon, Is.EqualTo(definition.Icon), definition.Id);
                Assert.That(effectItem.Description, Is.EqualTo(definition.Description), definition.Id);
            }
        });
    }

    [AvaloniaTest]
    public void SchemaDrivenFilterDialog_Blur_Apply_Uses_Configured_Radius()
    {
        FilterDefinition definition = GetDefinition("blur");
        var dialog = new SchemaDrivenFilterDialog(definition);
        SliderFilterParameterState radius = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "radius");
        radius.Value = 17;

        EffectEventArgs args = CaptureApply(dialog);

        using SKBitmap source = CreateSampleBitmap();
        using SKBitmap expectedInput = source.Copy();
        using SKBitmap actualInput = source.Copy();
        using SKBitmap expected = new BlurImageEffect { Radius = 17 }.Apply(expectedInput);
        using SKBitmap actual = args.EffectOperation(actualInput);

        Assert.That(args.StatusMessage, Is.EqualTo("Applied Blur"));
        AssertBitmapsEqual(expected, actual);
    }

    [AvaloniaTest]
    public void SchemaDrivenFilterDialog_Dithering_Apply_Uses_Configured_Enum_Settings()
    {
        FilterDefinition definition = GetDefinition("dithering");
        var dialog = new SchemaDrivenFilterDialog(definition);

        EnumFilterParameterState method = dialog.ParameterStates.OfType<EnumFilterParameterState>().Single(parameter => parameter.Key == "method");
        EnumFilterParameterState palette = dialog.ParameterStates.OfType<EnumFilterParameterState>().Single(parameter => parameter.Key == "palette");
        SliderFilterParameterState strength = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "strength");

        method.SelectedOption = method.Options.Single(option => Equals(option.Value, DitheringMethod.Bayer4x4));
        palette.SelectedOption = palette.Options.Single(option => Equals(option.Value, DitheringPalette.RGB332));
        strength.Value = 64;

        EffectEventArgs args = CaptureApply(dialog);

        using SKBitmap source = CreateSampleBitmap();
        using SKBitmap expectedInput = source.Copy();
        using SKBitmap actualInput = source.Copy();
        using SKBitmap expected = new DitheringImageEffect
        {
            Method = DitheringMethod.Bayer4x4,
            Palette = DitheringPalette.RGB332,
            Serpentine = true,
            Strength = 64
        }.Apply(expectedInput);
        using SKBitmap actual = args.EffectOperation(actualInput);

        Assert.That(args.StatusMessage, Is.EqualTo("Applied Dithering"));
        AssertBitmapsEqual(expected, actual);
    }

    [AvaloniaTest]
    public void SchemaDrivenFilterDialog_Glow_Apply_Uses_Configured_Color_And_Toggle()
    {
        FilterDefinition definition = GetDefinition("glow");
        var dialog = new SchemaDrivenFilterDialog(definition);

        SliderFilterParameterState size = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "size");
        SliderFilterParameterState strength = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "strength");
        SliderFilterParameterState offsetX = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "offset_x");
        SliderFilterParameterState offsetY = dialog.ParameterStates.OfType<SliderFilterParameterState>().Single(parameter => parameter.Key == "offset_y");
        ColorFilterParameterState color = dialog.ParameterStates.OfType<ColorFilterParameterState>().Single(parameter => parameter.Key == "color");
        CheckboxFilterParameterState autoResize = dialog.ParameterStates.OfType<CheckboxFilterParameterState>().Single(parameter => parameter.Key == "auto_resize");

        size.Value = 12;
        strength.Value = 65;
        offsetX.Value = -4;
        offsetY.Value = 7;
        color.Value = Color.FromArgb(255, 32, 160, 220);
        autoResize.Value = false;

        EffectEventArgs args = CaptureApply(dialog);

        using SKBitmap source = CreateSampleBitmap();
        using SKBitmap expectedInput = source.Copy();
        using SKBitmap actualInput = source.Copy();
        using SKBitmap expected = new GlowImageEffect(
            size: 12,
            strength: 65,
            color: new SKColor(32, 160, 220, 255),
            offsetX: -4,
            offsetY: 7,
            autoResize: false).Apply(expectedInput);
        using SKBitmap actual = args.EffectOperation(actualInput);

        Assert.That(args.StatusMessage, Is.EqualTo("Applied Glow"));
        AssertBitmapsEqual(expected, actual);
    }

    private static FilterDefinition GetDefinition(string id)
    {
        Assert.That(FilterCatalog.TryGetDefinition(id, out FilterDefinition? definition), Is.True, id);
        Assert.That(definition, Is.Not.Null, id);
        return definition!;
    }

    private static EffectEventArgs CaptureApply(SchemaDrivenFilterDialog dialog)
    {
        EffectEventArgs? capturedArgs = null;
        dialog.ApplyRequested += (_, args) => capturedArgs = args;

        Button? applyButton = dialog.FindControl<Button>("ApplyButton");
        Assert.That(applyButton, Is.Not.Null);

        applyButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.That(capturedArgs, Is.Not.Null);
        return capturedArgs!;
    }

    private static SKBitmap CreateSampleBitmap()
    {
        var bitmap = new SKBitmap(16, 16);

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(
                    x,
                    y,
                    new SKColor(
                        red: (byte)((x * 17) % 256),
                        green: (byte)((y * 19) % 256),
                        blue: (byte)(((x * y) * 11) % 256),
                        alpha: 255));
            }
        }

        return bitmap;
    }

    private static void AssertBitmapsEqual(SKBitmap expected, SKBitmap actual)
    {
        Assert.That(actual.Width, Is.EqualTo(expected.Width));
        Assert.That(actual.Height, Is.EqualTo(expected.Height));

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.That(actual.GetPixel(x, y), Is.EqualTo(expected.GetPixel(x, y)), $"Pixel mismatch at ({x}, {y}).");
            }
        }
    }
}
