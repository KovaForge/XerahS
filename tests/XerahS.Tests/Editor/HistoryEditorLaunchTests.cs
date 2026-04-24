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
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Persistence;
using ShareX.ImageEditor.Hosting;
using SkiaSharp;
using XerahS.Core;
using XerahS.History;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Editor;

[TestFixture]
public class HistoryEditorLaunchTests
{
    [Test]
    public async Task EditImage_PassesHistoryFilePath_ToUiEditorHost()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), $"xerahs-history-editor-{Guid.NewGuid():N}.png");

        using (var bitmap = new SKBitmap(8, 8))
        {
            bitmap.Erase(SKColors.Red);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(imagePath);
            data.SaveTo(stream);
        }

        var uiService = new TrackingUiService();
        PlatformServices.RegisterUIService(uiService);

        try
        {
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
            var item = new HistoryItem
            {
                FilePath = imagePath
            };

            await viewModel.EditImageCommand.ExecuteAsync(item);

            Assert.That(uiService.LastSourceFilePath, Is.EqualTo(imagePath));
        }
        finally
        {
            PlatformServices.Reset();

            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Test]
    public async Task EditImage_IgnoresAnnotationSidecar_AndOpensPlainImageEditor()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "annotated.png");

        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        SaveBitmap(imagePath, bitmap);

        string? sidecarPath = await XannProjectFileService.SaveAsync(
            imagePath,
            bitmap,
            new Annotation[]
            {
                new RectangleAnnotation
                {
                    StartPoint = new SKPoint(1, 1),
                    EndPoint = new SKPoint(6, 6)
                }
            });

        var uiService = new TrackingUiService();
        PlatformServices.RegisterUIService(uiService);

        try
        {
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
            var item = new HistoryItem
            {
                FilePath = imagePath,
                AnnotationSidecarPath = sidecarPath
            };

            await viewModel.EditImageCommand.ExecuteAsync(item);

            Assert.That(uiService.EditorLaunchCount, Is.EqualTo(1));
            Assert.That(uiService.SessionLaunchCount, Is.EqualTo(0));
            Assert.That(uiService.LastSourceFilePath, Is.EqualTo(imagePath));
            Assert.That(uiService.LastAnnotationCount, Is.EqualTo(0));
            Assert.That(uiService.LastRestoredAnnotations, Is.False);
        }
        finally
        {
            PlatformServices.Reset();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task EditAnnotations_UsesAnnotationSidecar_WhenAvailable()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "annotated.png");

        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        SaveBitmap(imagePath, bitmap);

        string? sidecarPath = await XannProjectFileService.SaveAsync(
            imagePath,
            bitmap,
            new Annotation[]
            {
                new RectangleAnnotation
                {
                    StartPoint = new SKPoint(1, 1),
                    EndPoint = new SKPoint(6, 6)
                }
            });

        var uiService = new TrackingUiService();
        PlatformServices.RegisterUIService(uiService);

        try
        {
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
            var item = new HistoryItem
            {
                FilePath = imagePath,
                AnnotationSidecarPath = sidecarPath
            };

            await viewModel.EditAnnotationsCommand.ExecuteAsync(item);

            Assert.That(uiService.EditorLaunchCount, Is.EqualTo(0));
            Assert.That(uiService.SessionLaunchCount, Is.EqualTo(1));
            Assert.That(uiService.LastSourceFilePath, Is.EqualTo(imagePath));
            Assert.That(uiService.LastAnnotationCount, Is.EqualTo(1));
            Assert.That(uiService.LastRestoredAnnotations, Is.True);
            Assert.That(uiService.LastSessionImageSize, Is.EqualTo(new SKSizeI(8, 8)));
        }
        finally
        {
            PlatformServices.Reset();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task EditAnnotations_UsesCurrentFileImage_WhenAnnotationSidecarHashMismatches()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "annotated.png");

        using var originalBitmap = new SKBitmap(8, 8);
        originalBitmap.Erase(SKColors.Red);
        SaveBitmap(imagePath, originalBitmap);

        string? sidecarPath = await XannProjectFileService.SaveAsync(
            imagePath,
            originalBitmap,
            new Annotation[]
            {
                new RectangleAnnotation
                {
                    StartPoint = new SKPoint(1, 1),
                    EndPoint = new SKPoint(6, 6)
                }
            });

        using (var updatedBitmap = new SKBitmap(16, 10))
        {
            updatedBitmap.Erase(SKColors.Green);
            SaveBitmap(imagePath, updatedBitmap);
        }

        var uiService = new TrackingUiService();
        PlatformServices.RegisterUIService(uiService);

        try
        {
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
            var item = new HistoryItem
            {
                FilePath = imagePath,
                AnnotationSidecarPath = sidecarPath
            };

            await viewModel.EditAnnotationsCommand.ExecuteAsync(item);

            Assert.That(uiService.SessionLaunchCount, Is.EqualTo(1));
            Assert.That(uiService.LastSourceFilePath, Is.EqualTo(imagePath));
            Assert.That(uiService.LastAnnotationCount, Is.EqualTo(1));
            Assert.That(uiService.LastRestoredAnnotations, Is.True);
            Assert.That(uiService.LastSessionImageSize, Is.EqualTo(new SKSizeI(16, 10)));
        }
        finally
        {
            PlatformServices.Reset();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task EditImage_RefreshesHistoryItem_WhenEditedFileChanges()
    {
        await VerifyHistoryItemRefreshesAfterEditorSessionAsync(lastWriteTimeTransform: static original => original.AddMinutes(1));
    }

    [Test]
    public async Task EditImage_RefreshesHistoryItem_WhenEditedFileKeepsSameTimestamp()
    {
        await VerifyHistoryItemRefreshesAfterEditorSessionAsync(lastWriteTimeTransform: static original => original);
    }

    private static async Task VerifyHistoryItemRefreshesAfterEditorSessionAsync(Func<DateTime, DateTime> lastWriteTimeTransform)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-history-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "annotated.png");

        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        SaveBitmap(imagePath, bitmap);

        string? sidecarPath = await XannProjectFileService.SaveAsync(
            imagePath,
            bitmap,
            new Annotation[]
            {
                new RectangleAnnotation
                {
                    StartPoint = new SKPoint(1, 1),
                    EndPoint = new SKPoint(6, 6)
                }
            });

        DateTime originalWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
        Action<string?> mutateEditedFile = sourceFilePath =>
        {
            Assert.That(sourceFilePath, Is.EqualTo(imagePath));

            using var updatedBitmap = new SKBitmap(16, 16);
            updatedBitmap.Erase(SKColors.Green);
            SaveBitmap(imagePath, updatedBitmap);
            File.SetLastWriteTimeUtc(imagePath, lastWriteTimeTransform(originalWriteTimeUtc));
        };

        var uiService = new TrackingUiService
        {
            ShowEditorCallback = mutateEditedFile,
            ShowEditorSessionCallback = mutateEditedFile
        };
        PlatformServices.RegisterUIService(uiService);

        try
        {
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
            var item = new HistoryItem
            {
                FilePath = imagePath,
                FileName = Path.GetFileName(imagePath),
                AnnotationSidecarPath = sidecarPath
            };
            viewModel.HistoryItems.Add(item);

            await viewModel.EditImageCommand.ExecuteAsync(item);

            Assert.That(viewModel.HistoryItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.HistoryItems[0], Is.Not.SameAs(item));
            Assert.That(viewModel.HistoryItems[0].FilePath, Is.EqualTo(imagePath));
            Assert.That(viewModel.HistoryItems[0].AnnotationSidecarPath, Is.EqualTo(sidecarPath));
        }
        finally
        {
            PlatformServices.Reset();
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TrackingUiService : IUIService
    {
        public string? LastSourceFilePath { get; private set; }
        public int LastAnnotationCount { get; private set; }
        public bool LastRestoredAnnotations { get; private set; }
        public int EditorLaunchCount { get; private set; }
        public int SessionLaunchCount { get; private set; }
        public SKSizeI LastSessionImageSize { get; private set; }
        public Action<string?>? ShowEditorCallback { get; init; }
        public Action<string?>? ShowEditorSessionCallback { get; init; }

        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
        {
            EditorLaunchCount++;
            LastSourceFilePath = sourceFilePath;
            LastAnnotationCount = 0;
            LastRestoredAnnotations = false;
            ShowEditorCallback?.Invoke(sourceFilePath);
            return Task.FromResult<SKBitmap?>(image);
        }

        public Task<ImageEditorSessionResult?> ShowEditorSessionAsync(
            SKBitmap image,
            string? sourceFilePath = null,
            bool taskMode = false,
            IReadOnlyList<Annotation>? annotations = null,
            bool restoredAnnotations = false)
        {
            SessionLaunchCount++;
            LastSourceFilePath = sourceFilePath;
            LastAnnotationCount = annotations?.Count ?? 0;
            LastRestoredAnnotations = restoredAnnotations;
            LastSessionImageSize = new SKSizeI(image.Width, image.Height);
            ShowEditorSessionCallback?.Invoke(sourceFilePath);
            return Task.FromResult<ImageEditorSessionResult?>(new ImageEditorSessionResult(
                image.Copy()!,
                image.Copy(),
                annotations?.Select(annotation => annotation.Clone()).ToList() ?? new List<Annotation>()));
        }

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload) => Task.FromResult((afterCapture, afterUpload, false));

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) => Task.FromResult(new SendToPromptResult());

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection) => Task.CompletedTask;

        public Task ShowOcrWindowAsync(SKBitmap image) => Task.CompletedTask;

        public Task ShowAnalyzerWindowAsync(SKBitmap image) => Task.CompletedTask;
    }

    private static void SaveBitmap(string path, SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
