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
using XerahS.Core.SendTo;
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
                    await UploadSelectionAsync(selection, source, decision);
                    return;

                default:
                    DebugHelper.WriteLine(
                        $"Shell integration ({source}): Upload skipped because Send-to action '{decision.Action}' was selected.");
                    try
                    {
                        await _uiService.ExecuteSendToActionAsync(decision.Action, selection, decision);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, $"Shell integration ({source}): Failed to execute Send-to action '{decision.Action}'.");
                    }
                    return;
            }
        }

        public async Task UploadSelectionAsync(SendToSelection selection, string source, SendToPromptResult? decision = null)
        {
            ArgumentNullException.ThrowIfNull(selection);

            SendToPromptResult effectiveDecision = decision ?? CreateDefaultDecision(selection);
            SendToResolvedFiles resolvedFiles = SendToPolicyResolver.ResolveFiles(selection, effectiveDecision.FolderPolicy);
            if (resolvedFiles.FilePaths.Count == 0)
            {
                DebugHelper.WriteLine(
                    $"Shell integration ({source}): Send-to upload requested but no uploadable files were resolved; " +
                    $"folderPolicy={effectiveDecision.FolderPolicy}, folders={selection.FolderPaths.Count}.");
                return;
            }

            DebugHelper.WriteLine(
                $"Shell integration ({source}): Resolved Send-to upload files direct={resolvedFiles.DirectFileCount}, " +
                $"fromFolders={resolvedFiles.FolderFileCount}, folderPolicy={resolvedFiles.FolderPolicy}, " +
                $"failedFolders={resolvedFiles.FailedFolderCount}, namingPolicy=task-name-pattern.");

            foreach (string file in resolvedFiles.FilePaths)
            {
                TaskSettings settings = _createUploadTaskSettings();
                settings.Job = WorkflowType.FileUpload;
                DebugHelper.WriteLine(
                    $"Shell integration ({source}): Starting Send-to upload source=\"{file}\", " +
                    "staging=false, resolvedUploadName=generated-by-task-manager.");
                await _taskManager.StartFileTask(settings, file);
            }
        }

        private async Task<SendToPromptResult> ResolveDecisionAsync(SendToSelection selection)
        {
            SendToPromptResult? rememberedDecision = SendToPolicyResolver.TryResolveRememberedDecision(
                selection,
                SettingsManager.Settings.SendToRememberedChoices);

            if (rememberedDecision != null)
            {
                return rememberedDecision;
            }

            try
            {
                SendToPromptResult decision = await _uiService.ShowSendToPromptAsync(selection);
                if (decision.RememberChoice && decision.Action != SendToAction.Cancel)
                {
                    SendToPolicyResolver.SaveRememberedDecision(SettingsManager.Settings.SendToRememberedChoices, decision);
                    _ = SettingsManager.SaveApplicationConfigAsync();
                    DebugHelper.WriteLine(
                        $"Shell integration: Saved Send-to remembered choice scope={decision.RememberScope}, action={decision.Action}, " +
                        $"folderPolicy={decision.FolderPolicy}, batchPolicy={decision.BatchExecutionPolicy}.");
                }

                return decision;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Shell integration: Send-to prompt failed; falling back to upload.");

                return new SendToPromptResult
                {
                    Action = SendToAction.UploadNow,
                    FolderPolicy = SettingsManager.Settings.SendToFolderPolicy,
                    RememberScope = SendToPolicyResolver.GetRememberScope(selection),
                    BatchExecutionPolicy = SettingsManager.Settings.SendToBatchExecutionPolicy,
                    BatchConfirmThreshold = SendToPolicyResolver.NormalizeBatchThreshold(SettingsManager.Settings.SendToBatchConfirmThreshold),
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
                $"Shell integration ({source}): Send-to decision action={decision.Action}, source={GetDecisionSource(decision)}, " +
                $"classification={selection.ClassificationLabel}, files={selection.FilePaths.Count}, folders={selection.FolderPaths.Count}, " +
                $"allImages={selection.AllFilesAreImages}, rememberedScope={decision.RememberScope}, folderPolicy={decision.FolderPolicy}, " +
                $"batchPolicy={decision.BatchExecutionPolicy}, batchThreshold={decision.BatchConfirmThreshold}, " +
                $"namingPolicy=task-name-pattern{fallbackSuffix}.");
        }

        private static string GetDecisionSource(SendToPromptResult decision)
        {
            if (decision.IsFallback) return "fallback";
            if (decision.IsRemembered) return "remembered";
            return "prompt";
        }

        private static SendToPromptResult CreateDefaultDecision(SendToSelection selection)
        {
            return SendToPolicyResolver.CreateDefaultDecision(
                selection,
                SettingsManager.Settings.SendToFolderPolicy,
                SettingsManager.Settings.SendToBatchExecutionPolicy,
                SettingsManager.Settings.SendToBatchConfirmThreshold);
        }
    }
}
