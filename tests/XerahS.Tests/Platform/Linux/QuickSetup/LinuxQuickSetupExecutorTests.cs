// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxQuickSetupExecutorTests
{
    [Test]
    public async Task RunAsync_LauncherUnavailable_ReturnsFailure()
    {
        var launcher = new FakeLauncher(isAvailable: false, failureMessage: "no launcher");
        var executor = new LinuxQuickSetupExecutor(
            identityResolver: () => new LinuxQuickSetupIdentity("alice", 1000),
            runProcessAsync: (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = await executor.RunAsync(
            launcher,
            new LinuxQuickSetupScriptOptions(),
            logContext: "test",
            unexpectedFailureMessage: "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("no launcher"));
    }

    [Test]
    public async Task RunAsync_IdentityUnresolved_ReturnsFailure()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = new LinuxQuickSetupExecutor(
            identityResolver: () => null,
            runProcessAsync: (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = await executor.RunAsync(
            launcher,
            new LinuxQuickSetupScriptOptions(),
            logContext: "test",
            unexpectedFailureMessage: "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("Could not determine"));
    }

    [Test]
    public async Task RunAsync_ProcessExitsZero_ReturnsSuccess()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = new LinuxQuickSetupExecutor(
            identityResolver: () => new LinuxQuickSetupIdentity("alice", 1000),
            runProcessAsync: (_, _) => Task.FromResult((0, "uinput=1 event=4\n", string.Empty)));

        var result = await executor.RunAsync(
            launcher,
            new LinuxQuickSetupScriptOptions(),
            logContext: "test",
            unexpectedFailureMessage: "unexpected");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("alice"));
        Assert.That(result.Message, Does.Contain("uinput=1 event=4"));
    }

    [Test]
    public async Task RunAsync_ProcessExitsNonZero_TranslatesExitCode()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = new LinuxQuickSetupExecutor(
            identityResolver: () => new LinuxQuickSetupIdentity("alice", 1000),
            runProcessAsync: (_, _) => Task.FromResult((22, "setfacl is missing on host\n", string.Empty)));

        var result = await executor.RunAsync(
            launcher,
            new LinuxQuickSetupScriptOptions(),
            logContext: "test",
            unexpectedFailureMessage: "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("acl"));
    }

    private sealed class FakeLauncher : IPrivilegedHostCommandLauncher
    {
        private readonly bool _isAvailable;
        private readonly string _failureMessage;

        public FakeLauncher(bool isAvailable, string failureMessage)
        {
            _isAvailable = isAvailable;
            _failureMessage = failureMessage;
        }

        public ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
            => new ValueTask<(bool, string)>((_isAvailable, _failureMessage));

        public ProcessStartInfo CreateStartInfo(string hostScript, string userIdentity)
            => new ProcessStartInfo
            {
                FileName = "/bin/sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
    }
}
