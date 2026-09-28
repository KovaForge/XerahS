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

namespace XerahS.UI.Services;

/// <summary>
/// XIP0088: on Omarchy-like sessions pins are OmaSnap pins (<c>omasnap --pin</c>), whose Upload
/// button runs <c>omaxerahs upload</c> so it uses the user's XerahS destination. Elsewhere, or
/// when OmaSnap cannot start, the XerahS pin window is used. PNG encoding and process start run
/// off the UI thread.
/// </summary>
internal static class OmaSnapPinRouter
{
    public static bool ShouldUseOmaSnap()
    {
        return OperatingSystem.IsLinux() &&
               PlatformServices.OmaSnap?.ShouldHandle(LinuxInteractiveRegionSelectorPreference.Automatic) == true;
    }

    /// <summary>Pins a copy of <paramref name="bitmap"/>; calls <paramref name="fallback"/> with that copy if OmaSnap fails.</summary>
    public static void Pin(SKBitmap bitmap, Action<SKBitmap> fallback)
    {
        SKBitmap? copy = bitmap.Copy();
        if (copy == null)
        {
            DebugHelper.WriteLine("OmaSnap pin: could not copy the image.");
            return;
        }

        _ = Task.Run(async () =>
        {
            bool pinned = false;
            try
            {
                string path = CreatePinPath();
                using (SKData data = copy.Encode(SKEncodedImageFormat.Png, 100))
                await using (FileStream stream = File.Create(path))
                {
                    data.SaveTo(stream);
                }

                pinned = await PinFileCoreAsync(path).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DebugHelper.WriteLine($"OmaSnap pin: {ex.Message}");
            }

            if (pinned)
            {
                copy.Dispose();
            }
            else
            {
                fallback(copy);
            }
        });
    }

    /// <summary>Pins an existing image file with OmaSnap. Returns false to use the XerahS pin window.</summary>
    public static Task<bool> PinFileAsync(string path) => PinFileCoreAsync(path);

    private static async Task<bool> PinFileCoreAsync(string path)
    {
        IOmaSnapService? omaSnap = PlatformServices.OmaSnap;
        if (omaSnap == null)
        {
            return false;
        }

        bool pinned = await omaSnap.PinAsync(path).ConfigureAwait(false);
        DebugHelper.WriteLine(pinned ? $"OmaSnap pin: pinned {path}." : "OmaSnap pin: could not start; using the XerahS pin window.");
        return pinned;
    }

    /// <summary>Pin images live under $XDG_RUNTIME_DIR, which the session clears at logout.</summary>
    private static string CreatePinPath()
    {
        string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        string root = !string.IsNullOrWhiteSpace(runtime) && Path.IsPathRooted(runtime)
            ? Path.Combine(runtime, "xerahs", "pins")
            : Path.Combine(Path.GetTempPath(), $"xerahs-{Environment.UserName}", "pins");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"pin-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png");
    }
}
