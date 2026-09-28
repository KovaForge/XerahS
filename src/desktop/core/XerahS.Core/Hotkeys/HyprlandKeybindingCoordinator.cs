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

using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Hotkeys;

/// <summary>What turning on Hyprland keybindings would do, shown to the user before consent.</summary>
public sealed record HyprlandKeybindingPlan(
    bool Supported,
    string? Problem,
    IReadOnlyList<HyprlandBindingEntry> Entries,
    IReadOnlyList<HyprlandBindingConflict> Conflicts,
    IReadOnlyList<string> Unsupported,
    string ManagedFilePath);

/// <summary>
/// Turns Hyprland-managed keybindings on and off (XIP0088 Phase 5). The user's Hyprland config is
/// changed only by <see cref="EnableAsync"/>, which the UI calls after explicit consent.
/// </summary>
public sealed class HyprlandKeybindingCoordinator
{
    private readonly Func<ICompositorKeybindingService?> _service;
    private readonly Func<string> _baseDirectory;

    public HyprlandKeybindingCoordinator(Func<ICompositorKeybindingService?>? service = null, Func<string>? baseDirectory = null)
    {
        _service = service ?? (() => PlatformServices.CompositorKeybindings);
        _baseDirectory = baseDirectory ?? (() => AppContext.BaseDirectory);
    }

    public bool IsSupported => _service()?.IsSupported == true;

    public bool IsEnabled => SettingsManager.Settings?.LinuxHyprlandKeybindings == true;

    public string OmaXerahsPath => Path.Combine(_baseDirectory(), "omaxerahs");

    public async Task<HyprlandKeybindingPlan> PlanAsync(IEnumerable<WorkflowSettings> workflows, CancellationToken cancellationToken = default)
    {
        ICompositorKeybindingService? service = _service();
        if (service?.IsSupported != true)
        {
            return new HyprlandKeybindingPlan(false, "Hyprland keybindings need a Hyprland session.", [], [], [], string.Empty);
        }

        if (!File.Exists(OmaXerahsPath))
        {
            return new HyprlandKeybindingPlan(false, $"omaxerahs was not found next to XerahS ({OmaXerahsPath}).", [], [], [], service.ManagedFilePath);
        }

        var (entries, unsupported) = HyprlandKeybindingGenerator.BuildEntries(workflows, OmaXerahsPath);
        IReadOnlyList<CompositorBinding> existing = await service.GetExistingBindingsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<HyprlandBindingConflict> conflicts = HyprlandKeybindingGenerator.FindConflicts(entries, existing);
        return new HyprlandKeybindingPlan(true, null, entries, conflicts, unsupported, service.ManagedFilePath);
    }

    /// <summary>Applies the plan after consent; <paramref name="approvedUnbinds"/> are conflicting keys the user allowed XerahS to take over.</summary>
    public async Task<CompositorKeybindingResult> EnableAsync(
        HyprlandKeybindingPlan plan,
        IReadOnlyCollection<string> approvedUnbinds,
        CancellationToken cancellationToken = default)
    {
        ICompositorKeybindingService? service = _service();
        if (!plan.Supported || service?.IsSupported != true)
        {
            return new CompositorKeybindingResult(false, plan.Problem ?? "Hyprland keybindings are not supported here.");
        }

        string content = HyprlandKeybindingGenerator.BuildFile(plan.Entries, service.UsesOmarchyHelpers, approvedUnbinds);
        CompositorKeybindingResult result = await service.ApplyAsync(content, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            SettingsManager.Settings.LinuxHyprlandKeybindings = true;
            SettingsManager.Settings.LinuxHyprlandApprovedUnbinds = approvedUnbinds.Select(CompositorBinding.Normalize).Distinct().ToList();
            await SettingsManager.SaveApplicationConfigAsync().ConfigureAwait(false);
        }

        return result;
    }

    public async Task<CompositorKeybindingResult> DisableAsync(CancellationToken cancellationToken = default)
    {
        ICompositorKeybindingService? service = _service();
        CompositorKeybindingResult result = service == null
            ? new CompositorKeybindingResult(true, "Hyprland keybindings were not set up.")
            : await service.DisableAsync(HyprlandKeybindingGenerator.BuildEmptyFile(), cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            SettingsManager.Settings.LinuxHyprlandKeybindings = false;
            await SettingsManager.SaveApplicationConfigAsync().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Regenerates the managed file after hotkeys changed, when the mode is on.</summary>
    public async Task RefreshAsync(IEnumerable<WorkflowSettings> workflows, CancellationToken cancellationToken = default)
    {
        ICompositorKeybindingService? service = _service();
        if (!IsEnabled || service?.IsSupported != true || !File.Exists(OmaXerahsPath))
        {
            return;
        }

        var (entries, _) = HyprlandKeybindingGenerator.BuildEntries(workflows, OmaXerahsPath);
        string content = HyprlandKeybindingGenerator.BuildFile(
            entries, service.UsesOmarchyHelpers, SettingsManager.Settings.LinuxHyprlandApprovedUnbinds ?? []);
        CompositorKeybindingResult result = await service.UpdateAsync(content, cancellationToken).ConfigureAwait(false);
        DebugHelper.WriteLine($"Hyprland keybindings: {result.Message}");
    }
}
