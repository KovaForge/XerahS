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
using XerahS.Common;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Helper for detecting PulseAudio/PipeWire audio sources used for capture.
/// </summary>
internal static class PulseAudioHelper
{
    /// <summary>
    /// Returns the monitor source for the default audio output sink.
    /// This captures what the user is actually hearing.
    /// Falls back to "default.monitor" if pactl is unavailable.
    /// </summary>
    public static string GetDefaultMonitorSource()
    {
        try
        {
            // First, get the default sink name
            string? defaultSink = RunPactl("get-default-sink");
            if (!string.IsNullOrWhiteSpace(defaultSink))
            {
                string monitorName = defaultSink.Trim() + ".monitor";

                // Verify this monitor source actually exists
                if (IsSourceAvailable(monitorName))
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using default sink monitor: {monitorName}");
                    return monitorName;
                }

                DebugHelper.WriteLine($"[PulseAudioHelper] Default sink monitor '{monitorName}' not found in sources, searching alternatives");
            }

            // Fallback: find the first RUNNING or IDLE .monitor source
            string? sourcesOutput = RunPactl("list sources short");
            if (!string.IsNullOrEmpty(sourcesOutput))
            {
                string? runningMonitor = null;
                string? anyMonitor = null;

                foreach (string line in sourcesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        string sourceName = parts[1].Trim();
                        if (!sourceName.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase))
                            continue;

                        anyMonitor ??= sourceName;

                        // Prefer RUNNING sources (actively playing audio)
                        if (parts.Length >= 5 && parts[4].Trim().Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
                        {
                            runningMonitor ??= sourceName;
                        }
                    }
                }

                if (runningMonitor != null)
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using running monitor source: {runningMonitor}");
                    return runningMonitor;
                }

                if (anyMonitor != null)
                {
                    DebugHelper.WriteLine($"[PulseAudioHelper] Using first available monitor source: {anyMonitor}");
                    return anyMonitor;
                }
            }

            DebugHelper.WriteLine("[PulseAudioHelper] No monitor source found, falling back to default.monitor");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PulseAudioHelper] Error querying pactl: {ex.Message}, falling back to default.monitor");
        }

        return "default.monitor";
    }

    /// <summary>
    /// Returns all available audio sources with their display names.
    /// Useful for populating a source selector UI.
    /// </summary>
    public static List<AudioSourceInfo> GetAvailableSources()
    {
        var sources = new List<AudioSourceInfo>();

        try
        {
            string? output = RunPactl("list sources short");
            if (string.IsNullOrEmpty(output)) return sources;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    string name = parts[1].Trim();
                    bool isMonitor = name.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase);
                    string state = parts.Length >= 5 ? parts[4].Trim() : "UNKNOWN";

                    sources.Add(new AudioSourceInfo
                    {
                        DeviceName = name,
                        IsMonitor = isMonitor,
                        State = state
                    });
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[PulseAudioHelper] Error listing sources: {ex.Message}");
        }

        return sources;
    }

    /// <summary>
    /// Checks whether a given PulseAudio source name exists and is available.
    /// </summary>
    public static bool IsSourceAvailable(string sourceName)
    {
        try
        {
            string? output = RunPactl("list sources short");
            if (string.IsNullOrEmpty(output)) return false;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2 && parts[1].Trim().Equals(sourceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
            // pactl not available
        }

        return false;
    }

    private static string? RunPactl(string arguments)
    {
        var (output, exitCode) = RunPactlCapture("pactl", arguments, 3000);
        if (exitCode == null)
        {
            return null;
        }

        return output;
    }

    /// <summary>
    /// Spawns a pactl (or other POSIX CLI) child process and captures stdout
    /// safely, draining stderr asynchronously and bounding the stdout read
    /// with <see cref="Task.WaitAny(Task[])"/> so a chatty child cannot fill
    /// the 64KB OS pipe buffer (pipe-fill deadlock) and a sleeping child
    /// cannot stretch the call past the configured timeout.
    /// Returns <c>(string.Empty, null)</c> on timeout or spawn failure.
    /// </summary>
    internal static (string Output, int? ExitCode) RunPactlCapture(
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
    /// without requiring a real <c>pactl</c> binary.
    /// </summary>
    internal static class TestAccessor
    {
        public static (string Output, int? ExitCode) RunPactlCapture(
            string fileName, string arguments, int timeoutMs)
            => PulseAudioHelper.RunPactlCapture(fileName, arguments, timeoutMs);
    }
}

/// <summary>
/// Represents an available PulseAudio/PipeWire audio source.
/// </summary>
internal class AudioSourceInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public bool IsMonitor { get; set; }
    public string State { get; set; } = string.Empty;
}
