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
using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Capture.OmaSnap;

/// <summary>
/// Talks to one OmaSnap binary through the host-mode contract (XIP0088 Phase 1): capture or edit,
/// write the PNG where XerahS says, report one JSON object. Output files live in a private runtime
/// folder and are deleted by <see cref="Release"/> once the pipeline has taken the image.
/// </summary>
internal sealed class OmaSnapClient
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    public const string HostUploadCommandVariable = "OMASNAP_HOST_UPLOAD_COMMAND";

    private readonly Func<string> _runtimeFolder;

    public OmaSnapClient(string executablePath, Func<string>? runtimeFolder = null)
    {
        ExecutablePath = executablePath;
        _runtimeFolder = runtimeFolder ?? OmaSnapRuntimeFolder.Ensure;
    }

    public string ExecutablePath { get; }

    public async Task<OmaSnapCapabilities> ProbeAsync(CancellationToken cancellationToken = default)
    {
        OmaSnapProcessResult result;
        try
        {
            result = await OmaSnapProcessRunner.RunAsync(
                ExecutablePath, OmaSnapArguments.Probe(), ProbeTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OmaSnapCapabilities.Unusable($"could not run {ExecutablePath} ({ex.Message})");
        }

        if (result.TimedOut)
        {
            return OmaSnapCapabilities.Unusable($"probe timed out after {ProbeTimeout.TotalSeconds:0}s");
        }

        string json = result.StandardOutput.Trim();
        if (json.Length == 0)
        {
            // Builds without host mode reject --host-capabilities: exactly the "too old" case.
            return OmaSnapCapabilities.Unusable($"no host mode (exit {result.ExitCode}; OmaSnap 1.22.0 or newer is required)");
        }

        OmaSnapCapabilities capabilities = OmaSnapCapabilities.Parse(json);
        if (result.ExitCode != 0 && capabilities.FailureReason == null && capabilities.Ok)
        {
            return OmaSnapCapabilities.Unusable($"probe exited with code {result.ExitCode}", json);
        }

        return capabilities;
    }

    public Task<HostedCaptureResult> CaptureAsync(HostedCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return RunHostedAsync(
            (output, resultJson) => OmaSnapArguments.Capture(request, output, resultJson),
            environment: null,
            cancellationToken);
    }

    public Task<HostedCaptureResult> AnnotateAsync(string inputPath, string editor, CancellationToken cancellationToken = default)
    {
        return RunHostedAsync(
            (output, resultJson) => OmaSnapArguments.Annotate(inputPath, output, resultJson, editor),
            environment: null,
            cancellationToken);
    }

    /// <summary>
    /// Starts a floating pin and returns once the process is running; the pin lives until the user
    /// closes it. With <paramref name="uploadCommand"/>, the pin's Upload button runs that command.
    /// Without it, hosted pins hide their upload control.
    /// </summary>
    public bool StartPin(string imagePath, IReadOnlyList<string>? uploadCommand)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        foreach (string argument in OmaSnapArguments.Pin(imagePath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (uploadCommand is { Count: > 0 })
        {
            startInfo.Environment[HostUploadCommandVariable] = FormatCommand(uploadCommand);
        }
        else
        {
            startInfo.Environment.Remove(HostUploadCommandVariable);
        }

        try
        {
            using Process? process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"OmaSnap: could not start pin ({ex.Message}).");
            return false;
        }
    }

    /// <summary>Deletes result files that live in the runtime folder. Never touches other paths.</summary>
    public void Release(HostedCaptureResult result)
    {
        if (string.IsNullOrEmpty(result.ImagePath))
        {
            return;
        }

        TryDeleteInRuntimeFolder(result.ImagePath);
    }

    /// <summary>
    /// Joins argv for <c>OMASNAP_HOST_UPLOAD_COMMAND</c>, which OmaSnap splits with shell-like
    /// quoting rules (no shell is involved). Arguments with spaces or quotes are double-quoted.
    /// </summary>
    internal static string FormatCommand(IReadOnlyList<string> argv)
    {
        var builder = new StringBuilder();
        foreach (string argument in argv)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            bool needsQuotes = argument.Length == 0 || argument.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '\\');
            if (!needsQuotes)
            {
                builder.Append(argument);
                continue;
            }

            builder.Append('"');
            foreach (char c in argument)
            {
                if (c is '"' or '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        return builder.ToString();
    }

    private async Task<HostedCaptureResult> RunHostedAsync(
        Func<string, string, IReadOnlyList<string>> buildArguments,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        string folder;
        try
        {
            folder = _runtimeFolder();
        }
        catch (Exception ex)
        {
            return HostedCaptureResult.Failed($"Cannot create the OmaSnap runtime folder ({ex.Message}).");
        }

        string id = Guid.NewGuid().ToString("N");
        string outputPath = Path.Combine(folder, $"cap-{id}.png");
        string resultPath = Path.Combine(folder, $"cap-{id}.json");
        IReadOnlyList<string> arguments = buildArguments(outputPath, resultPath);

        OmaSnapProcessResult process;
        try
        {
            // Captures wait for the user: no timeout. Cancellation kills the process tree.
            process = await OmaSnapProcessRunner.RunAsync(ExecutablePath, arguments, timeout: null, environment, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryDelete(outputPath);
            TryDelete(resultPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(outputPath);
            TryDelete(resultPath);
            return HostedCaptureResult.Failed($"Could not run OmaSnap ({ex.Message}).");
        }

        string? json = null;
        try
        {
            if (File.Exists(resultPath))
            {
                json = await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            json = null;
        }
        finally
        {
            TryDelete(resultPath);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            json = process.StandardOutput.Trim();
        }

        HostedCaptureResult result = OmaSnapResultParser.Parse(json, process.ExitCode, outputPath);
        if (!result.IsOk)
        {
            TryDelete(outputPath);
            if (result.Status == HostedCaptureStatus.Failed && !string.IsNullOrWhiteSpace(process.StandardError))
            {
                DebugHelper.WriteLine($"OmaSnap stderr: {process.StandardError.Trim()}");
            }
        }

        return result;
    }

    private void TryDeleteInRuntimeFolder(string path)
    {
        try
        {
            string folder = Path.GetFullPath(_runtimeFolder());
            string full = Path.GetFullPath(path);
            if (full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                TryDelete(full);
            }
        }
        catch
        {
            // Cleanup is best effort.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort.
        }
    }
}
