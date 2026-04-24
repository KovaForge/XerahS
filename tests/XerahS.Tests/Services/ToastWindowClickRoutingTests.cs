using Avalonia.Input;
using NUnit.Framework;
using Point = global::Avalonia.Point;
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
