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
        Dictionary<string, EffectItem> filterEffectsById = filtersCategory.AllEffects
            .ToDictionary(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, EffectItem> allEffectsById = panel.Categories
            .Where(category => category.Name != "Favorites")
            .SelectMany(category => category.AllEffects)
            .ToDictionary(effect => effect.EffectId, StringComparer.OrdinalIgnoreCase);

        Assert.Multiple(() =>
        {
            foreach (FilterDefinition definition in FilterCatalog.Definitions)
            {
                Assert.That(allEffectsById.TryGetValue(definition.Id, out EffectItem? effectItem), Is.True, definition.Id);
                Assert.That(effectItem!.Name, Is.EqualTo(definition.BrowserLabel), definition.Id);
                Assert.That(effectItem.Icon, Is.EqualTo(definition.Icon), definition.Id);
                Assert.That(effectItem.Description, Is.EqualTo(definition.Description), definition.Id);
                Assert.That(filterEffectsById.ContainsKey(definition.Id), Is.EqualTo(definition.IncludeInFiltersCategory), definition.Id);
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

        SliderFilterParameterState size = GetParameterState<SliderFilterParameterState>(dialog, "size");
        SliderFilterParameterState strength = GetParameterState<SliderFilterParameterState>(dialog, "strength");
        SliderFilterParameterState offsetX = GetParameterState<SliderFilterParameterState>(dialog, "offset_x");
        SliderFilterParameterState offsetY = GetParameterState<SliderFilterParameterState>(dialog, "offset_y");
        ColorFilterParameterState color = GetParameterState<ColorFilterParameterState>(dialog, "color");
        CheckboxFilterParameterState autoResize = GetParameterState<CheckboxFilterParameterState>(dialog, "auto_resize");

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

    [AvaloniaTest]
    public void SchemaDrivenFilterDialog_ASCIIArt_Apply_Uses_Configured_Text_And_Checkboxes()
    {
        FilterDefinition definition = GetDefinition("ascii_art");
        var dialog = new SchemaDrivenFilterDialog(definition);

        GetParameterState<SliderFilterParameterState>(dialog, "cell_size").Value = 10;
        GetParameterState<SliderFilterParameterState>(dialog, "contrast").Value = 145;
        GetParameterState<CheckboxFilterParameterState>(dialog, "invert").Value = true;
        GetParameterState<CheckboxFilterParameterState>(dialog, "dark_background").Value = false;
        GetParameterState<CheckboxFilterParameterState>(dialog, "use_source_color").Value = false;
        GetParameterState<TextFilterParameterState>(dialog, "character_set").Value = "@. ";

        EffectEventArgs args = CaptureApply(dialog);

        using SKBitmap source = CreateSampleBitmap();
        using SKBitmap expectedInput = source.Copy();
        using SKBitmap actualInput = source.Copy();
        using SKBitmap expected = new ASCIIArtImageEffect
        {
            CellSize = 10,
            Contrast = 145,
            CharacterSet = "@. ",
            Invert = true,
            DarkBackground = false,
            UseSourceColor = false
        }.Apply(expectedInput);
        using SKBitmap actual = args.EffectOperation(actualInput);

        Assert.That(args.StatusMessage, Is.EqualTo("Applied ASCII art"));
        AssertBitmapsEqual(expected, actual);
    }

    [AvaloniaTest]
    public void SchemaDrivenFilterDialog_ConvolutionMatrix_Apply_Uses_Configured_Numeric_Settings()
    {
        FilterDefinition definition = GetDefinition("convolution_matrix");
        var dialog = new SchemaDrivenFilterDialog(definition);

        GetParameterState<NumericFilterParameterState>(dialog, "x1_y0").Value = -1;
        GetParameterState<NumericFilterParameterState>(dialog, "x0_y1").Value = -1;
        GetParameterState<NumericFilterParameterState>(dialog, "x1_y1").Value = 5;
        GetParameterState<NumericFilterParameterState>(dialog, "x2_y1").Value = -1;
        GetParameterState<NumericFilterParameterState>(dialog, "x1_y2").Value = -1;
        GetParameterState<NumericFilterParameterState>(dialog, "factor").Value = 2.5m;
        GetParameterState<NumericFilterParameterState>(dialog, "offset").Value = 6;

        EffectEventArgs args = CaptureApply(dialog);

        using SKBitmap source = CreateSampleBitmap();
        using SKBitmap expectedInput = source.Copy();
        using SKBitmap actualInput = source.Copy();
        using SKBitmap expected = new ConvolutionMatrixImageEffect
        {
            X0Y0 = 0,
            X1Y0 = -1,
            X2Y0 = 0,
            X0Y1 = -1,
            X1Y1 = 5,
            X2Y1 = -1,
            X0Y2 = 0,
            X1Y2 = -1,
            X2Y2 = 0,
            Factor = 2.5,
            Offset = 6
        }.Apply(expectedInput);
        using SKBitmap actual = args.EffectOperation(actualInput);

        Assert.That(args.StatusMessage, Is.EqualTo("Applied Convolution matrix"));
        AssertBitmapsEqual(expected, actual);
    }

    private static FilterDefinition GetDefinition(string id)
    {
        Assert.That(FilterCatalog.TryGetDefinition(id, out FilterDefinition? definition), Is.True, id);
        Assert.That(definition, Is.Not.Null, id);
        return definition!;
    }

    private static TState GetParameterState<TState>(SchemaDrivenFilterDialog dialog, string key)
        where TState : FilterParameterState
    {
        return dialog.ParameterStates
            .OfType<TState>()
            .Single(parameter => parameter.Key == key);
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
