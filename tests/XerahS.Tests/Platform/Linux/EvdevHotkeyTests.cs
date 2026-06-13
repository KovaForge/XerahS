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
using XerahS.Platform.Linux.Input;
using XerahS.Platform.Linux.Input.Evdev;

namespace XerahS.Tests.Platform.Linux;

public class EvdevHotkeyTests
{
    [Test]
    public void EvdevKeyMap_PrintScreen_MapsToSysRq()
    {
        Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.PrintScreen, out var code), Is.True);
        Assert.That(code, Is.EqualTo((ushort)99));
    }

    [Test]
    public void EvdevKeyMap_Snapshot_MapsToSysRq()
    {
        Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.Snapshot, out var code), Is.True);
        Assert.That(code, Is.EqualTo((ushort)99));
    }

    [Test]
    public void EvdevKeyMap_LettersAndDigits_MapToKernelCodes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.A, out var a) && a == 30, Is.True);
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.Z, out var z) && z == 44, Is.True);
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.D1, out var d1) && d1 == 2, Is.True);
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.D0, out var d0) && d0 == 11, Is.True);
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.F12, out var f12) && f12 == 88, Is.True);
            Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.NumPad5, out var kp5) && kp5 == 76, Is.True);
        });
    }

    [Test]
    public void EvdevKeyMap_UnmappedKey_ReturnsFalse()
    {
        Assert.That(EvdevKeyMap.TryGetEvdevCode(Key.None, out _), Is.False);
    }

    [Test]
    public void EvdevKeyMap_GetModifierFlag_ResolvesLeftAndRightVariants()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EvdevKeyMap.GetModifierFlag(InputEventCodes.KEY_LEFTCTRL), Is.EqualTo(KeyModifiers.Control));
            Assert.That(EvdevKeyMap.GetModifierFlag(InputEventCodes.KEY_RIGHTCTRL), Is.EqualTo(KeyModifiers.Control));
            Assert.That(EvdevKeyMap.GetModifierFlag(InputEventCodes.KEY_LEFTSHIFT), Is.EqualTo(KeyModifiers.Shift));
            Assert.That(EvdevKeyMap.GetModifierFlag(InputEventCodes.KEY_RIGHTALT), Is.EqualTo(KeyModifiers.Alt));
            Assert.That(EvdevKeyMap.GetModifierFlag(InputEventCodes.KEY_LEFTMETA), Is.EqualTo(KeyModifiers.Meta));
            Assert.That(EvdevKeyMap.GetModifierFlag(30), Is.EqualTo(KeyModifiers.None)); // 'A'
        });
    }

    [Test]
    public void ModifierStateTracker_TracksPressAndRelease()
    {
        var tracker = new ModifierStateTracker();

        tracker.OnKeyDown(InputEventCodes.KEY_LEFTCTRL);
        tracker.OnKeyDown(InputEventCodes.KEY_LEFTSHIFT);
        Assert.That(tracker.CurrentModifiers, Is.EqualTo(KeyModifiers.Control | KeyModifiers.Shift));

        tracker.OnKeyUp(InputEventCodes.KEY_LEFTSHIFT);
        Assert.That(tracker.CurrentModifiers, Is.EqualTo(KeyModifiers.Control));

        tracker.Clear();
        Assert.That(tracker.CurrentModifiers, Is.EqualTo(KeyModifiers.None));
    }

    [Test]
    public void ModifierStateTracker_IgnoresNonModifierKeys()
    {
        var tracker = new ModifierStateTracker();
        tracker.OnKeyDown(30); // 'A'
        Assert.That(tracker.CurrentModifiers, Is.EqualTo(KeyModifiers.None));
    }

    [Test]
    public void EvdevHotkeyMatcher_IsMatch_RequiresExactModifiers()
    {
        var hotkey = new HotkeyInfo(Key.S, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Multiple(() =>
        {
            // 'S' is evdev code 31.
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 31, KeyModifiers.Control | KeyModifiers.Shift), Is.True);
            // Missing a modifier.
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 31, KeyModifiers.Control), Is.False);
            // Extra modifier.
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 31, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt), Is.False);
            // Wrong key.
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 30, KeyModifiers.Control | KeyModifiers.Shift), Is.False);
        });
    }

    [Test]
    public void EvdevHotkeyMatcher_IsMatch_NoModifierHotkey()
    {
        var hotkey = new HotkeyInfo(Key.PrintScreen);

        Assert.Multiple(() =>
        {
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 99, KeyModifiers.None), Is.True);
            Assert.That(EvdevHotkeyMatcher.IsMatch(hotkey, 99, KeyModifiers.Control), Is.False);
        });
    }

    [Test]
    public void EvdevHotkeyMatcher_TryMatch_DebouncesWithinWindow()
    {
        var matcher = new EvdevHotkeyMatcher { DebounceMs = 250 };
        var hotkey = new HotkeyInfo(Key.PrintScreen) { Id = 1 };

        Assert.Multiple(() =>
        {
            Assert.That(matcher.TryMatch(hotkey, 99, KeyModifiers.None, 1000), Is.True);
            // Within debounce window -> suppressed.
            Assert.That(matcher.TryMatch(hotkey, 99, KeyModifiers.None, 1100), Is.False);
            // After window -> allowed again.
            Assert.That(matcher.TryMatch(hotkey, 99, KeyModifiers.None, 1300), Is.True);
        });
    }

    [Test]
    public void EvdevHotkeyMatcher_TryMatch_NonMatchIsNotDebounced()
    {
        var matcher = new EvdevHotkeyMatcher { DebounceMs = 250 };
        var hotkey = new HotkeyInfo(Key.PrintScreen) { Id = 1 };

        // A non-matching event must never record a debounce timestamp.
        Assert.That(matcher.TryMatch(hotkey, 30, KeyModifiers.None, 1000), Is.False);
        Assert.That(matcher.TryMatch(hotkey, 99, KeyModifiers.None, 1000), Is.True);
    }
}
