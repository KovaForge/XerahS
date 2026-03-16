using System.Text;
using SkiaSharp;

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Monitors system clipboard changes and raises notifications.
/// </summary>
public interface IClipboardMonitorService : IDisposable
{
    bool IsSupported { get; }
    bool IsMonitoring { get; }
    event EventHandler? ClipboardChanged;
    void Start();
    void Stop();
}

/// <summary>
/// No-op clipboard monitor for unsupported environments.
/// </summary>
public sealed class UnsupportedClipboardMonitorService : IClipboardMonitorService
{
    public bool IsSupported => false;
    public bool IsMonitoring => false;
    public event EventHandler? ClipboardChanged
    {
        add { }
        remove { }
    }

    public void Start() { }
    public void Stop() { }
    public void Dispose() { }
}

/// <summary>
/// Polling-based clipboard monitor baseline for platforms where push notifications are unavailable.
/// </summary>
public abstract class PollingClipboardMonitorService : IClipboardMonitorService
{
    private readonly IClipboardService _clipboardService;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private string? _lastFingerprint;
    private bool _hasBaseline;
    private bool _disposed;

    protected PollingClipboardMonitorService(IClipboardService clipboardService, TimeSpan pollInterval)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _pollInterval = pollInterval;
    }

    public virtual bool IsSupported => true;
    public bool IsMonitoring => _monitorTask != null && !_monitorTask.IsCompleted;

    public event EventHandler? ClipboardChanged;

    public void Start()
    {
        if (_disposed || IsMonitoring)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_cts == null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore cancellation and shutdown exceptions.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _monitorTask = null;
            _hasBaseline = false;
            _lastFingerprint = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string fingerprint = BuildClipboardFingerprintSafe();

            if (_hasBaseline && !string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }

            _lastFingerprint = fingerprint;
            _hasBaseline = true;

            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private string BuildClipboardFingerprintSafe()
    {
        try
        {
            return BuildClipboardFingerprint();
        }
        catch
        {
            return "error";
        }
    }

    private string BuildClipboardFingerprint()
    {
        if (_clipboardService.ContainsText())
        {
            string? text = _clipboardService.GetText() ?? string.Empty;
            return $"text:{ComputeStringHash(text)}";
        }

        if (_clipboardService.ContainsFileDropList())
        {
            string[] files = _clipboardService.GetFileDropList() ?? [];
            return $"files:{ComputeStringHash(string.Join("|", files.Select(f => f?.Trim() ?? string.Empty)))}";
        }

        if (_clipboardService.ContainsImage())
        {
            using SKBitmap? image = _clipboardService.GetImage();
            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return "image:null";
            }

            // Lightweight sampled fingerprint to detect common screenshot/image changes.
            var sb = new StringBuilder();
            sb.Append("image:").Append(image.Width).Append('x').Append(image.Height).Append(':');

            int[] sampleX = [0, image.Width / 2, Math.Max(image.Width - 1, 0)];
            int[] sampleY = [0, image.Height / 2, Math.Max(image.Height - 1, 0)];
            foreach (int y in sampleY)
            {
                foreach (int x in sampleX)
                {
                    var color = image.GetPixel(x, y);
                    sb.Append(color.Red).Append(',')
                      .Append(color.Green).Append(',')
                      .Append(color.Blue).Append(',')
                      .Append(color.Alpha).Append(';');
                }
            }

            return sb.ToString();
        }

        return "empty";
    }

    private static string ComputeStringHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
