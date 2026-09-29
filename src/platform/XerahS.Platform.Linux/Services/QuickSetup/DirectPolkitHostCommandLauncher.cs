// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Real launcher: probes the host for pkexec/run0 and builds a
// ProcessStartInfo that invokes /bin/sh -c with the inline script.
// Modeled on CrossMacro's DirectPolkitHostCommandLauncher.
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPolkitHostCommandLauncher : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExists;
    private readonly Func<CancellationToken, ValueTask<bool>> _pkexecIsUsable;
    private int _selectedKind;

    public DirectPolkitHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync, HostCommandProbe.PkexecIsUsableAsync)
    {
    }

    internal DirectPolkitHostCommandLauncher(
        Func<string, CancellationToken, ValueTask<bool>> commandExists,
        Func<CancellationToken, ValueTask<bool>> pkexecIsUsable)
    {
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
        _pkexecIsUsable = pkexecIsUsable ?? throw new ArgumentNullException(nameof(pkexecIsUsable));
    }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var (kind, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            _commandExists,
            _pkexecIsUsable,
            cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _selectedKind, (int)kind);

        return kind == HostPrivilegeKind.None
            ? (false, failureMessage)
            : (true, string.Empty);
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, string userIdentity)
    {
        var kind = (HostPrivilegeKind)Volatile.Read(ref _selectedKind);
        var startInfo = new ProcessStartInfo
        {
            FileName = HostPrivilegeCommand.GetFileName(kind),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        HostPrivilegeCommand.AddArguments(
            startInfo,
            kind,
            hostScript,
            userIdentity,
            "xerahs-quick-setup");

        return startInfo;
    }
}
