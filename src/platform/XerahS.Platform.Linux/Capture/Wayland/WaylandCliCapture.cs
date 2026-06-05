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

using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Wayland.WindowQuery;

namespace XerahS.Platform.Linux.Capture.Wayland;

/// <summary>
/// Wayland capture via grim, slurp, grimblast, hyprshot. Use only when Wayland is active.
/// </summary>
internal static class WaylandCliCapture
{
    public static bool IsWayland =>
        Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSlurpAvailable()
    {
        // No-stderr variant: `which` on a missing tool writes to stderr in some
        // shells. We do not redirect stderr here, so pipe-fill is not a concern.
        // `which` exits non-zero fast when the tool is missing, so the
        // WaitForExit(3000) is a safety net only.
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "slurp",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Spawn a CLI capture tool and capture (stdout, exitCode) with the
    /// LinuxScreenService v0.23.91 template: drains stderr asynchronously so a
    /// chatty child cannot fill the 64KB pipe buffer, reads stdout asynchronously
    /// so a child that sleeps without output cannot stretch the call beyond the
    /// configured timeout (anti-pattern B), and bounds the async drainers with a
    /// 1s wait after Kill() so they finish reading the (now-broken) pipes before
    /// process disposal. Returns (string.Empty, null) on timeout.
    /// </summary>
    internal static (string output, int? exitCode) RunCliCapture(
        string fileName, string arguments, int timeoutMs)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            process = Process.Start(startInfo);
            if (process == null) return (string.Empty, null);

            // Drain stderr asynchronously so a chatty child cannot fill the
            // 64KB pipe buffer. Discard the text — surface to the caller only
            // if the exit code is non-zero (future enhancement).
            var stderrDrain = process.StandardError.ReadToEndAsync()
                .ContinueWith(_ => { }, TaskScheduler.Default);

            // Read stdout ASYNCHRONOUSLY so a child that sleeps with no output
            // (anti-pattern B) cannot stretch the call beyond the configured
            // timeout. Task.WaitAny returns the index of the first completed
            // task; if the delay wins, we kill the child and return null.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var timeoutTask = Task.Delay(timeoutMs);

            if (Task.WaitAny(stdoutTask, timeoutTask) != 0)
            {
                // Timeout: kill the child, bounded wait for the async drainers.
                try { process.Kill(); } catch { /* best effort */ }
                try { Task.WaitAll(new Task[] { stdoutTask, stderrDrain }, 1000); }
                catch { /* drainer may have faulted on a closed stream */ }
                return (string.Empty, null);
            }

            // stdout closed within the timeout — the child has exited (or is
            // about to). Wait for exit to capture the exit code, with a
            // bounded follow-up for the case where stdout closed but the
            // process has not fully released.
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* best effort */ }
                process.WaitForExit(1000);
                return (stdoutTask.Result, null);
            }

            // Best-effort: ensure the stderr drainer is done before disposing
            // the process. Bounded so a stuck drainer cannot hang us.
            try { Task.WaitAll(new Task[] { stderrDrain }, 1000); }
            catch { /* ignore */ }

            return (stdoutTask.Result, process.ExitCode);
        }
        catch
        {
            return (string.Empty, null);
        }
        finally
        {
            // Manual dispose: the helper has multiple return paths and is not
            // a `using` block.
            process?.Dispose();
        }
    }

    /// <summary>Expose RunCliCapture for regression tests without a real CLI tool.</summary>
    internal static class TestAccessor
    {
        public static (string output, int? exitCode) RunCliCapture(
            string fileName, string arguments, int timeoutMs)
            => WaylandCliCapture.RunCliCapture(fileName, arguments, timeoutMs);
    }

    /// <summary>
    /// Region selection using slurp (coordinates only, no screenshot). For recording.
    /// </summary>
    public static async Task<SKRectI> SelectRegionWithSlurpAsync()
    {
        try
        {
            var (output, exitCode) = await Task.Run(() => RunCliCapture(
                "slurp", "-f \"%x %y %w %h\"", 60000)).ConfigureAwait(false);
            if (exitCode == null)
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: slurp timed out");
                return SKRectI.Empty;
            }
            if (exitCode != 0)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exited with code {exitCode} (likely cancelled)");
                return SKRectI.Empty;
            }
            output = output.Trim();
            DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp output: '{output}'");
            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                int.TryParse(parts[0], out int x) &&
                int.TryParse(parts[1], out int y) &&
                int.TryParse(parts[2], out int w) &&
                int.TryParse(parts[3], out int h))
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp region selected: x={x}, y={y}, w={w}, h={h}");
                return new SKRectI(x, y, x + w, y + h);
            }
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Failed to parse slurp output: '{output}'");
            return SKRectI.Empty;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: slurp exception: {ex.Message}");
            return SKRectI.Empty;
        }
    }

    /// <summary>
    /// Capture using Wayland CLI tools (grim/slurp/grimblast/hyprshot). Returns null if not Wayland.
    /// </summary>
    public static async Task<SKBitmap?> CaptureAsync(LinuxCaptureKind kind, string? desktop)
    {
        if (!IsWayland)
        {
            return null;
        }
        DebugHelper.WriteLine("LinuxScreenCaptureService: [Stage 3/4] Trying Wayland protocol/tool fallbacks");

        return kind switch
        {
            LinuxCaptureKind.Region => await CaptureRegionAsync(desktop).ConfigureAwait(false),
            LinuxCaptureKind.FullScreen => await CaptureWithGrimAsync().ConfigureAwait(false),
            LinuxCaptureKind.ActiveWindow => await CaptureActiveWindowAsync(desktop).ConfigureAwait(false),
            _ => null
        };
    }

    private static bool IsWlrootsDesktop(string? desktop) =>
        desktop == "HYPRLAND" || desktop == "SWAY";

    private static async Task<SKBitmap?> CaptureRegionAsync(string? desktop)
    {
        if (desktop == "HYPRLAND")
        {
            var r = await CaptureWithGrimblastRegionAsync().ConfigureAwait(false);
            if (r != null) return r;
            r = await CaptureWithHyprshotRegionAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        if (IsWlrootsDesktop(desktop) || desktop == null)
        {
            var r = await CaptureWithGrimSlurpAsync().ConfigureAwait(false);
            if (r != null) return r;
        }

        // KDE Plasma: use Spectacle's region capture mode.
        // This provides native rectangle selection, unlike the XDG Portal Screenshot dialog
        // which may only offer full-screen capture on some KDE versions.
        if (desktop is "KDE")
        {
            var r = await CaptureWithSpectacleRegionAsync().ConfigureAwait(false);
            if (r != null) return r;
        }

        // GNOME: gnome-screenshot provides area selection natively.
        if (desktop is "GNOME")
        {
            var r = await CaptureWithGnomeScreenshotAreaAsync().ConfigureAwait(false);
            if (r != null) return r;
        }

        return null;
    }

    private static async Task<SKBitmap?> CaptureActiveWindowAsync(string? desktop)
    {
        if (desktop == "HYPRLAND")
        {
            var r = await CaptureWithGrimblastActiveWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
            r = await CaptureWithHyprshotWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        if (IsWlrootsDesktop(desktop) || desktop == null)
        {
            // grimblast "save active" works on SWAY/wlroots in addition to Hyprland.
            // Fall back to a focused-window geometry query so we still get a single
            // window capture when grimblast is missing. The previous implementation
            // fell through to CaptureWithGrimSlurpAsync which runs `slurp` with no
            // arguments, prompting the user to draw a region with the mouse — the
            // wrong UX for an active-window capture.
            var r = await CaptureWithGrimblastActiveWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
            r = await CaptureWithSwayFocusedWindowAsync().ConfigureAwait(false);
            if (r != null) return r;
        }
        return null;
    }

    private static async Task<SKBitmap?> CaptureWithGrimblastRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "grimblast", $"save area \"{tempFile}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimSlurpAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (slurpOutput, slurpExit) = await Task.Run(() => RunCliCapture(
                "slurp", string.Empty, 60000)).ConfigureAwait(false);
            if (slurpExit == null || slurpExit != 0) return null;
            var geometry = slurpOutput.Trim();
            if (string.IsNullOrEmpty(geometry)) return null;

            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "grim", $"-g \"{geometry}\" \"{tempFile}\"", 10000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim+slurp");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithHyprshotRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "hyprshot", $"-m region -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimblastActiveWindowAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "grimblast", $"save active \"{tempFile}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grimblast (active window)");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithHyprshotWindowAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "hyprshot", $"-m window -o \"{Path.GetDirectoryName(tempFile)}\" -f \"{Path.GetFileName(tempFile)}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with hyprshot (window)");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    /// <summary>
    /// Active-window capture fallback for wlroots compositors without grimblast
    /// active support. Queries swaymsg for the focused window geometry, then
    /// runs grim with the geometry expression. Returns null if swaymsg or grim
    /// are unavailable, or if the focused window cannot be determined.
    /// </summary>
    private static async Task<SKBitmap?> CaptureWithSwayFocusedWindowAsync()
    {
        string? geometry = await TryGetFocusedWindowGeometryAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(geometry))
        {
            DebugHelper.WriteLine("LinuxScreenCaptureService: swaymsg focused window query returned no geometry");
            return null;
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "grim", $"-g \"{geometry}\" \"{tempFile}\"", 10000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Screenshot captured with grim -g {geometry}");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<string?> TryGetFocusedWindowGeometryAsync()
    {
        // Reuse the same swaymsg invocation pattern as SwayWindowPointQueryHelper.
        // The two -r/-t get_tree modes are equivalent on modern Sway, but the
        // readable variant is preferred when supported.
        try
        {
            var (output, exitCode) = await Task.Run(() => RunCliCapture(
                "swaymsg", "-t get_tree -r", 10000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0) return null;
            return SwayWindowPointQueryHelper.TryGetFocusedWindowGeometryExpression(output, out string? geometry)
                ? geometry
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SKBitmap?> CaptureWithSpectacleRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            // spectacle --region: opens Spectacle's native rectangle selection UI.
            // --nonotify: suppresses the notification after capture.
            // --output: saves directly to the specified path.
            // --background: runs without showing the main Spectacle window.
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "spectacle", $"--region --nonotify --background --output \"{tempFile}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with spectacle --region");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGnomeScreenshotAreaAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            // gnome-screenshot -a: opens GNOME's native area selection crosshair.
            // -f: saves to the specified file path.
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "gnome-screenshot", $"-a -f \"{tempFile}\"", 60000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with gnome-screenshot -a");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static async Task<SKBitmap?> CaptureWithGrimAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_screenshot_{Guid.NewGuid():N}.png");
        try
        {
            var (_, exitCode) = await Task.Run(() => RunCliCapture(
                "grim", $"\"{tempFile}\"", 10000)).ConfigureAwait(false);
            if (exitCode == null || exitCode != 0 || !File.Exists(tempFile)) return null;
            DebugHelper.WriteLine("LinuxScreenCaptureService: Screenshot captured with grim");
            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        catch { return null; }
        finally { TryDelete(tempFile); }
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { File.Delete(path); } catch { }
    }
}
