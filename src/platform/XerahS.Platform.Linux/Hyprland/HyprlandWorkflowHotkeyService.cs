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

using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Hyprland;

/// <summary>
/// The hotkey service workflow hotkeys go through on Hyprland. With Hyprland mode off it is the
/// portal, evdev or X11 service unchanged. With it on, nothing is grabbed: Hyprland runs the
/// workflow through <c>omaxerahs workflow run</c>, so a key never fires twice. Other hotkeys
/// (assistant, command palette) keep using the inner service directly.
/// </summary>
internal sealed class HyprlandWorkflowHotkeyService : IHotkeyService
{
    // Ids above the range the inner services hand out, so WorkflowManager's map never collides.
    private const ushort FirstManagedId = 0xC000;

    private readonly IHotkeyService _inner;
    private readonly Func<bool> _isHyprlandMode;
    private readonly Action _changed;
    private readonly HashSet<ushort> _managedIds = [];
    private ushort _nextManagedId = FirstManagedId;

    public HyprlandWorkflowHotkeyService(IHotkeyService inner, Func<bool> isHyprlandMode, Action changed)
    {
        _inner = inner;
        _isHyprlandMode = isHyprlandMode;
        _changed = changed;
        _inner.HotkeyTriggered += OnInnerTriggered;
        _inner.HotkeysChanged += OnInnerChanged;
    }

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    public event EventHandler? HotkeysChanged;

    public bool IsSuspended
    {
        get => _inner.IsSuspended;
        set => _inner.IsSuspended = value;
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (!_isHyprlandMode())
        {
            return _inner.RegisterHotkey(hotkeyInfo);
        }

        hotkeyInfo.Id = _nextManagedId == ushort.MaxValue ? _nextManagedId : _nextManagedId++;
        _managedIds.Add(hotkeyInfo.Id);
        hotkeyInfo.Status = HyprlandKeyMap.TryFormat(hotkeyInfo, out string keys) ? HotkeyStatus.Registered : HotkeyStatus.Failed;
        hotkeyInfo.NativeTriggerDescription = hotkeyInfo.Status == HotkeyStatus.Registered ? $"{keys} (Hyprland)" : null;
        _changed();
        return hotkeyInfo.Status == HotkeyStatus.Registered;
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (_managedIds.Remove(hotkeyInfo.Id))
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            _changed();
            return true;
        }

        return _inner.UnregisterHotkey(hotkeyInfo);
    }

    public void UnregisterAll()
    {
        bool hadManaged = _managedIds.Count > 0;
        _managedIds.Clear();
        _inner.UnregisterAll();
        if (hadManaged)
        {
            _changed();
        }
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo) =>
        _managedIds.Contains(hotkeyInfo.Id) || _inner.IsRegistered(hotkeyInfo);

    public Task<bool> ShowInteractiveConfigurationAsync() =>
        _isHyprlandMode() ? Task.FromResult(false) : _inner.ShowInteractiveConfigurationAsync();

    public void NotifyWindowReady() => _inner.NotifyWindowReady();

    public HotkeyDiagnostics GetDiagnostics() =>
        _isHyprlandMode()
            ? new HotkeyDiagnostics(HotkeyBackendState.Native, "hyprland", null)
            : _inner.GetDiagnostics();

    public void Dispose()
    {
        _inner.HotkeyTriggered -= OnInnerTriggered;
        _inner.HotkeysChanged -= OnInnerChanged;
    }

    private void OnInnerTriggered(object? sender, HotkeyTriggeredEventArgs e) => HotkeyTriggered?.Invoke(this, e);

    private void OnInnerChanged(object? sender, EventArgs e) => HotkeysChanged?.Invoke(this, e);
}
