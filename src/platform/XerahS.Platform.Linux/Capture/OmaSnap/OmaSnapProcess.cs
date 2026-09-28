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

namespace XerahS.Platform.Linux.Capture.OmaSnap;

internal sealed record OmaSnapProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool Cancelled);

/// <summary>
/// Runs omasnap with <see cref="ProcessStartInfo.ArgumentList"/> (never a shell string), reads
/// both streams asynchronously and kills the whole process tree on cancellation or timeout.
/// </summary>
internal static class OmaSnapProcess
{
    public static async Task<OmaSnapProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment != null)
        {
            foreach (var (key, value) in environment)
            {
                if (value == null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new OmaSnapProcessResult(-1, string.Empty, "Process did not start.", TimedOut: false, Cancelled: false);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        bool timedOut = false;
        bool cancelled = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            cancelled = !timedOut;
            Kill(process);
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

        string output = await ReadOrEmptyAsync(stdout).ConfigureAwait(false);
        string error = await ReadOrEmptyAsync(stderr).ConfigureAwait(false);
        int exitCode = process.HasExited ? process.ExitCode : -1;
        return new OmaSnapProcessResult(exitCode, output, error, timedOut, cancelled);
    }

    /// <summary>Starts a long-lived omasnap (a pin) without waiting for it; the exit is reaped in the background.</summary>
    public static bool StartDetached(string fileName, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string?>? environment)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment != null)
        {
            foreach (var (key, value) in environment)
            {
                if (value == null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        var process = Process.Start(startInfo);
        if (process == null)
        {
            return false;
        }

        _ = process.WaitForExitAsync().ContinueWith(_ => process.Dispose(), TaskScheduler.Default);
        return true;
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static async Task<string> ReadOrEmptyAsync(Task<string> read)
    {
        try
        {
            return await read.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
