// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Abstraction over the host-side privilege-escalation launcher so the
// executor and tests can mock it without spawning real processes.
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal interface IPrivilegedHostCommandLauncher
{
    /// <summary>
    /// Detect whether a usable launcher is available on the host.
    /// Returns (false, message) when neither pkexec nor run0 is usable.
    /// </summary>
    ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Build a <see cref="ProcessStartInfo"/> that will run the supplied
    /// shell script as root with the chosen launcher. <paramref name="userIdentity"/>
    /// is passed as <c>$1</c> to the script so it knows which user to grant
    /// ACL to.
    /// </summary>
    ProcessStartInfo CreateStartInfo(string hostScript, string userIdentity);
}
