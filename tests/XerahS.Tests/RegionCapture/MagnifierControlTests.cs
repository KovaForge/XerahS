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

using Avalonia;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using SkiaSharp;
using XerahS.RegionCapture;
using XerahS.RegionCapture.UI;
using CaptureMonitorInfo = XerahS.RegionCapture.Models.MonitorInfo;
using CapturePixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using CapturePixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
[NonParallelizable]
public class MagnifierControlTests
{
    [Test]
    public void NormalizePixelCount_KeepsOddValuesAndClampsToDisplaySize()
    {
        Assert.That(MagnifierLayout.NormalizePixelCount(15, 1), Is.EqualTo(15));
        Assert.That(MagnifierLayout.NormalizePixelCount(14, 1), Is.EqualTo(15));
        Assert.That(MagnifierLayout.NormalizePixelCount(2, 1), Is.EqualTo(3));
        Assert.That(MagnifierLayout.NormalizePixelCount(99, 1), Is.EqualTo(25));
        Assert.That(MagnifierLayout.NormalizePixelCount(99, 2), Is.EqualTo(35));
    }

    [Test]
    public void PixelCountFromWheel_ScrollUpZoomsIn()
    {
        Assert.That(MagnifierLayout.PixelCountFromWheel(15, 1, 1), Is.EqualTo(13));
        Assert.That(MagnifierLayout.PixelCountFromWheel(15, -1, 1), Is.EqualTo(17));
        Assert.That(MagnifierLayout.PixelCountFromWheel(3, 1, 1), Is.EqualTo(3));
    }

    [Test]
    public void CalculatePosition_FlipsAwayFromPointerNearBottomRight()
    {
        var position = MagnifierControl.CalculatePosition(
            new Point(900, 700),
            new Size(1000, 800),
            new Size(152, 190),
            renderScale: 1);

        Assert.That(position.X, Is.EqualTo(900 - 152 - MagnifierLayout.PointerOffset));
        Assert.That(position.Y, Is.EqualTo(700 - 190 - MagnifierLayout.PointerOffset));
    }

    [Test]
    public void CalculatePosition_SnapsToPhysicalPixels()
    {
        var position = MagnifierControl.CalculatePosition(
            new Point(10.4, 12.6),
            new Size(1000, 800),
            new Size(152, 190),
            renderScale: 2);

        Assert.That(position.X, Is.EqualTo(28.5));
        Assert.That(position.Y, Is.EqualTo(30.5));
    }

    [AvaloniaTest]
    public void RegionCaptureControl_AppliesSquareShapeAndPixelCount()
    {
        var monitor = new CaptureMonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new CapturePixelRect(0, 0, 1920, 1080),
            WorkArea: new CapturePixelRect(0, 0, 1920, 1040),
            ScaleFactor: 1.0,
            IsPrimary: true);

        var control = new RegionCaptureControl(monitor, new RegionCaptureOptions
        {
            UseSquareMagnifier = true,
            MagnifierPixelCount = 21,
            EnableMagnifier = true,
            ShowInfo = true
        });

        Assert.That(control.MagnifierUsesSquareForTests, Is.True);
        Assert.That(control.MagnifierPixelCountForTests, Is.EqualTo(21));
        Assert.That(control.MagnifierForTests.UsesSquareShapeForTests, Is.True);
        Assert.That(control.MagnifierForTests.PixelCountForTests, Is.EqualTo(21));
        Assert.That(control.MagnifierForTests.PixelGridForTests.PixelCount, Is.EqualTo(21));
    }

    [AvaloniaTest]
    public void TryAdjustMagnifierFromWheel_RevealsMagnifierAndChangesPixelCount()
    {
        var monitor = new CaptureMonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new CapturePixelRect(0, 0, 1920, 1080),
            WorkArea: new CapturePixelRect(0, 0, 1920, 1040),
            ScaleFactor: 1.0,
            IsPrimary: true);

        var control = new RegionCaptureControl(monitor, new RegionCaptureOptions
        {
            EnableMagnifier = false,
            MagnifierPixelCount = 15,
            ShowInfo = true
        });

        Assert.That(control.TryAdjustMagnifierFromWheel(1), Is.True);
        Assert.That(control.MagnifierPixelCountForTests, Is.EqualTo(13));
        Assert.That(control.MagnifierForTests.MagnifierViewVisibleForTests, Is.True);
        Assert.That(control.MagnifierForTests.PixelCountForTests, Is.EqualTo(13));
    }

    [AvaloniaTest]
    public void UpdateFromBackground_SamplesCenterPixelAndWritesInfo()
    {
        using var bitmap = new SKBitmap(3, 3, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(0, 0, 0));
        bitmap.SetPixel(1, 1, new SKColor(10, 20, 30, 255));

        var magnifier = new MagnifierControl();
        magnifier.SetPixelCount(3);
        magnifier.UpdateFromBackground(
            new CapturePixelPoint(1, 1),
            bitmap,
            new CapturePixelRect(0, 0, 3, 3));

        Assert.That(magnifier.CenterPixelColorForTests, Is.EqualTo(global::Avalonia.Media.Color.FromRgb(10, 20, 30)));
        Assert.That(magnifier.InfoTextForTests, Does.Contain("X: 1 Y: 1"));
        Assert.That(magnifier.InfoTextForTests, Does.Contain("#0A141E"));
    }
}
