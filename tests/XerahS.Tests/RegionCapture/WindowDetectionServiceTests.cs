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

using System.Drawing;
using NUnit.Framework;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.Platform.Abstractions;
using PlatformWindowInfo = XerahS.Platform.Abstractions.WindowInfo;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class WindowDetectionServiceTests
{
    [Test]
    public void ConvertPlatformWindows_FiltersNonSelectableWindows_AndProjectsBounds()
    {
        WindowDetectionService.ExcludeHandle((nint)22);

        try
        {
            PlatformWindowInfo[] windows =
            [
                new()
                {
                    Handle = (nint)11,
                    Title = "Terminal",
                    ClassName = "org.gnome.Terminal",
                    Bounds = new Rectangle(10, 20, 300, 200),
                    IsVisible = true,
                    IsMinimized = false
                },
                new()
                {
                    Handle = (nint)22,
                    Title = "Overlay",
                    Bounds = new Rectangle(0, 0, 1920, 1080),
                    IsVisible = true,
                    IsMinimized = false
                },
                new()
                {
                    Handle = (nint)33,
                    Title = "",
                    Bounds = new Rectangle(0, 0, 100, 100),
                    IsVisible = true,
                    IsMinimized = false
                },
                new()
                {
                    Handle = (nint)44,
                    Title = "Hidden",
                    Bounds = new Rectangle(0, 0, 100, 100),
                    IsVisible = false,
                    IsMinimized = false
                },
                new()
                {
                    Handle = (nint)55,
                    Title = "Minimized",
                    Bounds = new Rectangle(0, 0, 100, 100),
                    IsVisible = true,
                    IsMinimized = true
                }
            ];

            IReadOnlyList<XerahS.RegionCapture.Models.WindowInfo> result =
                WindowDetectionService.ConvertPlatformWindows(windows);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Handle, Is.EqualTo((nint)11));
            Assert.That(result[0].Bounds, Is.EqualTo(new PixelRect(10, 20, 300, 200)));
            Assert.That(result[0].VisualBounds, Is.EqualTo(new PixelRect(10, 20, 300, 200)));
            Assert.That(result[0].ZOrder, Is.EqualTo(0));
        }
        finally
        {
            WindowDetectionService.RemoveExcludedHandle((nint)22);
        }
    }

    [Test]
    public void GetWindowAtPoint_ReturnsFirstMatchingWindow_FromInjectedTopmostOrder()
    {
        IReadOnlyList<XerahS.RegionCapture.Models.WindowInfo> windows =
        [
            new(
                Handle: (nint)101,
                Title: "Top",
                ClassName: "TopClass",
                Bounds: new PixelRect(0, 0, 200, 200),
                VisualBounds: new PixelRect(0, 0, 200, 200),
                IsMinimized: false,
                ZOrder: 0),
            new(
                Handle: (nint)202,
                Title: "Bottom",
                ClassName: "BottomClass",
                Bounds: new PixelRect(0, 0, 250, 250),
                VisualBounds: new PixelRect(0, 0, 250, 250),
                IsMinimized: false,
                ZOrder: 1)
        ];

        var service = new WindowDetectionService(() => windows);
        service.RefreshWindows();

        XerahS.RegionCapture.Models.WindowInfo? hoveredWindow =
            service.GetWindowAtPoint(new PixelPoint(50, 50));

        Assert.That(hoveredWindow, Is.Not.Null);
        Assert.That(hoveredWindow!.Handle, Is.EqualTo((nint)101));
    }

    [Test]
    public void WindowQueries_SkipExcludedHandlesEvenWhenWindowListWasAlreadyRefreshed()
    {
        IReadOnlyList<XerahS.RegionCapture.Models.WindowInfo> windows =
        [
            new(
                Handle: (nint)404,
                Title: "Overlay",
                ClassName: "OverlayClass",
                Bounds: new PixelRect(0, 0, 200, 200),
                VisualBounds: new PixelRect(0, 0, 200, 200),
                IsMinimized: false,
                ZOrder: 0)
        ];

        var service = new WindowDetectionService(() => windows);
        service.RefreshWindows();
        WindowDetectionService.ExcludeHandle((nint)404);

        try
        {
            Assert.That(service.GetWindowAtPoint(new PixelPoint(50, 50)), Is.Null);
            Assert.That(service.GetWindowsInRegion(new PixelRect(25, 25, 50, 50)), Is.Empty);
            Assert.That(service.GetWindowsNearPoint(new PixelPoint(50, 50), 10), Is.Empty);

            SnapEdges edges = service.GetSnapEdges(new PixelPoint(0, 0), 10);
            Assert.That(edges.HorizontalEdges, Is.Empty);
            Assert.That(edges.VerticalEdges, Is.Empty);
        }
        finally
        {
            WindowDetectionService.RemoveExcludedHandle((nint)404);
        }
    }

    [Test]
    public void GetWindowAtPoint_DoesNotReturnCachedDirectWindowAfterHandleBecomesExcluded()
    {
        var directWindow = new XerahS.RegionCapture.Models.WindowInfo(
            Handle: (nint)505,
            Title: "Direct overlay",
            ClassName: "OverlayClass",
            Bounds: new PixelRect(0, 0, 200, 200),
            VisualBounds: new PixelRect(0, 0, 200, 200),
            IsMinimized: false,
            ZOrder: 0);

        int directQueryCount = 0;
        var service = new WindowDetectionService(
            () => [],
            _ => directQueryCount++ == 0
                ? new WindowPointQueryResult(Handled: true, Window: directWindow)
                : default);

        Assert.That(service.GetWindowAtPoint(new PixelPoint(50, 50))?.Handle, Is.EqualTo((nint)505));

        WindowDetectionService.ExcludeHandle((nint)505);

        try
        {
            Assert.That(service.GetWindowAtPoint(new PixelPoint(60, 60)), Is.Null);
            Assert.That(directQueryCount, Is.EqualTo(2));
        }
        finally
        {
            WindowDetectionService.RemoveExcludedHandle((nint)505);
        }
    }

    [Test]
    public void GetWindowAtPoint_PrefersDirectWaylandProbe_WhenHandled()
    {
        IReadOnlyList<XerahS.RegionCapture.Models.WindowInfo> windows =
        [
            new(
                Handle: (nint)101,
                Title: "Enumerated",
                ClassName: "EnumeratedClass",
                Bounds: new PixelRect(0, 0, 300, 200),
                VisualBounds: new PixelRect(0, 0, 300, 200),
                IsMinimized: false,
                ZOrder: 0)
        ];

        var directWindow = new XerahS.RegionCapture.Models.WindowInfo(
            Handle: (nint)202,
            Title: "Direct",
            ClassName: "DirectClass",
            Bounds: new PixelRect(10, 20, 150, 120),
            VisualBounds: new PixelRect(10, 20, 150, 120),
            IsMinimized: false,
            ZOrder: 0);

        var service = new WindowDetectionService(
            () => windows,
            _ => new WindowPointQueryResult(Handled: true, Window: directWindow));

        XerahS.RegionCapture.Models.WindowInfo? hoveredWindow =
            service.GetWindowAtPoint(new PixelPoint(25, 25));

        Assert.That(hoveredWindow, Is.Not.Null);
        Assert.That(hoveredWindow!.Handle, Is.EqualTo((nint)202));
    }

    [TestCase(false, true, null, false, (int)WindowPreselectionSupportLevel.Full, null)]
    [TestCase(true, true, "GNOME", false, (int)WindowPreselectionSupportLevel.Partial, "Wayland session: native window snapping helper is unavailable; only X11/XWayland windows can be snapped.")]
    [TestCase(true, false, "GNOME", false, (int)WindowPreselectionSupportLevel.Unsupported, "Wayland session: native window snapping helper is unavailable on this compositor.")]
    [TestCase(true, false, "HYPRLAND", true, (int)WindowPreselectionSupportLevel.Full, null)]
    [TestCase(true, true, "SWAY", true, (int)WindowPreselectionSupportLevel.Full, null)]
    public void GetLinuxWindowPreselectionCapability_ReturnsExpectedSupportLevel(
        bool isWaylandSession,
        bool hasX11Display,
        string? compositor,
        bool helperAvailable,
        int expectedLevel,
        string? expectedMessage)
    {
        var capability = WindowDetectionService.GetLinuxWindowPreselectionCapability(
            isWaylandSession,
            hasX11Display,
            compositor,
            _ => helperAvailable);

        Assert.That((int)capability.Level, Is.EqualTo(expectedLevel));
        Assert.That(capability.UserMessage, Is.EqualTo(expectedMessage));
        Assert.That(capability.IsEnabled, Is.EqualTo(expectedLevel != (int)WindowPreselectionSupportLevel.Unsupported));
    }

    [Test]
    public void GetLinuxWindowPreselectionCapability_PrefersDirectCapabilityWhenEnabled()
    {
        var capability = WindowDetectionService.GetLinuxWindowPreselectionCapability(
            isWaylandSession: true,
            hasX11Display: false,
            compositor: "GNOME",
            _ => false,
            new WindowPointQueryCapability(WindowPointQuerySupportLevel.Full, null));

        Assert.That(capability.Level, Is.EqualTo(WindowPreselectionSupportLevel.Full));
        Assert.That(capability.UserMessage, Is.Null);
    }

    [Test]
    public void GetLinuxWindowPreselectionCapability_MapsPartialDirectCapability()
    {
        var capability = WindowDetectionService.GetLinuxWindowPreselectionCapability(
            isWaylandSession: true,
            hasX11Display: false,
            compositor: "WAYLAND",
            _ => false,
            new WindowPointQueryCapability(
                WindowPointQuerySupportLevel.Partial,
                "Wayland session: helper only exposes the active workspace."));

        Assert.That(capability.Level, Is.EqualTo(WindowPreselectionSupportLevel.Partial));
        Assert.That(capability.UserMessage, Is.EqualTo("Wayland session: helper only exposes the active workspace."));
    }

    [Test]
    public void TryConvertPhysicalToLogicalPoint_UsesMonitorScaleAndOverlayBounds()
    {
        IReadOnlyList<MonitorInfo> monitors =
        [
            new MonitorInfo(
                "Display 1",
                new PixelRect(0, 0, 200, 100),
                new PixelRect(0, 0, 200, 100),
                2.0,
                true,
                OverlayBoundsOverride: new PixelRect(0, 0, 100, 50))
        ];

        bool converted = WindowDetectionService.TryConvertPhysicalToLogicalPoint(
            new PixelPoint(50, 40),
            monitors,
            out Point logicalPoint);

        Assert.That(converted, Is.True);
        Assert.That(logicalPoint, Is.EqualTo(new Point(25, 20)));
    }

    [Test]
    public void ConvertLogicalPlatformWindow_ConvertsBoundsToPhysicalAndFiltersOverlay()
    {
        IReadOnlyList<MonitorInfo> monitors =
        [
            new MonitorInfo(
                "Display 1",
                new PixelRect(0, 0, 300, 200),
                new PixelRect(0, 0, 300, 200),
                1.5,
                true,
                OverlayBoundsOverride: new PixelRect(0, 0, 200, 133.33333333333334))
        ];

        var overlayWindow = new PlatformWindowInfo
        {
            Handle = (nint)12,
            Title = PlatformWindowTitles.RegionCaptureOverlay,
            Bounds = new Rectangle(0, 0, 100, 100),
            IsVisible = true
        };

        var normalWindow = new PlatformWindowInfo
        {
            Handle = (nint)34,
            Title = "Notes",
            ClassName = "org.example.Notes",
            Bounds = new Rectangle(10, 20, 80, 40),
            IsVisible = true
        };

        Assert.That(
            WindowDetectionService.ConvertLogicalPlatformWindow(overlayWindow, monitors),
            Is.Null);

        var converted = WindowDetectionService.ConvertLogicalPlatformWindow(normalWindow, monitors);

        Assert.That(converted, Is.Not.Null);
        Assert.That(converted!.Handle, Is.EqualTo((nint)34));
        Assert.That(converted.Bounds, Is.EqualTo(new PixelRect(15, 30, 120, 60)));
        Assert.That(converted.ClassName, Is.EqualTo("org.example.Notes"));
    }

    [Test]
    public void ConvertLogicalPlatformWindow_FiltersHiddenAndMinimizedWindows()
    {
        IReadOnlyList<MonitorInfo> monitors =
        [
            new MonitorInfo(
                "Display 1",
                new PixelRect(0, 0, 200, 100),
                new PixelRect(0, 0, 200, 100),
                1.0,
                true)
        ];

        var hiddenWindow = new PlatformWindowInfo
        {
            Handle = (nint)91,
            Title = "Hidden helper",
            ClassName = "org.example.Hidden",
            Bounds = new Rectangle(0, 0, 100, 50),
            IsVisible = false,
            IsMinimized = false
        };

        var minimizedWindow = new PlatformWindowInfo
        {
            Handle = (nint)92,
            Title = "Minimized app",
            ClassName = "org.example.Minimized",
            Bounds = new Rectangle(0, 0, 100, 50),
            IsVisible = true,
            IsMinimized = true
        };

        Assert.That(WindowDetectionService.ConvertLogicalPlatformWindow(hiddenWindow, monitors), Is.Null);
        Assert.That(WindowDetectionService.ConvertLogicalPlatformWindow(minimizedWindow, monitors), Is.Null);
    }

    [Test]
    public void ConvertLogicalPlatformWindow_FiltersExcludedHandles()
    {
        IReadOnlyList<MonitorInfo> monitors =
        [
            new MonitorInfo(
                "Display 1",
                new PixelRect(0, 0, 200, 100),
                new PixelRect(0, 0, 200, 100),
                1.0,
                true)
        ];

        var overlayWindow = new PlatformWindowInfo
        {
            Handle = (nint)88,
            Title = "Transient helper window",
            ClassName = "XerahS.Overlay",
            Bounds = new Rectangle(0, 0, 100, 50),
            IsVisible = true
        };

        WindowDetectionService.ExcludeHandle((nint)88);

        try
        {
            Assert.That(
                WindowDetectionService.ConvertLogicalPlatformWindow(overlayWindow, monitors),
                Is.Null);
        }
        finally
        {
            WindowDetectionService.RemoveExcludedHandle((nint)88);
        }
    }
}
