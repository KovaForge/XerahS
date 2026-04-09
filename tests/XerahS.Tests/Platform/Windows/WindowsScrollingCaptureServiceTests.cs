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

using NUnit.Framework;
using XerahS.Platform.Windows;

namespace XerahS.Tests.Platform.Windows;

[TestFixture]
public class WindowsScrollingCaptureServiceTests
{
    [Test]
    public void ResolveScrollTarget_PrefersLargestVisibleScrollablePaneOverScrollbarControl()
    {
        IntPtr parentWindow = new(123);
        IntPtr scrollbarHandle = new(456);
        IntPtr contentHandle = new(789);

        WindowsScrollingCaptureService.ScrollTargetCandidate[] candidates =
        [
            new(scrollbarHandle, HasVerticalScrollStyle: true, IsVisible: true, ClientWidth: 17, ClientHeight: 600, ClassName: "ScrollBar"),
            new(contentHandle, HasVerticalScrollStyle: true, IsVisible: true, ClientWidth: 900, ClientHeight: 700, ClassName: "Internet Explorer_Server")
        ];

        IntPtr result = WindowsScrollingCaptureService.ResolveScrollTarget(parentWindow, candidates);

        Assert.That(result, Is.EqualTo(contentHandle));
    }

    [Test]
    public void ResolveScrollTarget_FallsBackToParentWhenChildNotFound()
    {
        IntPtr parentWindow = new(123);

        IntPtr result = WindowsScrollingCaptureService.ResolveScrollTarget(parentWindow, []);

        Assert.That(result, Is.EqualTo(parentWindow));
    }

    [Test]
    public void ResolveScrollTarget_FallsBackToScrollbarControlWhenItIsOnlyScrollableCandidate()
    {
        IntPtr parentWindow = new(123);
        IntPtr scrollbarHandle = new(456);

        WindowsScrollingCaptureService.ScrollTargetCandidate[] candidates =
        [
            new(scrollbarHandle, HasVerticalScrollStyle: true, IsVisible: true, ClientWidth: 17, ClientHeight: 600, ClassName: "ScrollBar")
        ];

        IntPtr result = WindowsScrollingCaptureService.ResolveScrollTarget(parentWindow, candidates);

        Assert.That(result, Is.EqualTo(scrollbarHandle));
    }
}
