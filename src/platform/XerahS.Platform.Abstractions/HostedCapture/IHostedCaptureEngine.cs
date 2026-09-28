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

namespace XerahS.Platform.Abstractions;

/// <summary>What a hosted capture engine (OmaSnap on Hyprland, XIP0088) should capture.</summary>
public enum HostedCaptureTarget
{
    /// <summary>Smart selection: drag a region or click a window or monitor.</summary>
    Smart,
    Region,
    Window,
    Fullscreen,
    Scroll
}

public enum HostedCaptureStatus
{
    Ok,
    Cancelled,
    Failed,

    /// <summary>The engine is not usable in this session; the caller must use its normal path.</summary>
    Unavailable
}

public sealed record HostedCaptureRequest(HostedCaptureTarget Target)
{
    /// <summary>Preselected region in logical desktop coordinates (for LastRegion / configured regions).</summary>
    public Rectangle? Region { get; init; }

    /// <summary>Open the engine's annotation editor before returning ("overlay" or "window").</summary>
    public string? Editor { get; init; }

    public bool DisableRecents { get; init; }
}

/// <summary>Result of a hosted capture. <see cref="ImagePath"/> is owned by the engine until released.</summary>
public sealed class HostedCaptureResult
{
    public HostedCaptureStatus Status { get; init; }
    public string? ImagePath { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public double Scale { get; init; } = 1.0;
    public string? Monitor { get; init; }
    public Rectangle? Region { get; init; }
    public string? WindowClass { get; init; }
    public string? WindowTitle { get; init; }
    public bool Annotated { get; init; }
    public string? EngineVersion { get; init; }
    public string? Error { get; init; }

    public bool IsOk => Status == HostedCaptureStatus.Ok && !string.IsNullOrEmpty(ImagePath);

    public static HostedCaptureResult Unavailable(string reason) =>
        new() { Status = HostedCaptureStatus.Unavailable, Error = reason };

    public static HostedCaptureResult Failed(string error) =>
        new() { Status = HostedCaptureStatus.Failed, Error = error };

    public static HostedCaptureResult Cancelled() =>
        new() { Status = HostedCaptureStatus.Cancelled };
}

/// <summary>Snapshot of whether a hosted capture engine can be used and why.</summary>
public sealed record HostedCaptureEngineStatus(
    bool Available,
    string EngineId,
    string? Version,
    string Summary,
    string? ProbeJson = null)
{
    public static HostedCaptureEngineStatus NotAvailable(string engineId, string summary) =>
        new(false, engineId, null, summary);
}

/// <summary>
/// A native capture front end that returns a PNG plus metadata and performs no copy, save,
/// upload or history side effects itself. XerahS feeds the result into its normal pipeline.
/// </summary>
public interface IHostedCaptureEngine
{
    string EngineId { get; }

    /// <summary>Cached status; never blocks. "Not probed yet" reports unavailable.</summary>
    HostedCaptureEngineStatus CurrentStatus { get; }

    /// <summary>Completes the capability probe if needed (bounded by a timeout) and returns the status.</summary>
    Task<HostedCaptureEngineStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs an interactive or preselected capture. Waits for the user; cancel kills the engine.</summary>
    Task<HostedCaptureResult> CaptureAsync(HostedCaptureRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens the engine's annotation editor on an existing PNG.</summary>
    Task<HostedCaptureResult> AnnotateAsync(string imagePath, string editor = "overlay", CancellationToken cancellationToken = default);

    /// <summary>Shows a floating pin of the image. The pin's Upload control uses <paramref name="uploadCommand"/> when given.</summary>
    Task<bool> PinAsync(string imagePath, IReadOnlyList<string>? uploadCommand = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes runtime files that belong to a result once the pipeline has taken the image.</summary>
    void Release(HostedCaptureResult result);
}
