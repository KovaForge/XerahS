// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxQuickSetupScriptBuilderTests
{
    [Test]
    public void Build_DefaultOptions_SetsReadAclOnEventDevices()
    {
        string script = LinuxQuickSetupScriptBuilder.Build(new LinuxQuickSetupScriptOptions());

        Assert.That(script, Does.Contain("set -eu"));
        Assert.That(script, Does.Contain("TARGET_IDENTITY=\"$1\""));
        Assert.That(script, Does.Contain("/dev/input/event*"));
        Assert.That(script, Does.Contain("u:${TARGET_IDENTITY}:r"));
    }

    [Test]
    public void Build_UInputRequired_SetsRwAclOnUInput()
    {
        var options = new LinuxQuickSetupScriptOptions(RequireInputEvents: true, RequireUInputDevice: true);

        string script = LinuxQuickSetupScriptBuilder.Build(options);

        Assert.That(script, Does.Contain("/dev/uinput"));
        Assert.That(script, Does.Contain("/dev/input/uinput"));
        Assert.That(script, Does.Contain("u:${TARGET_IDENTITY}:rw"));
        Assert.That(script, Does.Contain("uinput_ok=1"));
        Assert.That(script, Does.Contain("exit 24"));
    }

    [Test]
    public void Build_RequireInputEvents_ReportsMissingEventExitCode()
    {
        string script = LinuxQuickSetupScriptBuilder.Build(new LinuxQuickSetupScriptOptions(RequireInputEvents: true, RequireUInputDevice: false));

        Assert.That(script, Does.Contain("event_ok=1"));
        Assert.That(script, Does.Contain("exit 25"));
    }

    [Test]
    public void Build_MissingSetfacl_ReportsMissingToolExitCode()
    {
        string script = LinuxQuickSetupScriptBuilder.Build(new LinuxQuickSetupScriptOptions());

        Assert.That(script, Does.Contain("setfacl is missing"));
        Assert.That(script, Does.Contain("exit 22"));
    }
}
