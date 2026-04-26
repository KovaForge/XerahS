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
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;
using XerahS.Assistant.Routing;
using XerahS.Assistant.Services;
using XerahS.Core;
using XerahS.History;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class AssistantServiceTests
{
    [Test]
    public async Task ProcessPromptAsync_UsesProviderPlannedSeparator_ForLatestScreenshotPaths()
    {
        var clipboard = new FakeClipboardService();
        PlatformServices.Clipboard = clipboard;

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService(
            [
                CreateHistoryItem(@"C:\Shots\1.png", "1.png"),
                CreateHistoryItem(@"C:\Shots\2.png", "2.png"),
                CreateHistoryItem(@"C:\Shots\3.png", "3.png"),
                CreateHistoryItem(@"C:\Shots\4.png", "4.png"),
                CreateHistoryItem(@"C:\Shots\5.png", "5.png")
            ]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore(),
            activeProviderResolver: () => new AssistantProviderRuntimeSettings(
                new AssistantProviderMetadata(
                    "test",
                    "Test",
                    AssistantProviderProtocol.OpenAiResponses,
                    "test-model",
                    "https://example.invalid",
                    SupportsTools: false,
                    SupportsImageInput: false,
                    ["test-model"]),
                "test-model",
                "https://example.invalid",
                "test-key"),
            providerFactory: _ => new FakeAssistantModelProvider(
                """{"intent":"latest_screenshot_paths","limit":5,"copyRequested":true,"separator":";"}"""));

        try
        {
            AssistantResponse response = await service.ProcessPromptAsync(
                "copy last 5 screenshots filepath into clipboard, separated by ;",
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
            Assert.That(response.Message, Is.EqualTo("Copied 5 path(s) to clipboard."));
            Assert.That(clipboard.Text, Is.EqualTo(@"C:\Shots\1.png;C:\Shots\2.png;C:\Shots\3.png;C:\Shots\4.png;C:\Shots\5.png"));
        }
        finally
        {
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ProcessPromptAsync_FallsBackToLocalParser_WhenProviderReturnsNoMatch()
    {
        var clipboard = new FakeClipboardService();
        PlatformServices.Clipboard = clipboard;

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService([CreateHistoryItem(@"C:\Shots\Latest.png", "Latest.png")]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore(),
            activeProviderResolver: () => new AssistantProviderRuntimeSettings(
                new AssistantProviderMetadata(
                    "test",
                    "Test",
                    AssistantProviderProtocol.OpenAiResponses,
                    "test-model",
                    "https://example.invalid",
                    SupportsTools: false,
                    SupportsImageInput: false,
                    ["test-model"]),
                "test-model",
                "https://example.invalid",
                "test-key"),
            providerFactory: _ => new FakeAssistantModelProvider("""{"intent":"no_match"}"""));

        try
        {
            AssistantResponse response = await service.ProcessPromptAsync(
                "copy the path of the latest screenshot",
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
            Assert.That(clipboard.Text, Is.EqualTo(@"C:\Shots\Latest.png"));
        }
        finally
        {
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ProcessPromptAsync_WithoutProvider_HandlesLastFiveScreenshotsLocalFilePathPrompt()
    {
        var clipboard = new FakeClipboardService();
        PlatformServices.Clipboard = clipboard;

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService(
            [
                CreateHistoryItem(@"C:\Shots\1.png", "1.png"),
                CreateHistoryItem(@"C:\Shots\2.png", "2.png"),
                CreateHistoryItem(@"C:\Shots\3.png", "3.png"),
                CreateHistoryItem(@"C:\Shots\4.png", "4.png"),
                CreateHistoryItem(@"C:\Shots\5.png", "5.png")
            ]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ProcessPromptAsync(
                "give me last 5 screenshots local file path separated by ;",
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
            Assert.That(response.Message, Is.EqualTo("Last 5 screenshot paths."));
            Assert.That(response.Items, Has.Count.EqualTo(5));
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Text, Is.EqualTo(@"C:\Shots\1.png;C:\Shots\2.png;C:\Shots\3.png;C:\Shots\4.png;C:\Shots\5.png"));
            Assert.That(clipboard.Text, Is.Null);
        }
        finally
        {
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ProcessPromptAsync_WithoutProvider_HandlesLastFiveScreenshotsLocalFilepathSingleWordPrompt()
    {
        var clipboard = new FakeClipboardService();
        PlatformServices.Clipboard = clipboard;

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService(
            [
                CreateHistoryItem(@"C:\Shots\1.png", "1.png"),
                CreateHistoryItem(@"C:\Shots\2.png", "2.png"),
                CreateHistoryItem(@"C:\Shots\3.png", "3.png"),
                CreateHistoryItem(@"C:\Shots\4.png", "4.png"),
                CreateHistoryItem(@"C:\Shots\5.png", "5.png")
            ]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ProcessPromptAsync(
                "give me the last 5 screenshots local filepath separated by ;",
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
            Assert.That(response.Message, Is.EqualTo("Last 5 screenshot paths."));
            Assert.That(response.Items, Has.Count.EqualTo(5));
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Text, Is.EqualTo(@"C:\Shots\1.png;C:\Shots\2.png;C:\Shots\3.png;C:\Shots\4.png;C:\Shots\5.png"));
        }
        finally
        {
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ProcessPromptAsync_ProviderTextFallback_NormalizesSemicolonsWordSeparator()
    {
        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService(
            [
                CreateHistoryItem(@"C:\Shots\1.png", "1.png"),
                CreateHistoryItem(@"C:\Shots\2.png", "2.png"),
                CreateHistoryItem(@"C:\Shots\3.png", "3.png"),
                CreateHistoryItem(@"C:\Shots\4.png", "4.png"),
                CreateHistoryItem(@"C:\Shots\5.png", "5.png")
            ]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore(),
            activeProviderResolver: () => new AssistantProviderRuntimeSettings(
                new AssistantProviderMetadata(
                    "test",
                    "Test",
                    AssistantProviderProtocol.OpenAiResponses,
                    "test-model",
                    "https://example.invalid",
                    SupportsTools: false,
                    SupportsImageInput: false,
                    ["test-model"]),
                "test-model",
                "https://example.invalid",
                "test-key"),
            providerFactory: _ => new FakeAssistantModelProvider("last 5 screenshot paths separated by semicolons."));

        AssistantResponse response = await service.ProcessPromptAsync(
            "give me the last 5 screenshots local filepath separated by ;",
            CancellationToken.None);

        Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
        Assert.That(response.Actions, Has.Count.EqualTo(1));
        Assert.That(response.Actions[0].Text, Is.EqualTo(@"C:\Shots\1.png;C:\Shots\2.png;C:\Shots\3.png;C:\Shots\4.png;C:\Shots\5.png"));
    }

    [Test]
    public async Task ProcessPromptAsync_ProviderTextFallback_IgnoresArticleBeforeSemicolon()
    {
        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService(
            [
                CreateHistoryItem(@"C:\Shots\1.png", "1.png"),
                CreateHistoryItem(@"C:\Shots\2.png", "2.png"),
                CreateHistoryItem(@"C:\Shots\3.png", "3.png"),
                CreateHistoryItem(@"C:\Shots\4.png", "4.png"),
                CreateHistoryItem(@"C:\Shots\5.png", "5.png")
            ]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore(),
            activeProviderResolver: () => new AssistantProviderRuntimeSettings(
                new AssistantProviderMetadata(
                    "test",
                    "Test",
                    AssistantProviderProtocol.OpenAiResponses,
                    "test-model",
                    "https://example.invalid",
                    SupportsTools: false,
                    SupportsImageInput: false,
                    ["test-model"]),
                "test-model",
                "https://example.invalid",
                "test-key"),
            providerFactory: _ => new FakeAssistantModelProvider("last 5 screenshot paths separated by a semicolon."));

        AssistantResponse response = await service.ProcessPromptAsync(
            "give me the last 5 screenshots local filepath separated by ;",
            CancellationToken.None);

        Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Results));
        Assert.That(response.Actions, Has.Count.EqualTo(1));
        Assert.That(response.Actions[0].Text, Is.EqualTo(@"C:\Shots\1.png;C:\Shots\2.png;C:\Shots\3.png;C:\Shots\4.png;C:\Shots\5.png"));
    }

    [Test]
    public async Task ExecuteActionAsync_RunOcr_UsesCachedTextWhenRecognizerUnavailable()
    {
        string imagePath = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"), "missing-cached-ocr.png");
        var history = new FakeHistoryService([CreateHistoryItem(imagePath, Path.GetFileName(imagePath))]);
        history.CachedOcrByPath[imagePath] = "cached bonjour";

        var service = new AssistantService(
            new AssistantCommandRouter(),
            history,
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ExecuteActionAsync(
                new AssistantAction(AssistantActionKind.RunOcr, "Run OCR", FilePath: imagePath),
                confirmed: true,
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Information));
            Assert.That(response.Message, Is.EqualTo("cached bonjour"));
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Text, Is.EqualTo("cached bonjour"));
        }
        finally
        {
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ExecuteActionAsync_RunOcr_WhenRecognizerThrows_ReturnsFriendlyError()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "ocr-throws.png");

        using (var bitmap = new SKBitmap(8, 8))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(imagePath))
        {
            data.SaveTo(stream);
        }

        PlatformServices.Ocr = new ThrowingOcrService();

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService([CreateHistoryItem(imagePath, Path.GetFileName(imagePath))]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ExecuteActionAsync(
                new AssistantAction(AssistantActionKind.RunOcr, "Run OCR", FilePath: imagePath),
                confirmed: true,
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Error));
            Assert.That(response.Message, Is.EqualTo("OCR failed: OCR engine offline."));
            Assert.That(response.Actions, Is.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ExecuteActionAsync_RunOcr_CopyRequestedWithoutClipboard_ReturnsFriendlyError()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "ocr-copy.png");

        using (var bitmap = new SKBitmap(8, 8))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(imagePath))
        {
            data.SaveTo(stream);
        }

        PlatformServices.Ocr = new RecordingOcrService();

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService([CreateHistoryItem(imagePath, Path.GetFileName(imagePath))]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ExecuteActionAsync(
                new AssistantAction(AssistantActionKind.RunOcr, "Run OCR", Text: "copy", FilePath: imagePath),
                confirmed: true,
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Error));
            Assert.That(response.Message, Is.EqualTo("Clipboard is not available right now."));
            Assert.That(response.Actions, Has.Count.EqualTo(1));
            Assert.That(response.Actions[0].Kind, Is.EqualTo(AssistantActionKind.CopyText));
            Assert.That(response.Actions[0].Text, Is.EqualTo("bonjour"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ExecuteActionAsync_RunOcr_UsesConfiguredTaskOcrOptions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "ocr.png");

        using (var bitmap = new SKBitmap(8, 8))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(imagePath))
        {
            data.SaveTo(stream);
        }

        var ocr = new RecordingOcrService();
        PlatformServices.Ocr = ocr;
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "fr";
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.ScaleFactor = 3.5f;
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.SingleLine = true;

        var service = new AssistantService(
            new AssistantCommandRouter(),
            new FakeHistoryService([CreateHistoryItem(imagePath, Path.GetFileName(imagePath))]),
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ExecuteActionAsync(
                new AssistantAction(AssistantActionKind.RunOcr, "Run OCR", FilePath: imagePath),
                confirmed: true,
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Information));
            Assert.That(response.Message, Is.EqualTo("bonjour"));
            Assert.That(ocr.LastOptions, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(ocr.LastOptions!.Language, Is.EqualTo("fr"));
                Assert.That(ocr.LastOptions.ScaleFactor, Is.EqualTo(3.5f));
                Assert.That(ocr.LastOptions.SingleLine, Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            PlatformServices.Reset();
        }
    }

    [Test]
    public async Task ExecuteActionAsync_RunOcr_CachesRecognizedTextForHistoryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string imagePath = Path.Combine(directory, "ocr-cache.png");

        using (var bitmap = new SKBitmap(8, 8))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(imagePath))
        {
            data.SaveTo(stream);
        }

        PlatformServices.Ocr = new RecordingOcrService();
        var history = new FakeHistoryService([CreateHistoryItem(imagePath, Path.GetFileName(imagePath))]);
        var service = new AssistantService(
            new AssistantCommandRouter(),
            history,
            new AssistantPrivacyGuard(),
            memoryStore: CreateMemoryStore());

        try
        {
            AssistantResponse response = await service.ExecuteActionAsync(
                new AssistantAction(AssistantActionKind.RunOcr, "Run OCR", FilePath: imagePath),
                confirmed: true,
                CancellationToken.None);

            Assert.That(response.Kind, Is.EqualTo(AssistantResponseKind.Information));
            Assert.That(history.CachedOcrByPath.TryGetValue(imagePath, out string? cached), Is.True);
            Assert.That(cached, Is.EqualTo("bonjour"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            PlatformServices.Reset();
        }
    }

    private static AssistantHistoryItem CreateHistoryItem(string filePath, string fileName) =>
        new(
            Guid.NewGuid().ToString("N"),
            filePath,
            fileName,
            DateTimeOffset.UtcNow,
            "Image",
            null,
            Exists: true,
            new HistoryItem
            {
                FilePath = filePath,
                FileName = fileName
            });

    private static AssistantLocalMemoryStore CreateMemoryStore()
    {
        string directory = Path.Combine(Path.GetTempPath(), "XerahS.Assistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new AssistantLocalMemoryStore(Path.Combine(directory, "history.db"));
    }

    private sealed class FakeHistoryService(IReadOnlyList<AssistantHistoryItem> items) : IAssistantHistoryService
    {
        public Dictionary<string, string> CachedOcrByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<AssistantHistoryItem>> GetLatestScreenshotsAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AssistantHistoryItem>>(items.Take(limit).ToList());

        public Task<string?> GetCachedOcrTextAsync(string filePath, CancellationToken cancellationToken)
        {
            CachedOcrByPath.TryGetValue(filePath, out string? value);
            return Task.FromResult<string?>(value);
        }

        public Task CacheOcrTextAsync(string filePath, string ocrText, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(ocrText))
            {
                CachedOcrByPath[filePath] = ocrText;
            }

            return Task.CompletedTask;
        }

        public bool IsKnownHistoryFile(string filePath) =>
            items.Any(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeAssistantModelProvider(string text) : IAssistantModelProvider
    {
        public AssistantProviderMetadata Metadata => new(
            "test",
            "Test",
            AssistantProviderProtocol.OpenAiResponses,
            "test-model",
            "https://example.invalid",
            SupportsTools: false,
            SupportsImageInput: false,
            ["test-model"]);

        public Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AssistantModelResult(AssistantModelResultKind.Text, text, [], null, null));

        public Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken) =>
            Task.FromResult(new AssistantModelResult(AssistantModelResultKind.Text, "ok", [], null, null));
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? Text { get; private set; }

        public void Clear() => Text = null;
        public bool ContainsText() => !string.IsNullOrEmpty(Text);
        public bool ContainsImage() => false;
        public bool ContainsFileDropList() => false;
        public string? GetText() => Text;
        public void SetText(string text) => Text = text;
        public SKBitmap? GetImage() => null;
        public void SetImage(SKBitmap image) => throw new NotSupportedException();
        public string[]? GetFileDropList() => null;
        public void SetFileDropList(string[] files) => throw new NotSupportedException();
        public object? GetData(string format) => null;
        public void SetData(string format, object data) => throw new NotSupportedException();
        public bool ContainsData(string format) => false;
        public Task<string?> GetTextAsync() => Task.FromResult(Text);
        public Task SetTextAsync(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOcrService : IOcrService
    {
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
                Text = "bonjour"
            });
        }

        public OcrLanguage[] GetAvailableLanguages() => [new("French", "fr")];
    }

    private sealed class ThrowingOcrService : IOcrService
    {
        public bool IsSupported => true;

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
            => throw new InvalidOperationException("OCR engine offline.");

        public OcrLanguage[] GetAvailableLanguages() => [new("English", "en")];
    }
}
