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
using SkiaSharp;
using XerahS.Media;

namespace XerahS.Tests.Media;

[TestFixture]
public class ImageBatchServiceTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xerahs-imagebatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string WritePng(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, ImageBatchService.Encode(bitmap, new ImageBatchOptions { Format = ImageBatchOutputFormat.Png }));
        return path;
    }

    [TestCase(ImageResizeMode.Stretch)]
    [TestCase(ImageResizeMode.Fit)]
    [TestCase(ImageResizeMode.Fill)]
    public void Resize_ProducesExactTargetSize(ImageResizeMode mode)
    {
        using var source = new SKBitmap(400, 100);
        source.Erase(SKColors.Red);

        using SKBitmap result = ImageBatchService.Resize(source, 200, 200, mode);

        Assert.That((result.Width, result.Height), Is.EqualTo((200, 200)));
        Assert.That(result.GetPixel(100, 100).Red, Is.GreaterThan(200), "centre is image content in every mode");
        if (mode == ImageResizeMode.Fit)
        {
            Assert.That(result.GetPixel(100, 5).Alpha, Is.EqualTo(0), "fit pads with transparency");
        }
        else
        {
            Assert.That(result.GetPixel(100, 5).Alpha, Is.EqualTo(255));
        }
    }

    [Test]
    public void Jpeg_FillsTransparencyWithBackground()
    {
        using var source = new SKBitmap(20, 20, SKColorType.Rgba8888, SKAlphaType.Premul);
        source.Erase(SKColors.Transparent);

        byte[] jpeg = ImageBatchService.Encode(source, new ImageBatchOptions { Format = ImageBatchOutputFormat.Jpeg, BackgroundColor = SKColors.Blue });
        using SKBitmap decoded = SKBitmap.Decode(jpeg);

        SKColor pixel = decoded.GetPixel(10, 10);
        Assert.That(pixel.Blue, Is.GreaterThan(200));
        Assert.That(pixel.Red, Is.LessThan(40));
    }

    [Test]
    public void Watermark_TextIsDrawnInChosenCorner()
    {
        using var source = new SKBitmap(400, 300);
        source.Erase(SKColors.Black);

        using SKBitmap result = ImageBatchService.ApplyWatermark(source, new ImageWatermarkOptions
        {
            Text = "WWWW",
            TextSize = 48,
            Opacity = 100,
            TextColor = SKColors.White,
            Position = ImageWatermarkPosition.BottomRight,
            Margin = 10
        });

        bool BrightIn(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (result.GetPixel(x, y).Red > 128) return true;
            return false;
        }

        Assert.That(BrightIn(200, 150, 400, 300), Is.True, "text in the bottom-right quadrant");
        Assert.That(BrightIn(0, 0, 200, 150), Is.False, "nothing in the top-left quadrant");
    }

    [Test]
    public void GetOutputPath_AppliesPatternAndRefusesToOverwriteSource()
    {
        string source = WritePng("shot.png", 10, 10, SKColors.Red);

        string output = ImageBatchService.GetOutputPath(source, new ImageBatchOptions { FileNamePattern = "$filename_small", Format = ImageBatchOutputFormat.Webp });
        Assert.That(Path.GetFileName(output), Is.EqualTo("shot_small.webp"));

        Assert.Throws<InvalidOperationException>(() =>
            ImageBatchService.GetOutputPath(source, new ImageBatchOptions { FileNamePattern = "$filename", Format = ImageBatchOutputFormat.Png }));
    }

    [Test]
    public void ProcessFile_ConvertWritesDecodableWebp()
    {
        string source = WritePng("shot.png", 32, 16, SKColors.Green);

        string output = ImageBatchService.ProcessFile(source, new ImageBatchOptions
        {
            Operation = ImageBatchOperation.Convert,
            Format = ImageBatchOutputFormat.Webp,
            FileNamePattern = "$filename"
        });

        using SKBitmap decoded = SKBitmap.Decode(output);
        Assert.That(Path.GetExtension(output), Is.EqualTo(".webp"));
        Assert.That((decoded.Width, decoded.Height), Is.EqualTo((32, 16)));
    }
}
