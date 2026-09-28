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
using System.Diagnostics;
using System.Text.RegularExpressions;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Automation;
using XerahS.Core.Hotkeys;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

internal sealed class WorkflowRunResponse
{
    public int SchemaVersion { get; init; } = 1;
    public bool Ok { get; init; } = true;
    public string WorkflowId { get; init; } = string.Empty;

    /// <summary>"running-instance" when relayed to XerahS, "started" when XerahS was launched with it.</summary>
    public string Delivery { get; init; } = string.Empty;
}

/// <summary>
/// "omaxerahs workflow run" and "omaxerahs capture" (XIP0088 Phases 5-6). Both relay
/// <c>--run-workflow &lt;id&gt;</c> over the single-instance channel, so the capture runs inside the app
/// (OmaSnap on Hyprland, the normal pipeline everywhere) and this command returns immediately.
/// </summary>
internal static partial class RunCommands
{
    /// <summary>Test seam: relays arguments to a running XerahS.</summary>
    internal static Func<string[], bool> SendToRunningInstance { get; set; } =
        args => SingleInstanceManager.TrySendToRunningInstance(AppContracts.SingleInstance.PipeName, args);

    /// <summary>Test seam: starts XerahS with arguments when it is not running.</summary>
    internal static Func<string[], bool> StartApp { get; set; } = StartXerahS;

    [GeneratedRegex("^[0-9a-fA-F]{8}([0-9a-fA-F-]{0,28})$")]
    private static partial Regex WorkflowIdPattern();

    internal static Command CreateWorkflowRun()
    {
        var command = new Command("run", "Run a workflow in the running XerahS, like its hotkey (used by Hyprland keybindings).");
        var workflowArgument = new Argument<string>("workflow") { Description = "Workflow id, unique id prefix, or name." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowArgument);
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            return Run(() => ResolveWorkflowId(parseResult.GetValue(workflowArgument)!));
        });
        return command;
    }

    internal static Command CreateCapture()
    {
        var command = new Command("capture", "Start a capture in XerahS through the same path as its hotkeys (OmaSnap on Hyprland).");
        var targetArgument = new Argument<string>("target") { Description = "region, window, fullscreen or scroll." };
        targetArgument.AcceptOnlyFromAmong("region", "window", "fullscreen", "scroll");
        var workflowOption = new Option<string?>("--workflow", "-w")
        {
            Description = "Workflow whose after-capture tasks and destinations to use (default: the first workflow for the target)."
        };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(targetArgument);
        command.Add(workflowOption);
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            string target = parseResult.GetValue(targetArgument)!;
            string? workflow = parseResult.GetValue(workflowOption);
            return Run(() => string.IsNullOrWhiteSpace(workflow)
                ? ResolveWorkflowForTarget(target)
                : ResolveWorkflowId(workflow));
        });
        return command;
    }

    internal static WorkflowType JobForTarget(string target) => target switch
    {
        "region" => WorkflowType.RectangleRegion,
        "window" => WorkflowType.CustomWindow,
        "fullscreen" => WorkflowType.PrintScreen,
        "scroll" => WorkflowType.ScrollingCapture,
        _ => throw new AutomationException(AutomationErrorCodes.NotFound, $"Unknown capture target '{target}'.")
    };

    /// <summary>
    /// Ids (8+ hex characters) are relayed without loading settings so a key press returns fast;
    /// names and short prefixes are resolved against WorkflowsConfig.
    /// </summary>
    internal static string ResolveWorkflowId(string idOrName)
    {
        string query = idOrName.Trim();
        if (WorkflowIdPattern().IsMatch(query))
        {
            return query;
        }

        UploadHost.EnsureBootstrappedAsync().GetAwaiter().GetResult();
        return WorkflowAutomation.FindWorkflow(query).Id;
    }

    private static string ResolveWorkflowForTarget(string target)
    {
        WorkflowType job = JobForTarget(target);
        UploadHost.EnsureBootstrappedAsync().GetAwaiter().GetResult();
        WorkflowSettings? workflow = WorkflowAutomation.GetWorkflows().FirstOrDefault(w => w.Job == job && w.Enabled);
        if (workflow == null)
        {
            throw new AutomationException(AutomationErrorCodes.NotFound,
                $"No enabled {job} workflow. Create one in XerahS or pass --workflow.");
        }

        return workflow.Id;
    }

    internal static int Run(Func<string> resolveWorkflowId)
    {
        try
        {
            string workflowId = resolveWorkflowId();
            string[] relay = [AppContracts.Cli.RunWorkflowFlag, workflowId];

            string delivery;
            if (Environment.GetEnvironmentVariable("XERAHS_NO_APP_NOTIFY") == "1")
            {
                delivery = "skipped";
            }
            else if (SendToRunningInstance(relay))
            {
                delivery = "running-instance";
            }
            else if (StartApp(relay))
            {
                delivery = "started";
            }
            else
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.NotReady, "XerahS is not running and could not be started.");
            }

            JsonStdout.Write(new WorkflowRunResponse { WorkflowId = workflowId, Delivery = delivery });
            return 0;
        }
        catch (Exception ex)
        {
            var (code, message) = ErrorMapper.FromException(ex);
            return JsonStdout.WriteFailureAndExit(code, message);
        }
    }

    private static bool StartXerahS(string[] args)
    {
        string app = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "XerahS.exe" : "XerahS");
        if (!File.Exists(app))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(app) { UseShellExecute = false };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(startInfo);
        return process != null;
    }
}
