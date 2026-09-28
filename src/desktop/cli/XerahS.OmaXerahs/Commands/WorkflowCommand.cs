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
using XerahS.Common;
using XerahS.Core.Automation;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

internal static class WorkflowCommand
{
    internal static Command Create()
    {
        var command = new Command("workflow", "Inspect and change XerahS workflows (capture hotkey jobs).");
        command.Add(CreateList());
        command.Add(CreateShow());
        command.Add(CreateTasks());
        command.Add(CreateTaskNames());
        command.Add(CreateRun());
        return command;
    }

    private static Command CreateList()
    {
        var command = new Command("list", "List workflows with their ids, jobs, after-capture tasks and image effects.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, _ => new WorkflowListResponse
        {
            Workflows = WorkflowAutomation.GetWorkflows().Select(WorkflowAutomation.Describe).ToArray()
        });
        return command;
    }

    private static Command CreateShow()
    {
        var command = new Command("show", "Show one workflow.");
        var workflowArgument = new Argument<string>("workflow") { Description = "Workflow id, unique id prefix, or name." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowArgument);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult => new WorkflowResponse
        {
            Workflow = WorkflowAutomation.Describe(WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowArgument)!))
        });
        return command;
    }

    private static Command CreateTasks()
    {
        var command = new Command("tasks", "Add or remove after-capture tasks, e.g. --add AddImageEffects --remove ShowAfterCaptureWindow.");
        var workflowArgument = new Argument<string>("workflow") { Description = "Workflow id, unique id prefix, or name." };
        var addOption = new Option<string[]>("--add") { Description = "After-capture task to turn on (repeatable).", AllowMultipleArgumentsPerToken = true };
        var removeOption = new Option<string[]>("--remove") { Description = "After-capture task to turn off (repeatable).", AllowMultipleArgumentsPerToken = true };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowArgument);
        command.Add(addOption);
        command.Add(removeOption);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult =>
        {
            var workflow = WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowArgument)!);
            WorkflowAutomation.UpdateAfterCaptureTasks(
                workflow,
                parseResult.GetValue(addOption) ?? [],
                parseResult.GetValue(removeOption) ?? []);
            bool notified = WorkflowAutomation.SaveAndNotifyRunningApp();
            return new WorkflowResponse { Workflow = WorkflowAutomation.Describe(workflow), AppNotified = notified };
        }, response => AutomationCommand.AppNotifiedHint(((WorkflowResponse)response).AppNotified == true));
        return command;
    }

    private static Command CreateTaskNames()
    {
        var command = new Command("task-names", "List valid after-capture task names.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            JsonStdout.Write(new TaskNamesResponse { AfterCaptureTasks = WorkflowAutomation.GetAfterCaptureTaskNames() });
            return 0;
        });
        return command;
    }
    private static Command CreateRun()
    {
        var command = new Command("run", "Run a workflow in XerahS as if its hotkey was pressed. Returns at once; the capture runs in the app. Hyprland keybindings call this.");
        var workflowArgument = new Argument<string>("workflow") { Description = "Workflow id, unique id prefix, or name." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowArgument);
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            // Called on every key press: no settings bootstrap. The app resolves the id or name.
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            string workflow = parseResult.GetValue(workflowArgument)!.Trim();
            if (workflow.Length == 0)
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.Usage, "Workflow id or name is required.");
            }

            return CaptureCommand.Run("workflow.run", workflow, [AppContracts.Cli.RunWorkflowFlag, workflow]);
        });
        return command;
    }
}
