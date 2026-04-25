using System.Linq;
using Avalonia.Input;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Platform.Abstractions;
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

    [TestCase("Cmd + Shift + Print Screen", Key.PrintScreen, KeyModifiers.Meta | KeyModifiers.Shift)]
    [TestCase("Command + Enter", Key.Return, KeyModifiers.Meta)]
    [TestCase("Ctrl + Backspace", Key.Back, KeyModifiers.Control)]
    [TestCase("Alt + Caps Lock", Key.Capital, KeyModifiers.Alt)]
    [TestCase("Shift + Numpad 7", Key.NumPad7, KeyModifiers.Shift)]
    [TestCase("Ctrl + 5", Key.D5, KeyModifiers.Control)]
    [TestCase("Ctrl+Alt+S", Key.S, KeyModifiers.Control | KeyModifiers.Alt)]
    public void PrimaryHotkeyText_ParsesFormattedDisplayNames(string text, Key expectedKey, KeyModifiers expectedModifiers)
    {
        HotkeyStepViewModel viewModel = new();

        viewModel.PrimaryHotkeyText = text;

        Assert.That(viewModel.PrimaryHotkey, Is.EqualTo(new HotkeyInfo(expectedKey, expectedModifiers)));
        Assert.That(viewModel.PrimaryHotkey?.IsValid, Is.True);
    }
}
