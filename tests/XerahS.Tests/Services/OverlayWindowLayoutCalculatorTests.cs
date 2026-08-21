using NUnit.Framework;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;

namespace XerahS.Tests.Services;

[TestFixture]
public class OverlayWindowLayoutCalculatorTests
{
    [Test]
    public void Calculate_WindowsMonitor_UsesPhysicalOriginAndLogicalSize()
    {
        var monitor = new MonitorInfo(
            DeviceName: @"\\.\DISPLAY1",
            PhysicalBounds: new PixelRect(100, 200, 2560, 1440),
            WorkArea: new PixelRect(100, 200, 2560, 1400),
            ScaleFactor: 1.25,
            IsPrimary: true);

        var layout = OverlayWindowLayoutCalculator.Calculate(
            monitor,
            isWindows: true,
            isLinux: false,
            isAvaloniaWayland: false);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Position, Is.EqualTo(new PixelPoint(100, 200)));
            Assert.That(layout.Width, Is.EqualTo(2048).Within(0.001));
            Assert.That(layout.Height, Is.EqualTo(1152).Within(0.001));
        });
    }

    [Test]
    public void Calculate_X11Monitor_UsesPhysicalOriginAndOverlaySize()
    {
        var monitor = new MonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new PixelRect(3840, 0, 3840, 2160),
            WorkArea: new PixelRect(3840, 0, 3840, 2112),
            ScaleFactor: 2.0,
            IsPrimary: false,
            OverlayBoundsOverride: new PixelRect(1920, 0, 1920, 1080),
            OverlayWorkAreaOverride: new PixelRect(1920, 0, 1920, 1056));

        var layout = OverlayWindowLayoutCalculator.Calculate(
            monitor,
            isWindows: false,
            isLinux: true,
            isAvaloniaWayland: false);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Position, Is.EqualTo(new PixelPoint(3840, 0)));
            Assert.That(layout.Width, Is.EqualTo(1920).Within(0.001));
            Assert.That(layout.Height, Is.EqualTo(1080).Within(0.001));
        });
    }

    [Test]
    public void Calculate_WaylandMonitor_UsesOverlayOriginAndOverlaySize()
    {
        var monitor = new MonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new PixelRect(0, 0, 1920, 1080),
            WorkArea: new PixelRect(0, 0, 1920, 1040),
            ScaleFactor: 1.0,
            IsPrimary: true,
            OverlayBoundsOverride: new PixelRect(-1920, 0, 1920, 1080),
            OverlayWorkAreaOverride: new PixelRect(-1920, 0, 1920, 1040));

        var layout = OverlayWindowLayoutCalculator.Calculate(
            monitor,
            isWindows: false,
            isLinux: true,
            isAvaloniaWayland: true);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Position, Is.EqualTo(new PixelPoint(-1920, 0)));
            Assert.That(layout.Width, Is.EqualTo(1920).Within(0.001));
            Assert.That(layout.Height, Is.EqualTo(1080).Within(0.001));
        });
    }

    [Test]
    public void Calculate_MacOSMonitor_UsesPhysicalOriginAndLogicalSize()
    {
        var monitor = new MonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new PixelRect(-1728, 0, 3456, 2234),
            WorkArea: new PixelRect(-1728, 25, 3456, 2170),
            ScaleFactor: 2.0,
            IsPrimary: true);

        var layout = OverlayWindowLayoutCalculator.Calculate(
            monitor,
            isWindows: false,
            isLinux: false,
            isAvaloniaWayland: false);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Position, Is.EqualTo(new PixelPoint(-1728, 0)));
            Assert.That(layout.Width, Is.EqualTo(1728).Within(0.001));
            Assert.That(layout.Height, Is.EqualTo(1117).Within(0.001));
        });
    }
}
