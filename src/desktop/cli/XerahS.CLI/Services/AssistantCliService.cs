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
using XerahS.Assistant.Models;
using XerahS.Assistant.Services;

namespace XerahS.CLI.Services
{
    public sealed class AssistantCliService
    {
        private readonly IAssistantService _assistantService;
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        public AssistantCliService(
            IAssistantService assistantService,
            TextWriter? output = null,
            TextWriter? error = null)
        {
            _assistantService = assistantService;
            _output = output ?? Console.Out;
            _error = error ?? Console.Error;
        }

        public async Task<int> RunAsync(string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                await _error.WriteLineAsync("Assistant prompt cannot be empty.");
                return 1;
            }

            DebugHelper.WriteLine($"[AssistantCLI] RunAsync invoked. Prompt='{prompt}'.");
            AssistantResponse response = await _assistantService.ProcessPromptAsync(prompt, cancellationToken);
            DebugHelper.WriteLine($"[AssistantCLI] Received response kind={response.Kind}, items={response.Items.Count}, actions={response.Actions.Count}, pendingConfirmation={response.PendingConfirmation != null}.");
            return await WriteResponseAsync(response);
        }

        private async Task<int> WriteResponseAsync(AssistantResponse response)
        {
            if (response.Kind == AssistantResponseKind.Error)
            {
                await _error.WriteLineAsync(response.Message);
                return 1;
            }

            if (response.Kind == AssistantResponseKind.ConfirmationRequired)
            {
                await _error.WriteLineAsync(response.PendingConfirmation?.Copy ?? response.Message);
                return 2;
            }

            string? outputText = ExtractOutputText(response);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                DebugHelper.WriteLine("[AssistantCLI] Response produced no printable output.");
                return 0;
            }

            await _output.WriteLineAsync(outputText);
            DebugHelper.WriteLine($"[AssistantCLI] Wrote output payload length={outputText.Length}.");
            return 0;
        }

        internal static string? ExtractOutputText(AssistantResponse response)
        {
            AssistantAction? copyAction = response.Actions.FirstOrDefault(action =>
                action.Kind == AssistantActionKind.CopyText &&
                !string.IsNullOrWhiteSpace(action.Text));

            if (!string.IsNullOrWhiteSpace(copyAction?.Text))
            {
                return copyAction.Text;
            }

            List<string> filePaths = response.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.FilePath))
                .Select(item => item.FilePath!)
                .ToList();

            if (filePaths.Count > 0)
            {
                return string.Join(Environment.NewLine, filePaths);
            }

            return string.IsNullOrWhiteSpace(response.Message) ? null : response.Message;
        }
    }
}
