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

        public OcrLanguage[] GetAvailableLanguages() =>
        [
            new("English", "en"),
            new("French", "fr")
        ];
    }
}
