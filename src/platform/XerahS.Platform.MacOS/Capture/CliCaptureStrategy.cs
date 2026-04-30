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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;

namespace ShareX.Avalonia.Platform.macOS.Capture;

/// <summary>
/// Fallback capture strategy using macOS screencapture CLI tool.
/// Universal compatibility, works on all macOS versions.
/// </summary>
internal sealed class CliCaptureStrategy : ICaptureStrategy
{
    public string Name => "screencapture CLI";

    public static bool IsSupported()
    {
        // screencapture is available on all macOS versions
        return OperatingSystem.IsMacOS();
    }

    public MonitorInfo[] GetMonitors()
    {
        // Use Quartz API for monitor enumeration (more reliable than CLI)
        var quartzStrategy = new QuartzCaptureStrategy();
        return quartzStrategy.GetMonitors();
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

        if (monitor == null)
            throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

        // Convert to logical coordinates for screencapture command, preserving the
        // full physical region when scaled bounds land between logical pixels.
        var (logicalX, logicalY, logicalWidth, logicalHeight) = ConvertToLogicalCaptureRegion(
            physicalRegion,
            monitor.ScaleFactor);

        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_capture_{Guid.NewGuid():N}.png");

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/screencapture",
                    Arguments = BuildCaptureArguments(logicalX, logicalY, logicalWidth, logicalHeight, tempFile, options),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"screencapture failed: {error}");
            }

            if (!File.Exists(tempFile))
                throw new InvalidOperationException("screencapture did not create output file");

            // Decode captured PNG
            var bitmap = SKBitmap.Decode(tempFile);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to decode captured image");

            return new CapturedBitmap(bitmap, physicalRegion, monitor.ScaleFactor);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "screencapture CLI",
            Version = "Universal",
            SupportsHardwareAcceleration = false,
            SupportsCursorCapture = false,
            SupportsHDR = false,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = true
        };
    }

    internal static (int X, int Y, int Width, int Height) ConvertToLogicalCaptureRegion(
        PhysicalRectangle physicalRegion,
        double scaleFactor)
    {
        if (scaleFactor <= 0 || double.IsNaN(scaleFactor) || double.IsInfinity(scaleFactor))
            throw new ArgumentOutOfRangeException(nameof(scaleFactor), scaleFactor, "Scale factor must be a positive finite value.");

        var left = (int)Math.Floor(physicalRegion.X / scaleFactor);
        var top = (int)Math.Floor(physicalRegion.Y / scaleFactor);
        var right = (int)Math.Ceiling((physicalRegion.X + physicalRegion.Width) / scaleFactor);
        var bottom = (int)Math.Ceiling((physicalRegion.Y + physicalRegion.Height) / scaleFactor);

        return (left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    internal static string BuildCaptureArguments(
        int logicalX,
        int logicalY,
        int logicalWidth,
        int logicalHeight,
        string outputPath,
        RegionCaptureOptions? options)
    {
        var soundArgument = options?.MacOSPlayCaptureSound == false ? "-x " : string.Empty;
        return $"{soundArgument}-R{logicalX},{logicalY},{logicalWidth},{logicalHeight} \"{outputPath}\"";
    }

    public void Dispose()
    {
        // No resources to clean up
    }
}
