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
using XerahS.Common;
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Services;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
[NonParallelizable]
public class RunCommandsTests
{
    private Func<string[], bool> _originalSend = null!;
    private Func<string[], bool> _originalStart = null!;
    private string? _originalNoNotify;
    private bool _originalJson;

    [SetUp]
    public void SetUp()
    {
        _originalSend = RunCommands.SendToRunningInstance;
        _originalStart = RunCommands.StartApp;
        _originalNoNotify = Environment.GetEnvironmentVariable("XERAHS_NO_APP_NOTIFY");
        _originalJson = JsonStdout.Enabled;
        Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", null);
    }

    [TearDown]
    public void TearDown()
    {
        RunCommands.SendToRunningInstance = _originalSend;
        RunCommands.StartApp = _originalStart;
        Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", _originalNoNotify);
        JsonStdout.Enabled = _originalJson;
    }

    [TestCase("workflow", "run", "Region capture")]
    [TestCase("workflow", "run", "abc")]
    [TestCase("workflow", "list", "deadbeef")]
    [TestCase("capture", "region", "deadbeef")]
    public void FastPath_DeclinesAnythingButARunById(string a, string b, string c)
    {
        Assert.That(RunCommands.TryRunFastPath([a, b, c], out _), Is.False);
    }

    [Test]
    public void FastPath_DeclinesUnknownOptions()
    {
        Assert.That(RunCommands.TryRunFastPath(["workflow", "run", "deadbeef", "--verbose"], out _), Is.False);
        Assert.That(RunCommands.TryRunFastPath(["workflow", "run"], out _), Is.False);
    }

    [Test]
    public void FastPath_RelaysAnIdToTheRunningInstance()
    {
        string[]? relayed = null;
        RunCommands.SendToRunningInstance = args => { relayed = args; return true; };
        RunCommands.StartApp = _ => throw new AssertionException("must not start a second instance");

        bool handled = RunCommands.TryRunFastPath(["workflow", "run", "deadbeef-1234", "--json"], out int exitCode);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(relayed, Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "deadbeef-1234" }));
        });
    }

    [Test]
    public void FastPath_StartsXerahSWhenNoneIsRunning()
    {
        string[]? started = null;
        RunCommands.SendToRunningInstance = _ => false;
        RunCommands.StartApp = args => { started = args; return true; };

        Assert.That(RunCommands.TryRunFastPath(["workflow", "run", "deadbeef"], out int exitCode), Is.True);
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(started, Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "deadbeef" }));
    }

    [Test]
    public void FastPath_FailsWhenXerahSCannotBeReachedOrStarted()
    {
        RunCommands.SendToRunningInstance = _ => false;
        RunCommands.StartApp = _ => false;

        Assert.That(RunCommands.TryRunFastPath(["workflow", "run", "deadbeef"], out int exitCode), Is.True);
        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void ResolveAppExecutable_PrefersAnExistingOverride()
    {
        string dir = Path.Combine(Path.GetTempPath(), "xerahs-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string bundled = Path.Combine(dir, OperatingSystem.IsWindows() ? "XerahS.exe" : "XerahS");
            string custom = Path.Combine(dir, "custom-xerahs");
            File.WriteAllText(bundled, string.Empty);
            File.WriteAllText(custom, string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(RunCommands.ResolveAppExecutable(dir, null), Is.EqualTo(bundled));
                Assert.That(RunCommands.ResolveAppExecutable(dir, custom), Is.EqualTo(custom));
                Assert.That(RunCommands.ResolveAppExecutable(dir, Path.Combine(dir, "missing")), Is.Null);
                Assert.That(RunCommands.ResolveAppExecutable(Path.Combine(dir, "empty"), null), Is.Null);
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
