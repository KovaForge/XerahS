// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Builds the inline shell script that grants the current user ACL on
// /dev/input/event* and /dev/uinput via setfacl. Modeled on
// CrossMacro's LinuxQuickSetupScriptBuilder.
using System.Text;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed record LinuxQuickSetupScriptOptions(
    bool RequireInputEvents = true,
    bool RequireUInputDevice = false);

internal static class LinuxQuickSetupScriptBuilder
{
    /// <summary>
    /// Builds a single-line-ish shell script suitable for passing to
    /// <c>/bin/sh -c "&lt;script&gt;"</c> via pkexec/run0. The script:
    /// <list type="bullet">
    ///   <item>Forbids unset variables and unhandled failures (<c>set -eu</c>).</item>
    ///   <item>Loads <c>uinput</c> if <c>modprobe</c> is available.</item>
    ///   <item>Calls <c>setfacl</c> on each <c>/dev/uinput</c> path and on each
    ///         <c>/dev/input/event*</c> for the user passed as <c>$1</c>.</item>
    ///   <item>Exits with a documented code if <c>setfacl</c> is missing
    ///         (22), if no event devices were found (25), or if a required
    ///         device is missing (24 for uinput).</item>
    /// </list>
    /// </summary>
    public static string Build(LinuxQuickSetupScriptOptions options)
    {
        var script = new StringBuilder();
        AppendPreamble(script);
        AppendUInputSection(script, options);
        AppendInputEventsSection(script, options);
        AppendFinalGuard(script);
        return script.ToString();
    }

    private static void AppendPreamble(StringBuilder script)
    {
        _ = script.Append("set -eu; ");
        _ = script.Append("PATH='/run/wrappers/bin:/run/current-system-sw/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin'; export PATH; ");
        _ = script.Append("TARGET_IDENTITY=\"$1\"; ");
        _ = script.Append("if ! command -v setfacl >/dev/null 2>&1; then ");
        _ = script.Append("echo 'setfacl is missing on host. Install the acl package and retry.' >&2; ");
        _ = script.Append("exit 22; ");
        _ = script.Append("fi; ");
        _ = script.Append("if command -v modprobe >/dev/null 2>&1; then modprobe uinput >/dev/null 2>&1 || true; fi; ");
        _ = script.Append("uinput_count=0; ");
        _ = script.Append("event_count=0; ");
    }

    private static void AppendUInputSection(StringBuilder script, LinuxQuickSetupScriptOptions options)
    {
        if (options.RequireUInputDevice)
        {
            _ = script.Append("uinput_ok=0; ");
        }

        _ = script.Append("for p in /dev/uinput /dev/input/uinput; do ");
        _ = script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:rw\" \"$p\"; uinput_count=$((uinput_count + 1)); ");
        if (options.RequireUInputDevice)
        {
            _ = script.Append("uinput_ok=1; ");
        }
        _ = script.Append("fi; ");
        _ = script.Append("done; ");

        if (options.RequireUInputDevice)
        {
            _ = script.Append("if [ \"$uinput_ok\" -ne 1 ]; then ");
            _ = script.Append("echo 'uinput device is not available. Load the uinput module and retry.' >&2; ");
            _ = script.Append("exit 24; ");
            _ = script.Append("fi; ");
        }
    }

    private static void AppendInputEventsSection(StringBuilder script, LinuxQuickSetupScriptOptions options)
    {
        if (options.RequireInputEvents)
        {
            _ = script.Append("event_ok=0; ");
        }

        _ = script.Append("for p in /dev/input/event*; do ");
        _ = script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:r\" \"$p\"; event_count=$((event_count + 1)); ");
        if (options.RequireInputEvents)
        {
            _ = script.Append("event_ok=1; ");
        }
        _ = script.Append("fi; ");
        _ = script.Append("done; ");

        if (options.RequireInputEvents)
        {
            _ = script.Append("if [ \"$event_ok\" -ne 1 ]; then ");
            _ = script.Append("echo 'No /dev/input/event* devices were found for session ACL setup.' >&2; ");
            _ = script.Append("exit 25; ");
            _ = script.Append("fi; ");
        }
    }

    private static void AppendFinalGuard(StringBuilder script)
    {
        _ = script.Append("printf 'uinput=%d event=%d\\n' \"$uinput_count\" \"$event_count\"; ");
        _ = script.Append("exit 0; ");
    }
}
