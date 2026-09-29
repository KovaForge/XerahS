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

using System.CommandLine;
using XerahS.Core.Automation;
using XerahS.OmaXerahs.Models;

namespace XerahS.OmaXerahs.Services;

/// <summary>
/// Shared plumbing for settings automation commands: bootstrap XerahS settings, run the
/// operation, and print exactly one JSON object (success payload or error envelope).
/// </summary>
internal static class AutomationCommand
{
    internal static Option<string> CreateWorkflowOption()
    {
        return new Option<string>("--workflow", "-w")
        {
            Description = "Workflow id, unique id prefix, or name (see: omaxerahs workflow list).",
            Required = true
        };
    }

    internal static void SetAction(Command command, Option<bool> jsonOption, Func<ParseResult, object> run, Func<object, string>? describe = null)
    {
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            try
            {
                UploadHost.EnsureBootstrappedAsync().GetAwaiter().GetResult();
                object response = run(parseResult);
                JsonStdout.Write(response);
                if (!JsonStdout.Enabled && describe != null)
                {
                    Console.Error.WriteLine(describe(response));
                }

                return 0;
            }
            catch (Exception ex)
            {
                var (code, message) = ErrorMapper.FromException(ex);
                return JsonStdout.WriteFailureAndExit(code, message);
            }
        });
    }

    internal static string AppNotifiedHint(bool appNotified)
    {
        return appNotified
            ? "XerahS reloaded the change."
            : "XerahS is not running; the change applies the next time it starts.";
    }
}

internal sealed class WorkflowListResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public WorkflowSummary[] Workflows { get; init; } = [];
}

internal sealed class WorkflowResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public WorkflowSummary Workflow { get; init; } = new();

    /// <summary>Only set by commands that change settings: whether a running XerahS reloaded them.</summary>
    public bool? AppNotified { get; init; }
}

internal sealed class EffectsImportResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public WorkflowSummary Workflow { get; init; } = new();
    public string[] SkippedEffects { get; init; } = [];
    public bool AppNotified { get; init; }
}

internal sealed class TaskNamesResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string[] AfterCaptureTasks { get; init; } = [];
}
