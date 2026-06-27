using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxScreenServiceTests
{
    [Test]
    public void RunXrandrCapture_HappyPath_ExitsZeroAndReturnsStdout()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Echo a small payload to stdout and exit 0. Verifies the happy
        // path through the run helper used by ParseScreens.
        var startUtc = DateTime.UtcNow;
        var (output, exitCode) = LinuxScreenService.TestAccessor
            .RunXrandrCapture("/bin/sh", "-c \"echo hello-screen; exit 0\"", 5000);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(0),
            "Helper should report exit code 0 for a trivial echo+exit-0 command.");
        Assert.That(output, Does.Contain("hello-screen"),
            "Helper should capture stdout text from the child process.");
        Assert.That(elapsedMs, Is.LessThan(2000),
            "Helper took longer than 2s for a trivial exit-0 command.");
    }

    [Test]
    public void RunXrandrCapture_StderrExceedsPipeBuffer_ToolDoesNotBlock()
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
        // the timeout.
        const int payloadBytes = 200_000;
        const int timeoutMs = 5000;

        var startUtc = DateTime.UtcNow;
        var (output, exitCode) = LinuxScreenService.TestAccessor
            .RunXrandrCapture(
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
    public void RunXrandrCapture_TimeoutReturnsNullExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Sleep for 5s with a 1s timeout. The helper should kill the
        // child and return null exit code (and bound elapsed time).
        const int timeoutMs = 1000;
        const int sleepSeconds = 5;

        var startUtc = DateTime.UtcNow;
        var (output, exitCode) = LinuxScreenService.TestAccessor
            .RunXrandrCapture(
                "/bin/sh",
                $"-c \"sleep {sleepSeconds}; exit 0\"",
                timeoutMs);
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.Null,
            $"Helper should report null exit code on timeout but reported {exitCode}. Elapsed: {elapsedMs:F0}ms.");
        Assert.That(elapsedMs, Is.LessThan(3000),
            $"Helper took {elapsedMs:F0}ms on a {timeoutMs}ms timeout — should have killed the child earlier.");
    }

    [Test]
    public void RunXrandrCapture_NonZeroExit_ReturnsExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Echo to stderr and exit 7. Verifies the helper surfaces the
        // non-zero exit code rather than swallowing it.
        var (output, exitCode) = LinuxScreenService.TestAccessor
            .RunXrandrCapture("/bin/sh", "-c \"echo noise 1>&2; exit 7\"", 5000);

        Assert.That(exitCode, Is.EqualTo(7),
            "Helper should report exit code 7 for a child that writes to stderr and exits 7.");
        // stdout is empty in this scenario
        Assert.That(output, Is.Empty.Or.Not.Contain("noise"),
            "Helper should not pull stderr text into the stdout field.");
    }
}
