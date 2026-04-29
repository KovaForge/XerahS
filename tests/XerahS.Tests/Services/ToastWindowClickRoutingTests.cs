using Avalonia.Input;
using NUnit.Framework;
using Point = global::Avalonia.Point;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.Tests.Services;

[TestFixture]
public class ToastWindowClickRoutingTests
{
    [Test]
    public void TryGetClickAction_LeftReleaseWithinThreshold_ReturnsLeftClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(18, 15),
            PointerUpdateKind.LeftButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.LeftClick));
        });
    }

    [Test]
    public void TryGetClickAction_MiddleReleaseWithinThreshold_ReturnsMiddleClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(12, 14),
            PointerUpdateKind.MiddleButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.MiddleClick));
        });
    }

    [Test]
    public void TryGetClickAction_DragBeyondThreshold_DoesNotTriggerClick()
    {
        var handled = ToastWindow.TryGetClickAction(
            new Point(10, 10),
            new Point(35, 10),
            PointerUpdateKind.LeftButtonReleased,
            out var action);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(action, Is.EqualTo(ToastWindow.ToastPointerAction.None));
        });
    }

    [Test]
    public void ToastConfig_IsValid_RejectsInvalidToastTimings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new ToastConfig { Duration = -1, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = -1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = float.PositiveInfinity, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = float.PositiveInfinity }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = float.NaN, FadeDuration = 1 }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = float.NaN }.IsValid, Is.False);
            Assert.That(new ToastConfig { Duration = 0, FadeDuration = 1 }.IsValid, Is.True);
            Assert.That(new ToastConfig { Duration = 1, FadeDuration = 0 }.IsValid, Is.True);
        });
    }

    [Test]
    public void ToastConfig_IsValid_AllowsStickyToastWithZeroTimers_WhenAutoHideDisabled()
    {
        var config = new ToastConfig
        {
            AutoHide = false,
            Duration = 0,
            FadeDuration = 0
        };

        Assert.That(config.IsValid, Is.True);
    }

    [Test]
    public void ToastConfig_IsValid_RejectsAutoHideToastWithZeroTimers()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 0
        };

        Assert.That(config.IsValid, Is.False);
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_StartsFadeImmediately_WhenDurationIsZero()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 0,
            FadeDuration = 1
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.StartFade));
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_WaitsForDuration_WhenDurationIsPositive()
    {
        var config = new ToastConfig
        {
            AutoHide = true,
            Duration = 1,
            FadeDuration = 0
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.WaitForDuration));
    }

    [Test]
    public void ToastViewModel_GetAutoHideStartMode_DoesNotAutoHide_WhenDisabled()
    {
        var config = new ToastConfig
        {
            AutoHide = false,
            Duration = 0,
            FadeDuration = 1
        };

        Assert.That(ToastViewModel.GetAutoHideStartMode(config), Is.EqualTo(ToastViewModel.ToastAutoHideStartMode.None));
    }

    [Test]
    public void BuildMarkdownImage_UsesMarkdownImageSyntax_WithEscapedAltText()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/capture.png", "Latest [Capture]");

        Assert.That(markdown, Is.EqualTo("![Latest \\[Capture\\]](https://example.com/capture.png)"));
    }

    [Test]
    public void BuildMarkdownImage_FallsBackToGenericAltText_WhenBlank()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/capture.png", " ");

        Assert.That(markdown, Is.EqualTo("![Image](https://example.com/capture.png)"));
    }

    [Test]
    public void BuildMarkdownImage_WrapsUrlsWithSpacesOrParentheses()
    {
        var markdown = ToastViewModel.BuildMarkdownImage("https://example.com/screenshots/file (1).png", "Capture");

        Assert.That(markdown, Is.EqualTo("![Capture](<https://example.com/screenshots/file (1).png>)"));
    }
}
