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

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputDuplicationPolicy
{
    /// <summary>
    /// Vortice's DuplicateOutput1 marshaller can access-violate (0xC0000005) on Windows
    /// ARM64 GPU drivers instead of returning DXGI_ERROR. That cannot be caught in .NET,
    /// so region capture must never call it there.
    /// </summary>
    internal static bool ShouldUseDuplicateOutput1(Architecture architecture) =>
        architecture is Architecture.X64 or Architecture.X86;
}
