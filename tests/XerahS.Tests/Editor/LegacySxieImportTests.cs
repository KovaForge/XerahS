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

using NUnit.Framework;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using SkiaSharp;
using XerahS.Common.Helpers;

namespace XerahS.Tests.Editor;

[TestFixture]
public class LegacySxieImportTests
{
    // Abridged Config.json from the ShareX "GoldBorder" preset.
    private const string GoldBorderJson = """
        {
          "Name": "GoldBorder",
          "Effects": [
            { "$type": "Canvas", "Margin": "5, 0, 5, 0", "Color": "Transparent", "Enabled": true },
            { "$type": "DrawBackground", "Color": "Black", "UseGradient": true,
              "Gradient": { "Type": "Vertical", "Colors": [
                { "Color": "0, 218, 180, 0", "Location": 0.0 },
                { "Color": "246, 203, 0", "Location": 50.0 },
                { "Color": "0, 218, 180, 0", "Location": 100.0 } ] },
              "Enabled": true },
            { "$type": "DrawBackground", "Color": "239, 185, 79", "UseGradient": false, "Enabled": true },
            { "$type": "Shadow", "Opacity": 0.6, "Size": 20, "Darkness": 0.0, "Color": "218, 180, 0", "Offset": "0, 0", "Enabled": true },
            { "$type": "AutoCrop", "Sides": "Top, Bottom, Left, Right", "Enabled": true }
          ]
        }
        """;

    [Test]
    public void ImportFromJson_MapsShareXShortTypeNames()
    {
        LegacyPresetImportResult result = LegacyImageEffectImporter.ImportFromJson(GoldBorderJson);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SkippedEffects, Is.Empty);
        Assert.That(result.MappedEffects.Select(e => e.TargetTypeName), Is.EqualTo(new[]
        {
            "ResizeCanvasImageEffect", "DrawBackgroundEffect", "DrawBackgroundEffect", "ShadowImageEffect", "AutoCropImageEffect"
        }));

        Dictionary<string, object?> canvas = result.MappedEffects[0].Properties;
        Assert.That(canvas["Left"], Is.EqualTo(5));
        Assert.That(canvas["Top"], Is.EqualTo(0));
        Assert.That(canvas["Right"], Is.EqualTo(5));
        Assert.That(canvas["BackgroundColor"], Is.EqualTo(SKColors.Transparent));

        var stops = (List<LegacyGradientStop>)result.MappedEffects[1].Properties["GradientStops"]!;
        Assert.That(stops[0].Color, Is.EqualTo(new SKColor(218, 180, 0, 0)));
        Assert.That(stops[1].Color, Is.EqualTo(new SKColor(246, 203, 0)));
        Assert.That(stops[1].Location, Is.EqualTo(50f));

        Assert.That(result.MappedEffects[2].Properties.ContainsKey("GradientStops"), Is.False);
        Assert.That(result.MappedEffects[2].Properties["Color"], Is.EqualTo(new SKColor(239, 185, 79)));

        Dictionary<string, object?> shadow = result.MappedEffects[3].Properties;
        Assert.That((float)shadow["Opacity"]!, Is.EqualTo(60f).Within(0.01f));
        Assert.That(shadow["OffsetX"], Is.EqualTo(0));
        Assert.That(shadow["Color"], Is.EqualTo(new SKColor(218, 180, 0)));
    }

    [Test]
    public void ParseLegacyColor_ResolvesAnyNamedColor()
    {
        Assert.That(LegacyImageEffectImporter.ParseLegacyColor("Gold"), Is.EqualTo(SKColors.Gold));
        Assert.That(LegacyImageEffectImporter.ParseLegacyColor("#FF0000"), Is.EqualTo(SKColors.Red));
    }

    [Test]
    public void DrawBackgroundEffect_Gradient_PaintsStops()
    {
        using var source = new SKBitmap(10, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.Transparent);
        var effect = new DrawBackgroundEffect
        {
            UseGradient = true,
            GradientType = DrawingGradientType.Vertical,
            GradientStops =
            [
                new DrawingGradientStop(SKColors.Red, 0),
                new DrawingGradientStop(SKColors.Blue, 100)
            ]
        };

        using SKBitmap result = effect.Apply(source);

        Assert.That(result.GetPixel(5, 0).Red, Is.GreaterThan(200));
        Assert.That(result.GetPixel(5, 99).Blue, Is.GreaterThan(200));
    }
}
