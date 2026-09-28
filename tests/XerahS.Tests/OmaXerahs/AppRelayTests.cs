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

using System.CommandLine;
using System.Text.Json;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Capture;
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Services;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
[NonParallelizable]
public class AppRelayTests
{
    private Func<string[], bool> _originalSend = null!;
    private Func<string, string[], bool> _originalStart = null!;
    private string? _originalAppPath;
    private TextWriter _originalOut = null!;
    private readonly List<string[]> _sent = [];
    private readonly List<(string Path, string[] Args)> _started = [];

    [SetUp]
    public void SetUp()
    {
        _originalSend = AppRelay.SendToRunningInstance;
        _originalStart = AppRelay.StartApp;
        _originalAppPath = Environment.GetEnvironmentVariable(AppRelay.AppPathEnvironmentVariable);
        _originalOut = Console.Out;
        _sent.Clear();
        _started.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        AppRelay.SendToRunningInstance = _originalSend;
        AppRelay.StartApp = _originalStart;
        Environment.SetEnvironmentVariable(AppRelay.AppPathEnvironmentVariable, _originalAppPath);
        Console.SetOut(_originalOut);
    }

    [Test]
    public void WorkflowRun_RunningApp_SendsRunWorkflowAndDoesNotStartAnotherInstance()
    {
        UseFakes(running: true);

        (int exitCode, JsonElement json) = Invoke(WorkflowCommand.Create(), "run", "bc904c7e-4edc-4726-b53e-4f6cf8646eee", "--json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(_sent, Has.Count.EqualTo(1));
        Assert.That(_sent[0], Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "bc904c7e-4edc-4726-b53e-4f6cf8646eee" }));
        Assert.That(_started, Is.Empty);
        Assert.That(json.GetProperty("ok").GetBoolean(), Is.True);
        Assert.That(json.GetProperty("action").GetString(), Is.EqualTo("workflow.run"));
        Assert.That(json.GetProperty("target").GetString(), Is.EqualTo("bc904c7e-4edc-4726-b53e-4f6cf8646eee"));
        Assert.That(json.GetProperty("delivery").GetString(), Is.EqualTo("delivered"));
    }

    [Test]
    public void WorkflowRun_NameWithSpaces_IsOneArgument()
    {
        UseFakes(running: true);

        (int exitCode, _) = Invoke(WorkflowCommand.Create(), "run", "Region capture", "--json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(_sent[0], Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "Region capture" }));
    }

    [Test]
    public void WorkflowRun_AppNotRunning_StartsXerahSWithTheRequest()
    {
        string appPath = CreateFakeApp();
        UseFakes(running: false);
        Environment.SetEnvironmentVariable(AppRelay.AppPathEnvironmentVariable, appPath);

        try
        {
            (int exitCode, JsonElement json) = Invoke(WorkflowCommand.Create(), "run", "abc123", "--json");

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(_started, Has.Count.EqualTo(1));
            Assert.That(_started[0].Path, Is.EqualTo(appPath));
            Assert.That(_started[0].Args, Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "abc123" }));
            Assert.That(json.GetProperty("delivery").GetString(), Is.EqualTo("started"));
        }
        finally
        {
            File.Delete(appPath);
        }
    }

    [Test]
    public void WorkflowRun_AppMissing_ReportsNotReady()
    {
        UseFakes(running: false);
        Environment.SetEnvironmentVariable(AppRelay.AppPathEnvironmentVariable, Path.Combine(Path.GetTempPath(), "missing-xerahs-" + Guid.NewGuid().ToString("N")));

        (int exitCode, JsonElement json) = Invoke(WorkflowCommand.Create(), "run", "abc123", "--json");

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(json.GetProperty("ok").GetBoolean(), Is.False);
        Assert.That(json.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("not_ready"));
        Assert.That(_started, Is.Empty);
    }

    [TestCase("region")]
    [TestCase("window")]
    [TestCase("fullscreen")]
    [TestCase("scroll")]
    public void Capture_Target_RelaysCaptureFlag(string target)
    {
        UseFakes(running: true);

        (int exitCode, JsonElement json) = Invoke(CaptureCommand.Create(), target, "--json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(_sent[0], Is.EqualTo(new[] { AppContracts.Cli.CaptureFlag, target }));
        Assert.That(json.GetProperty("action").GetString(), Is.EqualTo("capture"));
    }

    [Test]
    public void Capture_WithWorkflow_RelaysRunWorkflow()
    {
        UseFakes(running: true);

        (int exitCode, _) = Invoke(CaptureCommand.Create(), "region", "--workflow", "bc904c7e", "--json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(_sent[0], Is.EqualTo(new[] { AppContracts.Cli.RunWorkflowFlag, "bc904c7e" }));
    }

    [Test]
    public void FastPath_HandlesPlainRunAndCaptureOnly()
    {
        UseFakes(running: true);
        var output = new StringWriter();
        Console.SetOut(output);

        Assert.That(CaptureCommand.TryRunFastPath(["workflow", "run", "Region capture", "--json"], out int exitCode), Is.True);
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(CaptureCommand.TryRunFastPath(["capture", "scroll"], out _), Is.True);
        Assert.That(_sent, Is.EqualTo(new[]
        {
            new[] { AppContracts.Cli.RunWorkflowFlag, "Region capture" },
            new[] { AppContracts.Cli.CaptureFlag, "scroll" }
        }));

        Assert.That(CaptureCommand.TryRunFastPath(["workflow", "run", "--help"], out _), Is.False);
        Assert.That(CaptureCommand.TryRunFastPath(["capture", "region", "--workflow", "abc"], out _), Is.False);
        Assert.That(CaptureCommand.TryRunFastPath(["capture", "selfie"], out _), Is.False);
        Assert.That(CaptureCommand.TryRunFastPath(["workflow", "list"], out _), Is.False);
        Assert.That(_sent, Has.Count.EqualTo(2));
    }

    [Test]
    public void Capture_UnknownTarget_IsAParseError()
    {
        ParseResult result = CaptureCommand.Create().Parse(["selfie"]);

        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void ResolveAppExecutable_UsesTheBinaryNextToOmaXerahs()
    {
        string directory = Directory.CreateTempSubdirectory("xerahs-relay-").FullName;
        try
        {
            Assert.That(AppRelay.ResolveAppExecutable(directory, null), Is.Null);

            string app = Path.Combine(directory, OperatingSystem.IsWindows() ? "XerahS.exe" : "XerahS");
            File.WriteAllText(app, string.Empty);
            Assert.That(AppRelay.ResolveAppExecutable(directory, null), Is.EqualTo(app));
            Assert.That(AppRelay.ResolveAppExecutable(directory, Path.Combine(directory, "nope")), Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Cli_RunWorkflowParsing_RequiresExactlyFlagAndTarget()
    {
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["--run-workflow", " abc "], out string target), Is.True);
        Assert.That(target, Is.EqualTo("abc"));
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["--RUN-WORKFLOW", "abc"], out _), Is.True);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["--run-workflow"], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["--run-workflow", "  "], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["--run-workflow", "a", "b"], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowTarget(["/tmp/file.png"], out _), Is.False);
    }

    [Test]
    public void Cli_CaptureParsing_AcceptsOnlyKnownTargets()
    {
        Assert.That(AppContracts.Cli.TryGetCaptureTarget(["--capture", "Region"], out string target), Is.True);
        Assert.That(target, Is.EqualTo("region"));
        Assert.That(AppContracts.Cli.TryGetCaptureTarget(["--capture", "selfie"], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetCaptureTarget(["--capture"], out _), Is.False);
    }

    [TestCase("region", false, WorkflowType.RectangleRegion)]
    [TestCase("region", true, WorkflowType.RectangleRegion)]
    [TestCase("window", false, WorkflowType.ActiveWindow)]
    [TestCase("window", true, WorkflowType.CustomWindow)]
    [TestCase("fullscreen", true, WorkflowType.PrintScreen)]
    [TestCase("scroll", true, WorkflowType.ScrollingCapture)]
    public void JobForCaptureTarget_MapsTargets(string target, bool omaSnapActive, WorkflowType expected)
    {
        Assert.That(OmaSnapWorkflowRouter.JobForCaptureTarget(target, omaSnapActive), Is.EqualTo(expected));
    }

    [Test]
    public void Capabilities_ListWorkflowRunAndCapture()
    {
        string[] capabilities = CapabilitiesCommand.BuildResponse().Capabilities;

        Assert.That(capabilities, Does.Contain("workflow.run"));
        Assert.That(capabilities, Does.Contain("capture"));
        Assert.That(capabilities, Does.Not.Contain("capture.omasnap"));
        Assert.That(CapabilitiesCommand.BuildResponse(omaSnapUsable: true).Capabilities, Does.Contain("capture.omasnap"));
    }

    private void UseFakes(bool running)
    {
        AppRelay.SendToRunningInstance = args =>
        {
            if (running)
            {
                _sent.Add(args);
            }

            return running;
        };
        AppRelay.StartApp = (path, args) =>
        {
            _started.Add((path, args));
            return true;
        };
    }

    private static string CreateFakeApp()
    {
        string path = Path.Combine(Path.GetTempPath(), "fake-xerahs-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static (int ExitCode, JsonElement Json) Invoke(Command command, params string[] args)
    {
        var output = new StringWriter();
        Console.SetOut(output);
        int exitCode = command.Parse(args).Invoke();
        string json = output.ToString().Trim();
        using var document = JsonDocument.Parse(json);
        return (exitCode, document.RootElement.Clone());
    }
}
