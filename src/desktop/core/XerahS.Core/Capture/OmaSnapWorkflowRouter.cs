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

namespace XerahS.Core.Capture;

/// <summary>
/// XIP0088 workflow mapping: which capture jobs OmaSnap fronts, and with which host target.
/// Jobs not listed (active window, named windows, color picker, ruler, OCR, recording) keep
/// their existing XerahS paths.
/// </summary>
public static class OmaSnapWorkflowRouter
{
    public static bool TryCreateRequest(
        WorkflowType job,
        TaskSettingsCapture captureSettings,
        Rectangle? lastRegion,
        out OmaSnapCaptureRequest request)
    {
        request = null!;
        switch (job)
        {
            case WorkflowType.RectangleRegion:
            case WorkflowType.RectangleTransparent:
                request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Smart);
                return true;

            case WorkflowType.LastRegion:
                if (lastRegion is { Width: > 0, Height: > 0 } last)
                {
                    request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Region) { Region = ToRegion(last) };
                    return true;
                }

                return false;

            case WorkflowType.CustomRegion:
                if (captureSettings.CaptureCustomRegion is { Width: > 0, Height: > 0 } custom)
                {
                    request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Region) { Region = ToRegion(custom) };
                    return true;
                }

                return false;

            case WorkflowType.CustomWindow:
                // A named target window keeps the existing search-and-capture path.
                if (string.IsNullOrEmpty(captureSettings.CaptureCustomWindow))
                {
                    request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Window);
                    return true;
                }

                return false;

            case WorkflowType.PrintScreen:
            case WorkflowType.ActiveMonitor:
                request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Fullscreen);
                return true;

            case WorkflowType.ScrollingCapture:
                request = new OmaSnapCaptureRequest(OmaSnapCaptureTarget.Scroll);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Region jobs remember what the user selected so Last region can repeat it.</summary>
    public static bool ShouldRememberRegion(WorkflowType job) =>
        job is WorkflowType.RectangleRegion or WorkflowType.RectangleTransparent;

    private static OmaSnapRegion ToRegion(Rectangle rectangle) =>
        new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
