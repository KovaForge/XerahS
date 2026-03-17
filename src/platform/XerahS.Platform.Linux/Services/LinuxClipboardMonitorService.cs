using System.Diagnostics;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Polling clipboard monitor for Linux (works on X11 and Wayland).
/// Overrides the base fingerprint to query only the list of available MIME types
/// instead of reading clipboard data.  Reading data via <c>wl-paste</c> triggers
/// the Wayland data-offer protocol which can steal focus from open menus/popups;
/// listing types avoids that interaction entirely.
/// </summary>
public sealed class LinuxClipboardMonitorService : PollingClipboardMonitorService
{
    private static readonly bool PreferWayland =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

    public LinuxClipboardMonitorService(IClipboardService clipboardService)
        : base(clipboardService, TimeSpan.FromMilliseconds(1200))
    {
    }

    /// <summary>
    /// Lightweight fingerprint that only asks the clipboard for available MIME types.
    /// This spawns <c>wl-paste --list-types</c> (Wayland) or <c>xclip -selection clipboard -t TARGETS -o</c>
    /// (X11) which return the type list without reading data, preventing Wayland
    /// data-offer focus interactions that dismiss menus.
    /// </summary>
    protected override string BuildClipboardFingerprint()
    {
        string? types = ListClipboardTypes();
        return types != null ? $"types:{types}" : "empty";
    }

    private static string? ListClipboardTypes()
    {
        if (PreferWayland)
        {
            var result = RunQuiet("wl-paste", "--list-types");
            if (result != null) return result;
        }

        {
            var result = RunQuiet("xclip", "-selection clipboard -t TARGETS -o");
            if (result != null) return result;
        }

        if (!PreferWayland)
        {
            var result = RunQuiet("wl-paste", "--list-types");
            if (result != null) return result;
        }

        return null;
    }

    private static string? RunQuiet(string fileName, string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process == null)
                return null;

            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.BeginOutputReadLine();

            if (!process.WaitForExit(1500))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            return process.ExitCode == 0 && output.Length > 0 ? output.ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}
