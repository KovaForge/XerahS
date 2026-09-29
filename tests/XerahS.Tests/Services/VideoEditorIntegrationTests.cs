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
using Omacut.Core;
using SkiaSharp;
using XerahS.Media;

namespace XerahS.Tests.Services;

/// <summary>
/// XerahS's side of the Omacut video editor integration: watermark rendering and a real
/// export through Omacut's pipeline (skipped when ffmpeg is not on PATH).
/// </summary>
[TestFixture]
[NonParallelizable]
public class VideoEditorIntegrationTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("xerahs-video-editor").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public void DisabledOrEmptyWatermarkHasNoImage()
    {
        Assert.That(VideoWatermarkRenderer.ResolveImage(null, _dir), Is.Null);
        Assert.That(VideoWatermarkRenderer.ResolveImage(new VideoWatermarkSettings { Enabled = false, Text = "x" }, _dir), Is.Null);
        Assert.That(VideoWatermarkRenderer.ResolveImage(new VideoWatermarkSettings { Enabled = true }, _dir), Is.Null);
    }

    [Test]
    public void ImageWatermarkIsUsedAsIs()
    {
        string image = Path.Combine(_dir, "logo.png");
        File.WriteAllBytes(image, [0x89, 0x50, 0x4E, 0x47]);
        var settings = new VideoWatermarkSettings { Enabled = true, Text = "ignored", ImagePath = image };
        Assert.That(VideoWatermarkRenderer.ResolveImage(settings, _dir), Is.EqualTo(image));
    }

    [Test]
    public void TextWatermarkRendersToTransparentPng()
    {
        string? path = VideoWatermarkRenderer.ResolveImage(
            new VideoWatermarkSettings { Enabled = true, Text = "XerahS", FontSize = 32, FontColor = "#FF0000" }, _dir);

        Assert.That(path, Is.Not.Null);
        using SKBitmap bitmap = SKBitmap.Decode(path);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Width, Is.GreaterThan(60));
            Assert.That(bitmap.Height, Is.GreaterThan(20));
            Assert.That(bitmap.GetPixel(0, 0).Alpha, Is.EqualTo(0), "the background stays transparent");
            Assert.That(Enumerable.Range(0, bitmap.Width).Any(x => bitmap.GetPixel(x, bitmap.Height / 2).Red > 200), Is.True, "text is drawn");
        });
    }

    [Test]
    public async Task ExportsTrimCropAndTextWatermarkThroughOmacut()
    {
        var tools = new FfmpegTools();
        if (!tools.HasFfmpeg || !tools.HasFfprobe)
        {
            Assert.Ignore("ffmpeg/ffprobe not on PATH");
        }

        string source = Path.Combine(_dir, "recording.mp4");
        var generate = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tools.FfmpegPath)
        {
            ArgumentList = { "-y", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=3", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", source },
            RedirectStandardError = true,
        })!;
        await generate.WaitForExitAsync();
        Assert.That(generate.ExitCode, Is.EqualTo(0), await generate.StandardError.ReadToEndAsync());

        string watermark = VideoWatermarkRenderer.ResolveImage(new VideoWatermarkSettings { Enabled = true, Text = "XerahS" }, _dir)!;
        MediaInfo info = await MediaProbe.ProbeAsync(tools, source);
        var service = new ExportService(tools, await EncoderSupport.ProbeAsync(tools));

        string output = await service.ExportAsync(
            new ExportOptions
            {
                SourcePath = source,
                Start = 0.5,
                End = 2.0,
                Crop = new CropRect(0, 0, 320, 180),
                Watermark = new WatermarkOverlay(watermark),
            },
            info,
            ExportFormats.SuggestedPath(source, ExportFormat.Mp4));

        MediaInfo exported = await MediaProbe.ProbeAsync(tools, output);
        Assert.Multiple(() =>
        {
            Assert.That(Path.GetFileName(output), Is.EqualTo("recording_trimmed.mp4"));
            Assert.That((exported.Width, exported.Height), Is.EqualTo((320, 180)));
            Assert.That(exported.Duration, Is.EqualTo(1.5).Within(0.1));
        });
    }
}
