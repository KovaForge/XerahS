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
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

internal static class CaptureCommand
{
    internal static Command Create()
    {
        var command = new Command("capture", "Ask XerahS to capture a region, window, fullscreen or scrolling region. The capture runs in the app (OmaSnap on Omarchy).");
        var targetArgument = new Argument<string>("target") { Description = "region, window, fullscreen or scroll." };
        targetArgument.AcceptOnlyFromAmong([.. AppContracts.Cli.CaptureTargets]);
        var workflowOption = new Option<string>("--workflow", "-w") { Description = "Run this workflow (id or name) instead of the first workflow for the target." };
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(targetArgument);
        command.Add(workflowOption);
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            string target = parseResult.GetValue(targetArgument)!.ToLowerInvariant();
            string? workflow = parseResult.GetValue(workflowOption);
            return string.IsNullOrWhiteSpace(workflow)
                ? Run("capture", target, [AppContracts.Cli.CaptureFlag, target])
                : Run("capture", workflow.Trim(), [AppContracts.Cli.RunWorkflowFlag, workflow.Trim()]);
        });
        return command;
    }

    /// <summary>Relays <paramref name="args"/> to XerahS and prints the one JSON result.</summary>
    internal static int Run(string action, string target, string[] args)
    {
        AppRelayOutcome outcome = AppRelay.Relay(args);
        if (outcome == AppRelayOutcome.Unavailable)
        {
            return JsonStdout.WriteFailureAndExit(
                CliErrorCodes.NotReady,
                "XerahS is not running and could not be started. Start XerahS, or set XERAHS_APP_PATH to its executable.");
        }

        JsonStdout.Write(new AppRelayResponse
        {
            Action = action,
            Target = target,
            Delivery = outcome == AppRelayOutcome.Delivered ? "delivered" : "started"
        });
        return 0;
    }
}
