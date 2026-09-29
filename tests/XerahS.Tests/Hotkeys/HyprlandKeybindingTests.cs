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

using Avalonia.Input;
using NUnit.Framework;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.OmaXerahs.Commands;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.OmaSnap;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Hotkeys;

/// <summary>XIP0088 Phase 5: Hyprland binding generator, conflicts, installer and workflow trigger.</summary>
[TestFixture]
[NonParallelizable]
public class HyprlandKeybindingTests
{
    internal static string Fixture(params string[] parts)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine([directory.FullName, "tests", "fixtures", .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }

    private static WorkflowSettings Workflow(string id, WorkflowType job, Key key, KeyModifiers modifiers = KeyModifiers.None, bool enabled = true) =>
        new(job, new HotkeyInfo(key, modifiers)) { Id = id, Enabled = enabled };

    [TestCase(Key.PrintScreen, KeyModifiers.None, "PRINT")]
    [TestCase(Key.F1, KeyModifiers.Control, "CTRL + F1")]
    [TestCase(Key.F12, KeyModifiers.Shift | KeyModifiers.Meta, "SUPER + SHIFT + F12")]
    [TestCase(Key.A, KeyModifiers.Alt | KeyModifiers.Control, "CTRL + ALT + A")]
    [TestCase(Key.D7, KeyModifiers.Meta, "SUPER + 7")]
    [TestCase(Key.NumPad3, KeyModifiers.None, "KP_3")]
    [TestCase(Key.Space, KeyModifiers.Meta, "SUPER + SPACE")]
    [TestCase(Key.PageUp, KeyModifiers.None, "PAGE_UP")]
    [TestCase(Key.OemMinus, KeyModifiers.Control, "CTRL + MINUS")]
    [TestCase(Key.Enter, KeyModifiers.Meta, "SUPER + RETURN")]
    [TestCase(Key.OemBackslash, KeyModifiers.Meta, "SUPER + LESS")]
    [TestCase(Key.OemPipe, KeyModifiers.None, "BACKSLASH")]
    [TestCase(Key.Apps, KeyModifiers.Control, "CTRL + MENU")]
    [TestCase(Key.CapsLock, KeyModifiers.Meta, "SUPER + CAPS_LOCK")]
    public void KeyMappingTable(Key key, KeyModifiers modifiers, string expected)
    {
        Assert.That(HyprlandKeybindingGenerator.ToHyprlandKeys(new HotkeyInfo(key, modifiers)), Is.EqualTo(expected));
    }

    [Test]
    public void UnmappableAndModifierOnlyKeys_AreRejected()
    {
        Assert.That(HyprlandKeybindingGenerator.ToHyprlandKeys(new HotkeyInfo(Key.LeftCtrl, KeyModifiers.Control)), Is.Null);
        Assert.That(HyprlandKeybindingGenerator.ToHyprlandKeys(new HotkeyInfo(Key.None)), Is.Null);
        Assert.That(HyprlandKeybindingGenerator.ToHyprlandKeys(new HotkeyInfo(Key.VolumeMute)), Is.Null);
    }

    [Test]
    public void Entries_SkipDisabledDuplicateAndKeylessWorkflows()
    {
        var (entries, unsupported) = HyprlandKeybindingGenerator.BuildEntries(
        [
            Workflow("bc904c7e-4edc-4726-b53e-4f6cf8646eee", WorkflowType.RectangleRegion, Key.PrintScreen),
            Workflow("aaaa0001", WorkflowType.PrintScreen, Key.F1, KeyModifiers.Control),
            Workflow("aaaa0002", WorkflowType.ActiveWindow, Key.F1, KeyModifiers.Control),
            Workflow("aaaa0003", WorkflowType.ScrollingCapture, Key.F5, enabled: false),
            Workflow("aaaa0004", WorkflowType.CustomWindow, Key.None),
            Workflow("aaaa0005", WorkflowType.ActiveMonitor, Key.VolumeMute)
        ], "/usr/lib/xerahs/omaxerahs");

        Assert.That(entries.Select(e => e.Keys), Is.EqualTo(new[] { "PRINT", "CTRL + F1" }));
        Assert.That(entries[0].Command, Is.EqualTo("/usr/lib/xerahs/omaxerahs workflow run bc904c7e-4edc-4726-b53e-4f6cf8646eee"));
        Assert.That(unsupported, Has.Count.EqualTo(2));
        Assert.That(unsupported[0], Does.Contain("already used"));
        Assert.That(unsupported[1], Does.Contain("no Hyprland key name"));
    }

    [Test]
    public void Conflicts_FromHyprctlFixture_IgnoreXerahSOwnMouseAndSubmapBinds()
    {
        IReadOnlyList<CompositorBinding> existing = HyprlandKeybindingService.ParseBinds(File.ReadAllText(Fixture("hyprland", "hyprctl-binds.json")));
        var (entries, _) = HyprlandKeybindingGenerator.BuildEntries(
        [
            Workflow("bc904c7e", WorkflowType.RectangleRegion, Key.PrintScreen),
            Workflow("aaaa0001", WorkflowType.PrintScreen, Key.F1, KeyModifiers.Control),
            Workflow("aaaa0002", WorkflowType.ActiveWindow, Key.F2),
            Workflow("aaaa0003", WorkflowType.CustomWindow, Key.S, KeyModifiers.Meta | KeyModifiers.Control)
        ], "/opt/xerahs/omaxerahs");

        IReadOnlyList<HyprlandBindingConflict> conflicts = HyprlandKeybindingGenerator.FindConflicts(entries, existing);

        Assert.That(existing.Select(b => b.Keys), Does.Contain("SHIFT + F1").And.Contain("SUPER + RETURN").And.Contain("SUPER + CTRL + S"));
        Assert.That(conflicts.Select(c => c.Entry.Keys), Is.EqualTo(new[] { "PRINT", "SUPER + CTRL + S" }),
            "CTRL + F1 is XerahS's own binding; F2 exists only in a submap");
        Assert.That(conflicts[0].Existing.Description, Is.EqualTo("Screenshot"));
    }

    [Test]
    public void ManagedFile_Snapshot_Omarchy()
    {
        var entries = new[]
        {
            new HyprlandBindingEntry("bc904c7e", "Region capture", "PRINT", "/usr/lib/xerahs/omaxerahs workflow run bc904c7e"),
            new HyprlandBindingEntry("aaaa0001", "Say \"hi\"", "CTRL + F1", "'/opt/my apps/omaxerahs' workflow run aaaa0001")
        };

        string content = HyprlandKeybindingGenerator.BuildFile(entries, useOmarchyHelpers: true, approvedUnbinds: ["print"]);

        Assert.That(content, Is.EqualTo(
            "-- Managed by XerahS. Changes are overwritten; edit hotkeys in XerahS.\n" +
            "-- Bindings run \"omaxerahs workflow run <id>\"; turn this off in XerahS > Settings > Hotkeys.\n" +
            "\n" +
            "hl.unbind(\"PRINT\")\n" +
            "o.bind(\"PRINT\", \"XerahS: Region capture\", \"/usr/lib/xerahs/omaxerahs workflow run bc904c7e\")\n" +
            "\n" +
            "o.bind(\"CTRL + F1\", \"XerahS: Say \\\"hi\\\"\", \"'/opt/my apps/omaxerahs' workflow run aaaa0001\")\n"));
    }

    [Test]
    public void ManagedFile_PlainHyprland_UsesHlBind_AndNoUnbindWithoutApproval()
    {
        var entries = new[] { new HyprlandBindingEntry("x", "Region capture", "PRINT", "omaxerahs workflow run x") };

        string content = HyprlandKeybindingGenerator.BuildFile(entries, useOmarchyHelpers: false, approvedUnbinds: []);

        Assert.That(content, Does.Contain("hl.bind(\"PRINT\", \"exec\", \"omaxerahs workflow run x\")"));
        Assert.That(content, Does.Not.Contain("hl.unbind"));
    }

    [Test]
    public void CommandQuoting()
    {
        Assert.That(HyprlandKeybindingGenerator.BuildCommand("/home/u/My Apps/omaxerahs", "id'1"),
            Is.EqualTo("'/home/u/My Apps/omaxerahs' workflow run 'id'\\''1'"));
    }

    private sealed class FakeHyprctl : IHyprctlRunner
    {
        public List<string> Calls { get; } = new();
        public Queue<string> ConfigErrors { get; } = new();

        public Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add(string.Join(' ', arguments));
            return Task.FromResult(arguments[0] switch
            {
                "configerrors" => (0, ConfigErrors.Count > 0 ? ConfigErrors.Dequeue() : "[\"\"]"),
                "binds" => (0, "[]"),
                _ => (0, "ok")
            });
        }
    }

    private static LinuxDesktopProfile HyprlandProfile(bool omarchy = true) => new(
        new LinuxDesktopFacts(IsWayland: true, IsHyprland: true, IsOmarchy: omarchy, IsSandboxed: false, Desktop: "Hyprland"),
        () => null,
        (_, _) => Task.FromResult(OmaSnapCapabilities.Unusable("n/a")));

    [Test]
    public async Task Installer_BacksUpIncludesOnceReloadsAndValidates()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hypr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "bindings.lua"), "o.bind(\"SUPER + RETURN\", \"Terminal\", \"kitty\")");
            var hyprctl = new FakeHyprctl();
            var service = new HyprlandKeybindingService(HyprlandProfile(), hyprctl, dir, () => DateTimeOffset.FromUnixTimeSeconds(1790000000));

            CompositorKeybindingResult first = await service.ApplyAsync("-- one\n");
            CompositorKeybindingResult second = await service.ApplyAsync("-- two\n");

            string bindings = File.ReadAllText(Path.Combine(dir, "bindings.lua"));
            Assert.Multiple(() =>
            {
                Assert.That(first.Success, Is.True, first.Message);
                Assert.That(first.BackupPath, Is.EqualTo(Path.Combine(dir, "bindings.lua.bak.1790000000")));
                Assert.That(File.ReadAllText(first.BackupPath!), Does.Not.Contain("require"));
                Assert.That(bindings.Split('\n').Count(l => l.Trim() == HyprlandKeybindingService.IncludeLine), Is.EqualTo(1));
                Assert.That(bindings, Does.StartWith("o.bind(\"SUPER + RETURN\""));
                Assert.That(File.ReadAllText(service.ManagedFilePath), Is.EqualTo("-- two\n"));
                Assert.That(hyprctl.Calls, Is.EqualTo(new[] { "reload", "configerrors -j", "reload", "configerrors -j" }));
                Assert.That(second.Success, Is.True, second.Message);
                Assert.That(second.BackupPath, Is.EqualTo(Path.Combine(dir, "bindings.lua.bak.1790000000-1")), "never overwrites an earlier backup");
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Installer_ConfigErrors_RollBackEverything()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hypr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            const string original = "o.bind(\"SUPER + RETURN\", \"Terminal\", \"kitty\")\n";
            File.WriteAllText(Path.Combine(dir, "bindings.lua"), original);
            var hyprctl = new FakeHyprctl();
            hyprctl.ConfigErrors.Enqueue("[\"xerahs.lua:4: unknown key PRNT\"]");
            var service = new HyprlandKeybindingService(HyprlandProfile(), hyprctl, dir, () => DateTimeOffset.FromUnixTimeSeconds(1790000001));

            CompositorKeybindingResult result = await service.ApplyAsync("broken");

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ConfigErrors, Is.EqualTo(new[] { "xerahs.lua:4: unknown key PRNT" }));
                Assert.That(File.ReadAllText(Path.Combine(dir, "bindings.lua")), Is.EqualTo(original));
                Assert.That(File.Exists(service.ManagedFilePath), Is.False);
                Assert.That(File.Exists(Path.Combine(dir, "bindings.lua.bak.1790000001")), Is.True, "backup is kept");
                Assert.That(hyprctl.Calls.Count(c => c == "reload"), Is.EqualTo(2), "reloaded again after rollback");
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Installer_NotHyprland_ChangesNothing()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hypr-{Guid.NewGuid():N}");
        var profile = new LinuxDesktopProfile(new LinuxDesktopFacts(true, false, false, false, "GNOME"), () => null,
            (_, _) => Task.FromResult(OmaSnapCapabilities.Unusable("n/a")));
        var hyprctl = new FakeHyprctl();

        CompositorKeybindingResult result = await new HyprlandKeybindingService(profile, hyprctl, dir).ApplyAsync("x");

        Assert.That(result.Success, Is.False);
        Assert.That(Directory.Exists(dir), Is.False);
        Assert.That(hyprctl.Calls, Is.Empty);
    }

    [Test]
    public void RunWorkflowFlag_Contract()
    {
        Assert.That(AppContracts.Cli.TryGetRunWorkflowId(["--run-workflow", " bc904c7e "], out string id), Is.True);
        Assert.That(id, Is.EqualTo("bc904c7e"));
        Assert.That(AppContracts.Cli.TryGetRunWorkflowId(["--run-workflow"], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowId(["--run-workflow", "a", "b"], out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetRunWorkflowId(["/tmp/file.png"], out _), Is.False);
    }

    [Test]
    public void OmaXerahsWorkflowRun_RelaysToRunningInstance_WithoutStartingAnother()
    {
        var originalSend = RunCommands.SendToRunningInstance;
        var originalStart = RunCommands.StartApp;
        string? notify = Environment.GetEnvironmentVariable("XERAHS_NO_APP_NOTIFY");
        var sent = new List<string[]>();
        int started = 0;
        var stdout = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", null);
            RunCommands.SendToRunningInstance = args => { sent.Add(args); return true; };
            RunCommands.StartApp = _ => { started++; return true; };
            Console.SetOut(stdout);

            int exitCode = RunCommands.Run(() => RunCommands.ResolveWorkflowId("bc904c7e-4edc-4726-b53e-4f6cf8646eee"));

            Assert.That(exitCode, Is.Zero);
            Assert.That(sent.Single(), Is.EqualTo(new[] { "--run-workflow", "bc904c7e-4edc-4726-b53e-4f6cf8646eee" }));
            Assert.That(started, Is.Zero);
            string json = stdout.ToString().Trim();
            Assert.That(json, Does.Contain("\"delivery\":\"running-instance\"").And.Contain("\"ok\":true"));
        }
        finally
        {
            Console.SetOut(originalOut);
            RunCommands.SendToRunningInstance = originalSend;
            RunCommands.StartApp = originalStart;
            Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", notify);
        }
    }

    [Test]
    public void OmaXerahsWorkflowRun_StartsXerahSWhenNotRunning()
    {
        var originalSend = RunCommands.SendToRunningInstance;
        var originalStart = RunCommands.StartApp;
        string? notify = Environment.GetEnvironmentVariable("XERAHS_NO_APP_NOTIFY");
        var originalOut = Console.Out;
        string[]? startArgs = null;
        try
        {
            Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", null);
            RunCommands.SendToRunningInstance = _ => false;
            RunCommands.StartApp = args => { startArgs = args; return true; };
            Console.SetOut(new StringWriter());

            Assert.That(RunCommands.Run(() => "abcdef12"), Is.Zero);
            Assert.That(startArgs, Is.EqualTo(new[] { "--run-workflow", "abcdef12" }));
        }
        finally
        {
            Console.SetOut(originalOut);
            RunCommands.SendToRunningInstance = originalSend;
            RunCommands.StartApp = originalStart;
            Environment.SetEnvironmentVariable("XERAHS_NO_APP_NOTIFY", notify);
        }
    }

    [TestCase("region", WorkflowType.RectangleRegion)]
    [TestCase("window", WorkflowType.CustomWindow)]
    [TestCase("fullscreen", WorkflowType.PrintScreen)]
    [TestCase("scroll", WorkflowType.ScrollingCapture)]
    public void CaptureTargets_MapToWorkflowJobs(string target, WorkflowType job)
    {
        Assert.That(RunCommands.JobForTarget(target), Is.EqualTo(job));
    }

    [Test]
    public void CompositorManagedHotkeys_AreNotRegisteredWithThePlatformService()
    {
        var original = WorkflowManager.CompositorManagesHotkeys;
        var service = new CountingHotkeyService();
        try
        {
            WorkflowManager.CompositorManagesHotkeys = () => true;
            var manager = new WorkflowManager(service);
            var workflow = Workflow("bc904c7e", WorkflowType.RectangleRegion, Key.F1, KeyModifiers.Control);

            bool registered = manager.RegisterHotkey(workflow);

            Assert.That(registered, Is.True);
            Assert.That(service.Registrations, Is.Zero, "no portal/evdev registration while Hyprland owns keys");
            Assert.That(workflow.HotkeyInfo.Status, Is.EqualTo(XerahS.Platform.Abstractions.HotkeyStatus.Registered));
            Assert.That(workflow.HotkeyInfo.NativeTriggerDescription, Is.EqualTo("Hyprland: CTRL + F1"));
        }
        finally
        {
            WorkflowManager.CompositorManagesHotkeys = original;
        }
    }

    private sealed class CountingHotkeyService : IHotkeyService
    {
        public int Registrations { get; private set; }
#pragma warning disable CS0067
        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
        public event EventHandler? HotkeysChanged;
#pragma warning restore CS0067
        public bool IsSuspended { get; set; }
        public bool RegisterHotkey(HotkeyInfo hotkeyInfo) { Registrations++; return true; }
        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo) => true;
        public void UnregisterAll() { }
        public bool IsRegistered(HotkeyInfo hotkeyInfo) => false;
        public void Dispose() { }
    }
}
