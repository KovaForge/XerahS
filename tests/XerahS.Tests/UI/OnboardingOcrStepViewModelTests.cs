using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using XerahS.UI.Onboarding;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class OnboardingOcrStepViewModelTests
{
    [Test]
    public void LoadFromState_WithUnsupportedLanguages_FallsBackToEnglish()
    {
        OcrStepViewModel viewModel = new();
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["xx", "yy"]
        };

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
}
