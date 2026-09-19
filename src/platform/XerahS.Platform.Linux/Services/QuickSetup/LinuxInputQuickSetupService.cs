// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Top-level entry point that ties the launcher, script builder, and executor
// together with an IsAvailable probe so the evdev hotkey backend can decide
// whether to attempt setup before declaring itself unavailable.
namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxInputQuickSetupService
{
    private readonly IPrivilegedHostCommandLauncher _launcher;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly Func<bool> _inputDevicesAlreadyReadable;

    /// <summary>
    /// Production constructor — uses the real launcher/executor and probes
    /// <c>/dev/input/event*</c> directly to short-circuit when input access
    /// is already granted (e.g. Fedora's udev rule + input group).
    /// </summary>
    public LinuxInputQuickSetupService()
        : this(
            launcher: new DirectPolkitHostCommandLauncher(),
            inputDevicesAlreadyReadable: InputDevicesReadableByCurrentUser)
    {
    }

    internal LinuxInputQuickSetupService(
        IPrivilegedHostCommandLauncher launcher,
        Func<bool>? inputDevicesAlreadyReadable = null)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _executor = new LinuxQuickSetupExecutor();
        _inputDevicesAlreadyReadable = inputDevicesAlreadyReadable ?? InputDevicesReadableByCurrentUser;
    }

    /// <summary>
    /// True if the current process can already read at least one keyboard
    /// input device — i.e. ACL/group access is already in place and Quick
    /// Setup is unnecessary. Skips the polkit prompt entirely in that case.
    /// </summary>
    public bool IsInputAccessAlreadyGranted() => _inputDevicesAlreadyReadable();

    /// <summary>
    /// True if a usable privilege-escalation command exists on the host
    /// (pkexec with setuid bit, or run0 from systemd 256+).
    /// </summary>
    public async ValueTask<bool> CanRunAsync(CancellationToken cancellationToken = default)
    {
        var (isAvailable, _) = await _launcher.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        return isAvailable;
    }

    /// <summary>
    /// Run the Quick Setup script. Returns a structured result so callers
    /// can decide whether to surface a UI banner or retry the evdev probe.
    /// </summary>
    public Task<QuickSetupResult> RunAsync(
        LinuxQuickSetupScriptOptions scriptOptions,
        CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(
            launcher: _launcher,
            scriptOptions: scriptOptions,
            logContext: "LinuxInputQuickSetupService",
            unexpectedFailureMessage: "Quick Setup could not be launched. Check that a polkit agent is available and try again.",
            cancellationToken: cancellationToken);
    }

    private static bool InputDevicesReadableByCurrentUser()
    {
        try
        {
            if (!Directory.Exists("/dev/input"))
            {
                return false;
            }

            foreach (string path in Directory.EnumerateFiles("/dev/input", "event*"))
            {
                try
                {
                    using FileStream fs = File.OpenRead(path);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    // Try the next device.
                }
                catch (IOException)
                {
                    // /dev/input/event* on Linux is a character device; File.OpenRead
                    // may throw IOException for ENODEV/EACCES depending on .NET version.
                    // Treat as "not readable" and continue.
                }
            }
        }
        catch
        {
            // /dev/input unreadable or missing entirely.
        }

        return false;
    }
}
