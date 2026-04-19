using System.Linq;
using NUnit.Framework;
using XerahS.Core;
using XerahS.UI.Onboarding;
using XerahS.UI.Onboarding.ViewModels.Steps;

namespace XerahS.Tests.UI;

[TestFixture]
public class OnboardingHotkeyConfigurationTests
{
    [Test]
    public void SecondaryHotkeyDefaults_DoNotDuplicatePrimaryWorkflow()
    {
        HotkeyStepViewModel viewModel = new();

        WorkflowType[] secondaryJobs = viewModel.SecondaryHotkeyItems
            .Select(item => item.Model.Job)
            .ToArray();

        Assert.That(secondaryJobs, Is.EqualTo(OnboardingWizardViewModel.GetSecondaryOnboardingWorkflowJobs()));
        Assert.That(secondaryJobs, Does.Not.Contain(WorkflowType.RectangleRegion));
        Assert.That(secondaryJobs.Distinct().Count(), Is.EqualTo(secondaryJobs.Length));
    }
}
