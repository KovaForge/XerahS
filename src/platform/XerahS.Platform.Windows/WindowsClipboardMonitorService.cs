using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows;

/// <summary>
/// Polling clipboard monitor for Windows.
/// </summary>
public sealed class WindowsClipboardMonitorService : PollingClipboardMonitorService
{
    public WindowsClipboardMonitorService(IClipboardService clipboardService)
        : base(clipboardService, TimeSpan.FromMilliseconds(900))
    {
    }
}
