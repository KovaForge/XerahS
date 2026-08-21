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
using XerahS.Platform.Abstractions;
using XerahS.UI.Onboarding;
using XerahS.UI.Onboarding.ViewModels.Steps;

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

    [Test]
    public async Task CommitSettingsAsync_SaveLocation_AppliesWhenStepNotSkipped()
    {
        string folder = Path.Combine(_rootPath, "Screenshots");
        OnboardingState state = new()
        {
            ScreenshotsFolder = folder,
            CreateDateSubfolders = true,
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.Multiple(() =>
        {
            Assert.That(SettingsManager.Settings.CustomScreenshotsPath, Is.EqualTo(folder));
            Assert.That(SettingsManager.Settings.UseCustomScreenshotsPath, Is.True);
            Assert.That(SettingsManager.Settings.SaveImageSubFolderPattern, Is.EqualTo("%y-%mo"));
        });
    }

    [Test]
    public async Task CommitSettingsAsync_SaveLocation_DoesNotApplyWhenStepSkipped()
    {
        SettingsManager.Settings.CustomScreenshotsPath = string.Empty;
        SettingsManager.Settings.UseCustomScreenshotsPath = false;

        OnboardingState state = new()
        {
            ScreenshotsFolder = Path.Combine(_rootPath, "Ignored"),
            SkippedSteps = [OnboardingStepIndices.SaveLocation],
        };

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        Assert.That(SettingsManager.Settings.UseCustomScreenshotsPath, Is.False);
    }

    [Test]
    public async Task CommitSettingsAsync_DoesNotModifyOcrSettings()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Language = "ja";
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.PreferredLanguages = new List<string> { "ja", "ko" };

        OnboardingState state = new();

        var viewModel = new OnboardingWizardViewModel();
        await viewModel.CommitSettingsAsync(state);

        var ocrOptions = SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions;
        Assert.Multiple(() =>
        {
            Assert.That(ocrOptions.Language, Is.EqualTo("ja"));
            Assert.That(ocrOptions.PreferredLanguages, Is.EquivalentTo(new[] { "ja", "ko" }));
        });
    }
}
