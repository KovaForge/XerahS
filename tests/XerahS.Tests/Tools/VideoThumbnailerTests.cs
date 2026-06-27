using System.Collections;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Media;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class VideoThumbnailerTests
{
    [Test]
    public void CombineScreenshots_WithNegativePaddingAndSpacing_ClampsToZeroInsteadOfCrashing()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-VideoThumbnailerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string thumbnailPath = Path.Combine(tempDirectory, "thumb.png");
            using (var bitmap = new SKBitmap(8, 6))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream stream = File.Create(thumbnailPath);
                data.SaveTo(stream);
            }

            var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
            {
                ColumnCount = 1,
                Padding = -100,
                Spacing = -50,
                AddVideoInfo = false,
                AddTimestamp = false,
                DrawBorder = false,
                DrawShadow = false,
                MaxThumbnailWidth = 0
            });

            using SKBitmap? combined = InvokeCombineScreenshots(thumbnailer, new List<VideoThumbnailInfo>
            {
                new VideoThumbnailInfo(thumbnailPath)
                {
                    Timestamp = TimeSpan.FromSeconds(5)
                }
            });

            Assert.That(combined, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(combined!.Width, Is.EqualTo(8));
                Assert.That(combined.Height, Is.EqualTo(6));
            });
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void CombineScreenshots_WithZeroColumnCount_ClampsToSingleColumn()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-VideoThumbnailerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string thumbnailPath = Path.Combine(tempDirectory, "thumb.png");
            using (var bitmap = new SKBitmap(8, 6))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream stream = File.Create(thumbnailPath);
                data.SaveTo(stream);
            }

            var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
            {
                ColumnCount = 0,
                Padding = 0,
                Spacing = 0,
                AddVideoInfo = false,
                AddTimestamp = false,
                DrawBorder = false,
                DrawShadow = false,
                MaxThumbnailWidth = 0
            });

            using SKBitmap? combined = InvokeCombineScreenshots(thumbnailer, new List<VideoThumbnailInfo>
            {
                new VideoThumbnailInfo(thumbnailPath)
                {
                    Timestamp = TimeSpan.FromSeconds(5)
                }
            });

            Assert.That(combined, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(combined!.Width, Is.EqualTo(8));
                Assert.That(combined.Height, Is.EqualTo(6));
            });
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }




    [Test]
    public void CombineScreenshots_WithMixedThumbnailSizes_UsesLargestCellSize()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-VideoThumbnailerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string smallPath = Path.Combine(tempDirectory, "small.png");
            string largePath = Path.Combine(tempDirectory, "large.png");
            WriteTestBitmap(smallPath, SKColors.CornflowerBlue, width: 8, height: 6);
            WriteTestBitmap(largePath, SKColors.MediumSeaGreen, width: 12, height: 10);

            var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
            {
                ColumnCount = 2,
                Padding = 1,
                Spacing = 3,
                AddVideoInfo = false,
                AddTimestamp = false,
                DrawBorder = false,
                DrawShadow = false,
                MaxThumbnailWidth = 0
            });

            using SKBitmap? combined = InvokeCombineScreenshots(thumbnailer, new List<VideoThumbnailInfo>
            {
                new VideoThumbnailInfo(smallPath),
                new VideoThumbnailInfo(largePath)
            });

            Assert.That(combined, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(combined!.Width, Is.EqualTo(29));
                Assert.That(combined.Height, Is.EqualTo(13));
            });
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void LoadThumbnailImages_SkipsUnreadableFilesWithoutShiftingTimestamps()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-VideoThumbnailerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string missingPath = Path.Combine(tempDirectory, "missing.png");
            string loadedPath = Path.Combine(tempDirectory, "loaded.png");
            WriteTestBitmap(loadedPath, SKColors.MediumSeaGreen);

            var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
            {
                MaxThumbnailWidth = 0
            });

            IList loadedThumbnails = InvokeLoadThumbnailImages(thumbnailer, new List<VideoThumbnailInfo>
            {
                new VideoThumbnailInfo(missingPath)
                {
                    Timestamp = TimeSpan.FromSeconds(5)
                },
                new VideoThumbnailInfo(loadedPath)
                {
                    Timestamp = TimeSpan.FromSeconds(42)
                }
            });

            try
            {
                Assert.That(loadedThumbnails, Has.Count.EqualTo(1));
                object loadedThumbnail = loadedThumbnails[0]!;
                PropertyInfo? timestampProperty = loadedThumbnail.GetType().GetProperty("Timestamp");
                Assert.That(timestampProperty, Is.Not.Null);
                Assert.That(timestampProperty!.GetValue(loadedThumbnail), Is.EqualTo(TimeSpan.FromSeconds(42)));
            }
            finally
            {
                foreach (object loadedThumbnail in loadedThumbnails)
                {
                    (loadedThumbnail as IDisposable)?.Dispose();
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void GetRandomTimeSlice_UsesThumbnailIndexSegment()
    {
        var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
        {
            ThumbnailCount = 4
        });

        SetVideoInfo(thumbnailer, TimeSpan.FromSeconds(120));

        Assert.Multiple(() =>
        {
            for (int thumbnailIndex = 0; thumbnailIndex < 4; thumbnailIndex++)
            {
                int start = 20 * (thumbnailIndex + 1);
                int end = 20 * (thumbnailIndex + 2) - 1;

                for (int attempt = 0; attempt < 20; attempt++)
                {
                    int timeSlice = InvokeGetRandomTimeSlice(thumbnailer, thumbnailIndex);
                    Assert.That(timeSlice, Is.InRange(start, end));
                }
            }
        });
    }

    [Test]
    public void GetRandomTimeSlice_WithShortVideo_ReturnsZeroInsteadOfOverflowingSlot()
    {
        var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
        {
            ThumbnailCount = 10
        });

        SetVideoInfo(thumbnailer, TimeSpan.FromSeconds(5));

        Assert.That(InvokeGetRandomTimeSlice(thumbnailer, 9), Is.EqualTo(0));
    }

    [Test]
    public void WaitForExitOrKill_WhenProcessTimesOut_TerminatesProcessTree()
    {
        using Process process = CreateSleepingProcess();
        process.Start();

        bool exitedCleanly = InvokeWaitForExitOrKill(process, TimeSpan.FromMilliseconds(100));

        Assert.Multiple(() =>
        {
            Assert.That(exitedCleanly, Is.False);
            Assert.That(process.HasExited, Is.True);
        });
    }

    [Test]
    public void WaitForExitOrKill_WhenProcessExitsBeforeTimeout_ReturnsTrue()
    {
        using Process process = CreateExitingProcess();
        process.Start();

        bool exitedCleanly = InvokeWaitForExitOrKill(process, TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(exitedCleanly, Is.True);
            Assert.That(process.HasExited, Is.True);
        });
    }

    private static Process CreateSleepingProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", "/c timeout /t 5 /nobreak > nul")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("sleep 5");
        return process;
    }

    private static Process CreateExitingProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", "/c exit 0")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("exit 0");
        return process;
    }

    private static void WriteTestBitmap(string path, SKColor color, int width = 8, int height = 6)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(path);
        data.SaveTo(stream);
    }

    [Test]
    public void CombineScreenshots_WithExcessiveOutputDimensions_ReturnsNullInsteadOfCrashing()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "XerahS-VideoThumbnailerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            // Create thumbnails that will cause combined output to exceed MaxCombinedWidth/Height (4096 default)
            // With ColumnCount=2, Padding=10, Spacing=10: 
            //   width = 20 + (2000 * 2) + 10 = 4030 + 20 = 4050 (under 4096)
            // With 3 thumbnails, rowCount=2, thumbHeight=2000, infoStringHeight=0:
            //   height = 30 + 0 + (2000 * 2) + 10 = 4040 (under 4096)
            // We need something that exceeds 4096 on at least one dimension
            // With ColumnCount=2, Padding=10, Spacing=10, and very large thumbnails:
            //   width = 20 + (2500 * 2) + 10 = 5030 (over 4096)
            var thumbnailInfos = new List<VideoThumbnailInfo>();
            for (int i = 0; i < 6; i++)
            {
                string thumbPath = Path.Combine(tempDirectory, $"thumb_{i}.png");
                using var bitmap = new SKBitmap(2500, 2000);
                bitmap.Erase(SKColors.CornflowerBlue);
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream stream = File.Create(thumbPath);
                data.SaveTo(stream);
                thumbnailInfos.Add(new VideoThumbnailInfo(thumbPath) { Timestamp = TimeSpan.FromSeconds(i * 5) });
            }

            var thumbnailer = new VideoThumbnailer("ffmpeg", new VideoThumbnailOptions
            {
                ColumnCount = 2,
                Padding = 10,
                Spacing = 10,
                AddVideoInfo = false,
                AddTimestamp = false,
                DrawBorder = false,
                DrawShadow = false,
                MaxThumbnailWidth = 0,
                MaxCombinedWidth = 4096,
                MaxCombinedHeight = 4096
            });

            using SKBitmap? combined = InvokeCombineScreenshots(thumbnailer, thumbnailInfos);

            // With 6 thumbnails at 2500x2000 and ColumnCount=2: rowCount=3
            // width = 20 + (2500 * 2) + 10 = 5030 > 4096 → returns null
            Assert.That(combined, Is.Null, "Combined screenshot should be null when dimensions exceed MaxCombinedWidth/Height limits.");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static SKBitmap? InvokeCombineScreenshots(VideoThumbnailer thumbnailer, List<VideoThumbnailInfo> thumbnails)
    {
        MethodInfo? method = typeof(VideoThumbnailer).GetMethod("CombineScreenshots", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (SKBitmap?)method!.Invoke(thumbnailer, new object[] { thumbnails });
    }

    private static IList InvokeLoadThumbnailImages(VideoThumbnailer thumbnailer, List<VideoThumbnailInfo> thumbnails)
    {
        MethodInfo? method = typeof(VideoThumbnailer).GetMethod("LoadThumbnailImages", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (IList)method!.Invoke(thumbnailer, new object[] { thumbnails })!;
    }

    private static int InvokeGetRandomTimeSlice(VideoThumbnailer thumbnailer, int thumbnailIndex)
    {
        MethodInfo? method = typeof(VideoThumbnailer).GetMethod("GetRandomTimeSlice", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (int)method!.Invoke(thumbnailer, new object[] { thumbnailIndex })!;
    }

    private static bool InvokeWaitForExitOrKill(Process process, TimeSpan timeout)
    {
        MethodInfo? method = typeof(VideoThumbnailer).GetMethod("WaitForExitOrKill", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method!.Invoke(null, new object[] { process, timeout })!;
    }

    private static void SetVideoInfo(VideoThumbnailer thumbnailer, TimeSpan duration)
    {
        PropertyInfo? property = typeof(VideoThumbnailer).GetProperty(nameof(VideoThumbnailer.VideoInfo));
        Assert.That(property, Is.Not.Null);
        property!.SetValue(thumbnailer, new VideoInfo
        {
            Duration = duration
        });
    }
}
