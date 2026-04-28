using System.Drawing;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Core.Tasks.Pipeline;

namespace XerahS.Tests.Tasks;

[TestFixture]
public class CaptureStageConfiguredRegionTests
{
    [Test]
    public void TryCreateConfiguredCaptureRect_AcceptsValidRegion()
    {
        bool result = CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, 20, 30, 40), out SKRect captureRect);

        Assert.That(result, Is.True);
        Assert.That(captureRect.Left, Is.EqualTo(10));
        Assert.That(captureRect.Top, Is.EqualTo(20));
        Assert.That(captureRect.Right, Is.EqualTo(40));
        Assert.That(captureRect.Bottom, Is.EqualTo(60));
    }

    [Test]
    public void TryCreateConfiguredCaptureRect_RejectsNonPositiveDimensions()
    {
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, 20, 0, 40), out _), Is.False);
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, 20, 30, 0), out _), Is.False);
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, 20, -30, 40), out _), Is.False);
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, 20, 30, -40), out _), Is.False);
    }

    [Test]
    public void TryCreateConfiguredCaptureRect_RejectsOverflowingRightOrBottomEdges()
    {
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(int.MaxValue, 20, 1, 40), out _), Is.False);
        Assert.That(CaptureStage.TryCreateConfiguredCaptureRect(new Rectangle(10, int.MaxValue, 30, 1), out _), Is.False);
    }
}
