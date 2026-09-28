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

/// <summary>An existing compositor keybinding, with keys normalised as "SUPER + SHIFT + F1".</summary>
public sealed record CompositorBinding(string Keys, string? Description, string Dispatcher, string? Argument)
{
    private static readonly string[] ModifierOrder = ["SUPER", "CTRL", "ALT", "SHIFT"];

    /// <summary>Canonical form: modifiers in SUPER, CTRL, ALT, SHIFT order, then the key, upper case.</summary>
    public static string FormatKeys(IEnumerable<string> modifiers, string key)
    {
        var set = new HashSet<string>(modifiers.Select(m => m.Trim().ToUpperInvariant()), StringComparer.Ordinal);
        var parts = ModifierOrder.Where(set.Contains).ToList();
        parts.Add(key.Trim().ToUpperInvariant());
        return string.Join(" + ", parts);
    }

    /// <summary>Parses "CTRL + F1" style text (any modifier order or case) into the canonical form.</summary>
    public static string Normalize(string keys)
    {
        string[] parts = keys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        IEnumerable<string> modifiers = parts[..^1].Select(p => p.ToUpperInvariant() switch
        {
            "CONTROL" => "CTRL",
            "WIN" or "META" or "MOD4" => "SUPER",
            "ALT" or "MOD1" => "ALT",
            var other => other
        });
        return FormatKeys(modifiers, parts[^1]);
    }
}

public sealed record CompositorKeybindingResult(bool Success, string Message, string? BackupPath = null, IReadOnlyList<string>? ConfigErrors = null);

/// <summary>
/// Compositor-managed keybindings (Hyprland, XIP0088 Phase 5). XerahS writes a managed file the
/// compositor loads; the app then receives workflow triggers through <c>omaxerahs workflow run</c>.
/// The user's configuration is changed only by <see cref="ApplyAsync"/>, which the UI calls after
/// explicit consent, always with a timestamped backup, validation and automatic rollback.
/// </summary>
public interface ICompositorKeybindingService
{
    /// <summary>True on a Hyprland session.</summary>
    bool IsSupported { get; }

    /// <summary>True when Omarchy's <c>o.bind</c> helper is available.</summary>
    bool UsesOmarchyHelpers { get; }

    /// <summary>Path of the managed file (e.g. ~/.config/hypr/xerahs.lua).</summary>
    string ManagedFilePath { get; }

    /// <summary>Current compositor bindings (<c>hyprctl binds -j</c>), for conflict detection.</summary>
    Task<IReadOnlyList<CompositorBinding>> GetExistingBindingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the managed file, includes it from the user config (once), reloads and validates; rolls back on errors.</summary>
    Task<CompositorKeybindingResult> ApplyAsync(string managedFileContent, CancellationToken cancellationToken = default);

    /// <summary>Rewrites the managed file with its current content when it is already included (hotkeys changed).</summary>
    Task<CompositorKeybindingResult> UpdateAsync(string managedFileContent, CancellationToken cancellationToken = default);

    /// <summary>Empties the managed file and reloads; the include line stays and loads nothing.</summary>
    Task<CompositorKeybindingResult> DisableAsync(string emptyManagedFileContent, CancellationToken cancellationToken = default);
}
