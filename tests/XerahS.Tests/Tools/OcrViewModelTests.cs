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
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class OcrViewModelTests
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
    public async Task SelectedLanguageChange_AfterSuccessfulRun_ReRunsOcr()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService(["first", "second"]);
        PlatformServices.Ocr = ocr;

        var viewModel = new OcrViewModel(bitmap);
        var secondLanguage = viewModel.AvailableLanguages[1];

        await viewModel.RunOcrAsync();
        viewModel.SelectedLanguage = secondLanguage;
        await Task.Delay(50);

        Assert.That(ocr.CallCount, Is.EqualTo(2));
        Assert.That(ocr.RequestedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.ResultText, Is.EqualTo("second"));
    }

    [Test]
    public async Task SelectedLanguageChange_AfterNoTextResult_ReRunsOcr()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService([string.Empty, "bonjour"]);
        PlatformServices.Ocr = ocr;

        var viewModel = new OcrViewModel(bitmap);
        var secondLanguage = viewModel.AvailableLanguages[1];

        await viewModel.RunOcrAsync();
        Assert.That(viewModel.HasResult, Is.False);

        viewModel.SelectedLanguage = secondLanguage;
        await Task.Delay(50);

        Assert.That(ocr.CallCount, Is.EqualTo(2));
        Assert.That(ocr.RequestedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.ResultText, Is.EqualTo("bonjour"));
        Assert.That(viewModel.HasResult, Is.True);
    }

    [Test]
    public async Task RunOcrAsync_TrimsLanguageAndRejectsNonFiniteScale()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new RecordingOcrService(["bonjour"]);
        PlatformServices.Ocr = ocr;

        var viewModel = new OcrViewModel(bitmap)
        {
            SelectedLanguage = new OcrLanguage("French", " fr "),
            ScaleFactor = double.PositiveInfinity
        };

        await viewModel.RunOcrAsync();

        Assert.That(ocr.LastOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ocr.LastOptions!.Language, Is.EqualTo("fr"));
            Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(1f));
        });
    }


    [Test]
    public async Task RunOcrAsync_ClearsPreviousResultStateWhileProcessing()
    {
        using var bitmap = new SKBitmap(8, 8);
        var ocr = new WaitingOcrService();
        PlatformServices.Ocr = ocr;

        var viewModel = new OcrViewModel(bitmap)
        {
            ResultText = "previous text",
            HasResult = true
        };

        Task runTask = viewModel.RunOcrAsync();
        await ocr.WaitForCallAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsProcessing, Is.True);
            Assert.That(viewModel.ResultText, Is.Empty);
            Assert.That(viewModel.HasResult, Is.False,
                "Starting a new OCR pass should immediately clear stale result state while recognition is still running.");
        });

        ocr.Complete("new text");
        await runTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ResultText, Is.EqualTo("new text"));
            Assert.That(viewModel.HasResult, Is.True);
        });
    }

    private sealed class WaitingOcrService : IOcrService
    {
        private readonly TaskCompletionSource _callStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _resultText = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsSupported => true;

        public Task WaitForCallAsync() => _callStarted.Task;

        public void Complete(string text) => _resultText.SetResult(text);

        public async Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
        {
            _callStarted.SetResult();
            string text = await _resultText.Task;
            return new OcrResult
            {
                Success = true,
                Text = text
            };
        }

        public OcrLanguage[] GetAvailableLanguages() =>
        [
            new("English", "en")
        ];
    }

    [Test]
    public void LoadAvailableLanguages_NormalizesPlatformTagsAndDisplayNames()
    {
        var ocr = new RecordingOcrService(["first"])
        {
            AvailableLanguages =
            [
                new(" English ", " en "),
                new(" English dup ", "En"),
                new(" ", " fr "),
                new("Japanese", ""),
                new("German", "de")
            ]
        };
        PlatformServices.Ocr = ocr;

        using var bitmap = new SKBitmap(8, 8);
        var viewModel = new OcrViewModel(bitmap);

        Assert.That(viewModel.AvailableLanguages.Count, Is.EqualTo(3));
        Assert.That(viewModel.AvailableLanguages.Select(l => l.LanguageTag), Is.EqualTo(new[] { "en", "fr", "de" }));
        Assert.That(viewModel.AvailableLanguages.First(l => l.LanguageTag == "en").DisplayName, Is.EqualTo("English"));
        Assert.That(viewModel.AvailableLanguages.First(l => l.LanguageTag == "fr").DisplayName, Is.EqualTo("fr"));
        Assert.That(viewModel.AvailableLanguages.First(l => l.LanguageTag == "de").DisplayName, Is.EqualTo("German"));
        Assert.That(viewModel.SelectedLanguage!.LanguageTag, Is.EqualTo("en"));
    }

    [Test]
    public void LoadAvailableLanguages_WhenPlatformEnumerationThrows_SurfacesStatusInsteadOfThrowing()
    {
        PlatformServices.Ocr = new ThrowingLanguageOcrService();

        using var bitmap = new SKBitmap(8, 8);
        var viewModel = new OcrViewModel(bitmap);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AvailableLanguages, Is.Empty);
            Assert.That(viewModel.SelectedLanguage, Is.Null);
            Assert.That(viewModel.StatusText, Does.Contain("OCR language enumeration failed."));
        });
    }

    private sealed class RecordingOcrService : IOcrService
    {
        private readonly Queue<string> _responses;

        public RecordingOcrService(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public bool IsSupported => true;

        public int CallCount { get; private set; }

        public List<string> RequestedLanguages { get; } = new();

        public OcrOptions? LastOptions { get; private set; }

        public OcrLanguage[] AvailableLanguages { get; set; } =
        [
            new("English", "en"),
            new("French", "fr")
        ];

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
        {
            CallCount++;
            LastOptions = new OcrOptions
            {
                Language = options.Language,
                ScaleFactor = options.ScaleFactor,
                SingleLine = options.SingleLine
            };
            RequestedLanguages.Add(options.Language);
            string text = _responses.Count > 0 ? _responses.Dequeue() : string.Empty;
            return Task.FromResult(new OcrResult
            {
                Success = true,
                Text = text
            });
        }

        public OcrLanguage[] GetAvailableLanguages() => AvailableLanguages;
    }

    private sealed class ThrowingLanguageOcrService : IOcrService
    {
        public bool IsSupported => true;

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options) =>
            Task.FromResult(new OcrResult { Success = true });

        public OcrLanguage[] GetAvailableLanguages() => throw new InvalidOperationException("OCR language enumeration failed.");
    }
}
