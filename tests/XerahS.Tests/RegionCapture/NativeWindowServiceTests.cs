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
using XerahS.RegionCapture.Platform;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class NativeWindowServiceTests
{
    private const nint VisibleStyle = (nint)0x10000000u;
    private const nint DisabledStyle = (nint)0x08000000u;
    private const nint NoActivateExStyle = (nint)0x08000000u;
    private const nint AppWindowExStyle = (nint)0x00040000u;

    [Test]
    public void ShouldIncludeWindowForCapture_RejectsCloakedSettingsHost()
    {
        bool result = NativeWindowCaptureFilter.ShouldIncludeWindowForCapture(
            isVisible: true,
            isMinimized: false,
            isCloaked: true,
            title: "Settings",
            className: "ApplicationFrameWindow",
            style: VisibleStyle,
            exStyle: 0);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldIncludeWindowForCapture_RejectsNoActivateInputSurface()
    {
        bool result = NativeWindowCaptureFilter.ShouldIncludeWindowForCapture(
            isVisible: true,
            isMinimized: false,
            isCloaked: false,
            title: "Windows Input Experience",
            className: "TextInputHostWindow",
            style: VisibleStyle,
            exStyle: NoActivateExStyle);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldIncludeWindowForCapture_RejectsIgnoredSystemClass()
    {
        bool result = NativeWindowCaptureFilter.ShouldIncludeWindowForCapture(
            isVisible: true,
            isMinimized: false,
            isCloaked: false,
            title: "Windows Input Experience",
            className: "Windows.UI.Core.CoreWindow",
            style: VisibleStyle,
            exStyle: 0);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldIncludeWindowForCapture_RejectsDisabledWindow()
    {
        bool result = NativeWindowCaptureFilter.ShouldIncludeWindowForCapture(
            isVisible: true,
            isMinimized: false,
            isCloaked: false,
            title: "Disabled",
            className: "NormalWindow",
            style: VisibleStyle | DisabledStyle,
            exStyle: AppWindowExStyle);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldIncludeWindowForCapture_AllowsNormalAppWindow()
    {
        bool result = NativeWindowCaptureFilter.ShouldIncludeWindowForCapture(
            isVisible: true,
            isMinimized: false,
            isCloaked: false,
            title: "ShareX",
            className: "ApplicationFrameWindow",
            style: VisibleStyle,
            exStyle: AppWindowExStyle);

        Assert.That(result, Is.True);
    }
}
