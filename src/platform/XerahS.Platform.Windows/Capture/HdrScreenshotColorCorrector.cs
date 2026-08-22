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
using System.Runtime.InteropServices;
using SkiaSharp;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Recaptures HDR outputs via DXGI and composites tone-mapped highlights onto a GDI screenshot.
/// SDR pixels that already match the GDI capture are left unchanged.
/// </summary>
internal static class HdrScreenshotColorCorrector
{
    private const uint FrameAcquireTimeout = 500;
    private const int MaxFrameAcquireAttempts = 3;
    private const int SdrReferenceTolerance = 4;

    public static SKBitmap ApplyIfEnabled(SKBitmap destination, Rectangle captureRectangle, bool enabled)
    {
        if (!enabled)
        {
            return destination;
        }

        SKBitmap working = destination.IsImmutable ? destination.Copy() : destination;
        if (!ReferenceEquals(working, destination))
        {
            destination.Dispose();
        }

        TryApply(working, captureRectangle);
        return working;
    }

    public static bool TryApply(SKBitmap destination, Rectangle captureRectangle)
    {
        if (destination.Width <= 0 || destination.Height <= 0 || captureRectangle.Width <= 0 || captureRectangle.Height <= 0)
        {
            return false;
        }

        try
        {
            using IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            bool corrected = false;

            for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out IDXGIAdapter1 adapter).Success; adapterIndex++)
            {
                using (adapter)
                {
                    corrected |= ApplyAdapterColorCorrection(adapter, destination, captureRectangle);
                }
            }

            return corrected;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"HDR screenshot color correction failed: {ex.Message}");
            return false;
        }
    }

    private static bool ApplyAdapterColorCorrection(IDXGIAdapter1 adapter, SKBitmap destination, Rectangle captureRectangle)
    {
        ID3D11Device? device = null;
        bool corrected = false;

        try
        {
            for (uint outputIndex = 0; adapter.EnumOutputs(outputIndex, out IDXGIOutput output).Success; outputIndex++)
            {
                using (output)
                {
                    try
                    {
                        if (!HdrToneMapContext.IsHdrOutput(output))
                        {
                            continue;
                        }

                        using IDXGIOutput6 output6 = output.QueryInterface<IDXGIOutput6>();
                        OutputDescription1 outputDescription = output6.Description1;
                        Rectangle outputBounds = GetOutputBounds(outputDescription);
                        if (!captureRectangle.IntersectsWith(outputBounds))
                        {
                            continue;
                        }

                        if (device is null)
                        {
                            if (D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport,
                                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0], out device).Failure || device is null)
                            {
                                return corrected;
                            }
                        }

                        HdrToneMapContext context = HdrToneMapContext.FromDescription(outputDescription);
                        using CapturedOutput? capturedOutput = CaptureOutput(output, outputDescription, device, context);
                        if (capturedOutput is null)
                        {
                            continue;
                        }

                        CopyOutputIntersection(destination, captureRectangle, capturedOutput, outputBounds);
                        corrected = true;
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteLine($"HDR capture failed for a display output: {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            device?.Dispose();
        }

        return corrected;
    }

    private static CapturedOutput? CaptureOutput(
        IDXGIOutput output,
        OutputDescription1 outputDescription,
        ID3D11Device device,
        HdrToneMapContext context)
    {
        using IDXGIOutputDuplication duplication = DxgiOutputDuplicationHelper.Create(output, device);
        IDXGIResource? desktopResource = null;
        bool frameAcquired = false;

        try
        {
            if (!TryAcquireDesktopFrame(duplication, out desktopResource) || desktopResource is null)
            {
                return null;
            }

            frameAcquired = true;
            using ID3D11Texture2D desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
            Texture2DDescription textureDescription = desktopTexture.Description;
            if (!DxgiHdrToneMapper.IsHdrFormat(textureDescription.Format))
            {
                return null;
            }

            Texture2DDescription stagingDescription = textureDescription;
            stagingDescription.Usage = ResourceUsage.Staging;
            stagingDescription.BindFlags = BindFlags.None;
            stagingDescription.CPUAccessFlags = CpuAccessFlags.Read;
            stagingDescription.MiscFlags = ResourceOptionFlags.None;

            using ID3D11Texture2D stagingTexture = device.CreateTexture2D(stagingDescription);
            device.ImmediateContext.CopyResource(stagingTexture, desktopTexture);
            MappedSubresource mapped = device.ImmediateContext.Map(stagingTexture, 0, MapMode.Read);
            try
            {
                SKBitmap? toneMappedBitmap = DxgiHdrToneMapper.TryConvertToBgra(mapped, textureDescription, context);
                if (toneMappedBitmap is null)
                {
                    return null;
                }

                SKBitmap sdrReferenceBitmap = DxgiHdrToneMapper.ConvertToSdrReference(mapped, textureDescription, context);
                int rotationDegrees = outputDescription.Rotation switch
                {
                    ModeRotation.Rotate90 => 90,
                    ModeRotation.Rotate180 => 180,
                    ModeRotation.Rotate270 => 270,
                    _ => 0
                };

                SKBitmap rotatedToneMapped = BitmapRotationHelper.RotateClockwise(toneMappedBitmap, rotationDegrees);
                SKBitmap rotatedReference = BitmapRotationHelper.RotateClockwise(sdrReferenceBitmap, rotationDegrees);
                toneMappedBitmap.Dispose();
                sdrReferenceBitmap.Dispose();

                Rectangle outputBounds = GetOutputBounds(outputDescription);
                if (rotatedToneMapped.Width != outputBounds.Width || rotatedToneMapped.Height != outputBounds.Height)
                {
                    rotatedToneMapped.Dispose();
                    rotatedReference.Dispose();
                    return null;
                }

                return new CapturedOutput(rotatedToneMapped, rotatedReference);
            }
            finally
            {
                device.ImmediateContext.Unmap(stagingTexture, 0);
            }
        }
        finally
        {
            if (frameAcquired)
            {
                try
                {
                    duplication.ReleaseFrame();
                }
                catch
                {
                    // Ignore release failures during cleanup.
                }
            }

            desktopResource?.Dispose();
        }
    }

    private static bool TryAcquireDesktopFrame(IDXGIOutputDuplication duplication, out IDXGIResource? desktopResource)
    {
        desktopResource = null;

        for (int attempt = 0; attempt < MaxFrameAcquireAttempts; attempt++)
        {
            DwmFlush();

            if (duplication.AcquireNextFrame(FrameAcquireTimeout, out OutduplFrameInfo frameInfo, out desktopResource).Failure)
            {
                return false;
            }

            if (DxgiFrameAcquisitionHelper.IsUsableFrame(true, desktopResource != null, frameInfo.LastPresentTime))
            {
                return true;
            }

            duplication.ReleaseFrame();
            desktopResource?.Dispose();
            desktopResource = null;
        }

        return false;
    }

    private static void CopyOutputIntersection(
        SKBitmap destination,
        Rectangle captureRectangle,
        CapturedOutput capturedOutput,
        Rectangle outputBounds)
    {
        Rectangle intersection = Rectangle.Intersect(captureRectangle, outputBounds);
        if (intersection.IsEmpty)
        {
            return;
        }

        var sourceRect = new SKRectI(
            intersection.X - outputBounds.X,
            intersection.Y - outputBounds.Y,
            intersection.X - outputBounds.X + intersection.Width,
            intersection.Y - outputBounds.Y + intersection.Height);
        var destinationRect = new SKRectI(
            intersection.X - captureRectangle.X,
            intersection.Y - captureRectangle.Y,
            intersection.X - captureRectangle.X + intersection.Width,
            intersection.Y - captureRectangle.Y + intersection.Height);

        CopyToneMappedPixels(destination, destinationRect, capturedOutput.ToneMappedBitmap,
            capturedOutput.SdrReferenceBitmap, sourceRect);
    }

    internal static unsafe void CopyToneMappedPixels(
        SKBitmap destination,
        SKRectI destinationRectangle,
        SKBitmap toneMappedBitmap,
        SKBitmap sdrReferenceBitmap,
        SKRectI sourceRectangle)
    {
        if (destination.ColorType != SKColorType.Bgra8888)
        {
            return;
        }

        byte* destinationBase = (byte*)destination.GetPixels();
        byte* toneMappedBase = (byte*)toneMappedBitmap.GetPixels();
        byte* referenceBase = (byte*)sdrReferenceBitmap.GetPixels();
        int width = sourceRectangle.Width;
        int height = sourceRectangle.Height;

        for (int y = 0; y < height; y++)
        {
            int destinationY = destinationRectangle.Top + y;
            int sourceY = sourceRectangle.Top + y;
            if (destinationY < 0 || destinationY >= destination.Height || sourceY < 0 || sourceY >= toneMappedBitmap.Height)
            {
                continue;
            }

            byte* destinationRow = destinationBase + (destinationY * destination.RowBytes);
            byte* toneMappedRow = toneMappedBase + (sourceY * toneMappedBitmap.RowBytes);
            byte* referenceRow = referenceBase + (sourceY * sdrReferenceBitmap.RowBytes);

            for (int x = 0; x < width; x++)
            {
                int destinationX = destinationRectangle.Left + x;
                int sourceX = sourceRectangle.Left + x;
                if (destinationX < 0 || destinationX >= destination.Width || sourceX < 0 || sourceX >= toneMappedBitmap.Width)
                {
                    continue;
                }

                byte* destinationPixel = destinationRow + (destinationX * 4);
                byte* toneMappedPixel = toneMappedRow + (sourceX * 4);
                byte* referencePixel = referenceRow + (sourceX * 4);
                bool extendedRange = referencePixel[3] != 0;
                bool legacyCaptureMatchesSdr =
                    Math.Abs(destinationPixel[0] - referencePixel[0]) <= SdrReferenceTolerance &&
                    Math.Abs(destinationPixel[1] - referencePixel[1]) <= SdrReferenceTolerance &&
                    Math.Abs(destinationPixel[2] - referencePixel[2]) <= SdrReferenceTolerance;

                if (extendedRange || !legacyCaptureMatchesSdr)
                {
                    destinationPixel[0] = toneMappedPixel[0];
                    destinationPixel[1] = toneMappedPixel[1];
                    destinationPixel[2] = toneMappedPixel[2];
                    destinationPixel[3] = 255;
                }
            }
        }
    }

    private static Rectangle GetOutputBounds(OutputDescription1 description)
    {
        return Rectangle.FromLTRB(
            description.DesktopCoordinates.Left,
            description.DesktopCoordinates.Top,
            description.DesktopCoordinates.Right,
            description.DesktopCoordinates.Bottom);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private sealed class CapturedOutput : IDisposable
    {
        public SKBitmap ToneMappedBitmap { get; }
        public SKBitmap SdrReferenceBitmap { get; }

        public CapturedOutput(SKBitmap toneMappedBitmap, SKBitmap sdrReferenceBitmap)
        {
            ToneMappedBitmap = toneMappedBitmap;
            SdrReferenceBitmap = sdrReferenceBitmap;
        }

        public void Dispose()
        {
            ToneMappedBitmap.Dispose();
            SdrReferenceBitmap.Dispose();
        }
    }
}
