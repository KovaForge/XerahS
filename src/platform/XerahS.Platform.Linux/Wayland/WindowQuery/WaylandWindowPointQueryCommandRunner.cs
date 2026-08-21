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
using System.Text;

namespace XerahS.Platform.Linux.Wayland.WindowQuery;

internal static class WaylandWindowPointQueryCommandRunner
{
    private const int DefaultTimeoutMs = 1500;

    public static bool CommandExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (Path.IsPathRooted(fileName))
            return File.Exists(fileName);

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(segment, fileName);
            if (File.Exists(candidate))
                return true;
        }

        return false;
    }

    public static CommandRunResult Run(string fileName, string arguments, int timeoutMs = DefaultTimeoutMs)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
                return CommandRunResult.Failed("Process failed to start.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            bool completed = process.WaitForExit(timeoutMs);
            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort timeout cleanup.
                }

                return CommandRunResult.Failed($"Timed out after {timeoutMs} ms.");
            }

            Task.WaitAll(stdoutTask, stderrTask);
            return new CommandRunResult(
                Success: process.ExitCode == 0,
                StandardOutput: stdoutTask.Result,
                StandardError: stderrTask.Result,
                FailureReason: process.ExitCode == 0
                    ? null
                    : CreateFailureReason(process.ExitCode, stderrTask.Result));
        }
        catch (Exception ex)
        {
            return CommandRunResult.Failed(ex.Message);
        }
    }

    private static string CreateFailureReason(int exitCode, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return $"Exited with code {exitCode}.";

        var builder = new StringBuilder();
        builder.Append($"Exited with code {exitCode}: ");
        builder.Append(stderr.Trim());
        return builder.ToString();
    }
}

internal readonly record struct CommandRunResult(
    bool Success,
    string StandardOutput,
    string StandardError,
    string? FailureReason)
{
    public static CommandRunResult Failed(string failureReason)
    {
        return new CommandRunResult(false, string.Empty, string.Empty, failureReason);
    }
}
