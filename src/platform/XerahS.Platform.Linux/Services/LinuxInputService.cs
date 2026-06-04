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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Basic cursor position retrieval using xdotool (X11) or wl-paste fallback.
/// </summary>
public sealed class LinuxInputService : IInputService
{
    public Point GetCursorPosition()
    {
        // Prefer xdotool which is widely available on X11
        if (TryGetWithXdotool(out var point))
            return point;

        return Point.Empty;
    }

    private static bool TryGetWithXdotool(out Point point)
    {
        point = Point.Empty;

        var (output, exitCode) = RunXdotoolCapture("xdotool", "getmouselocation --shell", 500);
        if (exitCode == null || exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return false;

        int x = 0, y = 0;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("X=", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line.Substring(2), out x);
            else if (line.StartsWith("Y=", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line.Substring(2), out y);
        }

        point = new Point(x, y);
        return true;
    }

    /// <summary>
    /// Spawns an xdotool (or other POSIX CLI) child process and captures stdout
    /// safely, draining stderr asynchronously and bounding the stdout read
    /// with <see cref="Task.WaitAny(Task[])"/> so a chatty child cannot fill
    /// the 64KB OS pipe buffer (pipe-fill deadlock) and a sleeping child
    /// cannot stretch the call past the configured timeout. Returns
    /// <c>(string.Empty, null)</c> on timeout or spawn failure.
    /// Mirrors the LinuxScreenService v0.23.91, LinuxThemeService v0.23.92,
    /// and PulseAudioHelper v0.23.93 templates.
    /// </summary>
    internal static (string Output, int? ExitCode) RunXdotoolCapture(
        string fileName, string arguments, int timeoutMs)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = Process.Start(startInfo);
            if (process == null) return (string.Empty, null);

            // Drain stderr asynchronously so a chatty child cannot block on
            // its own write to a full pipe. Discard the text — caller can
            // surface it via the log if needed.
            var stderrDrain = process.StandardError.ReadToEndAsync()
                .ContinueWith(_ => { }, TaskScheduler.Default);

            // Read stdout ASYNCHRONOUSLY so a child that sleeps without
            // producing output (anti-pattern B) cannot stretch the call
            // beyond the timeout.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var timeoutTask = Task.Delay(timeoutMs);

            if (Task.WaitAny(stdoutTask, timeoutTask) != 0)
            {
                try { process.Kill(); } catch { /* best effort */ }
                try { Task.WaitAll(new Task[] { stdoutTask, stderrDrain }, 1000); }
                catch { /* drainer may have faulted on a closed stream */ }
                return (string.Empty, null);
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* best effort */ }
                process.WaitForExit(1000);
                return (stdoutTask.Result, null);
            }

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
            process?.Dispose();
        }
    }

    /// <summary>
    /// Test accessor exposing the run helper to the XerahS.Tests assembly
    /// so regression tests can drive synthetic <c>/bin/sh</c> commands
    /// without requiring a real <c>xdotool</c> binary.
    /// </summary>
    internal static class TestAccessor
    {
        public static (string Output, int? ExitCode) RunXdotoolCapture(
            string fileName, string arguments, int timeoutMs)
            => LinuxInputService.RunXdotoolCapture(fileName, arguments, timeoutMs);
    }
}
