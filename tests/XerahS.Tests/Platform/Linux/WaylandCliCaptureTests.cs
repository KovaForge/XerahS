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

using NUnit.Framework;
using XerahS.Platform.Linux.Capture.Wayland;

namespace XerahS.Tests.Platform.Linux;

/// <summary>
/// Regression tests for the pipe-fill and timeout-stretching deadlocks in
/// <see cref="WaylandCliCapture.RunCliCapture"/>. The fix mirrors the
/// LinuxScreenService v0.23.91, LinuxThemeService v0.23.92, PulseAudioHelper
/// v0.23.93, LinuxInputService v0.23.94, and MacOSInputService v0.23.95
/// templates: drain stderr asynchronously and bound the stdout read with
/// <see cref="Task.WaitAny(Task[])"/> so a child that sleeps without output
/// cannot stretch the call beyond the configured timeout (anti-pattern B).
/// WaylandCliCapture previously had 11 separate Process.Start sites (grim /
/// slurp / grimblast / hyprshot / swaymsg / spectacle / gnome-screenshot),
/// each with the same anti-pattern. They all now share the single
/// RunCliCapture helper.
/// </summary>
[TestFixture]
public class WaylandCliCaptureTests
{
    [Test]
    public void RunCliCapture_HappyPath_ExitsZeroAndReturnsStdout()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Mimic grim's stdout ("0 0 1920 1080" geometry from slurp) and exit 0.
        var startUtc = DateTime.UtcNow;
        var (output, exitCode) = WaylandCliCapture.TestAccessor
            .RunCliCapture("/bin/sh", "-c \"printf '0 0 1920 1080\\\\n'; exit 0\"", 5000);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(0),
            "Helper should report exit code 0 for a trivial exit-0 command.");
        Assert.That(output, Does.Contain("0 0 1920 1080"),
            "Helper should capture stdout text from the child process.");
        Assert.That(elapsedMs, Is.LessThan(2000),
            $"Helper took longer than 2s for a trivial exit-0 command. Elapsed: {elapsedMs:F0}ms.");
    }

    [Test]
    public void RunCliCapture_StderrExceedsPipeBuffer_ToolDoesNotBlock()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // The OS pipe buffer on POSIX systems is typically 64KB. We write
        // ~200KB of garbage to stderr and then exit 0. Without the async
        // stderr drainer the helper would deadlock (the child blocks
        // writing to a full pipe) and the call would only return after
        // the timeout, returning a null exit code. With the fix, the
        // exit code should be 0 and the call should complete well under
        // the timeout. grim/grimblast are the prime real-world candidates
        // for this anti-pattern when the compositor emits warnings to
        // stderr.
        const int payloadBytes = 200_000;
        const int timeoutMs = 5000;

        var startUtc = DateTime.UtcNow;
        var (output, exitCode) = WaylandCliCapture.TestAccessor
            .RunCliCapture(
                "/bin/sh",
                $"-c \"yes x 2>&1 | head -c {payloadBytes} >/dev/null; echo done; exit 0\"",
                timeoutMs);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(0),
            $"Tool wrote {payloadBytes} bytes to stderr and exited 0; helper should report exit code 0 " +
            $"but reported null (likely a pipe-fill deadlock). Elapsed: {elapsedMs:F0}ms.");
        Assert.That(output, Does.Contain("done"),
            "Helper should still capture stdout even when stderr is flooded.");
        Assert.That(elapsedMs, Is.LessThan(timeoutMs - 500),
            $"Helper took {elapsedMs:F0}ms which is too close to the {timeoutMs}ms timeout — " +
            "suggests the pipe-fill deadlock is not fully fixed.");
    }

    [Test]
    public void RunCliCapture_TimeoutReturnsNullExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Sleep for 5s with a 1s timeout. The helper should kill the
        // child and return null exit code (and bound elapsed time). This
        // catches anti-pattern B (sync stdout read stretching the timeout
        // to the full sleep duration) — the pre-fix helper would return
        // exit code 0 after ~5s because the sync ReadToEnd blocked on
        // the still-open stdout pipe. grim uses a 10s timeout, slurp and
        // grimblast 60s — the original code used Task.Run(() => WaitForExit)
        // for those, but a chatty child with delayed stdout could still
        // produce a misleading null/exit-code result without the
        // Task.WaitAny(stdoutTask, Task.Delay) bound.
        const int timeoutMs = 1000;
        const int sleepSeconds = 5;

        var startUtc = DateTime.UtcNow;
        var (_, exitCode) = WaylandCliCapture.TestAccessor
            .RunCliCapture("/bin/sh", $"-c \"sleep {sleepSeconds}; exit 0\"", timeoutMs);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.Null,
            $"Helper should return null exit code on timeout (kill a sleeping child). " +
            $"Got: {exitCode}. Elapsed: {elapsedMs:F0}ms.");
        Assert.That(elapsedMs, Is.LessThan(4000),
            $"Helper took {elapsedMs:F0}ms which is too close to the {sleepSeconds}s sleep " +
            "— suggests anti-pattern B (sync stdout read) is not fully fixed.");
    }

    [Test]
    public void RunCliCapture_NonZeroExitCode_PropagatesExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Exit with a non-zero code (grim/grimblast return 1 when no
        // compositor is available, slurp returns 1 when the user cancels
        // region selection) and a small stderr message. The helper should
        // propagate the exit code, not report null.
        const int expectedExit = 1;

        var startUtc = DateTime.UtcNow;
        var (_, exitCode) = WaylandCliCapture.TestAccessor
            .RunCliCapture("/bin/sh", $"-c \"echo 'no display' 1>&2; exit {expectedExit}\"", 5000);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(expectedExit),
            $"Helper should propagate the child's exit code {expectedExit}; got: {exitCode}.");
        Assert.That(elapsedMs, Is.LessThan(2000),
            $"Helper took longer than 2s for a trivial non-zero-exit command. Elapsed: {elapsedMs:F0}ms.");
    }

    [Test]
    public void CaptureActiveWindowRouting_Hyprland_GrimblastThenHyprshot()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Hyprland should be handled by the first block only: grimblast ->
        // hyprshot. The second block (swaymsg fallback) must NOT be reached
        // because IsWlrootsDesktop("HYPRLAND") is true -- the previous
        // implementation would enter the second block after the first fell
        // through, making the second block's grimblast call dead code.
        var seq = WaylandCliCapture.TestAccessor
            .CaptureActiveWindowRoutingTest("HYPRLAND");

        Assert.That(seq, Is.EqualTo(new[]
        {
            "CaptureWithGrimblastActiveWindowAsync",
            "CaptureWithHyprshotWindowAsync"
        }));
        Assert.That(seq.Count, Is.EqualTo(2),
            "Hyprland should take exactly 2 steps; the swaymsg fallback is not applicable.");
    }

    [Test]
    public void CaptureActiveWindowRouting_Sway_GrimblastThenSwayFocused()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Sway is wlroots but not Hyprland, so it bypasses the first block
        // and goes straight to the second: grimblast -> sway-focused-window.
        var seq = WaylandCliCapture.TestAccessor
            .CaptureActiveWindowRoutingTest("SWAY");

        Assert.That(seq, Is.EqualTo(new[]
        {
            "CaptureWithGrimblastActiveWindowAsync",
            "CaptureWithSwayFocusedWindowAsync"
        }));
        Assert.That(seq.Count, Is.EqualTo(2),
            "Sway should take exactly 2 steps: grimblast then sway-focused-window.");
    }

    [Test]
    public void CaptureActiveWindowRouting_NullDesktop_GrimblastThenSwayFocused()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // When DESKTOP_SESSION is unset (null), we treat it as an unknown
        // wlroots environment and attempt grimblast -> sway-focused-window
        // (swaymsg will fail fast on non-sway compositors, giving null, and
        // the caller gets null).
        var seq = WaylandCliCapture.TestAccessor
            .CaptureActiveWindowRoutingTest(null);

        Assert.That(seq, Is.EqualTo(new[]
        {
            "CaptureWithGrimblastActiveWindowAsync",
            "CaptureWithSwayFocusedWindowAsync"
        }));
        Assert.That(seq.Count, Is.EqualTo(2));
    }

    [Test]
    public void CaptureActiveWindowRouting_NonWlroots_ReturnsEmpty()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // KDE/GNOME/XFCE etc. should not be routed to any active-window
        // helper from this method (they have their own capture paths).
        foreach (var desktop in new[] { "KDE", "GNOME", "XFCE", "i3", "LXDE" })
        {
            var seq = WaylandCliCapture.TestAccessor
                .CaptureActiveWindowRoutingTest(desktop);
            Assert.That(seq, Is.Empty,
                $"Desktop '{desktop}' should not be routed through the wlroots "
                + "active-window helpers in CaptureActiveWindowAsync.");
        }
    }
}
