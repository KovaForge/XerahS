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
using XerahS.Platform.MacOS;

namespace XerahS.Tests.Platform.MacOS;

[TestFixture]
public class MacOSWindowServiceTests
{
    [Test]
    public void TryParseFrontWindowInfo_ParsesDistinctWindowTitleAndAppName()
    {
        string payload = string.Join(
            MacOSWindowService.FrontWindowInfoSeparator,
            "Document 1",
            "Preview",
            "10",
            "20",
            "800",
            "600",
            "1234");

        bool result = MacOSWindowService.TryParseFrontWindowInfo(payload, out var windowInfo);

        Assert.That(result, Is.True);
        Assert.That(windowInfo.WindowTitle, Is.EqualTo("Document 1"));
        Assert.That(windowInfo.AppName, Is.EqualTo("Preview"));
        Assert.That(windowInfo.Bounds, Is.EqualTo(new Rectangle(10, 20, 800, 600)));
        Assert.That(windowInfo.ProcessId, Is.EqualTo(1234u));
    }

    [Test]
    public void TryParseFrontWindowInfo_FallsBackToAppName_WhenWindowTitleMissing()
    {
        string payload = string.Join(
            MacOSWindowService.FrontWindowInfoSeparator,
            string.Empty,
            "Finder",
            "0",
            "0",
            "1440",
            "900",
            "77");

        bool result = MacOSWindowService.TryParseFrontWindowInfo(payload, out var windowInfo);

        Assert.That(result, Is.True);
        Assert.That(windowInfo.WindowTitle, Is.EqualTo("Finder"));
        Assert.That(windowInfo.AppName, Is.EqualTo("Finder"));
    }

    [Test]
    public void TryParseFrontWindowInfo_RejectsMalformedPayload()
    {
        bool result = MacOSWindowService.TryParseFrontWindowInfo("broken", out _);

        Assert.That(result, Is.False);
    }

    [TestCase(0, 600)]
    [TestCase(800, 0)]
    [TestCase(-1, 600)]
    [TestCase(800, -1)]
    public void TryParseFrontWindowInfo_RejectsInvalidWindowSize(int width, int height)
    {
        string payload = string.Join(
            MacOSWindowService.FrontWindowInfoSeparator,
            "Document 1",
            "Preview",
            "10",
            "20",
            width.ToString(),
            height.ToString(),
            "1234");

        bool result = MacOSWindowService.TryParseFrontWindowInfo(payload, out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryParseFrontWindowInfo_PreservesTitleWhitespace()
    {
        string payload = string.Join(
            MacOSWindowService.FrontWindowInfoSeparator,
            " Document 1 ",
            "Preview",
            "10",
            "20",
            "800",
            "600",
            "1234") + Environment.NewLine;

        bool result = MacOSWindowService.TryParseFrontWindowInfo(payload, out var windowInfo);

        Assert.That(result, Is.True);
        Assert.That(windowInfo.WindowTitle, Is.EqualTo(" Document 1 "));
    }

    [Test]
    public void IsSearchMatch_MatchesWindowTitleOrAppName()
    {
        var windowInfo = new MacOSWindowService.FrontWindowInfo(
            "Preview",
            "Quarterly report.pdf",
            new Rectangle(10, 20, 800, 600),
            1234);

        Assert.Multiple(() =>
        {
            Assert.That(MacOSWindowService.IsSearchMatch(windowInfo, "report"), Is.True);
            Assert.That(MacOSWindowService.IsSearchMatch(windowInfo, "preview"), Is.True);
            Assert.That(MacOSWindowService.IsSearchMatch(windowInfo, "terminal"), Is.False);
        });
    }

    [Test]
    public void FrontWindowHandle_IsNonZeroSentinel()
    {
        Assert.That(MacOSWindowService.FrontWindowHandle, Is.Not.EqualTo(IntPtr.Zero));
    }
}
