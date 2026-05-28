#nullable enable

using System;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture, NonParallelizable]
public class HSBTests
{
    [Test]
    public void Equals_DifferentAlpha_SameHSB_ReturnsFalse()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 10);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 20);

        Assert.That(hsb1, Is.Not.EqualTo(hsb2));
    }

    [Test]
    public void Equals_SameAlpha_SameHSB_ReturnsTrue()
    {
        var hsb1 = HSB.TestAccessor.Create(0.5, 0.6, 0.7, 255);
        var hsb2 = HSB.TestAccessor.Create(0.5, 0.6, 0.7, 255);

        Assert.That(hsb1, Is.EqualTo(hsb2));
    }

    [Test]
    public void GetHashCode_SameHSBDifferentAlpha_ReturnsDifferentHashCodes()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 10);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 20);

        Assert.That(hsb1.GetHashCode(), Is.Not.EqualTo(hsb2.GetHashCode()));
    }

    [Test]
    public void GetHashCode_SameHSBSameAlpha_ReturnsSameHashCodes()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 255);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 255);

        Assert.That(hsb1.GetHashCode(), Is.EqualTo(hsb2.GetHashCode()));
    }

    [Test]
    public void OperatorEquality_DifferentAlpha_SameHSB_ReturnsFalse()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 100);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 200);

        // ReSharper disable EqualExpressionComparison
        Assert.That(hsb1 == hsb2, Is.False);
        Assert.That(hsb1 != hsb2, Is.True);
        // ReSharper restore EqualExpressionComparison
    }

    [Test]
    public void Equals_DifferentHue_ReturnsFalse()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 255);
        var hsb2 = HSB.TestAccessor.Create(0.5, 0.2, 0.3, 255);

        Assert.That(hsb1, Is.Not.EqualTo(hsb2));
    }

    [Test]
    public void Equals_DifferentSaturation_ReturnsFalse()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 255);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.8, 0.3, 255);

        Assert.That(hsb1, Is.Not.EqualTo(hsb2));
    }

    [Test]
    public void Equals_DifferentBrightness_ReturnsFalse()
    {
        var hsb1 = HSB.TestAccessor.Create(0.1, 0.2, 0.3, 255);
        var hsb2 = HSB.TestAccessor.Create(0.1, 0.2, 0.9, 255);

        Assert.That(hsb1, Is.Not.EqualTo(hsb2));
    }
}