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

using System.Text;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.OmaSnap;

namespace XerahS.Platform.Linux.Hyprland;

/// <summary>Result of one <c>hyprctl</c> call.</summary>
public sealed record HyprctlResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs <c>hyprctl</c>; replaced in tests.</summary>
public interface IHyprctlRunner
{
    Task<HyprctlResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

internal sealed class HyprctlRunner : IHyprctlRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HyprctlResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var result = await OmaSnapProcess.RunAsync("hyprctl", arguments, environment: null, Timeout, cancellationToken).ConfigureAwait(false);
            if (result.TimedOut)
            {
                return new HyprctlResult(-1, result.StandardOutput, "hyprctl timed out.");
            }

            return new HyprctlResult(result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new HyprctlResult(-1, string.Empty, $"hyprctl could not run: {ex.Message}");
        }
    }
}

/// <summary>
/// XIP0088 Phase 5. Owns <c>~/.config/hypr/xerahs.lua</c> and the consent flow that loads it.
/// Never edits anything outside the user's own Hyprland config folder, and never edits the
/// user config without a timestamped backup and a rollback on <c>hyprctl configerrors</c>.
/// </summary>
public sealed class HyprlandKeybindingService : IHyprlandKeybindingService
{
    private readonly IHyprctlRunner _hyprctl;
    private readonly string _configDirectory;
    private readonly bool _useOmarchyHelpers;
    private readonly string _omaXerahsPath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HyprlandKeybindingService(bool useOmarchyHelpers)
        : this(new HyprctlRunner(), ResolveConfigDirectory(), useOmarchyHelpers, Path.Combine(AppContext.BaseDirectory, "omaxerahs"), () => DateTimeOffset.UtcNow)
    {
    }

    public HyprlandKeybindingService(
        IHyprctlRunner hyprctl,
        string configDirectory,
        bool useOmarchyHelpers,
        string omaXerahsPath,
        Func<DateTimeOffset> clock)
    {
        _hyprctl = hyprctl;
        _configDirectory = configDirectory;
        _useOmarchyHelpers = useOmarchyHelpers;
        _omaXerahsPath = omaXerahsPath;
        _clock = clock;
    }

    public bool IsEnabled { get; set; }

    public string ManagedFilePath => Path.Combine(_configDirectory, HyprlandManagedConfig.FileName);

    /// <summary>
    /// Omarchy keeps personal keybindings in <c>bindings.lua</c>; plain Hyprland Lua setups get
    /// the line in <c>hyprland.lua</c>.
    /// </summary>
    public string IncludeTargetPath
    {
        get
        {
            string bindings = Path.Combine(_configDirectory, "bindings.lua");
            return File.Exists(bindings) ? bindings : Path.Combine(_configDirectory, "hyprland.lua");
        }
    }

    public bool IsIncluded
    {
        get
        {
            try
            {
                string target = IncludeTargetPath;
                return File.Exists(target) && HyprlandManagedConfig.ContainsInclude(File.ReadAllText(target), ManagedFilePath);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    public event EventHandler? WorkflowHotkeysChanged;

    public IHotkeyService WrapWorkflowHotkeys(IHotkeyService inner) =>
        new HyprlandWorkflowHotkeyService(inner, () => IsEnabled, () => WorkflowHotkeysChanged?.Invoke(this, EventArgs.Empty));

    public async Task<HyprlandBindingScan> ScanAsync(IReadOnlyList<HyprlandWorkflowBinding> bindings, CancellationToken cancellationToken = default)
    {
        var skipped = new List<string>();
        IReadOnlyList<HyprlandManagedBinding> managed = HyprlandManagedConfig.ToManagedBindings(bindings, skipped);

        HyprctlResult result = await _hyprctl.RunAsync(["-j", "binds"], cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return new HyprlandBindingScan(bindings, [], skipped, DescribeFailure("hyprctl binds", result));
        }

        IReadOnlyList<HyprlandExistingBind> existing;
        try
        {
            existing = HyprlandBindsParser.Parse(result.Output);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new HyprlandBindingScan(bindings, [], skipped, $"Could not read hyprctl binds: {ex.Message}");
        }

        var conflicts = new List<HyprlandBindingConflict>();
        foreach (HyprlandManagedBinding binding in managed)
        {
            HyprlandWorkflowBinding source = bindings.First(b => b.WorkflowId == binding.WorkflowId);
            HyprlandKeyMap.TryGetKeyName(source.Hotkey.Key, out string keyName);
            HyprlandExistingBind? conflict = HyprlandBindsParser.FindConflict(existing, HyprlandKeyMap.ToModmask(source.Hotkey), keyName);
            if (conflict != null)
            {
                conflicts.Add(new HyprlandBindingConflict(binding.Keys, binding.Description, conflict.Summary));
            }
        }

        return new HyprlandBindingScan(bindings, conflicts, skipped, null);
    }

    public async Task<HyprlandApplyResult> EnableAsync(
        IReadOnlyList<HyprlandWorkflowBinding> bindings,
        IReadOnlyCollection<string> approvedUnbinds,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string target = IncludeTargetPath;
            if (!File.Exists(target))
            {
                return HyprlandApplyResult.Fail(
                    $"No Hyprland Lua config at {target}. XerahS keybindings need Hyprland's Lua config (hyprland.lua).");
            }

            (string? content, string? error, int count) = await RenderAsync(bindings, approvedUnbinds, cancellationToken).ConfigureAwait(false);
            if (content == null)
            {
                return HyprlandApplyResult.Fail(error!);
            }

            string? previousManaged = ReadIfExists(ManagedFilePath);
            string? backupPath = null;
            try
            {
                WriteAtomically(ManagedFilePath, content);

                string targetText = File.ReadAllText(target);
                if (!HyprlandManagedConfig.ContainsInclude(targetText, ManagedFilePath))
                {
                    backupPath = $"{target}.bak.{_clock().ToUnixTimeSeconds()}";
                    File.Copy(target, backupPath, overwrite: false);
                    File.AppendAllText(target, HyprlandManagedConfig.RenderInclude(ManagedFilePath), new UTF8Encoding(false));
                    DebugHelper.WriteLine($"Hyprland keybindings: backed up {target} to {backupPath} and added the XerahS include.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Restore(target, backupPath, previousManaged);
                return HyprlandApplyResult.Fail($"Could not write the Hyprland config: {ex.Message}", backupPath);
            }

            IReadOnlyList<string> errors = await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
            if (errors.Count > 0)
            {
                Restore(target, backupPath, previousManaged);
                await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
                return HyprlandApplyResult.Fail(
                    "Hyprland reported config errors, so XerahS put your config back: " + string.Join("; ", errors),
                    backupPath);
            }

            IsEnabled = true;
            string backupNote = backupPath == null ? string.Empty : $" Your previous config is saved as {Path.GetFileName(backupPath)}.";
            return new HyprlandApplyResult(true, $"Hyprland now runs {count} XerahS hotkey(s).{backupNote}", backupPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HyprlandApplyResult> SyncAsync(
        IReadOnlyList<HyprlandWorkflowBinding> bindings,
        IReadOnlyCollection<string> approvedUnbinds,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (string? content, string? error, int count) = await RenderAsync(bindings, approvedUnbinds, cancellationToken).ConfigureAwait(false);
            if (content == null)
            {
                return HyprlandApplyResult.Fail(error!);
            }

            string? previousManaged = ReadIfExists(ManagedFilePath);
            if (previousManaged == content)
            {
                return new HyprlandApplyResult(true, $"Hyprland keybindings are up to date ({count}).");
            }

            try
            {
                WriteAtomically(ManagedFilePath, content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return HyprlandApplyResult.Fail($"Could not write {ManagedFilePath}: {ex.Message}");
            }

            IReadOnlyList<string> errors = await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
            if (errors.Count > 0)
            {
                Restore(targetPath: null, backupPath: null, previousManaged);
                await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
                return HyprlandApplyResult.Fail("Hyprland rejected the new keybindings, so XerahS kept the previous ones: " + string.Join("; ", errors));
            }

            return new HyprlandApplyResult(true, $"Hyprland keybindings updated ({count}).");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HyprlandApplyResult> DisableAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IsEnabled = false;
            if (File.Exists(ManagedFilePath))
            {
                try
                {
                    WriteAtomically(ManagedFilePath, HyprlandManagedConfig.Render([], [], _useOmarchyHelpers, _omaXerahsPath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return HyprlandApplyResult.Fail($"Could not clear {ManagedFilePath}: {ex.Message}");
                }

                await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HyprlandApplyResult(true, "Hyprland keybindings removed; XerahS registers its hotkeys itself again.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Scans live binds so keys taken by someone else are only used when the user approved the takeover.</summary>
    private async Task<(string? Content, string? Error, int Count)> RenderAsync(
        IReadOnlyList<HyprlandWorkflowBinding> bindings,
        IReadOnlyCollection<string> approvedUnbinds,
        CancellationToken cancellationToken)
    {
        HyprlandBindingScan scan = await ScanAsync(bindings, cancellationToken).ConfigureAwait(false);
        if (scan.Error != null)
        {
            return (null, scan.Error, 0);
        }

        var approved = new HashSet<string>(approvedUnbinds, StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<string>(
            scan.Conflicts.Select(conflict => conflict.Keys).Where(keys => !approved.Contains(keys)),
            StringComparer.OrdinalIgnoreCase);
        foreach (HyprlandBindingConflict conflict in scan.Conflicts.Where(conflict => blocked.Contains(conflict.Keys)))
        {
            DebugHelper.WriteLine($"Hyprland keybindings: skipped {conflict}; the key stays with its current binding.");
        }

        List<HyprlandManagedBinding> managed = HyprlandManagedConfig.ToManagedBindings(bindings)
            .Where(binding => !blocked.Contains(binding.Keys))
            .ToList();
        return (HyprlandManagedConfig.Render(managed, approved, _useOmarchyHelpers, _omaXerahsPath), null, managed.Count);
    }

    private async Task<IReadOnlyList<string>> ReloadAndCheckAsync(CancellationToken cancellationToken)
    {
        HyprctlResult reload = await _hyprctl.RunAsync(["reload"], cancellationToken).ConfigureAwait(false);
        if (!reload.Succeeded)
        {
            return [DescribeFailure("hyprctl reload", reload)];
        }

        HyprctlResult check = await _hyprctl.RunAsync(["-j", "configerrors"], cancellationToken).ConfigureAwait(false);
        if (!check.Succeeded)
        {
            return [DescribeFailure("hyprctl configerrors", check)];
        }

        return HyprlandBindsParser.ParseConfigErrors(check.Output);
    }

    private void Restore(string? targetPath, string? backupPath, string? previousManaged)
    {
        try
        {
            if (targetPath != null && backupPath != null && File.Exists(backupPath))
            {
                File.Copy(backupPath, targetPath, overwrite: true);
            }

            if (previousManaged != null)
            {
                WriteAtomically(ManagedFilePath, previousManaged);
            }
            else if (File.Exists(ManagedFilePath))
            {
                File.Delete(ManagedFilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugHelper.WriteLine($"Hyprland keybindings: rollback incomplete: {ex.Message}. Backup: {backupPath ?? "none"}");
        }
    }

    private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp-" + Environment.ProcessId;
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    private static string DescribeFailure(string command, HyprctlResult result)
    {
        string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim();
        return string.IsNullOrEmpty(detail) ? $"{command} failed (exit {result.ExitCode})." : $"{command} failed: {detail}";
    }

    private static string ResolveConfigDirectory()
    {
        string? xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string root = !string.IsNullOrWhiteSpace(xdgConfig) && Path.IsPathRooted(xdgConfig)
            ? xdgConfig
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(root, "hypr");
    }
}
