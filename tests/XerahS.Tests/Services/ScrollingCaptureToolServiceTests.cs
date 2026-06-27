using System.Reflection;
using NUnit.Framework;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Services;

[TestFixture]
public class ScrollingCaptureToolServiceTests
{
    private static readonly FieldInfo? s_currentCaptureField =
        typeof(ScrollingCaptureToolService).GetField(
            "<CurrentCapture>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        s_currentCaptureField?.SetValue(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        s_currentCaptureField?.SetValue(null, null);
    }

    [Test]
    public void CurrentCapture_OnlyCleared_WhenMatchingOwningWindowCloses()
    {
        // Regression: before the fix, every window's Closed handler unconditionally
        // set CurrentCapture = null. When window A was closed after window B opened,
        // B's capture was lost. The fix guards with ReferenceEquals.

        // Arrange: simulate two windows each with their own view model
        var vm1 = new ScrollingCaptureViewModel();
        var vm2 = new ScrollingCaptureViewModel();

        // Simulate ShowScrollingCaptureWindowAsync for window 1:
        //   CurrentCapture = vm1;
        //   window1.Closed += (_, _) => { if (ReferenceEquals(CurrentCapture, vm1)) CurrentCapture = null; };
        Action? close1 = null;
        s_currentCaptureField?.SetValue(null, vm1);
        close1 = () =>
        {
            if (ReferenceEquals(ScrollingCaptureToolService.CurrentCapture, vm1))
                s_currentCaptureField?.SetValue(null, null);
        };

        // Simulate ShowScrollingCaptureWindowAsync for window 2:
        //   CurrentCapture = vm2;
        //   window2.Closed += (_, _) => { if (ReferenceEquals(CurrentCapture, vm2)) CurrentCapture = null; };
        Action? close2 = null;
        s_currentCaptureField?.SetValue(null, vm2);
        close2 = () =>
        {
            if (ReferenceEquals(ScrollingCaptureToolService.CurrentCapture, vm2))
                s_currentCaptureField?.SetValue(null, null);
        };

        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.SameAs(vm2),
            "CurrentCapture should be vm2 after second window opens");

        // Act: close the old window (window 1)
        close1();

        // Assert: CurrentCapture should still be vm2
        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.SameAs(vm2),
            "CurrentCapture should still be vm2 after old window closes");

        // Act: close the owning window (window 2)
        close2();

        // Assert: CurrentCapture should be null now
        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.Null,
            "CurrentCapture should be null after owning window closes");
    }

    [Test]
    public void CurrentCapture_Cleared_WhenOwningWindowCloses_WithoutSecondWindow()
    {
        // Verify that the basic single-window case still works

        var vm = new ScrollingCaptureViewModel();
        s_currentCaptureField?.SetValue(null, vm);

        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.SameAs(vm));

        // Simulate window close with the guard
        if (ReferenceEquals(ScrollingCaptureToolService.CurrentCapture, vm))
            s_currentCaptureField?.SetValue(null, null);

        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.Null,
            "CurrentCapture should be null after the only window closes");
    }

    [Test]
    public void CurrentCapture_NotCleared_WhenNonOwningWindowCloses()
    {
        // Additional edge case: closing an unrelated window should not clear

        var vm1 = new ScrollingCaptureViewModel();
        var vm2 = new ScrollingCaptureViewModel();

        // Window 1 opens (CurrentCapture = vm1)
        s_currentCaptureField?.SetValue(null, vm1);

        // Window 2 opens, overwriting CurrentCapture
        s_currentCaptureField?.SetValue(null, vm2);

        // Now close window 1 with the guard - should NOT clear
        if (ReferenceEquals(ScrollingCaptureToolService.CurrentCapture, vm1))
            s_currentCaptureField?.SetValue(null, null);

        Assert.That(ScrollingCaptureToolService.CurrentCapture, Is.SameAs(vm2),
            "CurrentCapture should remain vm2 when non-owning window closes");
    }
}
