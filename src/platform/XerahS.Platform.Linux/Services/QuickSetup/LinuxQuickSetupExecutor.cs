// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Runs the host-side Quick Setup script via the launcher (pkexec/run0),
// captures exit codes and surfaces the polkit/CLI failure messages.
// Modeled on CrossMacro's LinuxQuickSetupExecutor.
using System.Diagnostics;
using System.Text;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed record QuickSetupResult(bool Success, string Message);

internal sealed class LinuxQuickSetupExecutor
{
    private readonly Func<LinuxQuickSetupIdentity?> _identityResolver;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync;

    public LinuxQuickSetupExecutor()
        : this(LinuxQuickSetupIdentityResolver.Resolve, RunProcessAsync)
    {
    }

    internal LinuxQuickSetupExecutor(
        Func<LinuxQuickSetupIdentity?> identityResolver,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
    {
        _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
        _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));
    }

    public async Task<QuickSetupResult> RunAsync(
        IPrivilegedHostCommandLauncher launcher,
        LinuxQuickSetupScriptOptions scriptOptions,
        string logContext,
        string unexpectedFailureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        var identity = _identityResolver();
        if (identity is null)
        {
            return new QuickSetupResult(
                Success: false,
                Message: "Could not determine a valid host identity for session setup.");
        }

        var (isAvailable, failureMessage) = await launcher.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (!isAvailable)
        {
            return new QuickSetupResult(
                Success: false,
                Message: failureMessage);
        }

        var startInfo = launcher.CreateStartInfo(
            LinuxQuickSetupScriptBuilder.Build(scriptOptions),
            identity.Specifier);

        try
        {
            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (exitCode == 0)
            {
                XerahS.Common.DebugHelper.WriteLine($"[{logContext}] Session helper completed successfully for {identity.LogDisplay}");
                return new QuickSetupResult(
                    Success: true,
                    Message: BuildSuccessMessage(stdout, identity.Specifier));
            }

            string errorText = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? "Unknown host setup error.";
            XerahS.Common.DebugHelper.WriteLine($"[{logContext}] Session helper failed (ExitCode={exitCode}): {errorText}");
            return new QuickSetupResult(
                Success: false,
                Message: BuildFailureMessage(exitCode, errorText));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            XerahS.Common.DebugHelper.WriteException(ex, $"[{logContext}] Failed to run session helper command");
            return new QuickSetupResult(
                Success: false,
                Message: unexpectedFailureMessage);
        }
    }

    private static string? FirstNonEmptyLine(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        foreach (string raw in content.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0)
            {
                return line;
            }
        }

        return null;
    }

    private static string BuildSuccessMessage(string stdout, string identity)
    {
        string? counts = FirstNonEmptyLine(stdout);
        return counts is null
            ? $"Granted {identity} read access to input devices."
            : $"Granted {identity} input access ({counts}).";
    }

    private static string BuildFailureMessage(int exitCode, string errorText)
    {
        return exitCode switch
        {
            22 => $"setfacl is missing on the host ({errorText}). Install the 'acl' package and retry.",
            24 => $"uinput device is missing ({errorText}). Load the uinput kernel module and retry.",
            25 => $"No /dev/input/event* devices were found ({errorText}).",
            126 or 127 => $"Host privilege command refused authorization ({errorText}). Check that a polkit agent is running and that the user is in the wheel/administrators group.",
            _ => $"Quick Setup failed (exit {exitCode}): {errorText}",
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _ = stdoutBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _ = stderrBuilder.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start session helper process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // ignored — the kill may race with normal exit.
            }
        });

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }
}
