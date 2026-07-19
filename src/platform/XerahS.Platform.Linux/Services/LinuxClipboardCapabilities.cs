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

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Probes wl-copy / xclip availability for CLI clipboard fallbacks (XIP0079 P3).
/// </summary>
public static class LinuxClipboardCapabilities
{
    private static Snapshot? _cached;

    public static bool HasWlCopy => GetSnapshot().HasWlCopy;

    public static bool HasXclip => GetSnapshot().HasXclip;

    public static bool PreferWaylandClipboard => GetSnapshot().PreferWaylandClipboard;

    public static bool CliClipboardHealthy => GetSnapshot().CliClipboardHealthy;

    public static string? UserFacingWarning => GetSnapshot().UserFacingWarning;

    public static string DiagnosticSummary => GetSnapshot().DiagnosticSummary;

    internal static void ResetForTests() => _cached = null;

    private static Snapshot GetSnapshot() => _cached ??= Probe();

    private static Snapshot Probe()
    {
        bool preferWayland = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                             string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

        bool hasWlCopy = CommandExists("wl-copy");
        bool hasXclip = CommandExists("xclip");
        bool healthy = preferWayland ? hasWlCopy || hasXclip : hasXclip || hasWlCopy;

        string? warning = null;
        if (!healthy)
        {
            warning = preferWayland
                ? "Install wl-clipboard (recommended) or xclip for background clipboard workflows: sudo apt install wl-clipboard"
                : "Install xclip (recommended) or wl-clipboard for background clipboard workflows: sudo apt install xclip";
        }

        string summary =
            $"PreferWayland={preferWayland}; wl-copy={ToStatus(hasWlCopy)}; xclip={ToStatus(hasXclip)}; healthy={ToStatus(healthy)}";

        return new Snapshot(hasWlCopy, hasXclip, preferWayland, healthy, warning, summary);
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return process != null && process.WaitForExit(2000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ToStatus(bool value) => value ? "OK" : "Missing";

    private readonly record struct Snapshot(
        bool HasWlCopy,
        bool HasXclip,
        bool PreferWaylandClipboard,
        bool CliClipboardHealthy,
        string? UserFacingWarning,
        string DiagnosticSummary);
}
