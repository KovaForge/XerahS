using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.UI.Onboarding;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class OnboardingOcrStepViewModelTests
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
    public void LoadFromState_UsesEnglishDefault()
    {
        OcrStepViewModel viewModel = new();
        OnboardingState state = new();

        viewModel.LoadFromState(state);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.Validate(), Is.True);
    }

    [Test]
    public void ReplacingSelectedLanguages_NormalizesUnsupportedEntries_AndSyncsOptions()
    {
        OcrStepViewModel viewModel = new();

        viewModel.SelectedLanguages = new ObservableCollection<string>(["fr", "xx", "fr"]);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "fr" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.False);
        Assert.That(viewModel.TotalDownloadSizeMb, Is.EqualTo(20));
    }

    [Test]
    public void ReplacingSelectedLanguages_NormalizesCaseToCanonicalTag_AndSyncsOptions()
    {
        OcrStepViewModel viewModel = new();

        viewModel.SelectedLanguages = new ObservableCollection<string>(["EN", "Fr"]);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
        Assert.That(viewModel.TotalDownloadSizeMb, Is.EqualTo(45));
    }

    [Test]
    public void ReplacingSelectedLanguages_UnsubscribesPreviousCollection()
    {
        OcrStepViewModel viewModel = new();
        ObservableCollection<string> previousSelection = viewModel.SelectedLanguages;

        viewModel.SelectedLanguages = new ObservableCollection<string>(["fr"]);
        previousSelection.Clear();

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "fr" }));
        Assert.That(viewModel.IsValid, Is.True);
        Assert.That(viewModel.HasValidationError, Is.False);
    }

    [Test]
    public void ReplacingSelectedLanguages_WithNullCollection_RecreatesFallbackSelection()
    {
        OcrStepViewModel viewModel = new();
        ObservableCollection<string> previousSelection = viewModel.SelectedLanguages;

        viewModel.SelectedLanguages = null!;
        previousSelection.Clear();

        Assert.That(viewModel.SelectedLanguages, Is.Not.Null);
        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.IsValid, Is.True);
        Assert.That(viewModel.HasValidationError, Is.False);
    }

    [Test]
    public void MutatingSelectedLanguages_NormalizesCollection_AndSyncsOptions()
    {
        OcrStepViewModel viewModel = new();

        viewModel.SelectedLanguages.Add("FR");
        viewModel.SelectedLanguages.Add("xx");
        viewModel.SelectedLanguages.Add("fr");

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.TotalDownloadSizeMb, Is.EqualTo(45));
    }

    [Test]
    public void SetDefaultLanguage_WithRegionalLanguageCode_SelectsNeutralLanguage()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["fr"]);

        viewModel.SetDefaultLanguage(" en-US ");

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "fr", "en" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
        Assert.That(viewModel.TotalDownloadSizeMb, Is.EqualTo(45));
    }

    [Test]
    public async Task RefreshAvailableLanguages_UnsubscribesRemovedOptions()
    {
        OcrStepViewModel viewModel = new();
        OcrLanguageOption removedSpanish = viewModel.AvailableLanguages.Single(language => language.LanguageTag == "es");
        PlatformServices.Ocr = new StubOcrService([new("English", "en")]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);
        removedSpanish.IsSelected = true;

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en" }));
        Assert.That(viewModel.AvailableLanguages.Select(language => language.LanguageTag), Is.EqualTo(new[] { "en" }));
    }

    [Test]
    public async Task RefreshAvailableLanguages_NormalizesAndDeduplicatesPlatformLanguageTags()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["EN", "fr"]);
        PlatformServices.Ocr = new StubOcrService(
        [
            new("English", " en "),
            new("English duplicate", "EN"),
            new("French", " fr "),
            new("Invalid", " ")
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.AvailableLanguages.Select(language => language.LanguageTag), Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
    }

    [Test]
    public async Task RefreshAvailableLanguages_TrimsSelectedLanguageTagsBeforeSync()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>([" fr "]);
        PlatformServices.Ocr = new StubOcrService(
        [
            new("English", "en"),
            new("French", "fr")
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "fr" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.False);
    }

    [Test]
    public async Task RefreshAvailableLanguages_ReordersSelectedLanguagesToPlatformOrder()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["fr", "en"]);
        PlatformServices.Ocr = new StubOcrService(
        [
            new("English", "en"),
            new("French", "fr")
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.AvailableLanguages.Select(language => language.LanguageTag), Is.EqualTo(new[] { "en", "fr" }));
        Assert.That(viewModel.AvailableLanguages.Where(language => language.IsSelected).Select(language => language.LanguageTag), Is.EqualTo(new[] { "en", "fr" }));
    }

    [Test]
    public async Task RefreshAvailableLanguages_MatchesNeutralSelectionToRegionalPlatformLanguage()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["en"]);
        PlatformServices.Ocr = new StubOcrService(
        [
            new("English (United States)", "en-US"),
            new("English (United Kingdom)", "en-GB"),
            new("French", "fr")
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en-US" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en-US").IsSelected, Is.True);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en-GB").IsSelected, Is.False);
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").IsSelected, Is.False);
    }

    [Test]
    public async Task RefreshAvailableLanguages_TrimsDisplayNames_AndFallsBackToLanguageTag()
    {
        OcrStepViewModel viewModel = new();
        PlatformServices.Ocr = new StubOcrService(
        [
            new(" English ", "en"),
            new(" ", "fr")
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").DisplayName, Is.EqualTo("English"));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "fr").DisplayName, Is.EqualTo("fr"));
    }

    [Test]
    public async Task RefreshAvailableLanguages_WithNoValidPlatformLanguages_KeepsFallbackLanguages()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["xx"]);
        PlatformServices.Ocr = new StubOcrService(
        [
            new("Invalid", " "),
            new("Missing", null!)
        ]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.AvailableLanguages.Select(language => language.LanguageTag), Does.Contain("en"));
        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.Validate(), Is.True);
    }

    [Test]
    public async Task RefreshAvailableLanguages_WhenPlatformEnumerationThrows_KeepsFallbackLanguages()
    {
        OcrStepViewModel viewModel = new();
        viewModel.SelectedLanguages = new ObservableCollection<string>(["xx"]);
        PlatformServices.Ocr = new ThrowingOcrService();

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.That(viewModel.AvailableLanguages.Select(language => language.LanguageTag), Does.Contain("en"));
        Assert.That(viewModel.SelectedLanguages, Is.EqualTo(new[] { "en" }));
        Assert.That(viewModel.AvailableLanguages.Single(language => language.LanguageTag == "en").IsSelected, Is.True);
        Assert.That(viewModel.Validate(), Is.True);
    }

    [Test]
    public async Task RefreshAvailableLanguages_WhenPlatformEnumerationThrows_SurfacesRefreshError()
    {
        OcrStepViewModel viewModel = new();
        PlatformServices.Ocr = new ThrowingOcrService();

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasLanguageRefreshError, Is.True);
            Assert.That(viewModel.LanguageRefreshError, Does.Contain("OCR language enumeration failed."));
        });
    }

    [Test]
    public async Task RefreshAvailableLanguages_AfterFailureClearsRefreshError()
    {
        OcrStepViewModel viewModel = new();
        PlatformServices.Ocr = new ThrowingOcrService();
        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);
        PlatformServices.Ocr = new StubOcrService([new("English", "en")]);

        await viewModel.RefreshAvailableLanguagesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasLanguageRefreshError, Is.False);
            Assert.That(viewModel.LanguageRefreshError, Is.Null);
        });
    }

    private sealed class StubOcrService(OcrLanguage[] languages) : IOcrService
    {
        public bool IsSupported => true;

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options) =>
            Task.FromResult(new OcrResult { Success = true });

        public OcrLanguage[] GetAvailableLanguages() => languages;
    }

    private sealed class ThrowingOcrService : IOcrService
    {
        public bool IsSupported => true;

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options) =>
            Task.FromResult(new OcrResult { Success = true });

        public OcrLanguage[] GetAvailableLanguages() => throw new InvalidOperationException("OCR language enumeration failed.");
    }
}
