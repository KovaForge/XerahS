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
public class AnimatedGifMakerServiceTests
{
    [TestCase(true, 0, 0)]
    [TestCase(false, 0, -1)]
    [TestCase(false, 3, 3)]
    public void GetLoopArgument_MapsLoopAndRepeat(bool loop, int repeat, int expected)
    {
        Assert.That(AnimatedGifMakerService.GetLoopArgument(new AnimatedGifOptions { Loop = loop, RepeatCount = repeat }), Is.EqualTo(expected));
    }

    [Test]
    public void GetCanvasSize_ScalesOnlyWhenLargerThanMax()
    {
        Assert.That(AnimatedGifMakerService.GetCanvasSize(800, 400, 0), Is.EqualTo((800, 400)));
        Assert.That(AnimatedGifMakerService.GetCanvasSize(800, 400, 400), Is.EqualTo((400, 200)));
    }

    [Test]
    public async Task CreateAsync_BuildsGifWithEveryFrameAndDelay()
    {
        string? ffmpeg = new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg" }.FirstOrDefault(File.Exists);
        if (ffmpeg == null)
        {
            Assert.Ignore("FFmpeg is not installed.");
        }

        string dir = Path.Combine(Path.GetTempPath(), "xerahs-gif-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var frames = new List<string>();
            (int w, int h, SKColor c)[] specs = [(120, 80, SKColors.Red), (60, 60, SKColors.Lime), (200, 100, SKColors.Blue)];
            for (int i = 0; i < specs.Length; i++)
            {
                using var bitmap = new SKBitmap(specs[i].w, specs[i].h);
                bitmap.Erase(specs[i].c);
                string path = Path.Combine(dir, $"f{i}.png");
                File.WriteAllBytes(path, ImageBatchService.Encode(bitmap, new ImageBatchOptions()));
                frames.Add(path);
            }

            string output = Path.Combine(dir, "out.gif");
            await new AnimatedGifMakerService(ffmpeg!).CreateAsync(frames, output, new AnimatedGifOptions { DelayMilliseconds = 300, Loop = false, RepeatCount = 2 });

            using var codec = SKCodec.Create(output);
            Assert.That(codec, Is.Not.Null);
            Assert.That(codec.FrameCount, Is.EqualTo(3));
            Assert.That(codec.FrameInfo.Select(f => f.Duration), Is.All.EqualTo(300));
            Assert.That(codec.RepetitionCount, Is.EqualTo(2));
            Assert.That((codec.Info.Width, codec.Info.Height), Is.EqualTo((120, 80)), "canvas follows the first image");
            Assert.That(Directory.GetFiles(dir, ".xerahs-gif-*"), Is.Empty);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
