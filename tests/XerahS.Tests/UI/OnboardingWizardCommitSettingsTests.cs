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
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Onboarding;

namespace XerahS.Tests.UI;

[TestFixture]
[NonParallelizable]
public class OnboardingWizardCommitSettingsTests
{
    private string _rootPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XerahS.Tests", "OnboardingWizard", Guid.NewGuid().ToString("N"));
        var personalFolder = Path.Combine(_rootPath, "Personal");
        Directory.CreateDirectory(personalFolder);
        SettingsManager.PersonalFolder = personalFolder;
        SettingsManager.LoadAllSettings();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        SettingsManager.PersonalFolder = null!;
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "en";
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.PreferredLanguages = new List<string>();
    }

    [TearDown]
    public void TearDown()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "en";
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.PreferredLanguages = new List<string>();
    }

    [Test]
    public async Task CommitSettingsAsync_WithSelectedOcrLanguages_SetsPrimaryLanguageInDefaultTaskSettings()
    {
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["fr", "de"]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.That(SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language, Is.EqualTo("fr"));
    }

    [Test]
    public async Task CommitSettingsAsync_EmptyOcrLanguages_DoesNotChangeExistingLanguage()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "ja";

        OnboardingState state = new()
        {
            SelectedOcrLanguages = []
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.That(SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language, Is.EqualTo("ja"));
    }

    [Test]
    public async Task CommitSettingsAsync_SkippedOcrStep_DoesNotOverwriteExistingLanguage()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "es";

        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["fr"],
            SkippedSteps = [4]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.That(SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language, Is.EqualTo("es"));
    }

    [Test]
    public async Task CommitSettingsAsync_SingleSelectedLanguage_SetsThatLanguage()
    {
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["en"]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.That(SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language, Is.EqualTo("en"));
    }

    [Test]
    public async Task CommitSettingsAsync_MultipleSelectedOcrLanguages_PersistsPreferredLanguagesList()
    {
        // Today the OCR runtime only supports a single language per call,
        // so OCROptions.Language carries the first selection as the primary.
        // The full list must still be persisted to PreferredLanguages so
        // the user's onboarding choice is not silently dropped.
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["fr", "de", "es"]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
        Assert.That(ocrOptions.Language, Is.EqualTo("fr"));
        Assert.That(ocrOptions.PreferredLanguages, Is.EquivalentTo(new[] { "fr", "de", "es" }));
    }

    [Test]
    public async Task CommitSettingsAsync_SingleSelectedLanguage_PersistsPreferredLanguagesListWithOneEntry()
    {
        // A one-entry selection must still round-trip through PreferredLanguages
        // so consumers reading the list (vs the scalar Language) see a consistent
        // view of the user's onboarding choice.
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["de"]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
        Assert.That(ocrOptions.Language, Is.EqualTo("de"));
        Assert.That(ocrOptions.PreferredLanguages, Is.EquivalentTo(new[] { "de" }));
    }

    [Test]
    public async Task CommitSettingsAsync_SkippedOcrStep_DoesNotOverwritePreferredLanguages()
    {
        // Skipping the OCR step must not clobber an existing PreferredLanguages
        // list (mirrors the existing Language non-overwrite behavior).
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.PreferredLanguages = new List<string> { "ja" };

        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["fr"],
            SkippedSteps = [4]
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
        Assert.That(ocrOptions.PreferredLanguages, Is.EquivalentTo(new[] { "ja" }));
    }

    [Test]
    public async Task CommitSettingsAsync_EmptyOcrLanguages_DoesNotChangePreferredLanguages()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.PreferredLanguages = new List<string> { "es", "it" };

        OnboardingState state = new()
        {
            SelectedOcrLanguages = []
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
        Assert.That(ocrOptions.PreferredLanguages, Is.EquivalentTo(new[] { "es", "it" }));
    }
}
