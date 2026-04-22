using NUnit.Framework;
using XerahS.History;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Services;

[TestFixture]
public class HistoryItemMenuContextTests
{
    [Test]
    public void HistoryItemMenuTargetAdapter_HasImageFile_TrueOnlyForImagePaths()
    {
        var imageItem = new HistoryItem { FilePath = "/tmp/capture.png" };
        var videoItem = new HistoryItem { FilePath = "/tmp/capture.mp4" };

        Assert.Multiple(() =>
        {
            Assert.That(new HistoryItemMenuTargetAdapter(imageItem).HasImageFile, Is.True);
            Assert.That(new HistoryItemMenuTargetAdapter(videoItem).HasImageFile, Is.False);
        });
    }

    [Test]
    public void ToastItemMenuTargetAdapter_HasImageFile_RequiresExistingImageFile()
    {
        using var tempDirectory = new TempDirectory();
        var imagePath = tempDirectory.CreateFile("toast.png");
        var videoPath = tempDirectory.CreateFile("toast.mp4");

        var imageToast = new ToastViewModel(new ToastConfig { FilePath = imagePath, AutoHide = false });
        var videoToast = new ToastViewModel(new ToastConfig { FilePath = videoPath, AutoHide = false });
        var missingToast = new ToastViewModel(new ToastConfig { FilePath = tempDirectory.GetPath("missing.png"), AutoHide = false });

        Assert.Multiple(() =>
        {
            Assert.That(new ToastItemMenuTargetAdapter(imageToast).HasImageFile, Is.True);
            Assert.That(new ToastItemMenuTargetAdapter(videoToast).HasImageFile, Is.False);
            Assert.That(new ToastItemMenuTargetAdapter(missingToast).HasImageFile, Is.False);
        });
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"xerahs-menu-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string CreateFile(string fileName)
        {
            var path = Path.Combine(_path, fileName);
            File.WriteAllText(path, "test");
            return path;
        }

        public string GetPath(string fileName) => Path.Combine(_path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
