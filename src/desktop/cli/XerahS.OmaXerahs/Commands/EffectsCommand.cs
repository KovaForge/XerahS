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
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

internal static class EffectsCommand
{
    internal static Command Create()
    {
        var command = new Command("effects", "Manage a workflow's image effects (applied to captures when AddImageEffects is on).");
        command.Add(CreateImport());
        command.Add(CreateShow());
        command.Add(CreateToggle("enable", "Turn on image effects for the workflow.", enable: true));
        command.Add(CreateToggle("disable", "Turn off image effects for the workflow (keeps the preset).", enable: false));
        command.Add(CreateClear());
        return command;
    }

    private static Command CreateImport()
    {
        var command = new Command("import", "Replace the workflow's image effects with a .xsie or ShareX .sxie preset and turn them on.");
        var fileArgument = new Argument<string>("file") { Description = "Path to a .xsie or .sxie preset." };
        var workflowOption = AutomationCommand.CreateWorkflowOption();
        var noEnableOption = new Option<bool>("--no-enable") { Description = "Import without turning on AddImageEffects." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(fileArgument);
        command.Add(workflowOption);
        command.Add(noEnableOption);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult =>
        {
            var workflow = WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowOption)!);
            string path = Path.GetFullPath(parseResult.GetValue(fileArgument)!);
            var skipped = WorkflowAutomation.ImportImageEffects(workflow, path, enable: !parseResult.GetValue(noEnableOption));
            bool notified = WorkflowAutomation.SaveAndNotifyRunningApp();
            return new EffectsImportResponse
            {
                Workflow = WorkflowAutomation.Describe(workflow),
                SkippedEffects = skipped.ToArray(),
                AppNotified = notified
            };
        }, response =>
        {
            var import = (EffectsImportResponse)response;
            string skipped = import.SkippedEffects.Length > 0
                ? $" Skipped (no XerahS equivalent): {string.Join(", ", import.SkippedEffects)}."
                : string.Empty;
            return $"Imported {import.Workflow.ImageEffects.Effects.Length} effect(s) into '{import.Workflow.Name}'.{skipped} {AutomationCommand.AppNotifiedHint(import.AppNotified)}";
        });
        return command;
    }

    private static Command CreateShow()
    {
        var command = new Command("show", "Show the workflow's image effects.");
        var workflowOption = AutomationCommand.CreateWorkflowOption();
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowOption);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult => new WorkflowResponse
        {
            Workflow = WorkflowAutomation.Describe(WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowOption)!))
        });
        return command;
    }

    private static Command CreateToggle(string name, string description, bool enable)
    {
        var command = new Command(name, description);
        var workflowOption = AutomationCommand.CreateWorkflowOption();
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowOption);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult =>
        {
            var workflow = WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowOption)!);
            string[] task = [nameof(XerahS.Core.AfterCaptureTasks.AddImageEffects)];
            WorkflowAutomation.UpdateAfterCaptureTasks(workflow, enable ? task : [], enable ? [] : task);
            bool notified = WorkflowAutomation.SaveAndNotifyRunningApp();
            return new WorkflowResponse { Workflow = WorkflowAutomation.Describe(workflow), AppNotified = notified };
        }, response => AutomationCommand.AppNotifiedHint(((WorkflowResponse)response).AppNotified == true));
        return command;
    }

    private static Command CreateClear()
    {
        var command = new Command("clear", "Remove all image effects from the workflow and turn them off.");
        var workflowOption = AutomationCommand.CreateWorkflowOption();
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(workflowOption);
        command.Add(jsonOption);
        AutomationCommand.SetAction(command, jsonOption, parseResult =>
        {
            var workflow = WorkflowAutomation.FindWorkflow(parseResult.GetValue(workflowOption)!);
            WorkflowAutomation.ClearImageEffects(workflow);
            bool notified = WorkflowAutomation.SaveAndNotifyRunningApp();
            return new WorkflowResponse { Workflow = WorkflowAutomation.Describe(workflow), AppNotified = notified };
        }, response => AutomationCommand.AppNotifiedHint(((WorkflowResponse)response).AppNotified == true));
        return command;
    }
}
