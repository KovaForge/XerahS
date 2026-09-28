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

namespace XerahS.Platform.Abstractions;

/// <summary>What OmaSnap captures in host mode (XIP0088).</summary>
public enum OmaSnapCaptureTarget
{
    /// <summary>OmaSnap's smart overlay: region, window or monitor in one gesture.</summary>
    Smart,
    Region,
    Window,
    Fullscreen,
    Scroll
}

public enum OmaSnapOutcome
{
    Succeeded,
    Cancelled,
    Failed,
    Unavailable
}

/// <summary>A rectangle in global logical pixels.</summary>
public readonly record struct OmaSnapRegion(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;

    public override string ToString() => $"{X},{Y},{Width},{Height}";
}

public sealed record OmaSnapCaptureRequest(OmaSnapCaptureTarget Target)
{
    /// <summary>Capture this rectangle without an overlay (last region, configured region).</summary>
    public OmaSnapRegion? Region { get; init; }

    /// <summary>Open OmaSnap's editor after selection (off: return the capture as selected).</summary>
    public bool OpenEditor { get; init; }
}

public sealed record OmaSnapCaptureResult(OmaSnapOutcome Outcome)
{
    /// <summary>The PNG OmaSnap wrote. Owned by the caller until <see cref="IOmaSnapService.Release"/>.</summary>
    public string? ImagePath { get; init; }

    public string? WindowClass { get; init; }

    public string? WindowTitle { get; init; }

    public OmaSnapRegion? Region { get; init; }

    public double Scale { get; init; } = 1.0;

    public string? Monitor { get; init; }

    public bool Annotated { get; init; }

    public string? Error { get; init; }

    public static OmaSnapCaptureResult Unavailable(string reason) => new(OmaSnapOutcome.Unavailable) { Error = reason };
}

/// <summary>Cached result of the OmaSnap capability probe.</summary>
public sealed record OmaSnapStatus(bool IsAvailable, string? Version, string? BinaryPath, string Summary)
{
    public static OmaSnapStatus NotProbed { get; } = new(false, null, null, "OmaSnap: not checked yet");
}

/// <summary>
/// OmaSnap as XerahS's capture front end on Omarchy-like Hyprland sessions (XIP0088). Only the
/// Linux platform registers an implementation; everywhere else <see cref="PlatformServices.OmaSnap"/>
/// is null and nothing changes. OmaSnap only returns a PNG and metadata: the normal capture
/// pipeline does everything after that.
/// </summary>
public interface IOmaSnapService
{
    OmaSnapStatus Status { get; }

    /// <summary>
    /// True when a capture with this selector preference should go through OmaSnap: the user
    /// chose OmaSnap, or chose Automatic on an Omarchy-like session. Always false until the
    /// probe has passed.
    /// </summary>
    bool ShouldHandle(LinuxInteractiveRegionSelectorPreference preference);

    Task<OmaSnapCaptureResult> CaptureAsync(OmaSnapCaptureRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens <paramref name="imagePath"/> in OmaSnap's editor and returns the flattened result.</summary>
    Task<OmaSnapCaptureResult> AnnotateAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Pins the image on screen with OmaSnap. The pin's Upload button runs omaxerahs upload.</summary>
    Task<bool> PinAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>Deletes the runtime files behind a result once the pipeline has taken the image.</summary>
    void Release(OmaSnapCaptureResult result);
}
