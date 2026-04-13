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
using XerahS.Common;
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

    public AssistantService()
        : this(new AssistantCommandRouter(), new AssistantHistoryService(), new AssistantPrivacyGuard())
    {
    }

    public AssistantService(
        AssistantCommandRouter router,
        IAssistantHistoryService history,
        AssistantPrivacyGuard privacyGuard)
    {
        _router = router;
        _history = history;
        _privacyGuard = privacyGuard;
    }

    public async Task<AssistantResponse> ProcessPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var intent = _router.Parse(prompt);
        return intent.Kind switch
        {
            AssistantDeterministicIntentKind.LatestScreenshotPaths => await GetLatestScreenshotPathsAsync(intent, cancellationToken),
            AssistantDeterministicIntentKind.CopyLatestScreenshotPath => await CopyLatestScreenshotPathAsync(cancellationToken),
            AssistantDeterministicIntentKind.OpenLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.OpenFile, cancellationToken),
            AssistantDeterministicIntentKind.RevealLatestScreenshot => await ExecuteLatestFileActionAsync(AssistantActionKind.RevealFile, cancellationToken),
            _ => AssistantResponse.Info(
                "AI provider not configured. XerahS Assistant can only run local commands without a provider.",
                new AssistantAction(AssistantActionKind.ConfigureProvider, "Configure AI Provider"))
        };
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
        CancellationToken cancellationToken)
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
                new AssistantAction(AssistantActionKind.RevealFile, "Reveal", FilePath: item.FilePath, ToolName: AssistantToolNames.FileReveal, RequiresConfirmation: true)
            ]);
    }

    private AssistantPrivacyDecision EvaluateAction(AssistantAction action)
    {
        string toolName = action.ToolName ?? action.Kind switch
        {
            AssistantActionKind.CopyText => AssistantToolNames.ClipboardCopyText,
            AssistantActionKind.OpenFile => AssistantToolNames.EditorOpenImage,
            AssistantActionKind.RevealFile => AssistantToolNames.FileReveal,
            _ => string.Empty
        };

        bool isKnown = string.IsNullOrWhiteSpace(action.FilePath) || _history.IsKnownHistoryFile(action.FilePath);
        return _privacyGuard.Evaluate(new AssistantPrivacyCheck(
            toolName,
            AssistantPrivacyScope.LocalContent,
            Text: action.Text,
            FilePath: action.FilePath,
            IsKnownHistoryItem: isKnown,
            UserExplicitlyRequested: true));
    }
}

