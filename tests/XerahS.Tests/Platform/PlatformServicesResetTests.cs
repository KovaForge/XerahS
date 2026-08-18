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
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;
using ShareX.ImageEditor.Hosting;

namespace XerahS.Tests.Platform;

[TestFixture]
public sealed class PlatformServicesResetTests
{
    [SetUp]
    public void SetUp()
    {
        PlatformServices.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        PlatformServices.Reset();
    }

    [Test]
    public void Reset_ClearsRegisteredUiService()
    {
        PlatformServices.RegisterUIService(new StubUiService());

        Assert.That(PlatformServices.UI, Is.Not.Null);

        PlatformServices.Reset();

        Assert.That(
            () => _ = PlatformServices.UI,
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.Contains("UI service not initialized"));
    }

    [Test]
    public void Reset_ClearsRegisteredImageEncoderService()
    {
        PlatformServices.RegisterImageEncoderService(new StubImageEncoderService());

        Assert.That(PlatformServices.ImageEncoder, Is.Not.Null);

        PlatformServices.Reset();

        Assert.That(
            () => _ = PlatformServices.ImageEncoder,
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.Contains("ImageEncoder service not initialized"));
    }

    private sealed class StubUiService : IUIService
    {
        public Task HideMainWindowAsync() => Task.CompletedTask;

        public Task RestoreMainWindowAsync() => Task.CompletedTask;

        public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
            => Task.FromResult<SKBitmap?>(null);

        public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath)
            => Task.FromResult<string?>(null);

        public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction)> ShowAfterCaptureWindowAsync(
            SKBitmap image,
            AfterCaptureTasks afterCapture,
            AfterUploadTasks afterUpload)
            => Task.FromResult((afterCapture, afterUpload, false, AfterCaptureQuickAction.None));

        public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

        public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection)
            => Task.FromResult(new SendToPromptResult());

        public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null)
            => Task.CompletedTask;

        public Task ShowOcrWindowAsync(SKBitmap image) => Task.CompletedTask;

        public Task ShowAnalyzerWindowAsync(SKBitmap image) => Task.CompletedTask;
    }

    private sealed class StubImageEncoderService : IImageEncoderService
    {
        public Task EncodeAsync(SKBitmap bitmap, string filePath, EImageFormat format, int quality = 100)
            => Task.CompletedTask;
    }
}
