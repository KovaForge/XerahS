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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Hyprland;

/// <summary>One line of the managed file: Hyprland keys and the workflow they run.</summary>
public sealed record HyprlandManagedBinding(string Keys, string Description, string WorkflowId);

/// <summary>
/// Renders <c>~/.config/hypr/xerahs.lua</c> and the one line that loads it from the user config.
/// </summary>
public static class HyprlandManagedConfig
{
    public const string FileName = "xerahs.lua";
    public const string DescriptionPrefix = "XerahS: ";
    public const string Header = "-- Managed by XerahS. Changes are overwritten; edit hotkeys in XerahS.";
    public const string IncludeMarker = "-- XerahS keybindings";

    /// <param name="bindings">Bindings to write, already cleared of unapproved conflicts.</param>
    /// <param name="approvedUnbinds">Keys the user agreed to take over from Omarchy or their config.</param>
    /// <param name="useOmarchyHelpers">Omarchy's <c>o.bind</c>, which feeds its keybinding menu; plain <c>hl.bind</c> otherwise.</param>
    /// <param name="omaXerahsPath">Absolute path of the omaxerahs binary each binding runs.</param>
    public static string Render(
        IReadOnlyList<HyprlandManagedBinding> bindings,
        IReadOnlyCollection<string> approvedUnbinds,
        bool useOmarchyHelpers,
        string omaXerahsPath)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        builder.Append("-- Turn off in XerahS: Settings > Hotkeys > Use Hyprland keybindings.").Append('\n');

        var bound = new HashSet<string>(bindings.Select(binding => binding.Keys), StringComparer.OrdinalIgnoreCase);
        List<string> unbinds = approvedUnbinds
            .Where(bound.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(keys => keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unbinds.Count > 0 || bindings.Count > 0)
        {
            builder.Append('\n');
        }

        foreach (string keys in unbinds)
        {
            builder.Append("hl.unbind(").Append(LuaString(keys)).Append(")\n");
        }

        foreach (HyprlandManagedBinding binding in bindings)
        {
            string command = ShellQuote(omaXerahsPath) + " workflow run " + ShellQuote(binding.WorkflowId);
            string description = DescriptionPrefix + binding.Description;
            if (useOmarchyHelpers)
            {
                builder.Append("o.bind(")
                    .Append(LuaString(binding.Keys)).Append(", ")
                    .Append(LuaString(description)).Append(", ")
                    .Append(LuaString(command)).Append(")\n");
            }
            else
            {
                builder.Append("hl.bind(")
                    .Append(LuaString(binding.Keys)).Append(", hl.dsp.exec_cmd(")
                    .Append(LuaString(command)).Append("), { description = ")
                    .Append(LuaString(description)).Append(" })\n");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Lines appended to the user config. <c>dofile</c> with an absolute path works with and
    /// without Omarchy's module path, and the existence check keeps Hyprland loading if the
    /// managed file is removed.
    /// </summary>
    public static string RenderInclude(string managedFilePath)
    {
        string path = LuaString(managedFilePath);
        return "\n" + IncludeMarker + " (added by XerahS; delete these two lines to stop loading them)\n" +
               $"do local path = {path}; local file = io.open(path, \"r\"); if file then file:close(); dofile(path) end end\n";
    }

    public static bool ContainsInclude(string configText, string managedFilePath) =>
        configText.Contains(IncludeMarker, StringComparison.Ordinal) &&
        configText.Contains(LuaString(managedFilePath), StringComparison.Ordinal);

    /// <summary>Turns workflow bindings into managed lines, reporting hotkeys Hyprland cannot express.</summary>
    public static IReadOnlyList<HyprlandManagedBinding> ToManagedBindings(
        IEnumerable<HyprlandWorkflowBinding> bindings,
        ICollection<string>? skipped = null)
    {
        var result = new List<HyprlandManagedBinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (HyprlandWorkflowBinding binding in bindings)
        {
            if (!HyprlandKeyMap.TryFormat(binding.Hotkey, out string keys))
            {
                skipped?.Add($"{binding.Description} ({binding.Hotkey}): Hyprland has no name for this key");
                continue;
            }

            if (!seen.Add(keys))
            {
                skipped?.Add($"{binding.Description} ({keys}): another workflow already uses this key");
                continue;
            }

            result.Add(new HyprlandManagedBinding(keys, SingleLine(binding.Description), binding.WorkflowId));
        }

        return result;
    }

    internal static string LuaString(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\0': builder.Append("\\0"); break;
                default: builder.Append(c); break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    /// <summary>POSIX single quoting: Hyprland runs exec commands through a shell.</summary>
    internal static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string SingleLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}
