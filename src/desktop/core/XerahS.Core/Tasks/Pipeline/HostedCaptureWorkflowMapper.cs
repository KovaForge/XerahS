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
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Tasks.Pipeline;

/// <summary>
/// Maps workflow jobs to a hosted capture engine request (OmaSnap on Hyprland, XIP0088) and decides
/// whether the engine is used at all. Pure functions so the mapping table and the "no change on
/// other systems" rule are unit tested.
/// </summary>
internal static class HostedCaptureWorkflowMapper
{
    /// <summary>
    /// Returns the engine request for <paramref name="job"/>, or null when the job keeps its normal
    /// XerahS path (tools such as color picker, ruler, QR and OCR, recordings, and jobs that need no
    /// interaction such as ActiveWindow or a named CustomWindow).
    /// </summary>
    public static HostedCaptureRequest? Map(
        WorkflowType job,
        bool regionOnly,
        Rectangle? customRegion,
        string? customWindowTitle,
        Rectangle? lastRegion)
    {
        switch (job)
        {
            case WorkflowType.RectangleRegion:
            case WorkflowType.RectangleTransparent:
                return new HostedCaptureRequest(regionOnly ? HostedCaptureTarget.Region : HostedCaptureTarget.Smart);

            case WorkflowType.CustomRegion:
                // A configured rectangle is captured without interaction; without one, select.
                return IsUsable(customRegion)
                    ? new HostedCaptureRequest(HostedCaptureTarget.Region) { Region = customRegion }
                    : new HostedCaptureRequest(regionOnly ? HostedCaptureTarget.Region : HostedCaptureTarget.Smart);

            case WorkflowType.LastRegion:
                return IsUsable(lastRegion)
                    ? new HostedCaptureRequest(HostedCaptureTarget.Region) { Region = lastRegion }
                    : null;

            case WorkflowType.CustomWindow:
                // A named window is found and captured by the existing path; otherwise pick one.
                return string.IsNullOrWhiteSpace(customWindowTitle)
                    ? new HostedCaptureRequest(HostedCaptureTarget.Window)
                    : null;

            case WorkflowType.PrintScreen:
            case WorkflowType.ActiveMonitor:
                return new HostedCaptureRequest(HostedCaptureTarget.Fullscreen);

            case WorkflowType.ScrollingCapture:
                return new HostedCaptureRequest(HostedCaptureTarget.Scroll);

            default:
                return null;
        }
    }

    /// <summary>
    /// The engine is used only when it reports available, and then only for the explicit OmaSnap
    /// selector or for Automatic. Every other selector, and every system where the probe did not
    /// succeed, keeps the existing capture chain unchanged.
    /// </summary>
    public static bool ShouldUseEngine(
        LinuxInteractiveRegionSelectorPreference preference,
        HostedCaptureEngineStatus? status,
        bool skipEngine)
    {
        if (skipEngine || status is not { Available: true })
        {
            return false;
        }

        return preference is LinuxInteractiveRegionSelectorPreference.Automatic or LinuxInteractiveRegionSelectorPreference.OmaSnap;
    }

    private static bool IsUsable(Rectangle? rect) => rect is { Width: > 0, Height: > 0 };
}
