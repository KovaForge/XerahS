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
using XerahS.Platform.Mobile;

namespace XerahS.Tests.Platform.Mobile;

public class MobileHotkeyServiceTests
{
    [Test]
    public void RegisterHotkey_WithConfiguredHotkey_MarksUnsupportedPlatform()
    {
        using var service = new MobileHotkeyService();
        var hotkey = new HotkeyInfo(Key.K, KeyModifiers.Control)
        {
            Id = 7,
            Status = HotkeyStatus.Registered,
            NativeTriggerDescription = "Ctrl+K"
        };

        bool registered = service.RegisterHotkey(hotkey);

        Assert.Multiple(() =>
        {
            Assert.That(registered, Is.False);
            Assert.That(hotkey.Status, Is.EqualTo(HotkeyStatus.UnsupportedPlatform));
            Assert.That(hotkey.Id, Is.EqualTo(7));
            Assert.That(hotkey.NativeTriggerDescription, Is.Null);
        });
    }

    [Test]
    public void RegisterHotkey_WithInvalidHotkey_MarksNotConfigured()
    {
        using var service = new MobileHotkeyService();
        var hotkey = new HotkeyInfo(Key.None, KeyModifiers.None)
        {
            Status = HotkeyStatus.Registered,
            NativeTriggerDescription = "stale"
        };

        bool registered = service.RegisterHotkey(hotkey);

        Assert.Multiple(() =>
        {
            Assert.That(registered, Is.False);
            Assert.That(hotkey.Status, Is.EqualTo(HotkeyStatus.NotConfigured));
            Assert.That(hotkey.NativeTriggerDescription, Is.Null);
        });
    }

    [Test]
    public void UnregisterHotkey_ClearsRuntimeMetadataEvenWhenUnsupported()
    {
        using var service = new MobileHotkeyService();
        var hotkey = new HotkeyInfo(Key.K, KeyModifiers.Control)
        {
            Id = 9,
            Status = HotkeyStatus.UnsupportedPlatform,
            NativeTriggerDescription = "Ctrl+K"
        };

        bool unregistered = service.UnregisterHotkey(hotkey);

        Assert.Multiple(() =>
        {
            Assert.That(unregistered, Is.False);
            Assert.That(hotkey.Id, Is.EqualTo(0));
            Assert.That(hotkey.Status, Is.EqualTo(HotkeyStatus.NotConfigured));
            Assert.That(hotkey.NativeTriggerDescription, Is.Null);
        });
    }
}
