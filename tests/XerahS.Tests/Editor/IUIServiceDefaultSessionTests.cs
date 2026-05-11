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
using ShareX.ImageEditor.Hosting;
using SkiaSharp;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Editor;

[TestFixture]
public class IUIServiceDefaultSessionTests
{
    [Test]
    public async Task ShowEditorSessionAsync_DefaultFallback_PreservesSourceImage_AndAnnotations()
    {
        using var image = new SKBitmap(12, 7);
        image.Erase(SKColors.CadetBlue);

        var annotation = new RectangleAnnotation
        {
            StartPoint = new SKPoint(1, 2),
            EndPoint = new SKPoint(10, 6)
        };

        var implementation = new DefaultSessionFallbackUiService();
        IUIService service = implementation;

        ImageEditorSessionResult? result = await service.ShowEditorSessionAsync(
            image,
            sourceFilePath: "/tmp/sample.png",
            annotations: new[] { annotation },
            restoredAnnotations: true);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.RenderedImage, Is.SameAs(implementation.RenderedImage));
        Assert.That(result.SourceImage, Is.Not.Null);
        Assert.That(result.SourceImage, Is.Not.SameAs(image));
        Assert.That(result.SourceImage!.Width, Is.EqualTo(image.Width));
        Assert.That(result.SourceImage.Height, Is.EqualTo(image.Height));
        Assert.That(result.Annotations, Has.Count.EqualTo(1));
        Assert.That(result.Annotations[0], Is.TypeOf<RectangleAnnotation>());
        Assert.That(result.Annotations[0], Is.Not.SameAs(annotation));

        var restoredRectangle = (RectangleAnnotation)result.Annotations[0];
        Assert.That(restoredRectangle.StartPoint, Is.EqualTo(annotation.StartPoint));
        Assert.That(restoredRectangle.EndPoint, Is.EqualTo(annotation.EndPoint));

        result.RenderedImage.Dispose();
        result.SourceImage.Dispose();
    }


    [Test]
    public async Task ShowEditorSessionAsync_DefaultFallback_CapturesSourceImage_BeforeEditorMutatesInput()
    {
        using var image = new SKBitmap(2, 1);
        image.SetPixel(0, 0, SKColors.CadetBlue);
        image.SetPixel(1, 0, SKColors.Goldenrod);

        var implementation = new MutatingDefaultSessionFallbackUiService();
        IUIService service = implementation;

        ImageEditorSessionResult? result = await service.ShowEditorSessionAsync(image);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SourceImage, Is.Not.Null);
        Assert.That(result.SourceImage!.GetPixel(0, 0), Is.EqualTo(SKColors.CadetBlue));
        Assert.That(result.SourceImage.GetPixel(1, 0), Is.EqualTo(SKColors.Goldenrod));
        Assert.That(result.RenderedImage.GetPixel(0, 0), Is.EqualTo(SKColors.Red));

        result.RenderedImage.Dispose();
        result.SourceImage.Dispose();
    }

    private sealed class DefaultSessionFallbackUiService : IUIService
    {
        public SKBitmap? RenderedImage { get; private set; }

        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
        {
            RenderedImage = image.Copy();
            return Task.FromResult<SKBitmap?>(RenderedImage);
        }

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload) => Task.FromResult((afterCapture, afterUpload, false));

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) => Task.FromResult(new SendToPromptResult());

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null) => Task.CompletedTask;

        public Task ShowOcrWindowAsync(SKBitmap image) => Task.CompletedTask;

        public Task ShowAnalyzerWindowAsync(SKBitmap image) => Task.CompletedTask;
    }

    private sealed class MutatingDefaultSessionFallbackUiService : IUIService
    {
        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
        {
            image.SetPixel(0, 0, SKColors.Red);
            return Task.FromResult<SKBitmap?>(image.Copy());
        }

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload) => Task.FromResult((afterCapture, afterUpload, false));

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) => Task.FromResult(new SendToPromptResult());

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null) => Task.CompletedTask;

        public Task ShowOcrWindowAsync(SKBitmap image) => Task.CompletedTask;

        public Task ShowAnalyzerWindowAsync(SKBitmap image) => Task.CompletedTask;
    }
}
