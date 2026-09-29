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

// Pure-function unit tests for the X / Twitter pre-softening planner.
// Source spec: KFIP0010 + KFIP0019 (screensnap.pro 2026-05 measurements)
// Source code: src/desktop/core/XerahS.Core/Media/PreSoftenXProfile.cs
// KFIP: docs/proposals/kfip/KFIP0020-capture-and-go-unified-gesture.md
//
// Tests 1-4 from KFIP0020 §E. Live in Media/ (not Workflows/) per
// KFIP0020 review item M3.

using NUnit.Framework;
using XerahS.Core.Media;

namespace XerahS.Tests.Media;

[TestFixture]
public class PreSoftenXProfileTests
{
    [Test]
    public void PreSoften_TallPortrait_PicksInFeed16x9_AndDownsamplesLongAxis()
    {
        // 4:5 portrait retina capture (2160x2700). aspect < 1 forces
        // InFeed16x9 (per spec, X crops 4:5 to ~16:9 in feed preview).
        // longAxis = 2700 > 1600 -> downsample. scale = 1200/2700 = 0.4444.
        // newWidth = round(2160 * 0.4444) = 960; newHeight = round(2700 * 0.4444) = 1200.
        var region = new XCapturedRegion(2160, 2700);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.InFeed16x9));
        Assert.That(plan.WillUpscale, Is.False);
        Assert.That(plan.TargetWidth, Is.EqualTo(960));
        Assert.That(plan.TargetHeight, Is.EqualTo(1200));
    }

    [Test]
    public void PreSoften_TallPortrait_AlreadySmall_KeepsSourceDims()
    {
        // 4:5 portrait at native resolution (1080x1350). longAxis = 1350 <= 1600
        // so the planner keeps source dims and tags the InFeed16x9 profile slot.
        var region = new XCapturedRegion(1080, 1350);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.InFeed16x9));
        Assert.That(plan.WillUpscale, Is.False);
        Assert.That(plan.TargetWidth, Is.EqualTo(1080));
        Assert.That(plan.TargetHeight, Is.EqualTo(1350));
    }

    [Test]
    public void PreSoften_WideBanner_21to9_PicksTwoUp2x1_AndDownsamplesLongAxis()
    {
        // 21:9 banner (2520x1080), aspect = 2.333 -> TwoUp2x1 profile.
        // longAxis 2520 > 1600 -> downsample. scale = 1200/2520 = 0.4762.
        // newWidth = round(2520 * 0.4762) = 1200; newHeight = round(1080 * 0.4762) = 514.
        var region = new XCapturedRegion(2520, 1080);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.TwoUp2x1));
        Assert.That(plan.WillUpscale, Is.False);
        Assert.That(plan.TargetWidth, Is.EqualTo(1200));
        Assert.That(plan.TargetHeight, Is.EqualTo(514));
    }

    [Test]
    public void PreSoften_TwitterCard_1200x628_ForAspectRatio1_91()
    {
        // 1.91:1 mapping -> TwitterCard. Bucket (1.85, 2.0].
        // longAxis 1910 > 1600 -> downsample. scale = 1200/1910 = 0.6283.
        // newWidth = round(1910 * 0.6283) = 1200; newHeight = round(1000 * 0.6283) = 628.
        var region = new XCapturedRegion(1910, 1000);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.TwitterCard));
        Assert.That(plan.WillUpscale, Is.False);
        Assert.That(plan.TargetWidth, Is.EqualTo(1200));
        Assert.That(plan.TargetHeight, Is.EqualTo(628));
    }

    [Test]
    public void PreSoften_Downsamples4K_DefaultsTo1200x675()
    {
        // 4K 16:9 (3840x2160). longAxis 3840 > 1600 -> downsample.
        // InFeed16x9 (1200x675). scale = 1200/3840 = 0.3125.
        // newWidth = round(3840 * 0.3125) = 1200; newHeight = round(2160 * 0.3125) = 675.
        var region = new XCapturedRegion(3840, 2160);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.InFeed16x9));
        Assert.That(plan.TargetWidth, Is.EqualTo(1200));
        Assert.That(plan.TargetHeight, Is.EqualTo(675));
        Assert.That(plan.WillUpscale, Is.False);
        double outAspect = (double)plan.TargetWidth / plan.TargetHeight;
        double inAspect = (double)region.Width / region.Height;
        Assert.That(outAspect, Is.EqualTo(inAspect).Within(XPreSoftenPlanner.AspectTolerance));
    }

    [Test]
    public void PreSoften_SourceSmallerThanTarget_WillNotUpscale()
    {
        // 800x600 capture is below all target profiles on at least one axis.
        // Per KFIP0020 §B the renderer must not upscale.
        var region = new XCapturedRegion(800, 600);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.WillUpscale, Is.True);
        Assert.That(plan.AdjustedWidth, Is.EqualTo(800));
        Assert.That(plan.AdjustedHeight, Is.EqualTo(600));
    }

    [Test]
    public void PreSoften_SourceExactlyTarget_WillNotUpscale()
    {
        // 1200x675 is exactly the InFeed16x9 dimensions. Aspect 1.778
        // lands in (1.30, 1.85] -> InFeed16x9. Source <= target on both
        // axes -> WillUpscale = true (no upsample), Adjusted dims = source.
        var region = new XCapturedRegion(1200, 675);
        var plan = XPreSoftenPlanner.Plan(region);

        Assert.That(plan.Profile, Is.EqualTo(XTargetProfile.InFeed16x9));
        Assert.That(plan.WillUpscale, Is.True);
        Assert.That(plan.AdjustedWidth, Is.EqualTo(1200));
        Assert.That(plan.AdjustedHeight, Is.EqualTo(675));
    }

    [Test]
    public void PreSoften_RejectsZeroDimensions()
    {
        var region = new XCapturedRegion(0, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => XPreSoftenPlanner.Plan(region));

        var region2 = new XCapturedRegion(100, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => XPreSoftenPlanner.Plan(region2));
    }
}
