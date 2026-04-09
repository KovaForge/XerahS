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
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.App
{
    public sealed class SendToIntegrationCoordinator
    {
        private readonly IUIService _uiService;
        private readonly ITaskManager _taskManager;
        private readonly Func<TaskSettings> _createUploadTaskSettings;

        public SendToIntegrationCoordinator(
            IUIService uiService,
            ITaskManager taskManager,
            Func<TaskSettings> createUploadTaskSettings)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
            _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
            _createUploadTaskSettings = createUploadTaskSettings ?? throw new ArgumentNullException(nameof(createUploadTaskSettings));
        }

        public async Task HandleAsync(SendToSelection selection, string source)
        {
            ArgumentNullException.ThrowIfNull(selection);

            SendToPromptResult decision = await ResolveDecisionAsync(selection);
            LogDecision(source, selection, decision);

            switch (decision.Action)
            {
                case SendToAction.Cancel:
                    DebugHelper.WriteLine($"Shell integration ({source}): Send-to cancelled; no task started.");
                    return;

                case SendToAction.UploadNow:
                    DebugHelper.WriteLine($"Shell integration ({source}): Upload explicitly requested from Send-to.");
                    await UploadSelectionAsync(selection, source);
                    return;

                default:
                    DebugHelper.WriteLine(
                        $"Shell integration ({source}): Upload skipped because Send-to action '{decision.Action}' was selected.");
                    try
                    {
                        await _uiService.ExecuteSendToActionAsync(decision.Action, selection);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, $"Shell integration ({source}): Failed to execute Send-to action '{decision.Action}'.");
                    }
                    return;
            }
        }

        public async Task UploadSelectionAsync(SendToSelection selection, string source)
        {
            ArgumentNullException.ThrowIfNull(selection);

            List<string> uploadFiles = ResolveUploadFiles(selection);
            if (uploadFiles.Count == 0)
            {
                DebugHelper.WriteLine(
                    $"Shell integration ({source}): Send-to upload requested but no uploadable files were resolved.");
                return;
            }

            foreach (string file in uploadFiles)
            {
                TaskSettings settings = _createUploadTaskSettings();
                settings.Job = WorkflowType.FileUpload;
                await _taskManager.StartFileTask(settings, file);
            }
        }

        private async Task<SendToPromptResult> ResolveDecisionAsync(SendToSelection selection)
        {
            try
            {
                return await _uiService.ShowSendToPromptAsync(selection);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Shell integration: Send-to prompt failed; falling back to upload.");

                return new SendToPromptResult
                {
                    Action = SendToAction.UploadNow,
                    IsFallback = true,
                    Reason = "Send-to prompt failed to open."
                };
            }
        }

        private static void LogDecision(string source, SendToSelection selection, SendToPromptResult decision)
        {
            string fallbackSuffix = decision.IsFallback
                ? $", fallbackReason=\"{decision.Reason ?? "none"}\""
                : string.Empty;

            DebugHelper.WriteLine(
                $"Shell integration ({source}): Send-to decision action={decision.Action}, source={(decision.IsFallback ? "fallback" : "prompt")}, " +
                $"classification={selection.ClassificationLabel}, files={selection.FilePaths.Count}, folders={selection.FolderPaths.Count}, " +
                $"allImages={selection.AllFilesAreImages}{fallbackSuffix}.");
        }

        private static List<string> ResolveUploadFiles(SendToSelection selection)
        {
            StringComparer comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

            HashSet<string> seen = new(comparer);
            List<string> uploadFiles = [];

            foreach (string filePath in selection.FilePaths)
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) && seen.Add(filePath))
                {
                    uploadFiles.Add(filePath);
                }
            }

            int expandedFolderFileCount = 0;

            foreach (string folderPath in selection.FolderPaths)
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    continue;
                }

                try
                {
                    foreach (string filePath in Directory.GetFiles(folderPath))
                    {
                        if (seen.Add(filePath))
                        {
                            uploadFiles.Add(filePath);
                            expandedFolderFileCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, $"Shell integration: Failed to enumerate folder '{folderPath}' for Send-to upload.");
                }
            }

            if (selection.FolderPaths.Count > 0)
            {
                DebugHelper.WriteLine(
                    $"Shell integration: Resolved {expandedFolderFileCount} top-level file(s) from {selection.FolderPaths.Count} folder Send-to item(s) for upload.");
            }

            return uploadFiles;
        }
    }
}
