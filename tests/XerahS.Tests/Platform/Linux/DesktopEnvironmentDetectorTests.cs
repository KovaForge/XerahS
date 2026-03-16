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
using XerahS.Platform.Linux.Capture.Detection;

namespace XerahS.Tests.Platform.Linux;

public class DesktopEnvironmentDetectorTests
{
    [TestCase("GNOME", "GNOME")]
    [TestCase("ubuntu:GNOME", "GNOME")]
    [TestCase("Budgie:GNOME", "GNOME")]
    [TestCase("Pantheon", "GNOME")]
    [TestCase("Unity", "GNOME")]
    [TestCase("KDE", "KDE")]
    [TestCase("X-Cinnamon", "CINNAMON")]
    [TestCase("LXQt", "LXQT")]
    [TestCase("LXDE", "LXDE")]
    public void NormalizeHint_KnownDesktopFamily_ReturnsNormalizedDesktop(string hint, string expected)
    {
        Assert.That(DesktopEnvironmentDetector.NormalizeHint(hint), Is.EqualTo(expected));
    }

    [Test]
    public void NormalizeHint_UnknownDesktop_ReturnsNull()
    {
        Assert.That(DesktopEnvironmentDetector.NormalizeHint("UNKNOWN_DESKTOP"), Is.Null);
    }
}
