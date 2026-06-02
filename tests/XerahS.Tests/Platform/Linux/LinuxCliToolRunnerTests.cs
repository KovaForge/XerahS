using NUnit.Framework;
using XerahS.Platform.Linux.Capture.Helpers;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxCliToolRunnerTests
{
    [Test]
    public void RunForTestAsync_ToolExitsQuickly_ReturnsExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Minimal: tool exits 0 immediately, no output to drain.
        // Verifies the happy path through the core run helper.
        var startUtc = DateTime.UtcNow;
        var exitCode = LinuxCliToolRunner.TestAccessor
            .RunForTestAsync("/bin/sh", "-c \"exit 0\"", 5000)
            .GetAwaiter()
            .GetResult();
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(0), "Tool exited 0 but helper reported a different exit code.");
        Assert.That(elapsedMs, Is.LessThan(2000), "Run helper took longer than 2s for a trivial exit-0 command.");
    }

    [Test]
    public void RunForTestAsync_StderrExceedsPipeBuffer_ToolDoesNotBlock()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // The OS pipe buffer on POSIX systems is typically 64KB. We write
        // ~200KB of garbage to stderr and then exit 0. Without an async
        // stderr drainer the helper would deadlock (the child blocks
        // writing to a full pipe) and the call would only return after
        // the timeout, returning a null exit code. With the fix, the
        // exit code should be 0 and the call should complete well under
        // the timeout.
        const int payloadBytes = 200_000;
        const int timeoutMs = 5000;

        var startUtc = DateTime.UtcNow;
        var exitCode = LinuxCliToolRunner.TestAccessor
            .RunForTestAsync(
                "/bin/sh",
                $"-c \"yes x 2>&1 | head -c {payloadBytes} >/dev/null; exit 0\"",
                timeoutMs)
            .GetAwaiter()
            .GetResult();
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(0),
            $"Tool wrote {payloadBytes} bytes to stderr and exited 0; helper should report exit code 0 " +
            $"but reported null (likely a pipe-fill deadlock). Elapsed: {elapsedMs:F0}ms.");
        Assert.That(elapsedMs, Is.LessThan(timeoutMs - 500),
            $"Helper took {elapsedMs:F0}ms, near the {timeoutMs}ms timeout — possible pipe-fill deadlock.");
    }

    [Test]
    public void RunForTestAsync_StderrExceedsPipeBufferNonZeroExit_ReportsExitCode()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Companion to the previous test: large stderr + non-zero exit.
        // Verifies the async drainer keeps the pipe clear AND the exit
        // code is propagated correctly.
        const int payloadBytes = 200_000;
        const int timeoutMs = 5000;

        var startUtc = DateTime.UtcNow;
        var exitCode = LinuxCliToolRunner.TestAccessor
            .RunForTestAsync(
                "/bin/sh",
                $"-c \"yes x 2>&1 | head -c {payloadBytes} >/dev/null; exit 7\"",
                timeoutMs)
            .GetAwaiter()
            .GetResult();
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.EqualTo(7),
            $"Tool wrote {payloadBytes} bytes to stderr and exited 7; helper should report exit code 7. " +
            $"Elapsed: {elapsedMs:F0}ms.");
        Assert.That(elapsedMs, Is.LessThan(timeoutMs - 500),
            $"Helper took {elapsedMs:F0}ms, near the {timeoutMs}ms timeout — possible pipe-fill deadlock.");
    }

    [Test]
    public void RunForTestAsync_ToolExceedsTimeout_ReturnsNull()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Assert.Ignore("POSIX-specific test (Linux/macOS).");
        }

        // Tool sleeps for longer than the timeout, then exits 0. The
        // helper should return null exit code (timeout path) and
        // complete near the timeout window, not hang.
        const int timeoutMs = 1000;

        var startUtc = DateTime.UtcNow;
        var exitCode = LinuxCliToolRunner.TestAccessor
            .RunForTestAsync("/bin/sh", "-c \"sleep 5; exit 0\"", timeoutMs)
            .GetAwaiter()
            .GetResult();
        var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

        Assert.That(exitCode, Is.Null,
            $"Tool was supposed to exceed {timeoutMs}ms timeout; helper should report null exit code.");
        Assert.That(elapsedMs, Is.LessThan(4000),
            $"Helper should have killed and returned within ~{timeoutMs}ms, took {elapsedMs:F0}ms.");
    }
}
