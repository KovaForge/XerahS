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

using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Tasks.Processors;

/// <summary>
/// OmaSnap annotation editor and pins on Hyprland (XIP0088 Phase 4). Every method reports whether
/// it handled the request; when it did not, callers keep the XerahS editor and pin window.
/// </summary>
public static class HostedEditorAndPinService
{
    /// <summary>Test seam; defaults to the platform engine.</summary>
    internal static Func<IHostedCaptureEngine?> EngineProvider { get; set; } = () => PlatformServices.HostedCaptureEngine;

    /// <summary>Test seam; defaults to the running OS.</summary>
    internal static Func<bool> IsLinux { get; set; } = OperatingSystem.IsLinux;

    internal static bool ShouldUseOmaSnapEditor(LinuxAnnotationEditor setting, IHostedCaptureEngine? engine) =>
        setting == LinuxAnnotationEditor.OmaSnap && engine?.CurrentStatus.Available == true;

    /// <summary>
    /// Runs the OmaSnap editor when selected and available. Returns (true, image) when OmaSnap
    /// handled it: a new annotated bitmap, or null when the user pressed Esc (keep the original).
    /// Returns (false, null) to fall back to the XerahS editor.
    /// </summary>
    public static async Task<(bool Handled, SKBitmap? Annotated)> TryAnnotateAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        IHostedCaptureEngine? engine = EngineProvider();
        if (!IsLinux() || !ShouldUseOmaSnapEditor(SettingsManager.Settings?.LinuxAnnotationEditor ?? LinuxAnnotationEditor.XerahS, engine))
        {
            return (false, null);
        }

        string? inputPath = WriteTempPng(image, "annotate");
        if (inputPath == null)
        {
            return (false, null);
        }

        try
        {
            HostedCaptureResult result = await engine!.AnnotateAsync(inputPath, "overlay", cancellationToken).ConfigureAwait(false);
            try
            {
                switch (result.Status)
                {
                    case HostedCaptureStatus.Ok when result.ImagePath != null:
                        SKBitmap? annotated = SKBitmap.Decode(result.ImagePath);
                        return annotated != null ? (true, annotated) : (false, null);
                    case HostedCaptureStatus.Cancelled:
                        DebugHelper.WriteLine("OmaSnap editor: cancelled; keeping the original capture.");
                        return (true, null);
                    default:
                        DebugHelper.WriteLine($"OmaSnap editor: {result.Status} ({result.Error}); using the XerahS editor.");
                        return (false, null);
                }
            }
            finally
            {
                engine.Release(result);
            }
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    /// <summary>
    /// Pins through OmaSnap on Omarchy-like systems. The pin's Upload button runs
    /// <c>omaxerahs upload --url-only</c>, so uploads go to the user's XerahS destination.
    /// </summary>
    public static async Task<bool> TryPinAsync(SKBitmap image, CancellationToken cancellationToken = default)
    {
        IHostedCaptureEngine? engine = EngineProvider();
        if (!IsLinux() || engine?.CurrentStatus.Available != true || PlatformServices.DesktopProfile?.IsOmarchyLike != true)
        {
            return false;
        }

        string? path = WriteTempPng(image, "pin");
        if (path == null)
        {
            return false;
        }

        try
        {
            return await engine.PinAsync(path, GetHostUploadCommand(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>Pins an existing image file (PinToScreenFromFile).</summary>
    public static async Task<bool> TryPinFileAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        IHostedCaptureEngine? engine = EngineProvider();
        if (!IsLinux() || engine?.CurrentStatus.Available != true || PlatformServices.DesktopProfile?.IsOmarchyLike != true ||
            !File.Exists(imagePath))
        {
            return false;
        }

        return await engine.PinAsync(imagePath, GetHostUploadCommand(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The bundled omaxerahs next to XerahS, or null when it is not shipped.</summary>
    internal static IReadOnlyList<string>? GetHostUploadCommand(string? baseDirectory = null)
    {
        string omaxerahs = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "omaxerahs");
        return File.Exists(omaxerahs) ? [omaxerahs, "upload", "--url-only"] : null;
    }

    private static string? WriteTempPng(SKBitmap image, string prefix)
    {
        try
        {
            string folder = Path.Combine(Path.GetTempPath(), $"xerahs-{Environment.UserName}-omasnap");
            Directory.CreateDirectory(folder);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            string path = Path.Combine(folder, $"{prefix}-{Guid.NewGuid():N}.png");
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(path);
            data.SaveTo(stream);
            return path;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"OmaSnap: cannot write temporary PNG ({ex.Message}).");
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
