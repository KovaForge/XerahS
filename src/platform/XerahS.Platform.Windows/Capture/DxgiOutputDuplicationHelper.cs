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

using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputDuplicationHelper
{
    private static readonly Format[] PreferredFormats =
    [
        Format.R16G16B16A16_Float,
        Format.R10G10B10A2_UNorm,
        Format.B8G8R8A8_UNorm
    ];

    /// <summary>
    /// Vortice's DuplicateOutput1 marshaller can access-violate (0xC0000005) on Windows
    /// ARM64 GPU drivers instead of returning DXGI_ERROR. That cannot be caught in .NET,
    /// so region capture must never call it there.
    /// </summary>
    internal static bool ShouldUseDuplicateOutput1(Architecture architecture) =>
        architecture is Architecture.X64 or Architecture.X86;

    public static IDXGIOutputDuplication Create(IDXGIOutput output, ID3D11Device device)
    {
        if (ShouldUseDuplicateOutput1(RuntimeInformation.ProcessArchitecture))
        {
            try
            {
                using var output5 = output.QueryInterface<IDXGIOutput5>();
                return output5.DuplicateOutput1(device, 0, PreferredFormats);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"DxgiOutputDuplicationHelper: DuplicateOutput1 failed, using DuplicateOutput. {ex.Message}");
            }
        }
        else
        {
            DebugHelper.WriteLine(
                $"DxgiOutputDuplicationHelper: Skipping DuplicateOutput1 on {RuntimeInformation.ProcessArchitecture}; using DuplicateOutput.");
        }

        using var output1 = output.QueryInterface<IDXGIOutput1>();
        return output1.DuplicateOutput(device);
    }
}
