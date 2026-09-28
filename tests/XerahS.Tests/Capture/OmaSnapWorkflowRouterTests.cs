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
using XerahS.Core.Capture;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Capture;

[TestFixture]
public sealed class OmaSnapWorkflowRouterTests
{
    [TestCase(WorkflowType.RectangleRegion, OmaSnapCaptureTarget.Smart)]
    [TestCase(WorkflowType.RectangleTransparent, OmaSnapCaptureTarget.Smart)]
    [TestCase(WorkflowType.CustomWindow, OmaSnapCaptureTarget.Window)]
    [TestCase(WorkflowType.PrintScreen, OmaSnapCaptureTarget.Fullscreen)]
    [TestCase(WorkflowType.ActiveMonitor, OmaSnapCaptureTarget.Fullscreen)]
    [TestCase(WorkflowType.ScrollingCapture, OmaSnapCaptureTarget.Scroll)]
    public void MappedJobs(WorkflowType job, OmaSnapCaptureTarget target)
    {
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(job, new TaskSettingsCapture(), null, out var request), Is.True);
        Assert.That(request.Target, Is.EqualTo(target));
        Assert.That(request.Region, Is.Null);
    }

    [TestCase(WorkflowType.ActiveWindow)]
    [TestCase(WorkflowType.ScreenColorPicker)]
    [TestCase(WorkflowType.Ruler)]
    [TestCase(WorkflowType.ScreenRecorder)]
    [TestCase(WorkflowType.FileUpload)]
    public void UnmappedJobs_KeepExistingPaths(WorkflowType job)
    {
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(job, new TaskSettingsCapture(), null, out _), Is.False);
    }

    [Test]
    public void LastRegion_UsesStoredRectangle()
    {
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(WorkflowType.LastRegion, new TaskSettingsCapture(), null, out _), Is.False);
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(WorkflowType.LastRegion, new TaskSettingsCapture(), new Rectangle(5, 6, 70, 80), out var request), Is.True);
        Assert.That(request.Target, Is.EqualTo(OmaSnapCaptureTarget.Region));
        Assert.That(request.Region, Is.EqualTo(new OmaSnapRegion(5, 6, 70, 80)));
    }

    [Test]
    public void CustomRegion_UsesConfiguredRectangle()
    {
        var settings = new TaskSettingsCapture { CaptureCustomRegion = new Rectangle(-1920, 0, 400, 300) };
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(WorkflowType.CustomRegion, settings, null, out var request), Is.True);
        Assert.That(request.Region, Is.EqualTo(new OmaSnapRegion(-1920, 0, 400, 300)));
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(WorkflowType.CustomRegion, new TaskSettingsCapture(), null, out _), Is.False);
    }

    [Test]
    public void NamedCustomWindow_KeepsExistingPath()
    {
        var settings = new TaskSettingsCapture { CaptureCustomWindow = "Firefox" };
        Assert.That(OmaSnapWorkflowRouter.TryCreateRequest(WorkflowType.CustomWindow, settings, null, out _), Is.False);
    }
}
