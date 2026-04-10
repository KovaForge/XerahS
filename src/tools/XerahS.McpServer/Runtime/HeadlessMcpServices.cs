using SkiaSharp;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.McpServer.Runtime;

internal sealed class HeadlessMcpUIService : IUIService
{
    public Task HideMainWindowAsync() => Task.CompletedTask;

    public Task RestoreMainWindowAsync() => Task.CompletedTask;

    public Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false) =>
        Task.FromResult<SKBitmap?>(image);

    public Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath) =>
        Task.FromResult<string?>(null);

    public Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel)> ShowAfterCaptureWindowAsync(
        SKBitmap image,
        AfterCaptureTasks afterCapture,
        AfterUploadTasks afterUpload) =>
        Task.FromResult((afterCapture, afterUpload, false));

    public Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info) => Task.CompletedTask;

    public Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection) =>
        Task.FromResult(new SendToPromptResult { Action = SendToAction.UploadNow, IsFallback = true, Reason = "MCP server runs headlessly." });

    public Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection) => Task.CompletedTask;
}

internal sealed class HeadlessMcpToastService : IToastService
{
    public void ShowToast(ToastConfig config)
    {
    }

    public void CloseActiveToast()
    {
    }
}
