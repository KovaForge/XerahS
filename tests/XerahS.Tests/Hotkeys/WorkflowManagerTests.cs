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
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Hotkeys;

public class WorkflowManagerTests
{
    [Test]
    public void RegisterHotkey_WhenClearedToNone_UnregistersPreviousBinding()
    {
        var service = new FakeHotkeyService();
        using var manager = new WorkflowManager(service);
        var settings = new WorkflowSettings(
            WorkflowType.RectangleRegion,
            new HotkeyInfo(Key.L, KeyModifiers.Control | KeyModifiers.Shift));

        var initialRegistration = manager.RegisterHotkey(settings);
        Assert.That(initialRegistration, Is.True);
        Assert.That(service.IsRegistered(settings.HotkeyInfo), Is.True);

        settings.HotkeyInfo.Key = Key.None;
        settings.HotkeyInfo.Modifiers = KeyModifiers.None;

        var clearedRegistration = manager.RegisterHotkey(settings);

        Assert.Multiple(() =>
        {
            Assert.That(clearedRegistration, Is.False);
            Assert.That(settings.HotkeyInfo.Status, Is.EqualTo(HotkeyStatus.NotConfigured));
            Assert.That(settings.HotkeyInfo.Id, Is.EqualTo(0));
            Assert.That(settings.HotkeyInfo.NativeTriggerDescription, Is.Null);
            Assert.That(service.IsRegistered(settings.HotkeyInfo), Is.False);
            Assert.That(service.UnregisterCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void RegisterHotkey_WhenCleanupFails_PreservesExistingRuntimeMetadataAndMapping()
    {
        var service = new FakeHotkeyService { FailNextUnregister = true };
        using var manager = new WorkflowManager(service);
        var settings = new WorkflowSettings(
            WorkflowType.RectangleRegion,
            new HotkeyInfo(Key.L, KeyModifiers.Control | KeyModifiers.Shift)
            {
                NativeTriggerDescription = "Ctrl+Shift+L"
            });
        bool triggered = false;

        Assert.That(manager.RegisterHotkey(settings), Is.True);
        settings.HotkeyInfo.NativeTriggerDescription = "Ctrl+Shift+L";
        manager.HotkeyTriggered += (_, workflow) => triggered = ReferenceEquals(workflow, settings);

        settings.HotkeyInfo.Key = Key.M;
        settings.HotkeyInfo.Modifiers = KeyModifiers.Control | KeyModifiers.Alt;

        var registrationResult = manager.RegisterHotkey(settings);
        service.RaiseHotkeyTriggered(settings.HotkeyInfo);

        Assert.Multiple(() =>
        {
            Assert.That(registrationResult, Is.False);
            Assert.That(settings.HotkeyInfo.Id, Is.Not.EqualTo(0));
            Assert.That(settings.HotkeyInfo.Status, Is.EqualTo(HotkeyStatus.Failed));
            Assert.That(settings.HotkeyInfo.NativeTriggerDescription, Is.EqualTo("Ctrl+Shift+L"));
            Assert.That(service.UnregisterCallCount, Is.EqualTo(1));
            Assert.That(triggered, Is.True);
            Assert.That(manager.Workflows.Contains(settings), Is.True);
        });
    }

    [Test]
    public void RegisterHotkey_WhenPreviousFailureLeftStaleId_RetriesWithoutUnregistering()
    {
        var service = new FakeHotkeyService { FailNextRegisterAfterAssigningId = true };
        using var manager = new WorkflowManager(service);
        var settings = new WorkflowSettings(
            WorkflowType.RectangleRegion,
            new HotkeyInfo(Key.L, KeyModifiers.Control | KeyModifiers.Shift));

        bool firstRegistration = manager.RegisterHotkey(settings);
        ushort failedId = settings.HotkeyInfo.Id;
        bool secondRegistration = manager.RegisterHotkey(settings);

        Assert.Multiple(() =>
        {
            Assert.That(firstRegistration, Is.False);
            Assert.That(failedId, Is.Not.EqualTo(0));
            Assert.That(secondRegistration, Is.True);
            Assert.That(settings.HotkeyInfo.Status, Is.EqualTo(HotkeyStatus.Registered));
            Assert.That(settings.HotkeyInfo.Id, Is.Not.EqualTo(failedId));
            Assert.That(service.UnregisterCallCount, Is.EqualTo(0));
            Assert.That(service.IsRegistered(settings.HotkeyInfo), Is.True);
        });
    }

    [Test]
    public void WorkflowsChanged_WhenHotkeyMetadataChanges_IsRelayedFromService()
    {
        var service = new FakeHotkeyService();
        using var manager = new WorkflowManager(service);
        int callCount = 0;

        manager.WorkflowsChanged += (_, _) => callCount++;
        service.RaiseHotkeysChanged();

        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void UnregisterHotkey_ClearsRuntimeMetadata()
    {
        var service = new FakeHotkeyService();
        using var manager = new WorkflowManager(service);
        var settings = new WorkflowSettings(
            WorkflowType.RectangleRegion,
            new HotkeyInfo(Key.K, KeyModifiers.Control)
            {
                NativeTriggerDescription = "Ctrl+K"
            });

        Assert.That(manager.RegisterHotkey(settings), Is.True);

        bool unregistered = manager.UnregisterHotkey(settings);

        Assert.Multiple(() =>
        {
            Assert.That(unregistered, Is.True);
            Assert.That(settings.HotkeyInfo.Id, Is.EqualTo(0));
            Assert.That(settings.HotkeyInfo.Status, Is.EqualTo(HotkeyStatus.NotConfigured));
            Assert.That(settings.HotkeyInfo.NativeTriggerDescription, Is.Null);
            Assert.That(service.UnregisterCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void UnregisterHotkey_WhenServiceFails_KeepsWorkflowMappedAndMetadataIntact()
    {
        var service = new FakeHotkeyService { FailNextUnregister = true };
        using var manager = new WorkflowManager(service);
        var settings = new WorkflowSettings(
            WorkflowType.RectangleRegion,
            new HotkeyInfo(Key.K, KeyModifiers.Control)
            {
                NativeTriggerDescription = "Ctrl+K"
            });
        bool triggered = false;

        Assert.That(manager.RegisterHotkey(settings), Is.True);
        settings.HotkeyInfo.NativeTriggerDescription = "Ctrl+K";
        manager.HotkeyTriggered += (_, workflow) => triggered = ReferenceEquals(workflow, settings);

        bool unregistered = manager.UnregisterHotkey(settings);
        service.RaiseHotkeyTriggered(settings.HotkeyInfo);

        Assert.Multiple(() =>
        {
            Assert.That(unregistered, Is.False);
            Assert.That(settings.HotkeyInfo.Id, Is.Not.EqualTo(0));
            Assert.That(settings.HotkeyInfo.Status, Is.EqualTo(HotkeyStatus.Failed));
            Assert.That(settings.HotkeyInfo.NativeTriggerDescription, Is.EqualTo("Ctrl+K"));
            Assert.That(manager.Workflows.Contains(settings), Is.True);
            Assert.That(triggered, Is.True);
        });
    }

    [Test]
    public void HotkeyInfo_DisplayString_PrefersNativeTriggerDescription()
    {
        var hotkey = new HotkeyInfo(Key.F, KeyModifiers.Control | KeyModifiers.Shift)
        {
            NativeTriggerDescription = "Ctrl+Alt+S"
        };

        Assert.That(hotkey.GetDisplayString(), Is.EqualTo("Ctrl+Alt+S"));
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
        private readonly HashSet<ushort> _registeredIds = new();
        private ushort _nextId = 1;

        public int UnregisterCallCount { get; private set; }
        public bool FailNextRegisterAfterAssigningId { get; set; }
        public bool FailNextUnregister { get; set; }

        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        public event EventHandler? HotkeysChanged;

        public bool IsSuspended { get; set; }

        public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (!hotkeyInfo.IsValid)
            {
                hotkeyInfo.Status = HotkeyStatus.NotConfigured;
                return false;
            }

            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = _nextId++;
            }

            if (FailNextRegisterAfterAssigningId)
            {
                FailNextRegisterAfterAssigningId = false;
                hotkeyInfo.Status = HotkeyStatus.Failed;
                return false;
            }

            hotkeyInfo.Status = HotkeyStatus.Registered;
            _registeredIds.Add(hotkeyInfo.Id);
            return true;
        }

        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (hotkeyInfo.Id == 0)
            {
                return false;
            }

            UnregisterCallCount++;

            if (FailNextUnregister)
            {
                FailNextUnregister = false;
                hotkeyInfo.Status = HotkeyStatus.Failed;
                return false;
            }

            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return _registeredIds.Remove(hotkeyInfo.Id);
        }

        public void UnregisterAll()
        {
            _registeredIds.Clear();
        }

        public bool IsRegistered(HotkeyInfo hotkeyInfo)
        {
            return hotkeyInfo.Id != 0 && _registeredIds.Contains(hotkeyInfo.Id);
        }

        public void Dispose()
        {
        }

        public void RaiseHotkeysChanged()
        {
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHotkeyTriggered(HotkeyInfo hotkeyInfo)
        {
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(hotkeyInfo));
        }
    }
}
