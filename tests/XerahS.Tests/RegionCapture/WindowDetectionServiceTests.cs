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
}
