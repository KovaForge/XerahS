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
using XerahS.Core.Helpers;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Automation;

/// <summary>Stable, serializable view of a workflow for automation clients (CLI, agents, MCP).</summary>
public sealed class WorkflowSummary
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Job { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string? Hotkey { get; init; }
    public string[] AfterCapture { get; init; } = [];
    public string[] AfterUpload { get; init; } = [];
    public ImageEffectsSummary ImageEffects { get; init; } = new();
}

public sealed class ImageEffectsSummary
{
    /// <summary>True when the workflow's after-capture tasks include AddImageEffects.</summary>
    public bool Enabled { get; init; }
    public string PresetName { get; init; } = string.Empty;
    public string[] Effects { get; init; } = [];
}

public sealed class AutomationException : Exception
{
    public AutomationException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Machine-readable error code (see <see cref="AutomationErrorCodes"/>).</summary>
    public string Code { get; }
}

public static class AutomationErrorCodes
{
    public const string NotFound = "not_found";
    public const string Ambiguous = "ambiguous";
    public const string InvalidPath = "invalid_path";
    public const string UnsupportedType = "unsupported_type";
    public const string InvalidValue = "invalid_value";
}

/// <summary>
/// Reads and changes workflows the same way the Workflows page does, then persists them and
/// asks a running XerahS instance to reload so its in-memory copy does not overwrite the change.
/// </summary>
public static class WorkflowAutomation
{
    public static IReadOnlyList<WorkflowSettings> GetWorkflows()
    {
        return SettingsManager.WorkflowsConfig?.Hotkeys ?? new List<WorkflowSettings>();
    }

    public static WorkflowSummary Describe(WorkflowSettings workflow)
    {
        TaskSettings task = workflow.TaskSettings ?? new TaskSettings();
        ImageEffectPreset? preset = task.ImageSettings?.ImageEffectsPreset;

        return new WorkflowSummary
        {
            Id = workflow.Id,
            Name = task.ToString(),
            Job = task.Job.ToString(),
            Enabled = workflow.Enabled,
            Hotkey = workflow.HotkeyInfo?.IsValid == true ? workflow.HotkeyInfo.ToString() : null,
            AfterCapture = FlagNames(task.AfterCaptureJob),
            AfterUpload = FlagNames(task.AfterUploadJob),
            ImageEffects = new ImageEffectsSummary
            {
                Enabled = task.AfterCaptureJob.HasFlag(AfterCaptureTasks.AddImageEffects),
                PresetName = preset?.Name ?? string.Empty,
                Effects = preset?.Effects?.Select(effect => effect.Name).ToArray() ?? []
            }
        };
    }

    /// <summary>
    /// Resolves a workflow by exact id, unique id prefix (6+ characters), or case-insensitive name.
    /// </summary>
    public static WorkflowSettings FindWorkflow(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            throw new AutomationException(AutomationErrorCodes.NotFound, "Workflow id or name is required.");
        }

        string query = idOrName.Trim();
        IReadOnlyList<WorkflowSettings> workflows = GetWorkflows();

        WorkflowSettings? exact = workflows.FirstOrDefault(w => string.Equals(w.Id, query, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        List<WorkflowSettings> matches = workflows
            .Where(w => string.Equals(w.TaskSettings?.ToString(), query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0 && query.Length >= 6)
        {
            matches = workflows.Where(w => w.Id.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new AutomationException(AutomationErrorCodes.NotFound,
                $"No workflow matches '{query}'. List workflows with: omaxerahs workflow list"),
            _ => throw new AutomationException(AutomationErrorCodes.Ambiguous,
                $"'{query}' matches {matches.Count} workflows ({string.Join(", ", matches.Select(w => w.Id))}). Use the workflow id.")
        };
    }

    /// <summary>Adds and removes after-capture tasks by name (e.g. AddImageEffects, ShowAfterCaptureWindow).</summary>
    public static void UpdateAfterCaptureTasks(WorkflowSettings workflow, IEnumerable<string> add, IEnumerable<string> remove)
    {
        TaskSettings task = workflow.TaskSettings;
        AfterCaptureTasks tasks = task.AfterCaptureJob;

        foreach (string name in add)
        {
            tasks |= ParseAfterCaptureTask(name);
        }

        foreach (string name in remove)
        {
            tasks &= ~ParseAfterCaptureTask(name);
        }

        task.AfterCaptureJob = tasks;
    }

    public static AfterCaptureTasks ParseAfterCaptureTask(string name)
    {
        if (Enum.TryParse(name?.Trim(), ignoreCase: true, out AfterCaptureTasks value) &&
            value != AfterCaptureTasks.None &&
            Enum.IsDefined(value))
        {
            return value;
        }

        throw new AutomationException(AutomationErrorCodes.InvalidValue,
            $"Unknown after-capture task '{name}'. Valid tasks: {string.Join(", ", GetAfterCaptureTaskNames())}");
    }

    public static string[] GetAfterCaptureTaskNames()
    {
        return CanonicalFlags<AfterCaptureTasks>().Select(flag => flag.Name).ToArray();
    }

    /// <summary>
    /// Replaces the workflow's image effects with the preset in <paramref name="presetPath"/>
    /// (.xsie or ShareX .sxie), like Import in the Image Effects dialog.
    /// </summary>
    public static IReadOnlyList<string> ImportImageEffects(WorkflowSettings workflow, string presetPath, bool enable)
    {
        if (string.IsNullOrWhiteSpace(presetPath) || !File.Exists(presetPath))
        {
            throw new AutomationException(AutomationErrorCodes.InvalidPath, $"Preset file not found: {presetPath}");
        }

        string extension = Path.GetExtension(presetPath).ToLowerInvariant();
        if (!ImageEffectPresetImporter.SupportedExtensions.Contains(extension))
        {
            throw new AutomationException(AutomationErrorCodes.UnsupportedType,
                $"Unsupported preset type '{extension}'. Supported: {string.Join(", ", ImageEffectPresetImporter.SupportedExtensions)}");
        }

        ImageEffectPreset preset = ImageEffectPresetImporter.LoadPresetFile(presetPath, out IReadOnlyList<string> skipped)
            ?? throw new AutomationException(AutomationErrorCodes.UnsupportedType,
                $"Could not read image effects from '{presetPath}'. See the XerahS log for details.");

        workflow.TaskSettings.ImageSettings.ImageEffectsPreset = preset;
        if (enable)
        {
            workflow.TaskSettings.AfterCaptureJob |= AfterCaptureTasks.AddImageEffects;
        }

        return skipped;
    }

    public static void ClearImageEffects(WorkflowSettings workflow)
    {
        workflow.TaskSettings.ImageSettings.ImageEffectsPreset = ImageEffectPreset.GetDefaultPreset();
        workflow.TaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.AddImageEffects;
    }

    /// <summary>
    /// Saves WorkflowsConfig and asks a running XerahS to reload it.
    /// Returns true when a running instance was notified.
    /// </summary>
    public static bool SaveAndNotifyRunningApp()
    {
        SettingsManager.SaveWorkflowsConfig();

        // Tests and isolated settings folders must not poke the user's real running instance.
        if (Environment.GetEnvironmentVariable("XERAHS_NO_APP_NOTIFY") == "1")
        {
            return false;
        }

        return SingleInstanceManager.TrySendToRunningInstance(
            AppContracts.SingleInstance.PipeName,
            [AppContracts.Cli.ReloadWorkflowsFlag]);
    }

    private static string[] FlagNames<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        long bits = Convert.ToInt64(value);
        return CanonicalFlags<TEnum>()
            .Where(flag => (bits & flag.Value) == flag.Value)
            .Select(flag => flag.Name)
            .ToArray();
    }

    /// <summary>
    /// Single-bit enum members by their current names, skipping [Obsolete] aliases
    /// (e.g. AnnotateImage -> AnnotateMedia). Obsolete names are still accepted as input.
    /// </summary>
    private static IEnumerable<(string Name, long Value)> CanonicalFlags<TEnum>() where TEnum : struct, Enum
    {
        var seen = new HashSet<long>();
        foreach (var field in typeof(TEnum).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.IsDefined(typeof(ObsoleteAttribute), inherit: false))
            {
                continue;
            }

            long value = Convert.ToInt64(field.GetValue(null));
            if (value != 0 && (value & (value - 1)) == 0 && seen.Add(value))
            {
                yield return (field.Name, value);
            }
        }
    }
}
