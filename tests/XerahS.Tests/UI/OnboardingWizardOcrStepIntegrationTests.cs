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
public class OnboardingWizardStepIntegrationTests
{
    [Test]
    public void InitializeSteps_HasFiveStepsWithoutOcr()
    {
        var wizard = new OnboardingWizardViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(wizard.Steps, Has.Count.EqualTo(5));
            Assert.That(wizard.Steps.OfType<OcrStepViewModel>(), Is.Empty);
            Assert.That(wizard.Steps[0], Is.InstanceOf<WelcomeStepViewModel>());
            Assert.That(wizard.Steps[1], Is.InstanceOf<SaveLocationStepViewModel>());
            Assert.That(wizard.Steps[2], Is.InstanceOf<HotkeyStepViewModel>());
            Assert.That(wizard.Steps[3], Is.InstanceOf<UploadStepViewModel>());
            Assert.That(wizard.Steps[4], Is.InstanceOf<CompleteStepViewModel>());
        });
    }

    [Test]
    public void Next_FromUploadStep_GoesToCompleteStep()
    {
        var wizard = new OnboardingWizardViewModel();

        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(wizard.CurrentStepIndex, Is.EqualTo(OnboardingStepIndices.Complete));
            Assert.That(wizard.CurrentStep, Is.InstanceOf<CompleteStepViewModel>());
        });
    }
}
