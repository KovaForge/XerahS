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

using System.Collections.Generic;

namespace XerahS.Core;

/// <summary>
/// Central workflow classification catalog used by both core execution and UI routing.
/// </summary>
public static class WorkflowCatalog
{
    private static readonly HashSet<WorkflowType> ToolWorkflows =
    [
        WorkflowType.ColorPicker,
        WorkflowType.ScreenColorPicker,
        WorkflowType.QRCode,
        WorkflowType.QRCodeDecodeFromScreen,
        WorkflowType.QRCodeScanRegion,
        WorkflowType.ScrollingCapture,
        WorkflowType.OCR,
        WorkflowType.ImageEditor,
        WorkflowType.HashCheck,
        WorkflowType.PinToScreen,
        WorkflowType.PinToScreenFromScreen,
        WorkflowType.PinToScreenFromClipboard,
        WorkflowType.PinToScreenFromFile,
        WorkflowType.PinToScreenCloseAll,
        WorkflowType.AutoCapture,
        WorkflowType.StartAutoCapture,
        WorkflowType.StopAutoCapture,
        WorkflowType.ImageCombiner,
        WorkflowType.ImageSplitter,
        WorkflowType.ImageThumbnailer,
        WorkflowType.VideoConverter,
        WorkflowType.VideoThumbnailer,
        WorkflowType.AnalyzeImage,
        WorkflowType.ClipboardViewer,
        WorkflowType.ClipboardUploadWithContentViewer,
        WorkflowType.MonitorTest,
        WorkflowType.Ruler
    ];

    private static readonly HashSet<WorkflowType> MainWindowHideCaptureWorkflows =
    [
        WorkflowType.PrintScreen,
        WorkflowType.ActiveWindow,
        WorkflowType.RectangleRegion,
        WorkflowType.RectangleTransparent,
        WorkflowType.CustomWindow,
        WorkflowType.ScrollingCapture,
        WorkflowType.OCR,
        WorkflowType.ActiveMonitor,
        WorkflowType.CustomRegion,
        WorkflowType.LastRegion
    ];

    public static bool IsToolWorkflow(WorkflowType workflowType)
    {
        return ToolWorkflows.Contains(workflowType);
    }

    public static bool RequiresHideMainWindowForCapture(WorkflowType workflowType)
    {
        return MainWindowHideCaptureWorkflows.Contains(workflowType);
    }
}
