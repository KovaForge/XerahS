using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Polling clipboard monitor for Linux (works on X11 and Wayland).
/// </summary>
public sealed class LinuxClipboardMonitorService : PollingClipboardMonitorService
{
    public LinuxClipboardMonitorService(IClipboardService clipboardService)
        : base(clipboardService, TimeSpan.FromMilliseconds(1200))
    {
    }
}
