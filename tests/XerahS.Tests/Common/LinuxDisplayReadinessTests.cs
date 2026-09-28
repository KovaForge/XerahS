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

#nullable enable

using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public sealed class LinuxDisplayReadinessTests
{
    [TestCase(":0", "/tmp/.X11-unix/X0")]
    [TestCase(":1.0", "/tmp/.X11-unix/X1")]
    [TestCase("unix:2", "/tmp/.X11-unix/X2")]
    [TestCase("remote:10.0", null)]
    [TestCase(":x", null)]
    [TestCase("", null)]
    public void GetLocalX11SocketPath(string display, string? expected)
    {
        Assert.That(LinuxDisplayReadiness.GetLocalX11SocketPath(display), Is.EqualTo(expected));
    }

    [Test]
    public void Evaluate_SocketPresent_IsReady()
    {
        Assert.That(LinuxDisplayReadiness.Evaluate(":0", "wayland-1", _ => true), Is.EqualTo(LinuxDisplayState.Ready));
    }

    [Test]
    public void Evaluate_SocketMissing_WaitsForX11()
    {
        Assert.That(LinuxDisplayReadiness.Evaluate(":0", "wayland-1", _ => false), Is.EqualTo(LinuxDisplayState.WaitingForX11));
    }

    [Test]
    public void Evaluate_RemoteDisplay_IsReady()
    {
        Assert.That(LinuxDisplayReadiness.Evaluate("host:10.0", null, _ => false), Is.EqualTo(LinuxDisplayState.Ready));
    }

    [Test]
    public void Evaluate_WaylandOnly_ReportsMissingXWayland()
    {
        Assert.That(LinuxDisplayReadiness.Evaluate(null, "wayland-1", _ => false), Is.EqualTo(LinuxDisplayState.WaylandWithoutX11));
        Assert.That(LinuxDisplayReadiness.DescribeProblem(LinuxDisplayState.WaylandWithoutX11, null), Does.Contain("XWayland"));
    }

    [Test]
    public void Evaluate_NothingSet_IsNoDisplay()
    {
        Assert.That(LinuxDisplayReadiness.Evaluate(null, null, _ => true), Is.EqualTo(LinuxDisplayState.NoDisplay));
    }
}
