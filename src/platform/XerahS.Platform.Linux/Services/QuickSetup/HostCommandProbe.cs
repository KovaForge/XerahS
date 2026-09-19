// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Probes for the host-side privilege-escalation command. Modeled on
// CrossMacro's HostCommandProbe but rewritten to fit XerahS conventions.
namespace XerahS.Platform.Linux.Services.QuickSetup;

internal static class HostCommandProbe
{
    /// <summary>
    /// Returns true if <paramref name="name"/> resolves to an executable file
    /// on PATH (or as an absolute path).
    /// </summary>
    public static ValueTask<bool> CommandExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Path.IsPathRooted(name))
        {
            return ValueTask.FromResult(File.Exists(name));
        }

        string? path = FindOnPath(name);
        return ValueTask.FromResult(!string.IsNullOrEmpty(path));
    }

    /// <summary>
    /// Returns true if <c>pkexec</c> is present AND its setuid-root bit is
    /// set. Without setuid, polkit cannot authorize it, so we treat it as
    /// absent. We do not call pkexec here — that would prompt the user.
    /// </summary>
    public static ValueTask<bool> PkexecIsUsableAsync(CancellationToken cancellationToken = default)
    {
        string? path = FindOnPath("pkexec") ?? "/usr/bin/pkexec";
        if (!File.Exists(path))
        {
            return ValueTask.FromResult(false);
        }

        try
        {
            // Read the file's mode via stat() and check for setuid bit (04000).
            var info = new FileInfo(path);
            // FileInfo doesn't expose Unix mode; use Mono.Unix-style fallback via Interop.
            // The simpler portable check: FileAccess via Process probe is heavyweight,
            // so we use statx-equivalent via System.IO's UnixFileMode (net7+).
            return ValueTask.FromResult((info.UnixFileMode & UnixFileMode.SetUser) != 0);
        }
        catch
        {
            return ValueTask.FromResult(false);
        }
    }

    private static string? FindOnPath(string name)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
