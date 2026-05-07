#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiCapabilitiesHelper
{
    public static BackendCapabilities Create()
    {
        return new BackendCapabilities
        {
            BackendName = "DXGI Desktop Duplication",
            Version = "1.2+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = false
        };
    }
}
