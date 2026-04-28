using System.Diagnostics;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Clipboard monitor for Linux that avoids stealing focus from menus/popups.
/// <para>
/// On Wayland: uses a single long-lived <c>wl-paste --watch</c> process that
/// receives event-driven notifications when the clipboard changes. This avoids
/// spawning processes repeatedly, which can interact with the Wayland data-offer
/// protocol and steal focus from open menus.
/// </para>
/// <para>
/// On X11: falls back to polling with <c>xclip -selection clipboard -t TARGETS -o</c>
/// which lists MIME types without reading data.
/// </para>
/// </summary>
public sealed class LinuxClipboardMonitorService : IClipboardMonitorService
{
    private static readonly bool PreferWayland =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

    private Process? _watchProcess;
    private Task? _pollTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _hasBaseline;
    private DateTime _suppressUntilUtc = DateTime.MinValue;

    public bool IsSupported => true;

    public bool IsMonitoring =>
        !_disposed &&
        (_watchProcess is { HasExited: false } ||
         (_pollTask != null && !_pollTask.IsCompleted));

    public event EventHandler? ClipboardChanged;

    public void SuppressInternalActivity(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        var candidate = DateTime.UtcNow.Add(duration);
        if (candidate > _suppressUntilUtc)
            _suppressUntilUtc = candidate;
    }

    public void Start()
    {
        if (_disposed || IsMonitoring)
            return;

        _cts = new CancellationTokenSource();
        _hasBaseline = false;

        if (PreferWayland && TryStartWaylandWatch())
            return;

        // X11 fallback (or wl-paste not available): poll with type-listing
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();

            if (_watchProcess != null)
            {
                try { _watchProcess.Kill(entireProcessTree: true); } catch { }
                _watchProcess.Dispose();
                _watchProcess = null;
            }

            // Only dispose _cts when _pollTask has genuinely completed.
            // If the task timed out it is still running with a cancelled token;
            // it will exit at the next await point and self-clean via the
            // ObjectDisposedException caught in PollLoopAsync.
            bool pollTaskFinished = false;
            try { pollTaskFinished = _pollTask?.Wait(TimeSpan.FromSeconds(1)) ?? true; } catch { pollTaskFinished = true; }

            if (!pollTaskFinished)
            {
                // Leave _cts alive so the abandoned loop can still observe cancellation.
                // _pollTask is intentionally not nulled here so the next Start() creates a fresh task.
                _pollTask = null;
                _hasBaseline = false;
                return;
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _pollTask = null;
            _hasBaseline = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }

    // ── Wayland: event-based via wl-paste --watch ──────────────────────

    private bool TryStartWaylandWatch()
    {
        try
        {
            _watchProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "wl-paste",
                Arguments = "--watch echo .",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (_watchProcess == null)
                return false;

            _watchProcess.OutputDataReceived += OnWatchOutput;
            _watchProcess.BeginOutputReadLine();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnWatchOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null || _cts is { IsCancellationRequested: true })
            return;

        // Treat the first notification as the baseline (current clipboard state).
        if (!_hasBaseline)
        {
            _hasBaseline = true;
            return;
        }

        if (DateTime.UtcNow >= _suppressUntilUtc)
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── X11 fallback: polling with type-listing ────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        string? lastFingerprint = null;

        while (!ct.IsCancellationRequested)
        {
            string fingerprint = GetTypeFingerprint();

            if (_hasBaseline &&
                !string.Equals(fingerprint, lastFingerprint, StringComparison.Ordinal))
            {
                if (DateTime.UtcNow >= _suppressUntilUtc)
                    ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }

            lastFingerprint = fingerprint;
            _hasBaseline = true;

            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static string GetTypeFingerprint()
    {
        // Try xclip first on X11
        var result = RunQuiet("xclip", "-selection clipboard -t TARGETS -o");
        if (result != null)
            return $"types:{result}";

        // Fall back to wl-paste --list-types (shouldn't normally reach here on X11)
        result = RunQuiet("wl-paste", "--list-types");
        if (result != null)
            return $"types:{result}";

        return "empty";
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

            if (!process.WaitForExit(2000))
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
