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

using System.Diagnostics;
using NUnit.Framework;
using ShareX.VideoEditor.Core;
using ShareX.VideoEditor.Hosting;
using XerahS.Common;

namespace XerahS.Tests.Services;

[TestFixture]
public class VideoEditorExportAutomationTests
{
    [Test]
    public void ArgumentBuilder_Trim_InsertsSeekAndDuration()
    {
        string args = FfmpegArgumentBuilder.Build(new VideoExportOptions
        {
            InputPath = @"C:\in.mp4",
            OutputPath = @"C:\out.mp4",
            IsTrimActive = true,
            TrimStart = TimeSpan.FromSeconds(1),
            TrimEnd = TimeSpan.FromSeconds(3),
            OutputFps = 0
        });

        Assert.That(args, Does.Contain("-ss 00:00:01.00"));
        Assert.That(args, Does.Contain("-t 00:00:02.00"));
        Assert.That(args, Does.Not.Contain("fps="));
    }

    [Test]
    public void ArgumentBuilder_Crop_AlignsEvenDimensions()
    {
        var options = new VideoExportOptions
        {
            InputPath = @"C:\in.mp4",
            OutputPath = @"C:\out.mp4",
            IsCropActive = true,
            CropX = 11,
            CropY = 13,
            CropWidth = 101,
            CropHeight = 99
        };

        Assert.That(FfmpegArgumentBuilder.TryNormalizeCrop(options, out int x, out int y, out int width, out int height), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(x, Is.EqualTo(10));
            Assert.That(y, Is.EqualTo(12));
            Assert.That(width, Is.EqualTo(100));
            Assert.That(height, Is.EqualTo(98));
        });

        string args = FfmpegArgumentBuilder.Build(options);
        Assert.That(args, Does.Contain("crop=100:98:10:12"));
    }

    [TestCase("WebM", "libvpx-vp9")]
    [TestCase("GIF", "paletteuse")]
    [TestCase("WebP", "libwebp_anim")]
    [TestCase("MP4", "libx264")]
    public void ArgumentBuilder_FormatConversion_SelectsCodec(string format, string expectedToken)
    {
        string args = FfmpegArgumentBuilder.Build(new VideoExportOptions
        {
            InputPath = @"C:\in.mp4",
            OutputPath = $@"C:\out.{format.ToLowerInvariant()}",
            OutputFormat = format
        });

        Assert.That(args, Does.Contain(expectedToken));
    }

    [Test]
    public void ArgumentBuilder_Watermark_IncludesDrawText()
    {
        string args = FfmpegArgumentBuilder.Build(new VideoExportOptions
        {
            InputPath = @"C:\in.mp4",
            OutputPath = @"C:\out.mp4",
            WatermarkText = "XerahS:preview",
            Watermark = new WatermarkSettings
            {
                Enabled = true,
                Text = "XerahS:preview",
                FontColor = "#FFFFFF",
                FontSize = 24,
                Opacity = 0.8,
                PositionX = 0.95,
                PositionY = 0.95
            }
        });

        Assert.That(args, Does.Contain("drawtext=text='XerahS\\:preview'"));
        Assert.That(args, Does.Contain("fontcolor=0xFFFFFF"));
    }

    [Test]
    public void ArgumentBuilder_ImageWatermark_UsesOverlay()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "xerahs-wm-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(imagePath, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            string args = FfmpegArgumentBuilder.Build(new VideoExportOptions
            {
                InputPath = @"C:\in.mp4",
                OutputPath = @"C:\out.mp4",
                WatermarkText = "Logo",
                Watermark = new WatermarkSettings
                {
                    Enabled = true,
                    Text = "Logo",
                    ImagePath = imagePath,
                    Opacity = 0.6,
                    PositionX = 0.1,
                    PositionY = 0.2
                }
            });

            Assert.That(args, Does.Contain("-filter_complex"));
            Assert.That(args, Does.Contain("overlay=x=(main_w-overlay_w)*0.1"));
            Assert.That(args, Does.Contain("colorchannelmixer=aa=0.6"));
            Assert.That(args, Does.Contain("drawtext=text='Logo'"));
            Assert.That(args, Does.Contain("-map [vout]"));
        }
        finally
        {
            try { File.Delete(imagePath); } catch { }
        }
    }

    [Test]
    public void CapabilityProbe_ParseEncoderList_MapsAdvertisedFormats()
    {
        const string listing =
            "Encoders:\n" +
            " V..... libx264             H.264 / AVC\n" +
            " V..... libvpx-vp9          Google VP9\n" +
            " V..... libwebp_anim        WebP animation\n" +
            " V..... gif                 GIF\n";

        FfmpegCapabilitySnapshot snapshot = FfmpegCapabilityProbe.ParseEncoderList(listing);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Supports("MP4"), Is.True);
            Assert.That(snapshot.Supports("WebM"), Is.True);
            Assert.That(snapshot.Supports("GIF"), Is.True);
            Assert.That(snapshot.Supports("WebP"), Is.True);
            Assert.That(snapshot.ResolveWebMCodec(), Is.EqualTo("libvpx-vp9"));
            Assert.That(snapshot.AvailableFormats, Is.EquivalentTo(new[] { "MP4", "WebM", "GIF", "WebP" }));
        });
    }

    [Test]
    public void CapabilityProbe_ParseEncoderList_FallsBackToVp8()
    {
        FfmpegCapabilitySnapshot snapshot = FfmpegCapabilityProbe.ParseEncoderList(
            " V..... libx264\n V..... libvpx              Google VP8\n");

        Assert.That(snapshot.Supports("WebM"), Is.True);
        Assert.That(snapshot.HasVp9, Is.False);
        Assert.That(snapshot.ResolveWebMCodec(), Is.EqualTo("libvpx"));
    }

    [Test]
    public async Task AutomationService_AdvertisedOperations_ExportWhenFfmpegAvailable()
    {
        string? ffmpegPath = ResolveFfmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            Assert.Ignore("FFmpeg is not available on this machine.");
        }

        string workDir = Path.Combine(Path.GetTempPath(), "XerahS-VideoEditorExport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            string inputPath = Path.Combine(workDir, "source.mp4");
            await CreateTestVideoAsync(ffmpegPath, inputPath);

            var service = new VideoEditorAutomationService(ffmpegPath);

            string trimPath = Path.Combine(workDir, "trim.mp4");
            VideoEditorTrimResult trimResult = await service.TrimAsync(new VideoEditorTrimRequest
            {
                InputPath = inputPath,
                OutputPath = trimPath,
                TrimStart = TimeSpan.FromSeconds(0.2),
                TrimEndOffset = TimeSpan.FromSeconds(0.2)
            });
            Assert.That(File.Exists(trimResult.OutputPath), Is.True);
            Assert.That(new FileInfo(trimResult.OutputPath).Length, Is.GreaterThan(0));

            string cropPath = Path.Combine(workDir, "crop.mp4");
            VideoEditorExportResult cropResult = await service.ExportAsync(new VideoEditorExportRequest
            {
                InputPath = inputPath,
                OutputPath = cropPath,
                IsCropActive = true,
                CropX = 10,
                CropY = 10,
                CropWidth = 160,
                CropHeight = 120
            });
            Assert.That(File.Exists(cropResult.OutputPath), Is.True);

            string convertPath = Path.Combine(workDir, "convert.webp");
            VideoEditorExportResult convertResult = await service.ExportAsync(new VideoEditorExportRequest
            {
                InputPath = inputPath,
                OutputPath = convertPath,
                OutputFormat = "WebP"
            });
            Assert.That(File.Exists(convertResult.OutputPath), Is.True);

            string watermarkPath = Path.Combine(workDir, "watermark.mp4");
            VideoEditorExportResult watermarkResult = await service.ExportAsync(new VideoEditorExportRequest
            {
                InputPath = inputPath,
                OutputPath = watermarkPath,
                WatermarkEnabled = true,
                WatermarkText = "XerahS"
            });
            Assert.That(File.Exists(watermarkResult.OutputPath), Is.True);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { }
        }
    }

    private static string? ResolveFfmpegPath()
    {
        try
        {
            string configured = PathsManager.GetFFmpegPath();
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }
        }
        catch
        {
        }

        string fileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task CreateTestVideoAsync(string ffmpegPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo(
            ffmpegPath,
            "-f lavfi -i testsrc=duration=2:size=320x240:rate=10 " +
            "-f lavfi -i sine=frequency=440:duration=2 " +
            "-c:v libx264 -pix_fmt yuv420p -c:a aac -shortest -y " +
            $"\"{outputPath}\"")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start FFmpeg to create a test clip.");

        await process.WaitForExitAsync();
        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            Assert.Ignore("Could not synthesize a test clip with the local FFmpeg build.");
        }
    }
}
