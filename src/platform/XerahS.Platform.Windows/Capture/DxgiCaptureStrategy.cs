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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Windows;
using XerahS.Platform.Windows.Capture;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ShareX.Avalonia.Platform.Windows.Capture;

/// <summary>
/// Capture strategy using DXGI Desktop Duplication API.
/// Provides hardware-accelerated screen capture with minimal CPU usage.
/// Requires Windows 8+ and DXGI 1.2+.
/// </summary>
internal sealed class DxgiCaptureStrategy : ICaptureStrategy
{
    private readonly Dictionary<string, DxgiMonitorContext> _monitorContexts = new();
    private bool _disposed;

    public string Name => "DXGI Desktop Duplication";

    public static bool IsSupported()
    {
        // DXGI 1.2+ required (Windows 8+)
        return Environment.OSVersion.Version >= new Version(6, 2);
    }

    public MonitorInfo[] GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        uint adapterIndex = 0;
        while (factory.EnumAdapters1(adapterIndex, out var adapter).Success)
        {
            uint outputIndex = 0;
            while (adapter.EnumOutputs(outputIndex, out var output).Success)
            {
                var desc = output.Description;

                // Get per-monitor DPI via GetDpiForMonitor
                var hMonitor = desc.Monitor;
                uint dpiX = 96, dpiY = 96;

                try
                {
                    NativeMethods.GetDpiForMonitor(
                        hMonitor,
                        MonitorDpiType.MDT_EFFECTIVE_DPI,
                        out dpiX,
                        out dpiY);
                }
                catch
                {
                    // Fallback to 96 DPI if GetDpiForMonitor fails
                }

                var scaleFactor = dpiX / 96.0;

                // Get monitor device name
                var deviceName = GetMonitorDeviceName(hMonitor);

                // Get working area (excluding taskbar)
                var workingArea = GetWorkingArea(hMonitor);

                monitors.Add(new MonitorInfo
                {
                    Id = desc.DeviceName,
                    Name = deviceName,
                    IsPrimary = desc.DesktopCoordinates.Left == 0 && desc.DesktopCoordinates.Top == 0,
                    Bounds = new PhysicalRectangle(
                        desc.DesktopCoordinates.Left,
                        desc.DesktopCoordinates.Top,
                        desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left,
                        desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top),
                    WorkingArea = workingArea,
                    ScaleFactor = scaleFactor,
                    Rotation = ConvertRotation(desc.Rotation),
                    BitsPerPixel = 32 // DXGI always uses 32-bit BGRA
                });

                // Pre-initialize DXGI duplication for this output
                try
                {
                    InitializeDuplication(output, adapter, desc.DeviceName);
                }
                catch
                {
                    // Ignore initialization failures for individual monitors
                }

                output.Dispose();
                outputIndex++;
            }

            adapter.Dispose();
            adapterIndex++;
        }

        return monitors.ToArray();
    }

    public async Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        // Find which monitor contains this region
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.Bounds.Intersect(physicalRegion) != null);

        if (monitor == null)
            throw new InvalidOperationException($"Region {physicalRegion} does not intersect any monitor");

        if (!_monitorContexts.TryGetValue(monitor.Id, out var context))
            throw new InvalidOperationException($"Monitor {monitor.Id} not initialized");

        // Capture the region using DXGI
        return await Task.Run(() => CaptureRegionInternal(context, monitor, physicalRegion, options));
    }

    private CapturedBitmap CaptureRegionInternal(
        DxgiMonitorContext context,
        MonitorInfo monitor,
        PhysicalRectangle region,
        RegionCaptureOptions options)
    {
        var duplication = context.IDXGIOutputDuplication;
        var device = context.Device;

        // Acquire next frame
        OutduplFrameInfo frameInfo;
        IDXGIResource desktopResource;

        try
        {
            duplication.AcquireNextFrame(100, out frameInfo, out desktopResource);
        }
        catch
        {
            // No frame available, try once more with longer timeout
            try
            {
                duplication.AcquireNextFrame(500, out frameInfo, out desktopResource);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to acquire frame: {ex.Message}", ex);
            }
        }

        try
        {
            using var texture = desktopResource.QueryInterface<ID3D11Texture2D>();

            // Calculate region relative to monitor
            var intersection = region.Intersect(monitor.Bounds);
            if (intersection == null || intersection.Value.IsEmpty)
                throw new ArgumentException($"Region {region} does not intersect monitor {monitor.Name}");

            var captureRegion = intersection.Value;
            var localX = captureRegion.X - monitor.Bounds.X;
            var localY = captureRegion.Y - monitor.Bounds.Y;
            var localRegion = new PhysicalRectangle(localX, localY, captureRegion.Width, captureRegion.Height);
            var textureDesc = texture.Description;
            var sourceBox = DxgiRotationHelper.CreateSourceBox(
                localRegion,
                monitor.Rotation,
                (int)textureDesc.Width,
                (int)textureDesc.Height);
            int sourceWidth = DxgiRotationHelper.GetSourceWidth(sourceBox);
            int sourceHeight = DxgiRotationHelper.GetSourceHeight(sourceBox);
            bool isHdr = DxgiHdrToneMapper.IsHdrFormat(textureDesc.Format);

            var stagingDesc = new Texture2DDescription
            {
                Width = isHdr ? textureDesc.Width : (uint)sourceWidth,
                Height = isHdr ? textureDesc.Height : (uint)sourceHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = isHdr ? textureDesc.Format : Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read
            };

            using var staging = device.CreateTexture2D(stagingDesc);

            if (isHdr)
            {
                context.ImmediateContext.CopyResource(staging, texture);
            }
            else
            {
                context.ImmediateContext.CopySubresourceRegion(
                    staging,
                    0,
                    0, 0, 0,
                    texture,
                    0,
                    sourceBox);
            }

            var mapped = context.ImmediateContext.Map(staging, 0, MapMode.Read);

            try
            {
                SKBitmap sourceBitmap;
                if (isHdr)
                {
                    using var toneMapped = DxgiHdrToneMapper.TryConvertToBgra(
                        mapped,
                        textureDesc,
                        HdrToneMapContext.FromOutput(context.Output))
                        ?? throw new InvalidOperationException($"Failed to tone-map HDR DXGI frame ({textureDesc.Format}).");
                    var crop = new SKRectI(sourceBox.Left, sourceBox.Top, sourceBox.Right, sourceBox.Bottom);
                    sourceBitmap = CropBitmap(toneMapped, crop);
                }
                else
                {
                    sourceBitmap = new SKBitmap(
                        sourceWidth,
                        sourceHeight,
                        SKColorType.Bgra8888,
                        SKAlphaType.Premul);

                    BgraRowCopyHelper.CopyRows(
                        mapped.DataPointer,
                        (int)mapped.RowPitch,
                        sourceBitmap.GetPixels(),
                        sourceBitmap.RowBytes,
                        sourceWidth * 4,
                        sourceHeight);
                }

                using (sourceBitmap)
                {
                    SKBitmap bitmap = BitmapRotationHelper.RotateClockwise(sourceBitmap, monitor.Rotation);
                    TryCompositeCursor(bitmap, captureRegion, options);
                    return new CapturedBitmap(bitmap, captureRegion, monitor.ScaleFactor);
                }
            }
            finally
            {
                context.ImmediateContext.Unmap(staging, 0);
            }
        }
        finally
        {
            desktopResource?.Dispose();
            duplication.ReleaseFrame();
        }
    }

    public BackendCapabilities GetCapabilities() => DxgiCapabilitiesHelper.Create();

    private static void TryCompositeCursor(SKBitmap bitmap, PhysicalRectangle captureRegion, RegionCaptureOptions options)
    {
        if (!options.IncludeCursor)
            return;

        try
        {
            var cursor = new CursorData();
            DxgiCursorCompositionHelper.TryCompositeCursor(
                bitmap,
                cursor.IsVisible,
                cursor.Position,
                cursor.Hotspot,
                cursor.Size,
                captureRegion,
                cursor.DrawCursor);
        }
        catch
        {
            // Cursor composition is best-effort; capture must still succeed if cursor APIs fail.
        }
    }

    private void InitializeDuplication(IDXGIOutput output, IDXGIAdapter1 adapter, string monitorId)
    {
        IDXGIOutput1? output1 = null;
        ID3D11Device? device = null;

        try
        {
            output1 = output.QueryInterface<IDXGIOutput1>();

            // Create D3D11 device for this adapter
            var featureLevels = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0 };
            var result = D3D11.D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                DeviceCreationFlags.None,
                featureLevels,
                out device);

            if (result.Failure || device == null)
                throw new InvalidOperationException($"Failed to create D3D11 device: {result}");

            var duplication = DxgiOutputDuplicationHelper.Create(output, device);
            var context = new DxgiMonitorContext
            {
                Device = device,
                ImmediateContext = device.ImmediateContext,
                Output = output1,
                IDXGIOutputDuplication = duplication
            };

            DisposableContextDictionary.Replace(_monitorContexts, monitorId, context);
            output1 = null;
            device = null;
        }
        finally
        {
            output1?.Dispose();
            device?.Dispose();
        }
    }

    private string GetMonitorDeviceName(IntPtr hMonitor)
    {
        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(mi);

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            // Get friendly display name
            var dd = new NativeMethods.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(dd);

            if (NativeMethods.EnumDisplayDevices(mi.szDevice, 0, ref dd, 0))
            {
                return dd.DeviceString;
            }
        }

        return "Unknown Monitor";
    }

    private PhysicalRectangle GetWorkingArea(IntPtr hMonitor)
    {
        var mi = new NativeMethods.MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf(mi);

        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
        {
            return new PhysicalRectangle(
                mi.rcWork.Left,
                mi.rcWork.Top,
                mi.rcWork.Right - mi.rcWork.Left,
                mi.rcWork.Bottom - mi.rcWork.Top);
        }

        // Fallback: return monitor bounds
        return default;
    }

    private static SKBitmap CropBitmap(SKBitmap source, SKRectI crop)
    {
        crop = SKRectI.Intersect(crop, new SKRectI(0, 0, source.Width, source.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
            throw new InvalidOperationException("HDR crop rectangle is empty.");

        var cropped = new SKBitmap(crop.Width, crop.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, crop, new SKRect(0, 0, crop.Width, crop.Height), SKSamplingOptions.Default);
        return cropped;
    }

    private static int ConvertRotation(Vortice.DXGI.ModeRotation rotation)
    {
        return rotation switch
        {
            Vortice.DXGI.ModeRotation.Rotate90 => 90,
            Vortice.DXGI.ModeRotation.Rotate180 => 180,
            Vortice.DXGI.ModeRotation.Rotate270 => 270,
            _ => 0
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var context in _monitorContexts.Values)
        {
            context.Dispose();
        }

        _monitorContexts.Clear();
    }
}

/// <summary>
/// Context information for a DXGI-monitored display.
/// </summary>
internal sealed class DxgiMonitorContext : IDisposable
{
    public required ID3D11Device Device { get; init; }
    public required ID3D11DeviceContext ImmediateContext { get; init; }
    public required IDXGIOutput1 Output { get; init; }
    public required IDXGIOutputDuplication IDXGIOutputDuplication { get; init; }

    public void Dispose()
    {
        IDXGIOutputDuplication.Dispose();
        Output.Dispose();
        ImmediateContext.Dispose();
        Device.Dispose();
    }
}

/// <summary>
/// Monitor DPI type for GetDpiForMonitor API.
/// </summary>
internal enum MonitorDpiType
{
    MDT_EFFECTIVE_DPI = 0,
    MDT_ANGULAR_DPI = 1,
    MDT_RAW_DPI = 2
}
