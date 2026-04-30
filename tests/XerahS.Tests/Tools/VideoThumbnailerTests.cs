using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Media;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class VideoThumbnailerTests
{
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

    private static void WriteTestBitmap(string path, SKColor color)
    {
        using var bitmap = new SKBitmap(8, 6);
        bitmap.Erase(color);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(path);
        data.SaveTo(stream);
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
