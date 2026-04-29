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

    private static SKBitmap? InvokeCombineScreenshots(VideoThumbnailer thumbnailer, List<VideoThumbnailInfo> thumbnails)
    {
        MethodInfo? method = typeof(VideoThumbnailer).GetMethod("CombineScreenshots", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (SKBitmap?)method!.Invoke(thumbnailer, new object[] { thumbnails });
    }
}
