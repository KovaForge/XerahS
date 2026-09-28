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
public class HyprlandKeybindingTests
{
    // Trimmed from `hyprctl binds -j` on Omarchy 4.0.4 (Appendix A), plus a mouse bind, a
    // submap bind and a bind left by an earlier XerahS managed file.
    private const string BindsFixture = """
    [
      {"locked": false, "mouse": false, "release": false, "repeat": false, "non_consuming": false, "has_description": true, "modmask": 0, "submap": "", "key": "Print", "keycode": 0, "catch_all": false, "description": "Screenshot", "dispatcher": "exec", "arg": "omarchy-capture-screenshot"},
      {"locked": false, "mouse": false, "release": false, "repeat": false, "non_consuming": false, "has_description": true, "modmask": 8, "submap": "", "key": "PRINT", "keycode": 0, "catch_all": false, "description": "Screencast", "dispatcher": "exec", "arg": "omarchy-capture-screenrecording"},
      {"locked": false, "mouse": false, "release": false, "repeat": false, "non_consuming": false, "has_description": true, "modmask": 64, "submap": "", "key": "SPACE", "keycode": 0, "catch_all": false, "description": "Launch apps", "dispatcher": "exec", "arg": "omarchy-launch-walker"},
      {"locked": false, "mouse": true, "release": false, "repeat": false, "non_consuming": false, "has_description": false, "modmask": 64, "submap": "", "key": "mouse:272", "keycode": 0, "catch_all": false, "description": "", "dispatcher": "movewindow", "arg": ""},
      {"locked": false, "mouse": false, "release": false, "repeat": false, "non_consuming": false, "has_description": false, "modmask": 0, "submap": "resize", "key": "F1", "keycode": 0, "catch_all": false, "description": "", "dispatcher": "resizeactive", "arg": "10 0"},
      {"locked": false, "mouse": false, "release": false, "repeat": false, "non_consuming": false, "has_description": true, "modmask": 20, "submap": "", "key": "F1", "keycode": 0, "catch_all": false, "description": "XerahS: Region capture", "dispatcher": "exec", "arg": "'/usr/lib/xerahs/omaxerahs' workflow run 'bc904c7e'"}
    ]
    """;

    [TestCase(Key.PrintScreen, KeyModifiers.None, "PRINT")]
    [TestCase(Key.PrintScreen, KeyModifiers.Alt, "ALT + PRINT")]
    [TestCase(Key.F1, KeyModifiers.Control, "CTRL + F1")]
    [TestCase(Key.S, KeyModifiers.Meta | KeyModifiers.Shift, "SUPER + SHIFT + S")]
    [TestCase(Key.D4, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta, "SUPER + CTRL + ALT + SHIFT + 4")]
    [TestCase(Key.NumPad7, KeyModifiers.None, "KP_7")]
    [TestCase(Key.F24, KeyModifiers.None, "F24")]
    [TestCase(Key.Space, KeyModifiers.Meta, "SUPER + SPACE")]
    [TestCase(Key.PageUp, KeyModifiers.None, "PAGE_UP")]
    [TestCase(Key.OemPlus, KeyModifiers.Control, "CTRL + EQUAL")]
    [TestCase(Key.Oem2, KeyModifiers.None, "SLASH")]
    [TestCase(Key.Enter, KeyModifiers.Shift, "SHIFT + RETURN")]
    public void KeyMap_FormatsHotkeys(Key key, KeyModifiers modifiers, string expected)
    {
        Assert.That(HyprlandKeyMap.TryFormat(new HotkeyInfo(key, modifiers), out string keys), Is.True);
        Assert.That(keys, Is.EqualTo(expected));
    }

    [Test]
    public void KeyMap_RejectsModifierOnlyAndUnnamedKeys()
    {
        Assert.That(HyprlandKeyMap.TryFormat(new HotkeyInfo(Key.LeftCtrl, KeyModifiers.Control), out _), Is.False);
        Assert.That(HyprlandKeyMap.TryFormat(new HotkeyInfo(Key.None), out _), Is.False);
        Assert.That(HyprlandKeyMap.TryFormat(new HotkeyInfo(Key.MediaPlayPause), out _), Is.False);
    }

    [Test]
    public void KeyMap_Modmask_MatchesHyprland()
    {
        Assert.That(HyprlandKeyMap.ToModmask(new HotkeyInfo(Key.A, KeyModifiers.Shift | KeyModifiers.Meta)), Is.EqualTo(65));
        Assert.That(HyprlandKeyMap.ToModmask(new HotkeyInfo(Key.A, KeyModifiers.Control | KeyModifiers.Alt)), Is.EqualTo(12));
        // Num Lock (Mod2 = 16) never makes a bind differ.
        Assert.That(HyprlandKeyMap.NormalizeModmask(20), Is.EqualTo(4));
        Assert.That(HyprlandKeyMap.NormalizeKeyName("Prior"), Is.EqualTo("PAGE_UP"));
        Assert.That(HyprlandKeyMap.NormalizeKeyName("print"), Is.EqualTo("PRINT"));
    }

    [Test]
    public void Parser_SkipsMouseAndSubmapBinds()
    {
        IReadOnlyList<HyprlandExistingBind> binds = HyprlandBindsParser.Parse(BindsFixture);

        Assert.That(binds.Select(b => b.Key), Is.EqualTo(new[] { "Print", "PRINT", "SPACE", "F1" }));
        Assert.That(binds[3].IsXerahS, Is.True);
    }

    [Test]
    public void Parser_FindsConflictsCaseInsensitivelyAndIgnoresXerahS()
    {
        IReadOnlyList<HyprlandExistingBind> binds = HyprlandBindsParser.Parse(BindsFixture);

        Assert.That(HyprlandBindsParser.FindConflict(binds, 0, "PRINT")?.Summary, Is.EqualTo("Screenshot"));
        Assert.That(HyprlandBindsParser.FindConflict(binds, HyprlandKeyMap.AltMask, "PRINT")?.Summary, Is.EqualTo("Screencast"));
        Assert.That(HyprlandBindsParser.FindConflict(binds, HyprlandKeyMap.ShiftMask, "PRINT"), Is.Null);
        Assert.That(HyprlandBindsParser.FindConflict(binds, HyprlandKeyMap.ControlMask, "F1"), Is.Null, "earlier XerahS binds are not conflicts");
        Assert.That(HyprlandBindsParser.FindConflict(binds, 0, "F1"), Is.Null, "submap binds are not global");
    }

    [TestCase("[\"\"]", 0)]
    [TestCase("", 0)]
    [TestCase("no errors", 0)]
    [TestCase("[\"Config error in file /home/u/.config/hypr/xerahs.lua at line 4: unknown key\"]", 1)]
    [TestCase("error one\nerror two\n", 2)]
    public void Parser_ReadsConfigErrors(string output, int expected)
    {
        Assert.That(HyprlandBindsParser.ParseConfigErrors(output), Has.Count.EqualTo(expected));
    }

    [Test]
    public void ManagedConfig_Omarchy_Snapshot()
    {
        var bindings = new[]
        {
            new HyprlandManagedBinding("PRINT", "Region capture", "bc904c7e-4edc-4726-b53e-4f6cf8646eee"),
            new HyprlandManagedBinding("CTRL + F1", "Say \"hi\"", "it's"),
        };

        string content = HyprlandManagedConfig.Render(bindings, ["PRINT", "ALT + PRINT"], useOmarchyHelpers: true, "/usr/lib/xerahs/omaxerahs");

        Assert.That(content, Is.EqualTo(
            "-- Managed by XerahS. Changes are overwritten; edit hotkeys in XerahS.\n" +
            "-- Turn off in XerahS: Settings > Hotkeys > Use Hyprland keybindings.\n" +
            "\n" +
            "hl.unbind(\"PRINT\")\n" +
            "o.bind(\"PRINT\", \"XerahS: Region capture\", \"'/usr/lib/xerahs/omaxerahs' workflow run 'bc904c7e-4edc-4726-b53e-4f6cf8646eee'\")\n" +
            "o.bind(\"CTRL + F1\", \"XerahS: Say \\\"hi\\\"\", \"'/usr/lib/xerahs/omaxerahs' workflow run 'it'\\\\''s'\")\n"));
    }

    [Test]
    public void ManagedConfig_PlainHyprland_UsesHlBind()
    {
        string content = HyprlandManagedConfig.Render(
            [new HyprlandManagedBinding("SUPER + SHIFT + S", "Region", "abc")], [], useOmarchyHelpers: false, "/opt/x/omaxerahs");

        Assert.That(content, Does.Contain("hl.bind(\"SUPER + SHIFT + S\", hl.dsp.exec_cmd(\"'/opt/x/omaxerahs' workflow run 'abc'\"), { description = \"XerahS: Region\" })\n"));
        Assert.That(content, Does.Not.Contain("o.bind"));
        Assert.That(content, Does.Not.Contain("hl.unbind"));
    }

    [Test]
    public void ManagedConfig_ToManagedBindings_SkipsUnnamedAndDuplicateKeys()
    {
        var skipped = new List<string>();
        IReadOnlyList<HyprlandManagedBinding> managed = HyprlandManagedConfig.ToManagedBindings(
        [
            new HyprlandWorkflowBinding("a", "Region", new HotkeyInfo(Key.PrintScreen)),
            new HyprlandWorkflowBinding("b", "Again", new HotkeyInfo(Key.PrintScreen)),
            new HyprlandWorkflowBinding("c", "Media", new HotkeyInfo(Key.MediaPlayPause)),
            new HyprlandWorkflowBinding("d", "Two\nlines", new HotkeyInfo(Key.F2)),
        ], skipped);

        Assert.That(managed.Select(m => m.WorkflowId), Is.EqualTo(new[] { "a", "d" }));
        Assert.That(managed[1].Description, Is.EqualTo("Two lines"));
        Assert.That(skipped, Has.Count.EqualTo(2));
    }

    [Test]
    public void ManagedConfig_Include_IsDetected()
    {
        string include = HyprlandManagedConfig.RenderInclude("/home/u/.config/hypr/xerahs.lua");

        Assert.That(include, Does.Contain("dofile(path)"));
        Assert.That(HyprlandManagedConfig.ContainsInclude("-- mine\n" + include, "/home/u/.config/hypr/xerahs.lua"), Is.True);
        Assert.That(HyprlandManagedConfig.ContainsInclude("-- mine\n", "/home/u/.config/hypr/xerahs.lua"), Is.False);
    }
}
