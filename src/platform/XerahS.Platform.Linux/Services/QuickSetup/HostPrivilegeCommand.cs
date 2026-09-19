// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Pattern modeled on CrossMacro's HostPrivilegeCommand
// (src/CrossMacro.Platform.Linux/Services/QuickSetup/HostPrivilegeCommand.cs,
// Apache-2.0), rewritten for XerahS conventions and trimmed to just the
// host-side launcher selection. No code copied verbatim.
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal enum HostPrivilegeKind
{
    None,
    Pkexec,
    Run0,
}

internal static class HostPrivilegeCommand
{
    /// <summary>
    /// Select the host-side privilege-escalation command used to run the
    /// Quick Setup script. Preference order: pkexec (setuid root) > run0
    /// (systemd 256+) > None. Either is enough for `setfacl` against the
    /// current user's identity; polkit handles the auth dialog.
    /// </summary>
    public static async ValueTask<(HostPrivilegeKind Kind, string FailureMessage)> SelectAsync(
        Func<string, CancellationToken, ValueTask<bool>> commandExists,
        Func<CancellationToken, ValueTask<bool>> pkexecIsUsable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandExists);
        ArgumentNullException.ThrowIfNull(pkexecIsUsable);

        bool hasPkexec = await commandExists("pkexec", cancellationToken).ConfigureAwait(false);
        if (hasPkexec && await pkexecIsUsable(cancellationToken).ConfigureAwait(false))
        {
            return (HostPrivilegeKind.Pkexec, string.Empty);
        }

        if (await commandExists("run0", cancellationToken).ConfigureAwait(false))
        {
            return (HostPrivilegeKind.Run0, string.Empty);
        }

        return (HostPrivilegeKind.None, hasPkexec
            ? "pkexec is installed but its setuid-root wrapper is disabled, and systemd run0 is unavailable. Enable pkexec (restore mode 4755 on /usr/bin/pkexec) or install systemd 256+ and retry."
            : "Neither pkexec nor systemd run0 is available on the host. Install polkit or systemd 256+ and retry.");
    }

    /// <summary>
    /// Append the launcher-specific args to a <see cref="ProcessStartInfo"/>
    /// so that the embedded shell script runs with the privilege-escalation
    /// tool's auth flow. <paramref name="hostScript"/> is the inline script
    /// body that <c>/bin/sh -c</c> will run; <paramref name="identity"/>
    /// provides the username to grant ACL to.
    /// </summary>
    public static void AddArguments(
        ProcessStartInfo startInfo,
        HostPrivilegeKind commandKind,
        string hostScript,
        string userIdentity,
        string helperName)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostScript);
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(helperName);

        if (commandKind == HostPrivilegeKind.Run0)
        {
            // run0 (systemd 256+) takes --description for the auth prompt.
            startInfo.ArgumentList.Add("--description=XerahS temporary input setup");
        }
        else if (commandKind != HostPrivilegeKind.Pkexec)
        {
            throw new InvalidOperationException("No host privilege command was selected.");
        }

        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add(helperName);     // $0
        startInfo.ArgumentList.Add(userIdentity);   // $1
    }

    public static string GetFileName(HostPrivilegeKind commandKind) => commandKind switch
    {
        HostPrivilegeKind.Pkexec => "pkexec",
        HostPrivilegeKind.Run0 => "run0",
        HostPrivilegeKind.None => throw new InvalidOperationException("No host privilege command was selected."),
        _ => throw new InvalidOperationException("No host privilege command was selected."),
    };
}
