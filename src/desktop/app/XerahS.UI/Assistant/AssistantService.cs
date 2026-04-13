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

using SkiaSharp;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Assistant;

public interface IAssistantService
{
    Task<AssistantResponse> ProcessPromptAsync(string prompt, CancellationToken cancellationToken);
    Task<AssistantResponse> ExecuteActionAsync(AssistantAction action, bool confirmed, CancellationToken cancellationToken);
}

public sealed class AssistantService : IAssistantService
{
    private readonly AssistantCommandRouter _router;
    private readonly IAssistantHistoryService _history;
    private readonly AssistantPrivacyGuard _privacyGuard;
    private readonly IDesktopTaskManager? _taskManager;

    public AssistantService()
        : this(new AssistantCommandRouter(), new AssistantHistoryService(), new AssistantPrivacyGuard(), null)
    {
    }

    public AssistantService(
        AssistantCommandRouter router,
        IAssistantHistoryService history,
        AssistantPrivacyGuard privacyGuard,
        IDesktopTaskManager? taskManager = null)
    {
        _router = router;
        _history = history;
        _privacyGuard = privacyGuard;
        _taskManager = taskManager ?? PlatformServices.RootProvider?.GetService(typeof(IDesktopTaskManager)) as IDesktopTaskManager;
    }

    public async Task<AssistantResponse> ProcessPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var intent = _router.Parse(prompt);
        if (intent.Kind == AssistantDeterministicIntentKind.Unknown)
        {
            AssistantResponse? providerResponse = await TryProcessWithProviderAsync(prompt, cancellationToken);
            if (providerResponse != null)
            {
                return providerResponse;
            }
        }

        return await ProcessIntentAsync(intent, cancellationToken);
    }

    private async Task<AssistantResponse> ProcessIntentAsync(
        AssistantDeterministicIntent intent,
        CancellationToken cancellationToken)
    {
        return intent.Kind switch
        {
            AssistantDeterministicIntentKind.LatestScreenshotPaths => await GetLatestScreenshotPathsAsync(intent, cancellationToken),
            AssistantDeterministicIntentKind.CopyLatestScreenshotPath => await CopyLatestScreenshotPathAsync(cancellationToken),
            AssistantDeterministicIntentKind.OpenLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.OpenFile, cancellationToken),
            AssistantDeterministicIntentKind.RevealLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RevealFile, cancellationToken),
            AssistantDeterministicIntentKind.OcrLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RunOcr, cancellationToken),
            AssistantDeterministicIntentKind.CopyOcrLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RunOcr, cancellationToken, "copy"),
            AssistantDeterministicIntentKind.UploadLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.UploadFile, cancellationToken),
            _ => AssistantResponse.Info(
                "AI provider not configured. XerahS Assistant can only run local commands without a provider.",
                new AssistantAction(AssistantActionKind.ConfigureProvider, "Configure AI Provider"))
        };
    }

    private async Task<AssistantResponse?> TryProcessWithProviderAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        if (!AssistantProviderSettingsResolver.TryGetActive(out AssistantProviderRuntimeSettings providerSettings))
        {
            return null;
        }

        AssistantPrivacyDecision decision = _privacyGuard.Evaluate(new AssistantPrivacyCheck(
            AssistantToolNames.HistorySearch,
            AssistantPrivacyScope.CloudText,
            Text: prompt,
            UserExplicitlyRequested: true));

        if (!decision.Allowed || decision.RequiresConfirmation)
        {
            return AssistantResponse.Error("The assistant did not send this prompt to the provider because it may contain paths, URLs, or sensitive text. Try one of the local command suggestions.");
        }

        string commandPrompt = string.Join(Environment.NewLine, AssistantCommandRouter.GetSuggestions().Select(item => $"- {item}"));
        AssistantModelRequest request = new(
            providerSettings.Metadata.Id,
            providerSettings.ModelId,
            [
                new AssistantMessage(
                    AssistantModelMessageRole.System,
                    "Convert the user's request into exactly one safe XerahS local command from the list. Return only the command text. Return NO_MATCH if none apply."),
                new AssistantMessage(
                    AssistantModelMessageRole.User,
                    $"Available commands:{Environment.NewLine}{commandPrompt}{Environment.NewLine}{Environment.NewLine}User request: {prompt}")
            ],
            [],
            AssistantPrivacyScope.CloudText,
            AllowImageContent: false);

        IAssistantModelProvider provider = AssistantModelProviderFactory.Create(providerSettings);
        AssistantModelResult result = await provider.CompleteAsync(request, cancellationToken);
        if (result.Kind == AssistantModelResultKind.Cancelled)
        {
            return AssistantResponse.Error("Provider request cancelled.");
        }

        if (result.Kind == AssistantModelResultKind.Error || string.IsNullOrWhiteSpace(result.Text))
        {
            return AssistantResponse.Error(result.Text ?? "Provider request failed.");
        }

        if (result.Text.Contains("NO_MATCH", StringComparison.OrdinalIgnoreCase))
        {
            return AssistantResponse.Info("AI provider could not map that request to a safe local command. Try one of the suggestions.");
        }

        AssistantDeterministicIntent inferredIntent = _router.Parse(result.Text);
        if (inferredIntent.Kind == AssistantDeterministicIntentKind.Unknown)
        {
            return AssistantResponse.Info("AI provider returned a response, but it did not match an allowlisted assistant command. Try one of the suggestions.");
        }

        return await ProcessIntentAsync(inferredIntent, cancellationToken);
    }

    public async Task<AssistantResponse> ExecuteActionAsync(AssistantAction action, bool confirmed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AssistantPrivacyDecision decision = EvaluateAction(action);
        if (!decision.Allowed)
        {
            return AssistantResponse.Error(decision.Reason ?? "Action blocked.");
        }

        if (decision.RequiresConfirmation && !confirmed)
        {
            return new AssistantResponse(
                AssistantResponseKind.ConfirmationRequired,
                decision.ConfirmationCopy ?? "Confirmation required.",
                [],
                [],
                new AssistantPendingConfirmation(action.ToolName ?? string.Empty, decision.ConfirmationCopy ?? "Confirmation required.", action));
        }

        try
        {
            switch (action.Kind)
            {
                case AssistantActionKind.CopyText:
                    await PlatformServices.Clipboard.SetTextAsync(action.Text ?? string.Empty);
                    return AssistantResponse.Info("Copied to clipboard.");

                case AssistantActionKind.RevealFile:
                    return PlatformServices.System.ShowFileInExplorer(action.FilePath ?? string.Empty)
                        ? AssistantResponse.Info("Opened file location.")
                        : AssistantResponse.Error("File no longer available. It may have been moved or deleted.");

                case AssistantActionKind.OpenFile:
                    return await OpenImageInEditorAsync(action.FilePath, cancellationToken);

                case AssistantActionKind.RunOcr:
                    return await RunOcrAsync(action, cancellationToken);

                case AssistantActionKind.UploadFile:
                    return await UploadFileAsync(action.FilePath, cancellationToken);

                default:
                    return AssistantResponse.Error("Unsupported assistant action.");
            }
        }
        catch (OperationCanceledException)
        {
            return AssistantResponse.Error("Action cancelled. You can retry if needed.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Assistant action failed: {action.Kind}");
            return AssistantResponse.Error("Assistant action failed.");
        }
    }

    private async Task<AssistantResponse> GetLatestScreenshotPathsAsync(
        AssistantDeterministicIntent intent,
        CancellationToken cancellationToken)
    {
        var items = await _history.GetLatestScreenshotsAsync(intent.Limit, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        string paths = string.Join(Environment.NewLine, items.Select(item => item.FilePath));
        if (intent.CopyRequested)
        {
            await PlatformServices.Clipboard.SetTextAsync(paths);
        }

        string message = intent.CopyRequested
            ? $"Copied {items.Count} path(s) to clipboard."
            : items.Count == 1
                ? "Latest screenshot path."
                : $"Last {items.Count} screenshot paths.";

        return new AssistantResponse(
            AssistantResponseKind.Results,
            message,
            items.Select(ToResultItem).ToList(),
            [new AssistantAction(AssistantActionKind.CopyText, "Copy paths", paths, ToolName: AssistantToolNames.ClipboardCopyText)]);
    }

    private async Task<AssistantResponse> CopyLatestScreenshotPathAsync(CancellationToken cancellationToken)
    {
        var items = await _history.GetLatestScreenshotsAsync(1, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        string path = items[0].FilePath;
        await PlatformServices.Clipboard.SetTextAsync(path);

        return new AssistantResponse(
            AssistantResponseKind.Results,
            "Copied the latest screenshot path to clipboard.",
            [ToResultItem(items[0])],
            [new AssistantAction(AssistantActionKind.CopyText, "Copy path", path, ToolName: AssistantToolNames.ClipboardCopyText)]);
    }

    private async Task<AssistantResponse> ExecuteLatestFileActionAsync(
        AssistantActionKind actionKind,
        CancellationToken cancellationToken,
        string? actionText = null)
    {
        var items = await _history.GetLatestScreenshotsAsync(1, cancellationToken);
        if (items.Count == 0)
        {
            return AssistantResponse.Error("No recent captures found. Try taking a screenshot first.");
        }

        AssistantHistoryItem item = items[0];
        AssistantAction action = actionKind switch
        {
            AssistantActionKind.OpenFile => new AssistantAction(
                AssistantActionKind.OpenFile,
                "Open",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.EditorOpenImage,
                RequiresConfirmation: true),
            AssistantActionKind.RunOcr => new AssistantAction(
                AssistantActionKind.RunOcr,
                string.Equals(actionText, "copy", StringComparison.OrdinalIgnoreCase) ? "Copy OCR text" : "Run OCR",
                Text: actionText,
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.OcrRun,
                RequiresConfirmation: true),
            AssistantActionKind.UploadFile => new AssistantAction(
                AssistantActionKind.UploadFile,
                "Upload",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.UploadFile,
                RequiresConfirmation: true),
            _ => new AssistantAction(
                AssistantActionKind.RevealFile,
                "Reveal",
                FilePath: item.FilePath,
                ToolName: AssistantToolNames.FileReveal,
                RequiresConfirmation: true)
        };

        return await ExecuteActionAsync(action, confirmed: false, cancellationToken);
    }

    private async Task<AssistantResponse> OpenImageInEditorAsync(string? filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        using SKBitmap? bitmap = SKBitmap.Decode(filePath);
        if (bitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        SKBitmap? editorBitmap = bitmap.Copy();
        if (editorBitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        SKBitmap? edited = await PlatformServices.UI.ShowEditorAsync(editorBitmap, filePath);
        edited?.Dispose();

        return AssistantResponse.Info("Opened latest screenshot in the editor.");
    }

    private async Task<AssistantResponse> RunOcrAsync(AssistantAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (PlatformServices.Ocr == null || !PlatformServices.Ocr.IsSupported)
        {
            return AssistantResponse.Error("OCR is not available on this platform.");
        }

        if (string.IsNullOrWhiteSpace(action.FilePath) || !File.Exists(action.FilePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        using SKBitmap? bitmap = SKBitmap.Decode(action.FilePath);
        if (bitmap == null)
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        OcrResult result = await PlatformServices.Ocr.RecognizeAsync(bitmap, new OcrOptions { Language = "en" });
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            return AssistantResponse.Error(result.ErrorMessage ?? "OCR did not find text in the latest screenshot.");
        }

        if (string.Equals(action.Text, "copy", StringComparison.OrdinalIgnoreCase))
        {
            await PlatformServices.Clipboard.SetTextAsync(result.Text);
            return AssistantResponse.Info(
                "Copied OCR text to clipboard.",
                new AssistantAction(AssistantActionKind.CopyText, "Copy OCR text", result.Text, ToolName: AssistantToolNames.ClipboardCopyText));
        }

        return AssistantResponse.Info(
            result.Text,
            new AssistantAction(AssistantActionKind.CopyText, "Copy OCR text", result.Text, ToolName: AssistantToolNames.ClipboardCopyText));
    }

    private async Task<AssistantResponse> UploadFileAsync(string? filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return AssistantResponse.Error("File no longer available. It may have been moved or deleted.");
        }

        if (_taskManager == null)
        {
            return AssistantResponse.Error("Upload services are not ready yet.");
        }

        TaskSettings settings = GetUploadTaskSettings();
        WorkerTask? task = null;
        void OnTaskStarted(object? sender, WorkerTask startedTask)
        {
            if (string.Equals(startedTask.Info.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                task = startedTask;
            }
        }

        _taskManager.TaskStarted += OnTaskStarted;
        try
        {
            await _taskManager.StartFileTask(settings, filePath);
        }
        finally
        {
            _taskManager.TaskStarted -= OnTaskStarted;
        }

        string? url = task?.Info.Metadata.UploadURL ?? task?.Info.Result?.URL;
        if (!string.IsNullOrWhiteSpace(url))
        {
            return AssistantResponse.Info(
                $"Uploaded latest screenshot: {url}",
                new AssistantAction(AssistantActionKind.CopyText, "Copy URL", url, ToolName: AssistantToolNames.ClipboardCopyText));
        }

        return task?.Error != null
            ? AssistantResponse.Error($"Upload failed: {task.Error.Message}")
            : AssistantResponse.Error(task?.Info.Result?.Response ?? "Upload finished without a URL.");
    }

    private AssistantResultItem ToResultItem(AssistantHistoryItem item)
    {
        return new AssistantResultItem(
            item.Id,
            item.FileName,
            item.Exists ? item.FilePath : $"{item.FilePath} (unavailable)",
            item.FilePath,
            item.CapturedAt,
            item.Exists,
            [
                new AssistantAction(AssistantActionKind.CopyText, "Copy path", item.FilePath, ToolName: AssistantToolNames.ClipboardCopyText),
                new AssistantAction(AssistantActionKind.OpenFile, "Open", FilePath: item.FilePath, ToolName: AssistantToolNames.EditorOpenImage, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.RevealFile, "Reveal", FilePath: item.FilePath, ToolName: AssistantToolNames.FileReveal, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.RunOcr, "OCR", FilePath: item.FilePath, ToolName: AssistantToolNames.OcrRun, RequiresConfirmation: true),
                new AssistantAction(AssistantActionKind.UploadFile, "Upload", FilePath: item.FilePath, ToolName: AssistantToolNames.UploadFile, RequiresConfirmation: true)
            ]);
    }

    private AssistantPrivacyDecision EvaluateAction(AssistantAction action)
    {
        string toolName = action.ToolName ?? action.Kind switch
        {
            AssistantActionKind.CopyText => AssistantToolNames.ClipboardCopyText,
            AssistantActionKind.OpenFile => AssistantToolNames.EditorOpenImage,
            AssistantActionKind.RevealFile => AssistantToolNames.FileReveal,
            AssistantActionKind.RunOcr => AssistantToolNames.OcrRun,
            AssistantActionKind.UploadFile => AssistantToolNames.UploadFile,
            _ => string.Empty
        };

        AssistantPrivacyScope scope = action.Kind == AssistantActionKind.UploadFile
            ? AssistantPrivacyScope.ExternalShare
            : AssistantPrivacyScope.LocalContent;

        bool isKnown = string.IsNullOrWhiteSpace(action.FilePath) || _history.IsKnownHistoryFile(action.FilePath);
        return _privacyGuard.Evaluate(new AssistantPrivacyCheck(
            toolName,
            scope,
            Text: action.Text,
            FilePath: action.FilePath,
            IsKnownHistoryItem: isKnown,
            UserExplicitlyRequested: true));
    }

    private static TaskSettings GetUploadTaskSettings()
    {
        var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
        TaskSettings settings = uploadWorkflow?.TaskSettings != null
            ? WatchFolderManager.CloneTaskSettings(uploadWorkflow.TaskSettings)
            : WatchFolderManager.CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());

        settings.Job = WorkflowType.FileUpload;
        settings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard;
        return settings;
    }
}
