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

using System.Buffers;
using System.CommandLine;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Handles the plain forms <c>workflow run &lt;workflow&gt; [--json]</c> and
    /// <c>capture &lt;target&gt; [--json]</c> without System.CommandLine, so a key press returns as
    /// fast as the runtime starts. Anything else (help, other options) takes the normal parser.
    /// </summary>
    internal static bool TryRunFastPath(IReadOnlyList<string> args, out int exitCode)
    {
        exitCode = 0;
        List<string> positional = args.Where(arg => arg != "--json").ToList();
        if (positional.Count == 3 && positional[0] == "workflow" && positional[1] == "run" &&
            !positional[2].StartsWith('-') && positional[2].Trim().Length > 0)
        {
            string workflow = positional[2].Trim();
            exitCode = Run("workflow.run", workflow, [AppContracts.Cli.RunWorkflowFlag, workflow]);
            return true;
        }

        if (positional.Count == 2 && positional[0] == "capture" && AppContracts.Cli.CaptureTargets.Contains(positional[1]))
        {
            exitCode = Run("capture", positional[1], [AppContracts.Cli.CaptureFlag, positional[1]]);
            return true;
        }

        return false;
    }

    /// <summary>Relays <paramref name="args"/> to XerahS and prints the one JSON result.</summary>
    internal static int Run(string action, string target, string[] args)
    {
        AppRelayOutcome outcome = AppRelay.Relay(args);
        if (outcome == AppRelayOutcome.Unavailable)
        {
            const string message = "XerahS is not running and could not be started. Start XerahS, or set XERAHS_APP_PATH to its executable.";
            WriteJson(writer =>
            {
                writer.WriteBoolean("ok", false);
                writer.WriteStartObject("error");
                writer.WriteString("code", CliErrorCodes.NotReady);
                writer.WriteString("message", message);
                writer.WriteEndObject();
            });
            if (!JsonStdout.Enabled)
            {
                Console.Error.WriteLine(message);
            }

            return 1;
        }

        WriteJson(writer =>
        {
            writer.WriteBoolean("ok", true);
            writer.WriteString("action", action);
            writer.WriteString("target", target);
            writer.WriteString("delivery", outcome == AppRelayOutcome.Delivered ? "delivered" : "started");
        });
        return 0;
    }

    /// <summary>
    /// Same shape as <see cref="JsonStdout.Write"/> output, written directly: the reflection
    /// serializer's first use costs more than the rest of a key press.
    /// </summary>
    private static void WriteJson(Action<Utf8JsonWriter> writeFields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JsonStdout.SerializerOptions.Encoder }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writeFields(writer);
            writer.WriteEndObject();
        }

        Console.Out.WriteLine(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }
}
