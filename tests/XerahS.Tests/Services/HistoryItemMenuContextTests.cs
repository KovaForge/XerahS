using NUnit.Framework;
using XerahS.History;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Services;

[TestFixture]
public class HistoryItemMenuContextTests
{
    [Test]
    public void HistoryItemMenuTargetAdapter_ExposesFileState_ForImageAndMissingPaths()
    {
        using var tempDirectory = new TempDirectory();
        var imagePath = tempDirectory.CreateFile("capture.png");
        var missingPath = tempDirectory.GetPath("missing.mp4");

        var imageTarget = new HistoryItemMenuTargetAdapter(new HistoryItem { FilePath = imagePath });
        var missingTarget = new HistoryItemMenuTargetAdapter(new HistoryItem { FilePath = missingPath });

        Assert.Multiple(() =>
        {
            Assert.That(imageTarget.HasImageFile, Is.True);
            Assert.That(imageTarget.HasFilePath, Is.True);
            Assert.That(imageTarget.HasExistingFile, Is.True);
            Assert.That(missingTarget.HasImageFile, Is.False);
            Assert.That(missingTarget.HasFilePath, Is.True);
            Assert.That(missingTarget.HasExistingFile, Is.False);
        });
    }

    [Test]
    public void ToastItemMenuTargetAdapter_RequiresExistingImageFile_AndTracksMenuFileState()
    {
        using var tempDirectory = new TempDirectory();
        var imagePath = tempDirectory.CreateFile("toast.png");
        var videoPath = tempDirectory.CreateFile("toast.mp4");
        var missingPath = tempDirectory.GetPath("missing.png");
        var noPathToast = new ToastViewModel(new ToastConfig { AutoHide = false });

        var imageTarget = new ToastItemMenuTargetAdapter(new ToastViewModel(new ToastConfig { FilePath = imagePath, AutoHide = false }));
        var videoTarget = new ToastItemMenuTargetAdapter(new ToastViewModel(new ToastConfig { FilePath = videoPath, AutoHide = false }));
        var missingTarget = new ToastItemMenuTargetAdapter(new ToastViewModel(new ToastConfig { FilePath = missingPath, AutoHide = false }));
        var noPathTarget = new ToastItemMenuTargetAdapter(noPathToast);

        Assert.Multiple(() =>
        {
            Assert.That(imageTarget.HasImageFile, Is.True);
            Assert.That(imageTarget.HasFilePath, Is.True);
            Assert.That(imageTarget.HasExistingFile, Is.True);
            Assert.That(videoTarget.HasImageFile, Is.False);
            Assert.That(videoTarget.HasFilePath, Is.True);
            Assert.That(videoTarget.HasExistingFile, Is.True);
            Assert.That(missingTarget.HasImageFile, Is.False);
            Assert.That(missingTarget.HasFilePath, Is.True);
            Assert.That(missingTarget.HasExistingFile, Is.False);
            Assert.That(noPathTarget.HasFilePath, Is.False);
            Assert.That(noPathTarget.HasExistingFile, Is.False);
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
