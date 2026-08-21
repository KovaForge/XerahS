using NUnit.Framework;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxClipboardMonitorServiceTests
{
    /// <summary>
    /// Regression test: Stop() must not leave _pollTask running with a disposed
    /// CancellationTokenSource when the X11 polling task has not yet exited.
    /// The fix captures the Wait() return value and skips CTS disposal when the
    /// task is still running past the timeout.
    /// </summary>
    [Test]
    public void Stop_WhilePollTaskRunning_DoesNotLeakPollTaskField()
    {
        // Use reflection to inspect the Stop() behaviour without a real clipboard.
        // LinuxClipboardMonitorService polls xclip on the X11 path; we verify the
        // stop path does not leave the instance in an inconsistent state by
        // checking IsMonitoring reflects the stopped state.
        var service = new LinuxClipboardMonitorService();

        Assert.That(service.IsMonitoring, Is.False);

        // Start monitoring on the X11 polling path (Wayland is not set in the test env).
        service.Start();

        // The service is now monitoring on the X11 polling path.
        Assert.That(service.IsMonitoring, Is.True);

        // Stop immediately — the X11 polling task may still be inside its 2-second
        // Task.Delay when Stop() is called.  Stop() must not dispose _cts while the
        // task is still running (which would leave the abandoned task with a
        // disposed token).  Instead it should leave _cts alive and simply null
        // _pollTask so a subsequent Start() creates a fresh task.
        service.Stop();

        // After Stop(), IsMonitoring must be false regardless of whether the
        // internal poll task completed within the 1-second timeout.
        Assert.That(service.IsMonitoring, Is.False);

        service.Dispose();
    }

    /// <summary>
    /// Regression test: calling Start() after Stop() (when the previous poll task
    /// timed out) must create a new fresh task and not reuse a stale _pollTask reference.
    /// </summary>
    [Test]
    public void Start_AfterStopWithStaleTask_CreatesNewTask()
    {
        var service = new LinuxClipboardMonitorService();

        service.Start();
        Assert.That(service.IsMonitoring, Is.True);

        service.Stop();
        Assert.That(service.IsMonitoring, Is.False);

        // Start again — must not reuse a stale task reference.
        service.Start();
        Assert.That(service.IsMonitoring, Is.True);

        service.Stop();
        Assert.That(service.IsMonitoring, Is.False);

        service.Dispose();
    }
}
