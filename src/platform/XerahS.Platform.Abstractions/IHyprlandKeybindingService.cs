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

namespace XerahS.Platform.Abstractions;

/// <summary>One workflow hotkey that Hyprland should run through <c>omaxerahs workflow run</c>.</summary>
public sealed record HyprlandWorkflowBinding(string WorkflowId, string Description, HotkeyInfo Hotkey);

/// <summary>A key XerahS wants that Hyprland already binds to something else.</summary>
public sealed record HyprlandBindingConflict(string Keys, string WorkflowDescription, string ExistingDescription)
{
    public override string ToString() => $"{Keys}: {WorkflowDescription} (now: {ExistingDescription})";
}

/// <summary>What <see cref="IHyprlandKeybindingService.ScanAsync"/> found.</summary>
public sealed record HyprlandBindingScan(
    IReadOnlyList<HyprlandWorkflowBinding> Bindings,
    IReadOnlyList<HyprlandBindingConflict> Conflicts,
    IReadOnlyList<string> Skipped,
    string? Error);

/// <summary>Outcome of writing, loading or removing the managed Hyprland keybindings.</summary>
public sealed record HyprlandApplyResult(bool Succeeded, string Message, string? BackupPath = null)
{
    public static HyprlandApplyResult Fail(string message, string? backupPath = null) => new(false, message, backupPath);
}

/// <summary>
/// XIP0088 Phase 5: workflow hotkeys as Hyprland keybindings instead of portal or evdev
/// shortcuts. XerahS owns a generated <c>~/.config/hypr/xerahs.lua</c>; loading it takes one
/// line in the user's config, added only after consent, with a timestamped backup, a
/// <c>hyprctl configerrors</c> check and automatic rollback.
/// Registered only on Hyprland sessions; null elsewhere.
/// </summary>
public interface IHyprlandKeybindingService
{
    /// <summary>True when workflow hotkeys are Hyprland keybindings rather than portal or evdev shortcuts.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Path of the managed file XerahS writes.</summary>
    string ManagedFilePath { get; }

    /// <summary>The user config that loads the managed file, or would after consent.</summary>
    string IncludeTargetPath { get; }

    /// <summary>True when the user config already loads the managed file.</summary>
    bool IsIncluded { get; }

    /// <summary>Raised when workflow hotkeys change while Hyprland mode is on, so the managed file can be rewritten.</summary>
    event EventHandler? WorkflowHotkeysChanged;

    /// <summary>Wraps the workflow hotkey service: portal or evdev when off, Hyprland-managed when on.</summary>
    IHotkeyService WrapWorkflowHotkeys(IHotkeyService inner);

    /// <summary>Reads <c>hyprctl binds -j</c> and reports keys already bound by Omarchy or the user.</summary>
    Task<HyprlandBindingScan> ScanAsync(IReadOnlyList<HyprlandWorkflowBinding> bindings, CancellationToken cancellationToken = default);

    /// <summary>
    /// The consent step: writes the managed file, backs up the user config and adds the include
    /// line when missing, reloads Hyprland and rolls everything back if it reports config errors.
    /// </summary>
    Task<HyprlandApplyResult> EnableAsync(IReadOnlyList<HyprlandWorkflowBinding> bindings, IReadOnlyCollection<string> approvedUnbinds, CancellationToken cancellationToken = default);

    /// <summary>Rewrites the managed file after hotkeys change. Never touches the user config.</summary>
    Task<HyprlandApplyResult> SyncAsync(IReadOnlyList<HyprlandWorkflowBinding> bindings, IReadOnlyCollection<string> approvedUnbinds, CancellationToken cancellationToken = default);

    /// <summary>Empties the managed file and reloads Hyprland, which also restores any keys it unbound.</summary>
    Task<HyprlandApplyResult> DisableAsync(CancellationToken cancellationToken = default);
}
