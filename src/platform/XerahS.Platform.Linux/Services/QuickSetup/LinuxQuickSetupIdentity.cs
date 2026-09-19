// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Resolves the calling user's identity for the Quick Setup script.
// The script needs a username (not a uid) because setfacl takes the form
// "u:<name>:r". If getlogin_r/geteuid fail we fall back to env vars and
// finally to "whoami".
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed record LinuxQuickSetupIdentity(string UserName, int Uid)
{
    public string Specifier => UserName;

    public string LogDisplay => UserName;
}

internal static class LinuxQuickSetupIdentityResolver
{
    /// <summary>
    /// Returns the current user's identity (uid + login name). Resolution
    /// priority: <c>getlogin_r</c>-equivalent (LOGNAME/USER env) -> passwd
    /// lookup by uid -> "whoami" shell fallback. Returns null when the
    /// identity cannot be determined; the caller must surface a clear error
    /// in that case rather than letting the script run with an empty target.
    /// </summary>
    public static LinuxQuickSetupIdentity? Resolve()
    {
        string? name = Environment.GetEnvironmentVariable("LOGNAME")
                       ?? Environment.GetEnvironmentVariable("USER")
                       ?? Environment.GetEnvironmentVariable("USERNAME");

        int uid;
        try
        {
            uid = Environment.GetEnvironmentVariable("UID") is { } uidEnv && int.TryParse(uidEnv, out int parsed)
                ? parsed
                : geteuid();
        }
        catch
        {
            uid = -1;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = LookupUserByUid(uid) ?? RunWhoami();
        }

        if (string.IsNullOrWhiteSpace(name) || uid < 0)
        {
            return null;
        }

        return new LinuxQuickSetupIdentity(name, uid);
    }

    private static int geteuid()
    {
        // Native geteuid via libc syscall would be ideal; for now we trust
        // the UID env var that pam_env/polkit set on graphical sessions, and
        // fall back to a `id -u` shell call.
        string? fromShell = RunCapturingStdout("id -u").Trim();
        if (int.TryParse(fromShell, out int parsed))
        {
            return parsed;
        }

        return -1;
    }

    private static string? LookupUserByUid(int uid)
    {
        if (uid < 0)
        {
            return null;
        }

        try
        {
            foreach (string line in File.ReadAllLines("/etc/passwd"))
            {
                int firstColon = line.IndexOf(':');
                int secondColon = firstColon >= 0 ? line.IndexOf(':', firstColon + 1) : -1;
                int thirdColon = secondColon >= 0 ? line.IndexOf(':', secondColon + 1) : -1;
                if (firstColon < 0 || secondColon < 0 || thirdColon < 0)
                {
                    continue;
                }

                string pwUidStr = line.Substring(secondColon + 1, thirdColon - secondColon - 1);
                if (int.TryParse(pwUidStr, out int pwUid) && pwUid == uid)
                {
                    return line.Substring(0, firstColon);
                }
            }
        }
        catch
        {
            // /etc/passwd unreadable; fall through.
        }

        return null;
    }

    private static string? RunWhoami()
    {
        try
        {
            return RunCapturingStdout("whoami").Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string RunCapturingStdout(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        if (p is null)
        {
            return string.Empty;
        }

        string stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(2000);
        return stdout;
    }
}
