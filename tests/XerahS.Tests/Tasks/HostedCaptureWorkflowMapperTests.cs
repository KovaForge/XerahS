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
using XerahS.Core;
using XerahS.Core.Tasks.Pipeline;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Tasks;

/// <summary>XIP0088 workflow → OmaSnap mapping table and the engine-use decision.</summary>
[TestFixture]
public class HostedCaptureWorkflowMapperTests
{
    private static readonly Rectangle Last = new(10, 20, 300, 200);

    [TestCase(WorkflowType.RectangleRegion, false, HostedCaptureTarget.Smart)]
    [TestCase(WorkflowType.RectangleRegion, true, HostedCaptureTarget.Region)]
    [TestCase(WorkflowType.RectangleTransparent, false, HostedCaptureTarget.Smart)]
    [TestCase(WorkflowType.CustomRegion, false, HostedCaptureTarget.Smart)]
    [TestCase(WorkflowType.CustomWindow, false, HostedCaptureTarget.Window)]
    [TestCase(WorkflowType.PrintScreen, false, HostedCaptureTarget.Fullscreen)]
    [TestCase(WorkflowType.ActiveMonitor, false, HostedCaptureTarget.Fullscreen)]
    [TestCase(WorkflowType.ScrollingCapture, false, HostedCaptureTarget.Scroll)]
    public void MappedJobs(WorkflowType job, bool regionOnly, HostedCaptureTarget expected)
    {
        var request = HostedCaptureWorkflowMapper.Map(job, regionOnly, customRegion: null, customWindowTitle: null, lastRegion: null);

        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Target, Is.EqualTo(expected));
        Assert.That(request.Region, Is.Null);
    }

    [Test]
    public void LastRegion_UsesStoredRegion_OrKeepsTheExistingPathWithoutOne()
    {
        var request = HostedCaptureWorkflowMapper.Map(WorkflowType.LastRegion, false, null, null, Last);

        Assert.That(request!.Target, Is.EqualTo(HostedCaptureTarget.Region));
        Assert.That(request.Region, Is.EqualTo(Last));
        Assert.That(HostedCaptureWorkflowMapper.Map(WorkflowType.LastRegion, false, null, null, null), Is.Null);
    }

    [Test]
    public void CustomRegion_WithConfiguredRectangle_IsPreselected()
    {
        var request = HostedCaptureWorkflowMapper.Map(WorkflowType.CustomRegion, false, Last, null, null);

        Assert.That(request!.Target, Is.EqualTo(HostedCaptureTarget.Region));
        Assert.That(request.Region, Is.EqualTo(Last));
    }

    [Test]
    public void CustomWindow_WithConfiguredTitle_KeepsTheExistingPath()
    {
        Assert.That(HostedCaptureWorkflowMapper.Map(WorkflowType.CustomWindow, false, null, "Firefox", null), Is.Null);
    }

    [TestCase(WorkflowType.ActiveWindow)]
    [TestCase(WorkflowType.ScreenColorPicker)]
    [TestCase(WorkflowType.Ruler)]
    [TestCase(WorkflowType.ScreenRecorder)]
    [TestCase(WorkflowType.ClipboardUpload)]
    [TestCase(WorkflowType.FileUpload)]
    public void UnmappedJobs_KeepTheirXerahSPath(WorkflowType job)
    {
        Assert.That(HostedCaptureWorkflowMapper.Map(job, false, null, null, Last), Is.Null);
    }

    [Test]
    public void EngineUse_TruthTable()
    {
        var available = new HostedCaptureEngineStatus(true, "omasnap", "1.22.0", "ready");
        var unavailable = HostedCaptureEngineStatus.NotAvailable("omasnap", "not a Hyprland session");

        foreach (var preference in Enum.GetValues<LinuxInteractiveRegionSelectorPreference>())
        {
            bool expected = preference is LinuxInteractiveRegionSelectorPreference.Automatic or LinuxInteractiveRegionSelectorPreference.OmaSnap;
            Assert.That(HostedCaptureWorkflowMapper.ShouldUseEngine(preference, available, skipEngine: false), Is.EqualTo(expected), preference.ToString());
            Assert.That(HostedCaptureWorkflowMapper.ShouldUseEngine(preference, unavailable, skipEngine: false), Is.False, "no probe, no change");
            Assert.That(HostedCaptureWorkflowMapper.ShouldUseEngine(preference, null, skipEngine: false), Is.False);
            Assert.That(HostedCaptureWorkflowMapper.ShouldUseEngine(preference, available, skipEngine: true), Is.False, "fallback after failure");
        }
    }
}
