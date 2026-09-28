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
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Hyprland;

namespace XerahS.Tests.Platform.Linux.Hyprland;

[TestFixture]
public class HyprlandKeybindingServiceTests
{
    private const string OmarchyBinds = """
    [{"mouse": false, "modmask": 0, "submap": "", "key": "PRINT", "description": "Screenshot", "dispatcher": "exec", "arg": "omarchy-capture-screenshot"}]
    """;

    private const string OriginalBindings = "-- my bindings\no.bind(\"SUPER + SHIFT + R\", \"SSH\", \"alacritty -e ssh box\")\n";

    private string _configDirectory = null!;
    private FakeHyprctl _hyprctl = null!;
    private HyprlandKeybindingService _service = null!;

    private static readonly HyprlandWorkflowBinding Region =
        new("bc904c7e-4edc-4726-b53e-4f6cf8646eee", "Region capture", new HotkeyInfo(Key.PrintScreen));

    private static readonly HyprlandWorkflowBinding Fullscreen =
        new("f00d", "Fullscreen", new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift));

    [SetUp]
    public void SetUp()
    {
        _configDirectory = Directory.CreateTempSubdirectory("xerahs-hypr-").FullName;
        File.WriteAllText(Path.Combine(_configDirectory, "hyprland.lua"), "require(\"hypr.bindings\")\n");
        File.WriteAllText(BindingsPath, OriginalBindings);
        _hyprctl = new FakeHyprctl { Binds = OmarchyBinds };
        _service = new HyprlandKeybindingService(_hyprctl, _configDirectory, useOmarchyHelpers: true, "/usr/lib/xerahs/omaxerahs", () => DateTimeOffset.FromUnixTimeSeconds(1790000000));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_configDirectory, recursive: true);

    private string BindingsPath => Path.Combine(_configDirectory, "bindings.lua");

    private string ManagedPath => Path.Combine(_configDirectory, "xerahs.lua");

    [Test]
    public void IncludeTarget_PrefersBindingsLua()
    {
        Assert.That(_service.IncludeTargetPath, Is.EqualTo(BindingsPath));
        File.Delete(BindingsPath);
        Assert.That(_service.IncludeTargetPath, Is.EqualTo(Path.Combine(_configDirectory, "hyprland.lua")));
    }

    [Test]
    public async Task Scan_ReportsKeysAlreadyBound()
    {
        HyprlandBindingScan scan = await _service.ScanAsync([Region, Fullscreen]);

        Assert.That(scan.Error, Is.Null);
        Assert.That(scan.Conflicts, Has.Count.EqualTo(1));
        Assert.That(scan.Conflicts[0].Keys, Is.EqualTo("PRINT"));
        Assert.That(scan.Conflicts[0].ExistingDescription, Is.EqualTo("Screenshot"));
    }

    [Test]
    public async Task Enable_BacksUpAddsIncludeReloadsAndChecks()
    {
        HyprlandApplyResult result = await _service.EnableAsync([Region, Fullscreen], ["PRINT"]);

        Assert.That(result.Succeeded, Is.True, result.Message);
        Assert.That(_service.IsEnabled, Is.True);
        Assert.That(result.BackupPath, Is.EqualTo(BindingsPath + ".bak.1790000000"));
        Assert.That(File.ReadAllText(result.BackupPath!), Is.EqualTo(OriginalBindings));
        Assert.That(File.ReadAllText(BindingsPath), Does.StartWith(OriginalBindings));
        Assert.That(_service.IsIncluded, Is.True);

        string managed = File.ReadAllText(ManagedPath);
        Assert.That(managed, Does.StartWith(HyprlandManagedConfig.Header));
        Assert.That(managed, Does.Contain("hl.unbind(\"PRINT\")"));
        Assert.That(managed, Does.Contain("o.bind(\"PRINT\", \"XerahS: Region capture\""));
        Assert.That(managed, Does.Contain("o.bind(\"SHIFT + PRINT\", \"XerahS: Fullscreen\""));
        Assert.That(_hyprctl.Calls, Is.EqualTo(new[] { "-j binds", "reload", "-j configerrors" }));
    }

    [Test]
    public async Task Enable_WithoutApproval_LeavesTheTakenKeyAlone()
    {
        HyprlandApplyResult result = await _service.EnableAsync([Region, Fullscreen], []);

        Assert.That(result.Succeeded, Is.True, result.Message);
        string managed = File.ReadAllText(ManagedPath);
        Assert.That(managed, Does.Not.Contain("hl.unbind"));
        Assert.That(managed, Does.Not.Contain("\"PRINT\", \"XerahS: Region capture\""));
        Assert.That(managed, Does.Contain("SHIFT + PRINT"));
    }

    [Test]
    public async Task Enable_ConfigErrors_RollsBackEverything()
    {
        _hyprctl.ConfigErrors = ["[\"Config error in xerahs.lua: bad bind\"]", "[\"\"]"];

        HyprlandApplyResult result = await _service.EnableAsync([Region], ["PRINT"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("bad bind"));
        Assert.That(_service.IsEnabled, Is.False);
        Assert.That(File.ReadAllText(BindingsPath), Is.EqualTo(OriginalBindings));
        Assert.That(File.Exists(ManagedPath), Is.False);
        Assert.That(File.Exists(result.BackupPath!), Is.True, "the backup stays for the user");
        Assert.That(_hyprctl.Calls.Count(call => call == "reload"), Is.EqualTo(2), "reloads again after restoring");
    }

    [Test]
    public async Task Enable_HyprctlMissing_ChangesNothing()
    {
        _hyprctl.BindsExitCode = -1;

        HyprlandApplyResult result = await _service.EnableAsync([Region], []);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(File.ReadAllText(BindingsPath), Is.EqualTo(OriginalBindings));
        Assert.That(File.Exists(ManagedPath), Is.False);
        Assert.That(Directory.GetFiles(_configDirectory, "*.bak.*"), Is.Empty);
    }

    [Test]
    public async Task Enable_Twice_DoesNotAddASecondIncludeOrBackup()
    {
        await _service.EnableAsync([Region], []);
        string afterFirst = File.ReadAllText(BindingsPath);

        HyprlandApplyResult second = await _service.EnableAsync([Region], []);

        Assert.That(second.BackupPath, Is.Null);
        Assert.That(File.ReadAllText(BindingsPath), Is.EqualTo(afterFirst));
    }

    [Test]
    public async Task Sync_RewritesOnlyTheManagedFileAndSkipsUnchangedContent()
    {
        await _service.EnableAsync([Region], ["PRINT"]);
        string bindingsAfterEnable = File.ReadAllText(BindingsPath);
        _hyprctl.Calls.Clear();

        HyprlandApplyResult unchanged = await _service.SyncAsync([Region], ["PRINT"]);
        Assert.That(unchanged.Succeeded, Is.True);
        Assert.That(_hyprctl.Calls, Is.EqualTo(new[] { "-j binds" }), "no reload when nothing changed");

        HyprlandApplyResult changed = await _service.SyncAsync([Region, Fullscreen], ["PRINT"]);
        Assert.That(changed.Succeeded, Is.True);
        Assert.That(File.ReadAllText(ManagedPath), Does.Contain("SHIFT + PRINT"));
        Assert.That(File.ReadAllText(BindingsPath), Is.EqualTo(bindingsAfterEnable));
    }

    [Test]
    public async Task Sync_ConfigErrors_KeepsThePreviousManagedFile()
    {
        await _service.EnableAsync([Region], ["PRINT"]);
        string previous = File.ReadAllText(ManagedPath);
        _hyprctl.ConfigErrors = ["[\"bad\"]", "[\"\"]"];

        HyprlandApplyResult result = await _service.SyncAsync([Region, Fullscreen], ["PRINT"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(File.ReadAllText(ManagedPath), Is.EqualTo(previous));
    }

    [Test]
    public async Task Disable_EmptiesTheManagedFileAndKeepsTheUserConfig()
    {
        await _service.EnableAsync([Region], ["PRINT"]);
        string bindingsAfterEnable = File.ReadAllText(BindingsPath);

        HyprlandApplyResult result = await _service.DisableAsync();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_service.IsEnabled, Is.False);
        string managed = File.ReadAllText(ManagedPath);
        Assert.That(managed, Does.StartWith(HyprlandManagedConfig.Header));
        Assert.That(managed, Does.Not.Contain("o.bind("));
        Assert.That(managed, Does.Not.Contain("hl.unbind("));
        Assert.That(File.ReadAllText(BindingsPath), Is.EqualTo(bindingsAfterEnable));
    }

    [Test]
    public void WorkflowHotkeys_HyprlandMode_DoesNotTouchTheInnerService()
    {
        var inner = new RecordingHotkeyService();
        int changes = 0;
        _service.WorkflowHotkeysChanged += (_, _) => changes++;
        IHotkeyService wrapped = _service.WrapWorkflowHotkeys(inner);
        var hotkey = new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control);

        _service.IsEnabled = true;
        Assert.That(wrapped.RegisterHotkey(hotkey), Is.True);
        Assert.That(inner.Registered, Is.Empty);
        Assert.That(hotkey.Status, Is.EqualTo(HotkeyStatus.Registered));
        Assert.That(hotkey.NativeTriggerDescription, Is.EqualTo("CTRL + PRINT (Hyprland)"));
        Assert.That(wrapped.IsRegistered(hotkey), Is.True);
        Assert.That(wrapped.GetDiagnostics().BackendName, Is.EqualTo("hyprland"));
        Assert.That(wrapped.UnregisterHotkey(hotkey), Is.True);
        Assert.That(changes, Is.EqualTo(2));

        _service.IsEnabled = false;
        wrapped.RegisterHotkey(hotkey);
        Assert.That(inner.Registered, Is.EqualTo(new[] { hotkey }));
    }

    private sealed class FakeHyprctl : IHyprctlRunner
    {
        public string Binds { get; set; } = "[]";

        public int BindsExitCode { get; set; }

        /// <summary>Successive configerrors outputs; the last one repeats.</summary>
        public List<string> ConfigErrors
        {
            get => _configErrors;
            set
            {
                _configErrors = value;
                _configErrorsCall = 0;
            }
        }

        private List<string> _configErrors = ["[\"\"]"];

        public List<string> Calls { get; } = [];

        private int _configErrorsCall;

        public Task<HyprctlResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            string call = string.Join(' ', arguments);
            Calls.Add(call);
            return Task.FromResult(call switch
            {
                "-j binds" => new HyprctlResult(BindsExitCode, BindsExitCode == 0 ? Binds : string.Empty, BindsExitCode == 0 ? string.Empty : "hyprctl: not found"),
                "reload" => new HyprctlResult(0, "ok", string.Empty),
                "-j configerrors" => new HyprctlResult(0, ConfigErrors[Math.Min(_configErrorsCall++, ConfigErrors.Count - 1)], string.Empty),
                _ => new HyprctlResult(1, string.Empty, "unexpected")
            });
        }
    }

    private sealed class RecordingHotkeyService : IHotkeyService
    {
        public List<HotkeyInfo> Registered { get; } = [];

        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered { add { } remove { } }

        public event EventHandler? HotkeysChanged { add { } remove { } }

        public bool IsSuspended { get; set; }

        public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
        {
            Registered.Add(hotkeyInfo);
            hotkeyInfo.Id = 1;
            return true;
        }

        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo) => Registered.Remove(hotkeyInfo);

        public void UnregisterAll() => Registered.Clear();

        public bool IsRegistered(HotkeyInfo hotkeyInfo) => Registered.Contains(hotkeyInfo);

        public void Dispose()
        {
        }
    }
}
