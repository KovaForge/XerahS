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
using XerahS.Core.Tasks.Processors;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Tasks;

[TestFixture]
public sealed class CaptureJobProcessorOcrTests
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
    public async Task ProcessAsync_DoOcr_UsesTaskOcrOptions()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService();
        PlatformServices.Ocr = ocr;

        var taskSettings = new TaskSettings();
        taskSettings.AfterCaptureJob = AfterCaptureTasks.DoOCR;
        taskSettings.CaptureSettings.OCROptions.Language = "fr";
        taskSettings.CaptureSettings.OCROptions.ScaleFactor = 3.5f;
        taskSettings.CaptureSettings.OCROptions.SingleLine = true;

        var info = new TaskInfo(taskSettings)
        {
            Metadata = new TaskMetadata(bitmap)
        };

        var processor = new CaptureJobProcessor();
        bool shouldContinue = await processor.ProcessAsync(info, CancellationToken.None);

        Assert.That(shouldContinue, Is.True);
        Assert.That(ocr.LastOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ocr.LastOptions!.Language, Is.EqualTo("fr"));
            Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(3.5f));
            Assert.That(ocr.LastOptions.SingleLine, Is.True);
            Assert.That(info.Metadata.OcrText, Is.EqualTo("bonjour"));
        });
    }

    [Test]
    public async Task ProcessAsync_DoOcr_FallsBackToDefaultLanguageAndMinimumScale()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService();
        PlatformServices.Ocr = ocr;

        var taskSettings = new TaskSettings();
        taskSettings.AfterCaptureJob = AfterCaptureTasks.DoOCR;
        taskSettings.CaptureSettings.OCROptions.Language = " ";
        taskSettings.CaptureSettings.OCROptions.ScaleFactor = 0.25f;

        var info = new TaskInfo(taskSettings)
        {
            Metadata = new TaskMetadata(bitmap)
        };

        var processor = new CaptureJobProcessor();
        bool shouldContinue = await processor.ProcessAsync(info, CancellationToken.None);

        Assert.That(shouldContinue, Is.True);
        Assert.That(ocr.LastOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ocr.LastOptions!.Language, Is.EqualTo("en"));
            Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(1f));
            Assert.That(ocr.LastOptions.SingleLine, Is.False);
        });
    }

    [Test]
    public async Task ProcessAsync_DoOcr_TrimsLanguageAndRejectsNonFiniteScale()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService();
        PlatformServices.Ocr = ocr;

        var taskSettings = new TaskSettings();
        taskSettings.AfterCaptureJob = AfterCaptureTasks.DoOCR;
        taskSettings.CaptureSettings.OCROptions.Language = " fr ";
        taskSettings.CaptureSettings.OCROptions.ScaleFactor = float.NaN;

        var info = new TaskInfo(taskSettings)
        {
            Metadata = new TaskMetadata(bitmap)
        };

        var processor = new CaptureJobProcessor();
        bool shouldContinue = await processor.ProcessAsync(info, CancellationToken.None);

        Assert.That(shouldContinue, Is.True);
        Assert.That(ocr.LastOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ocr.LastOptions!.Language, Is.EqualTo("fr"));
            Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(1f));
        });
    }

    [Test]
    public async Task ProcessAsync_DoOcr_MissingOcrOptions_UsesDefaults()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService();
        PlatformServices.Ocr = ocr;

        var taskSettings = new TaskSettings();
        taskSettings.AfterCaptureJob = AfterCaptureTasks.DoOCR;
        taskSettings.CaptureSettings.OCROptions = null!;

        var info = new TaskInfo(taskSettings)
        {
            Metadata = new TaskMetadata(bitmap)
        };

        var processor = new CaptureJobProcessor();
        bool shouldContinue = await processor.ProcessAsync(info, CancellationToken.None);

        Assert.That(shouldContinue, Is.True);
        Assert.That(ocr.LastOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ocr.LastOptions!.Language, Is.EqualTo("en"));
            Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(2f));
            Assert.That(ocr.LastOptions.SingleLine, Is.False);
        });
    }

    [Test]
    public async Task ProcessAsync_DoOcr_WhitespaceOnlyResult_DoesNotPersistOcrText()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService("   \n\t");
        PlatformServices.Ocr = ocr;

        var taskSettings = new TaskSettings();
        taskSettings.AfterCaptureJob = AfterCaptureTasks.DoOCR;

        var info = new TaskInfo(taskSettings)
        {
            Metadata = new TaskMetadata(bitmap)
        };

        var processor = new CaptureJobProcessor();
        bool shouldContinue = await processor.ProcessAsync(info, CancellationToken.None);

        Assert.That(shouldContinue, Is.True);
        Assert.That(info.Metadata.OcrText, Is.Null);
    }

    private sealed class RecordingOcrService : IOcrService
    {
        private readonly string _recognizedText;

        public RecordingOcrService(string recognizedText = "bonjour")
        {
            _recognizedText = recognizedText;
        }

        public bool IsSupported => true;

        public OcrOptions? LastOptions { get; private set; }

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
        {
            LastOptions = new OcrOptions
            {
                Language = options.Language,
                ScaleFactor = options.ScaleFactor,
                SingleLine = options.SingleLine
            };

            return Task.FromResult(new OcrResult
            {
                Success = true,
                Text = _recognizedText
            });
        }

        public OcrLanguage[] GetAvailableLanguages() => [new("French", "fr")];
    }
}
