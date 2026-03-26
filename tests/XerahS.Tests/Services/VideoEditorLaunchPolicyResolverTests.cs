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
using XerahS.Common;

namespace XerahS.Tests.Services;

[TestFixture]
public class VideoEditorLaunchPolicyResolverTests
{
    [Test]
    public void ResolvePolicy_WaylandKde_DisablesAutoLaunchAndEnablesMitigation()
    {
        VideoEditorLaunchPolicy policy = VideoEditorLaunchPolicyResolver.ResolvePolicy(
            isLinux: true,
            sessionType: "wayland",
            waylandDisplay: "wayland-0",
            currentDesktop: "KDE",
            sessionDesktop: "plasma",
            desktopSession: "plasma");

        Assert.Multiple(() =>
        {
            Assert.That(policy.AllowInteractiveLaunch, Is.True);
            Assert.That(policy.AllowAutoLaunchAfterCapture, Is.False);
            Assert.That(policy.EnableLinuxWaylandExplicitSyncMitigation, Is.True);
            Assert.That(policy.AutoLaunchBlockedReason, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void ResolvePolicy_NonLinux_ReturnsDefaultPolicy()
    {
        VideoEditorLaunchPolicy policy = VideoEditorLaunchPolicyResolver.ResolvePolicy(
            isLinux: false,
            sessionType: "wayland",
            waylandDisplay: "wayland-0",
            currentDesktop: "KDE",
            sessionDesktop: "plasma",
            desktopSession: "plasma");

        Assert.Multiple(() =>
        {
            Assert.That(policy.AllowInteractiveLaunch, Is.True);
            Assert.That(policy.AllowAutoLaunchAfterCapture, Is.True);
            Assert.That(policy.EnableLinuxWaylandExplicitSyncMitigation, Is.False);
        });
    }
}
