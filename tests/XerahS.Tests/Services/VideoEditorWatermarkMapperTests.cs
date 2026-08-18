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
using ShareX.ImageEditor.Core.ImageEffects;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.VideoEditor.Hosting;
using SkiaSharp;
using XerahS.UI.Services;

namespace XerahS.Tests.Services;

[TestFixture]
public class VideoEditorWatermarkMapperTests
{
    [Test]
    public void FromEffects_ReturnsNull_WhenPresetIsEmpty()
    {
        Assert.That(VideoEditorWatermarkMapper.FromEffects([]), Is.Null);
    }

    [Test]
    public void FromEffects_MapsTextWatermark()
    {
        WatermarkSettings? settings = VideoEditorWatermarkMapper.FromEffects(
        [
            new TextWatermarkEffect
            {
                Text = "XerahS",
                FontSize = 32,
                TextColor = new SKColor(255, 0, 0),
                Placement = DrawingPlacement.TopLeft
            }
        ]);

        Assert.That(settings, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(settings!.Enabled, Is.True);
            Assert.That(settings.Text, Is.EqualTo("XerahS"));
            Assert.That(settings.FontSize, Is.EqualTo(32));
            Assert.That(settings.FontColor, Is.EqualTo("#FF0000"));
            Assert.That(settings.PositionX, Is.EqualTo(0.05).Within(0.001));
            Assert.That(settings.PositionY, Is.EqualTo(0.05).Within(0.001));
        });
    }

    [Test]
    public void FromEffects_MapsExistingImageWatermark()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "xerahs-map-wm-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(imagePath, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            WatermarkSettings? settings = VideoEditorWatermarkMapper.FromEffects(
            [
                new DrawImageEffect
                {
                    ImageLocation = imagePath,
                    Opacity = 40,
                    Placement = DrawingPlacement.BottomRight
                }
            ]);

            Assert.That(settings, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(settings!.Enabled, Is.True);
                Assert.That(settings.ImagePath, Is.EqualTo(imagePath));
                Assert.That(settings.Opacity, Is.EqualTo(0.4).Within(0.001));
                Assert.That(settings.PositionX, Is.EqualTo(0.95).Within(0.001));
                Assert.That(settings.PositionY, Is.EqualTo(0.95).Within(0.001));
            });
        }
        finally
        {
            try { File.Delete(imagePath); } catch { }
        }
    }
}
