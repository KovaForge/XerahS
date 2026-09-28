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

using System.Text.Json;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.OmaSnap;

namespace XerahS.Platform.Linux.Services;

/// <summary>Runs hyprctl (injectable for tests).</summary>
internal interface IHyprctlRunner
{
    Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

internal sealed class HyprctlRunner : IHyprctlRunner
{
    public async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        OmaSnapProcessResult result = await OmaSnapProcessRunner
            .RunAsync("hyprctl", arguments, TimeSpan.FromSeconds(10), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return (result.TimedOut ? -1 : result.ExitCode, result.StandardOutput);
    }
}

/// <summary>
/// Hyprland-managed keybindings (XIP0088 Phase 5). Writes <c>~/.config/hypr/xerahs.lua</c>, adds one
/// <c>require("xerahs")</c> line to <c>~/.config/hypr/bindings.lua</c> after a timestamped backup,
/// reloads Hyprland and checks <c>hyprctl configerrors</c>, rolling everything back on errors.
/// Never touches <c>/usr/share/omarchy</c>.
/// </summary>
internal sealed class HyprlandKeybindingService : ICompositorKeybindingService
{
    public const string IncludeLine = "require(\"xerahs\")";
    private const string IncludeComment = "-- XerahS managed keybindings (XIP0088). Remove this line to stop loading them.";

    private readonly LinuxDesktopProfile _profile;
    private readonly IHyprctlRunner _hyprctl;
    private readonly string _configDirectory;
    private readonly Func<DateTimeOffset> _now;

    public HyprlandKeybindingService(
        LinuxDesktopProfile profile,
        IHyprctlRunner? hyprctl = null,
        string? configDirectory = null,
        Func<DateTimeOffset>? now = null)
    {
        _profile = profile;
        _hyprctl = hyprctl ?? new HyprctlRunner();
        _configDirectory = configDirectory ?? DefaultConfigDirectory();
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsSupported => _profile.IsHyprland && !_profile.IsSandboxed;

    public bool UsesOmarchyHelpers => _profile.IsOmarchy;

    public string ManagedFilePath => Path.Combine(_configDirectory, "xerahs.lua");

    internal string BindingsFilePath => Path.Combine(_configDirectory, "bindings.lua");

    public async Task<IReadOnlyList<CompositorBinding>> GetExistingBindingsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return [];
        }

        var (exitCode, output) = await _hyprctl.RunAsync(["binds", "-j"], cancellationToken).ConfigureAwait(false);
        return exitCode == 0 ? ParseBinds(output) : [];
    }

    public async Task<CompositorKeybindingResult> ApplyAsync(string managedFileContent, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            return new CompositorKeybindingResult(false, "Hyprland keybindings need a Hyprland session.");
        }

        Directory.CreateDirectory(_configDirectory);
        string? previousManaged = ReadIfExists(ManagedFilePath);
        string? previousBindings = ReadIfExists(BindingsFilePath);
        string? backupPath = null;

        if (previousBindings != null)
        {
            string stem = $"{BindingsFilePath}.bak.{_now().ToUnixTimeSeconds()}";
            backupPath = stem;
            for (int suffix = 1; File.Exists(backupPath); suffix++)
            {
                backupPath = $"{stem}-{suffix}";
            }

            File.Copy(BindingsFilePath, backupPath, overwrite: false);
        }

        try
        {
            await File.WriteAllTextAsync(ManagedFilePath, managedFileContent, cancellationToken).ConfigureAwait(false);
            if (!ContainsInclude(previousBindings))
            {
                string prefix = string.IsNullOrEmpty(previousBindings) || previousBindings.EndsWith('\n') ? string.Empty : "\n";
                await File.AppendAllTextAsync(BindingsFilePath, $"{prefix}\n{IncludeComment}\n{IncludeLine}\n", cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<string> errors = await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
            if (errors.Count > 0)
            {
                Restore(ManagedFilePath, previousManaged);
                Restore(BindingsFilePath, previousBindings);
                await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
                return new CompositorKeybindingResult(false,
                    "Hyprland reported configuration errors, so the change was rolled back.", backupPath, errors);
            }

            DebugHelper.WriteLine($"Hyprland keybindings: wrote {ManagedFilePath} (backup: {backupPath ?? "none, bindings.lua was new"}).");
            return new CompositorKeybindingResult(true, $"Hyprland keybindings are active ({ManagedFilePath}).", backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Restore(ManagedFilePath, previousManaged);
            Restore(BindingsFilePath, previousBindings);
            return new CompositorKeybindingResult(false, $"Could not update the Hyprland configuration: {ex.Message}", backupPath);
        }
    }

    public async Task<CompositorKeybindingResult> UpdateAsync(string managedFileContent, CancellationToken cancellationToken = default)
    {
        if (!IsSupported || !ContainsInclude(ReadIfExists(BindingsFilePath)))
        {
            return new CompositorKeybindingResult(false, "Hyprland keybindings are not set up; use Settings > Hotkeys to turn them on.");
        }

        string? previous = ReadIfExists(ManagedFilePath);
        if (previous == managedFileContent)
        {
            return new CompositorKeybindingResult(true, "Hyprland keybindings are up to date.");
        }

        await File.WriteAllTextAsync(ManagedFilePath, managedFileContent, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> errors = await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            Restore(ManagedFilePath, previous);
            await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
            return new CompositorKeybindingResult(false, "Hyprland reported configuration errors, so the update was rolled back.", null, errors);
        }

        return new CompositorKeybindingResult(true, "Hyprland keybindings updated.");
    }

    public async Task<CompositorKeybindingResult> DisableAsync(string emptyManagedFileContent, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ManagedFilePath))
        {
            return new CompositorKeybindingResult(true, "Hyprland keybindings were not set up.");
        }

        await File.WriteAllTextAsync(ManagedFilePath, emptyManagedFileContent, cancellationToken).ConfigureAwait(false);
        if (IsSupported)
        {
            await ReloadAndCheckAsync(cancellationToken).ConfigureAwait(false);
        }

        return new CompositorKeybindingResult(true, "Hyprland keybindings removed; XerahS registers its hotkeys again.");
    }

    internal static bool ContainsInclude(string? bindingsContent) =>
        bindingsContent != null &&
        bindingsContent.Split('\n').Any(line => line.Trim() == IncludeLine || line.Trim() == "require('xerahs')");

    /// <summary>Parses <c>hyprctl binds -j</c>. modmask bits: SHIFT 1, CTRL 4, ALT 8, SUPER 64.</summary>
    internal static IReadOnlyList<CompositorBinding> ParseBinds(string json)
    {
        var bindings = new List<CompositorBinding>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return bindings;
            }

            foreach (JsonElement bind in document.RootElement.EnumerateArray())
            {
                if (bind.TryGetProperty("submap", out JsonElement submap) && !string.IsNullOrEmpty(submap.GetString()))
                {
                    continue;
                }

                if (bind.TryGetProperty("mouse", out JsonElement mouse) && mouse.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                string key = bind.TryGetProperty("key", out JsonElement keyElement) ? keyElement.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                int modmask = bind.TryGetProperty("modmask", out JsonElement modElement) && modElement.TryGetInt32(out int m) ? m : 0;
                var modifiers = new List<string>();
                if ((modmask & 64) != 0) modifiers.Add("SUPER");
                if ((modmask & 4) != 0) modifiers.Add("CTRL");
                if ((modmask & 8) != 0) modifiers.Add("ALT");
                if ((modmask & 1) != 0) modifiers.Add("SHIFT");

                bindings.Add(new CompositorBinding(
                    CompositorBinding.FormatKeys(modifiers, NormalizeKeyName(key)),
                    bind.TryGetProperty("description", out JsonElement description) ? description.GetString() : null,
                    bind.TryGetProperty("dispatcher", out JsonElement dispatcher) ? dispatcher.GetString() ?? string.Empty : string.Empty,
                    bind.TryGetProperty("arg", out JsonElement arg) ? arg.GetString() : null));
            }
        }
        catch (JsonException ex)
        {
            DebugHelper.WriteLine($"Hyprland keybindings: cannot parse hyprctl binds ({ex.Message}).");
        }

        return bindings;
    }

    /// <summary>Maps xkb key names from hyprctl (Print, Prior, Return) to the names XerahS writes.</summary>
    internal static string NormalizeKeyName(string key) => key.ToUpperInvariant() switch
    {
        "PRIOR" => "PAGE_UP",
        "NEXT" => "PAGE_DOWN",
        "ENTER" => "RETURN",
        "SYS_REQ" => "PRINT",
        var other => other
    };

    private async Task<IReadOnlyList<string>> ReloadAndCheckAsync(CancellationToken cancellationToken)
    {
        await _hyprctl.RunAsync(["reload"], cancellationToken).ConfigureAwait(false);
        var (exitCode, output) = await _hyprctl.RunAsync(["configerrors", "-j"], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            return [$"hyprctl configerrors failed (exit {exitCode})."];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Plain text output: any non-blank line is an error.
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.Equals("no errors", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static void Restore(string path, string? content)
    {
        try
        {
            if (content == null)
            {
                File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, content);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugHelper.WriteLine($"Hyprland keybindings: rollback of {path} failed ({ex.Message}).");
        }
    }

    private static string DefaultConfigDirectory()
    {
        string? configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
        {
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(configHome, "hypr");
    }
}
