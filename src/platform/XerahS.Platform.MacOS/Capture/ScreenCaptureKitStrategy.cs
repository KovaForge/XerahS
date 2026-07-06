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
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using XerahS.Platform.MacOS.Native;

namespace XerahS.Platform.MacOS.Capture;

/// <summary>
/// Capture strategy backed by the native ScreenCaptureKit bridge (macOS 12.3+).
/// The bridge uses SCScreenshotManager, so stills avoid the deprecated
/// CGDisplayCreateImage path and Sequoia's recurring re-approval nags (XIP0078 P7/P8).
/// Monitor enumeration reuses the Quartz strategy; capture goes through
/// libscreencapturekit_bridge.dylib.
/// </summary>
internal sealed class ScreenCaptureKitStrategy : ICaptureStrategy
{
    private readonly QuartzCaptureStrategy _monitorSource = new();

    public string Name => "ScreenCaptureKit";

    public static bool IsSupported()
    {
        return OperatingSystem.IsMacOSVersionAtLeast(12, 3) && ScreenCaptureKitInterop.TryLoad();
    }

    public MonitorInfo[] GetMonitors()
    {
        // Quartz display enumeration stays authoritative for geometry; only pixel capture
        // is routed through ScreenCaptureKit.
        return _monitorSource.GetMonitors();
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        return Task.Run(() =>
        {
            // The bridge's sourceRect is in display points; monitor bounds are physical pixels
            // scaled by ScaleFactor (see QuartzCaptureStrategy.GetMonitors), so divide to convert.
            double scale = 1.0;
            foreach (var monitor in GetMonitors())
            {
                if (monitor.Bounds.Intersect(physicalRegion) != null)
                {
                    scale = monitor.ScaleFactor;
                    break;
                }
            }

            IntPtr dataPtr = IntPtr.Zero;

            try
            {
                int result = ScreenCaptureKitInterop.CaptureRect(
                    (float)(physicalRegion.X / scale),
                    (float)(physicalRegion.Y / scale),
                    (float)(physicalRegion.Width / scale),
                    (float)(physicalRegion.Height / scale),
                    out dataPtr,
                    out int length);

                if (result != ScreenCaptureKitInterop.SUCCESS || dataPtr == IntPtr.Zero || length <= 0)
                {
                    throw new InvalidOperationException(
                        $"ScreenCaptureKit capture failed: {ScreenCaptureKitInterop.GetErrorMessage(result)}");
                }

                var bytes = new byte[length];
                Marshal.Copy(dataPtr, bytes, 0, length);

                using var stream = new MemoryStream(bytes);
                var bitmap = SKBitmap.Decode(stream)
                    ?? throw new InvalidOperationException("Failed to decode ScreenCaptureKit PNG data");

                return new CapturedBitmap(bitmap, physicalRegion, scale);
            }
            finally
            {
                if (dataPtr != IntPtr.Zero)
                {
                    ScreenCaptureKitInterop.FreeBuffer(dataPtr);
                }
            }
        });
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "ScreenCaptureKit",
            Version = "12.3+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = true,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = true
        };
    }

    public void Dispose()
    {
        _monitorSource.Dispose();
    }
}
