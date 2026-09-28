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
using Avalonia.Input;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Hotkeys;

/// <summary>One managed Hyprland binding for a workflow.</summary>
public sealed record HyprlandBindingEntry(string WorkflowId, string WorkflowName, string Keys, string Command);

/// <summary>A key already bound by Omarchy or the user that a XerahS workflow wants.</summary>
public sealed record HyprlandBindingConflict(HyprlandBindingEntry Entry, CompositorBinding Existing);

public sealed record HyprlandManagedFile(
    string Content,
    IReadOnlyList<HyprlandBindingEntry> Entries,
    IReadOnlyList<string> Unsupported);

/// <summary>
/// Turns XerahS workflow hotkeys into the managed Hyprland Lua file <c>~/.config/hypr/xerahs.lua</c>
/// (XIP0088 Phase 5). Pure functions: key mapping table, conflict detection and file content are
/// unit tested against fixtures.
/// </summary>
public static class HyprlandKeybindingGenerator
{
    public const string Header = "-- Managed by XerahS. Changes are overwritten; edit hotkeys in XerahS.";

    private static readonly Dictionary<Key, string> NamedKeys = new()
    {
        [Key.Snapshot] = "PRINT",
        [Key.Print] = "PRINT",
        [Key.PrintScreen] = "PRINT",
        [Key.Space] = "SPACE",
        [Key.Enter] = "RETURN",
        [Key.Escape] = "ESCAPE",
        [Key.Tab] = "TAB",
        [Key.Back] = "BACKSPACE",
        [Key.Delete] = "DELETE",
        [Key.Insert] = "INSERT",
        [Key.Home] = "HOME",
        [Key.End] = "END",
        [Key.PageUp] = "PAGE_UP",
        [Key.PageDown] = "PAGE_DOWN",
        [Key.Left] = "LEFT",
        [Key.Right] = "RIGHT",
        [Key.Up] = "UP",
        [Key.Down] = "DOWN",
        [Key.Pause] = "PAUSE",
        [Key.Scroll] = "SCROLL_LOCK",
        [Key.OemMinus] = "MINUS",
        [Key.OemPlus] = "EQUAL",
        [Key.OemComma] = "COMMA",
        [Key.OemPeriod] = "PERIOD",
        [Key.OemQuestion] = "SLASH",
        [Key.OemSemicolon] = "SEMICOLON",
        [Key.OemQuotes] = "APOSTROPHE",
        [Key.OemOpenBrackets] = "BRACKETLEFT",
        [Key.OemCloseBrackets] = "BRACKETRIGHT",
        [Key.OemPipe] = "BACKSLASH",
        [Key.OemBackslash] = "BACKSLASH",
        [Key.OemTilde] = "GRAVE",
        [Key.Multiply] = "KP_MULTIPLY",
        [Key.Add] = "KP_ADD",
        [Key.Subtract] = "KP_SUBTRACT",
        [Key.Divide] = "KP_DIVIDE",
        [Key.Decimal] = "KP_DECIMAL"
    };

    /// <summary>Hyprland key text such as "CTRL + F1" or "SUPER + SHIFT + PRINT"; null when the key has no mapping.</summary>
    public static string? ToHyprlandKeys(HotkeyInfo hotkey)
    {
        if (!hotkey.IsValid)
        {
            return null;
        }

        string? key = MapKey(hotkey.Key);
        if (key == null)
        {
            return null;
        }

        var modifiers = new List<string>();
        if (hotkey.HasMeta) modifiers.Add("SUPER");
        if (hotkey.HasControl) modifiers.Add("CTRL");
        if (hotkey.HasAlt) modifiers.Add("ALT");
        if (hotkey.HasShift) modifiers.Add("SHIFT");
        return CompositorBinding.FormatKeys(modifiers, key);
    }

    internal static string? MapKey(Key key)
    {
        if (NamedKeys.TryGetValue(key, out string? named))
        {
            return named;
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return "KP_" + ((int)(key - Key.NumPad0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return "F" + ((int)(key - Key.F1) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>The command a binding runs: <c>omaxerahs workflow run &lt;id&gt;</c>, shell-quoted.</summary>
    public static string BuildCommand(string omaxerahsPath, string workflowId) =>
        $"{ShellQuote(omaxerahsPath)} workflow run {ShellQuote(workflowId)}";

    /// <summary>Managed bindings for enabled workflows that have a hotkey.</summary>
    public static (IReadOnlyList<HyprlandBindingEntry> Entries, IReadOnlyList<string> Unsupported) BuildEntries(
        IEnumerable<WorkflowSettings> workflows,
        string omaxerahsPath)
    {
        var entries = new List<HyprlandBindingEntry>();
        var unsupported = new List<string>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (WorkflowSettings workflow in workflows)
        {
            if (!workflow.Enabled || workflow.Job == WorkflowType.None || workflow.HotkeyInfo is not { IsValid: true } hotkey ||
                string.IsNullOrWhiteSpace(workflow.Id))
            {
                continue;
            }

            string? keys = ToHyprlandKeys(hotkey);
            if (keys == null)
            {
                unsupported.Add($"{workflow}: {hotkey} has no Hyprland key name");
                continue;
            }

            if (!usedKeys.Add(keys))
            {
                unsupported.Add($"{workflow}: {keys} is already used by another XerahS workflow");
                continue;
            }

            entries.Add(new HyprlandBindingEntry(workflow.Id, DisplayName(workflow), keys, BuildCommand(omaxerahsPath, workflow.Id)));
        }

        return (entries, unsupported);
    }

    /// <summary>Existing bindings (not XerahS's own) on the same keys as a managed entry.</summary>
    public static IReadOnlyList<HyprlandBindingConflict> FindConflicts(
        IReadOnlyList<HyprlandBindingEntry> entries,
        IEnumerable<CompositorBinding> existing)
    {
        var byKeys = existing
            .Where(binding => binding.Argument?.Contains("workflow run", StringComparison.Ordinal) != true ||
                              binding.Argument?.Contains("omaxerahs", StringComparison.Ordinal) != true)
            .GroupBy(binding => CompositorBinding.Normalize(binding.Keys), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return entries
            .Where(entry => byKeys.ContainsKey(entry.Keys))
            .Select(entry => new HyprlandBindingConflict(entry, byKeys[entry.Keys]))
            .ToList();
    }

    /// <summary>
    /// Managed Lua file. Omarchy uses <c>o.bind(keys, description, command)</c>; plain Hyprland uses
    /// <c>hl.bind</c>. <c>hl.unbind</c> is emitted only for keys the user approved.
    /// </summary>
    public static string BuildFile(
        IReadOnlyList<HyprlandBindingEntry> entries,
        bool useOmarchyHelpers,
        IEnumerable<string> approvedUnbinds)
    {
        var approved = new HashSet<string>(approvedUnbinds.Select(CompositorBinding.Normalize), StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("-- Bindings run \"omaxerahs workflow run <id>\"; turn this off in XerahS > Settings > Hotkeys.\n");

        foreach (HyprlandBindingEntry entry in entries)
        {
            builder.Append('\n');
            if (approved.Contains(entry.Keys))
            {
                builder.Append("hl.unbind(").Append(LuaString(entry.Keys)).Append(")\n");
            }

            string description = "XerahS: " + entry.WorkflowName;
            if (useOmarchyHelpers)
            {
                builder.Append("o.bind(").Append(LuaString(entry.Keys)).Append(", ").Append(LuaString(description))
                    .Append(", ").Append(LuaString(entry.Command)).Append(")\n");
            }
            else
            {
                builder.Append("hl.bind(").Append(LuaString(entry.Keys)).Append(", \"exec\", ").Append(LuaString(entry.Command))
                    .Append(") -- ").Append(description.Replace('\n', ' ')).Append('\n');
            }
        }

        return builder.ToString();
    }

    public static string BuildEmptyFile() =>
        Header + "\n-- Hyprland keybindings are turned off in XerahS; XerahS registers its hotkeys itself.\n";

    internal static string LuaString(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\").Append(((int)c).ToString("000", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    internal static string ShellQuote(string value)
    {
        if (value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c is '/' or '.' or '_' or '-' or ':' or '+' or '='))
        {
            return value;
        }

        return "'" + value.Replace("'", "'\\''") + "'";
    }

    private static string DisplayName(WorkflowSettings workflow)
    {
        string text = workflow.ToString();
        return string.IsNullOrWhiteSpace(text) ? workflow.Job.ToString() : text;
    }
}
