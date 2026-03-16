using XerahS.Platform.Abstractions;

namespace XerahS.Platform.MacOS;

/// <summary>
/// Polling clipboard monitor for macOS.
/// </summary>
public sealed class MacOSClipboardMonitorService : PollingClipboardMonitorService
{
    public MacOSClipboardMonitorService(IClipboardService clipboardService)
        : base(clipboardService, TimeSpan.FromMilliseconds(1000))
    {
    }
}
