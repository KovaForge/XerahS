using NUnit.Framework;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Platform;

[TestFixture]
public class WatchFolderDaemonServiceBaseTests
{
    [Test]
    public async Task RestartAsync_WhenStopFails_DoesNotStartDaemon()
    {
        var expected = WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "stop failed");
        var service = new StubWatchFolderDaemonService(expected, WatchFolderDaemonResult.Ok("started"));

        WatchFolderDaemonResult result = await service.RestartAsync(
            WatchFolderDaemonScope.User,
            "/tmp/settings",
            startAtStartup: true,
            gracefulTimeout: TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(WatchFolderDaemonErrorCode.CommandFailed));
            Assert.That(result.Message, Is.EqualTo("stop failed"));
            Assert.That(service.StopCallCount, Is.EqualTo(1));
            Assert.That(service.StartCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task RestartAsync_WhenStopSucceeds_StartsDaemon()
    {
        var service = new StubWatchFolderDaemonService(
            WatchFolderDaemonResult.Ok("stopped"),
            WatchFolderDaemonResult.Ok("started"));

        WatchFolderDaemonResult result = await service.RestartAsync(
            WatchFolderDaemonScope.User,
            "/tmp/settings",
            startAtStartup: false,
            gracefulTimeout: TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("started"));
            Assert.That(service.StopCallCount, Is.EqualTo(1));
            Assert.That(service.StartCallCount, Is.EqualTo(1));
        });
    }

    private sealed class StubWatchFolderDaemonService : WatchFolderDaemonServiceBase
    {
        private readonly WatchFolderDaemonResult _stopResult;
        private readonly WatchFolderDaemonResult _startResult;

        public StubWatchFolderDaemonService(WatchFolderDaemonResult stopResult, WatchFolderDaemonResult startResult)
        {
            _stopResult = stopResult;
            _startResult = startResult;
        }

        public int StopCallCount { get; private set; }

        public int StartCallCount { get; private set; }

        public override bool IsSupported => true;

        public override bool SupportsScope(WatchFolderDaemonScope scope) => true;

        public override Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WatchFolderDaemonStatus { Scope = scope });
        }

        public override Task<WatchFolderDaemonResult> StartAsync(
            WatchFolderDaemonScope scope,
            string settingsFolder,
            bool startAtStartup,
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            return Task.FromResult(_startResult);
        }

        public override Task<WatchFolderDaemonResult> StopAsync(
            WatchFolderDaemonScope scope,
            TimeSpan gracefulTimeout,
            CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            return Task.FromResult(_stopResult);
        }
    }
}
