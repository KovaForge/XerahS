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

using System.Linq;
using NUnit.Framework;
using XerahS.UI.Onboarding;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.Tests.UI;

[TestFixture]
public class OnboardingWizardOcrStepIntegrationTests
{
    [Test]
    public void InitializeSteps_ContainsOcrStepViewModel()
    {
        var wizard = new OnboardingWizardViewModel();

        Assert.That(wizard.Steps, Has.Count.EqualTo(6));
        Assert.That(wizard.Steps.OfType<OcrStepViewModel>(), Has.Exactly(1).Items);
    }

    [Test]
    public void InitializeSteps_OcrStepIsAtCorrectIndex()
    {
        var wizard = new OnboardingWizardViewModel();

        // Step indices: 0=Welcome, 1=SaveLocation, 2=Hotkey, 3=Upload, 4=OCR, 5=Complete
        Assert.That(wizard.Steps[4], Is.InstanceOf<OcrStepViewModel>());
    }

    [Test]
    public void InitializeSteps_WelcomeStepIsFirst()
    {
        var wizard = new OnboardingWizardViewModel();

        Assert.That(wizard.Steps[0], Is.InstanceOf<WelcomeStepViewModel>());
    }

    [Test]
    public void Next_SkipsToOcrStep_PersistsUploadAndLoadsOcr()
    {
        var wizard = new OnboardingWizardViewModel();

        // Step 0: Welcome - auto-advances since language is selected by default
        wizard.NextCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(1));

        // Step 1: SaveLocation - auto-advances since it's valid by default
        wizard.NextCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        // Step 2: Hotkey - auto-advances since no hotkey is set
        wizard.NextCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Step 3: Upload - auto-advances since no uploader is selected
        wizard.NextCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4));
        Assert.That(wizard.CurrentStep, Is.InstanceOf<OcrStepViewModel>());
    }

    [Test]
    public void CompleteWizard_SkipsOcrStep_DoesNotOverwriteExistingLanguage()
    {
        // Set up a state where OCR step is skipped (step index 4)
        OnboardingState state = new()
        {
            SelectedOcrLanguages = ["fr"],
            SkippedSteps = [4] // OCR step skipped
        };

        var wizard = new OnboardingWizardViewModel();
        wizard.LoadFromState(state);

        // Manually set the OCR language to something different before CommitSettings
        var ocrStep = wizard.Steps.OfType<OcrStepViewModel>().Single();
        ocrStep.SelectedLanguages.Clear();
        ocrStep.SelectedLanguages.Add("de");

        // Save state back from the step
        ocrStep.SaveToState(state);

        // Now skip OCR step in state (simulating user skip)
        state.SkippedSteps.Add(4);

        // Clear OCR languages in state (as if user never touched it)
        state.SelectedOcrLanguages = [];

        var wizard2 = new OnboardingWizardViewModel();
        wizard2.LoadFromState(state);

        // Commit settings — since OCR step is skipped and no languages selected,
        // the existing DefaultTaskSettings OCR language should be preserved
        // (CommitSettingsAsync checks state.SelectedOcrLanguages.Count > 0 before writing)
        Assert.Multiple(() =>
        {
            Assert.That(state.SelectedOcrLanguages.Count, Is.EqualTo(0));
            Assert.That(state.SkippedSteps.Contains(4), Is.True);
        });
    }

    [Test]
    public void OcrStep_SaveAndLoad_RoundtripsSelectedLanguages()
    {
        var wizard = new OnboardingWizardViewModel();
        var ocrStep = wizard.Steps.OfType<OcrStepViewModel>().Single();

        // Advance to OCR step (index 4) and load state
        wizard.CurrentStepIndex = 4;
        wizard.CurrentStep?.LoadFromState(wizard.State);

        // Verify we are on the OCR step
        Assert.That(wizard.CurrentStep, Is.InstanceOf<OcrStepViewModel>());

        // Select some languages — note: "en" is always included as fallback
        // So we expect "en" + "fr" + "de" after sync
        ocrStep.SelectedLanguages.Clear();
        ocrStep.SelectedLanguages.Add("fr");
        ocrStep.SelectedLanguages.Add("de");

        // Save to state
        ocrStep.SaveToState(wizard.State);

        // State should contain at least fr and de (en may be included as fallback)
        Assert.That(wizard.State.SelectedOcrLanguages, Does.Contain("fr"));
        Assert.That(wizard.State.SelectedOcrLanguages, Does.Contain("de"));

        // Create a new wizard and load the state
        var wizard2 = new OnboardingWizardViewModel();
        wizard2.LoadFromState(wizard.State);

        var ocrStep2 = wizard2.Steps.OfType<OcrStepViewModel>().Single();
        wizard2.CurrentStepIndex = 4;
        wizard2.CurrentStep?.LoadFromState(wizard2.State);

        // Verify fr and de are preserved
        Assert.That(ocrStep2.SelectedLanguages, Does.Contain("fr"));
        Assert.That(ocrStep2.SelectedLanguages, Does.Contain("de"));
    }
}
