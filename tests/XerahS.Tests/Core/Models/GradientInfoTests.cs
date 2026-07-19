#nullable enable
// LinearGradientMode / System.Drawing.Color are Windows-annotated APIs; the
// production GradientInfo type already lives under the same platform surface
// (TaskSettingsOptions.cs disables CA1416). Mirror that here so the regression
// suite builds under TreatWarningsAsErrors on non-Windows hosts.
#pragma warning disable CA1416 // Validate platform compatibility

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using NUnit.Framework;
using XerahS.Core;

namespace XerahS.Tests.Core.Models;

[TestFixture]
public sealed class GradientInfoTests
{
    [Test]
    public void Ctor_ZeroColors_LeavesEmptyStopList()
    {
        var gradient = new GradientInfo(LinearGradientMode.Vertical, Array.Empty<Color>());

        Assert.That(gradient.Colors, Is.Empty);
    }

    [Test]
    public void Ctor_SingleColor_ProducesFiniteStopAtZero()
    {
        // Regression: previously divided by (Length - 1) == 0 and produced
        // Infinity/NaN stop locations for solid-color gradients.
        var gradient = new GradientInfo(LinearGradientMode.Vertical, Color.Red);

        Assert.That(gradient.Colors, Has.Count.EqualTo(1));
        Assert.That(float.IsFinite(gradient.Colors[0].Location), Is.True);
        Assert.That(gradient.Colors[0].Location, Is.EqualTo(0).Within(0.001f));
        Assert.That(gradient.Colors[0].Location, Is.InRange(0f, 100f));
        Assert.That(gradient.Colors[0].Color.ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
    }

    [Test]
    public void Ctor_TwoColors_PlacesStopsAtZeroAndOneHundred()
    {
        var gradient = new GradientInfo(LinearGradientMode.Horizontal, Color.Red, Color.Blue);

        Assert.That(gradient.Colors, Has.Count.EqualTo(2));
        Assert.That(gradient.Colors[0].Location, Is.EqualTo(0).Within(0.001f));
        Assert.That(gradient.Colors[1].Location, Is.EqualTo(100).Within(0.001f));
        Assert.That(float.IsFinite(gradient.Colors[0].Location), Is.True);
        Assert.That(float.IsFinite(gradient.Colors[1].Location), Is.True);
    }

    [Test]
    public void Ctor_ThreeColors_PlacesStopsEvenlyWithinZeroToOneHundred()
    {
        var gradient = new GradientInfo(
            LinearGradientMode.ForwardDiagonal,
            Color.Red,
            Color.Green,
            Color.Blue);

        Assert.That(gradient.Colors, Has.Count.EqualTo(3));
        Assert.That(gradient.Colors[0].Location, Is.EqualTo(0).Within(0.001f));
        Assert.That(gradient.Colors[1].Location, Is.EqualTo(50).Within(0.001f));
        Assert.That(gradient.Colors[2].Location, Is.EqualTo(100).Within(0.001f));

        foreach (var stop in gradient.Colors)
        {
            Assert.That(float.IsFinite(stop.Location), Is.True);
            Assert.That(stop.Location, Is.InRange(0f, 100f));
        }
    }
}
