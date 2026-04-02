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
            var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService());
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

    private sealed class TrackingUiService : IUIService
    {
        public string? LastSourceFilePath { get; private set; }

        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
        {
            LastSourceFilePath = sourceFilePath;
            return Task.FromResult<SKBitmap?>(image);
        }

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload) => Task.FromResult((afterCapture, afterUpload, false));

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) => Task.FromResult(new SendToPromptResult());

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection) => Task.CompletedTask;
    }
}
