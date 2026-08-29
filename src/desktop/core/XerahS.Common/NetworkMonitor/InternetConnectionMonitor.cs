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

namespace XerahS.Common.NetworkMonitor;

/// <summary>
/// ICMP probe loop modeled on Jaex NetworkMonitor: rotate public DNS targets,
/// require consecutive failures before treating the link as down, and stamp
/// disconnects at the first failed probe rather than the confirmation probe.
/// </summary>
public sealed class InternetConnectionMonitor : IDisposable
{
    private readonly INetworkProbe _probe;
    private readonly Func<DateTime> _clock;
    private readonly object _sync = new();
    private NetworkMonitorOptions _options;
    private CancellationTokenSource? _loopCts;
    private int _failCount;
    private int _addressIndex;
    private bool _isFirstEvent = true;
    private DateTime _firstFailDate;
    private bool _disposed;

    public event Action<NetworkStatusEvent>? StatusChanged;
    public event Action<NetworkLatencySample>? SampleReceived;

    public InternetConnectionMonitor(
        INetworkProbe? probe = null,
        NetworkMonitorOptions? options = null,
        Func<DateTime>? clock = null)
    {
        _probe = probe ?? new IcmpNetworkProbe();
        _options = CloneOptions(options ?? new NetworkMonitorOptions());
        _clock = clock ?? (() => DateTime.Now);
    }

    public bool IsMonitoring { get; private set; }
    public bool IsConnected { get; private set; }
    public int DisconnectCount { get; private set; }
    public string LastAddress { get; private set; } = string.Empty;
    public NetworkLatencySample? LastSample { get; private set; }

    public NetworkMonitorOptions Options
    {
        get
        {
            lock (_sync)
            {
                return CloneOptions(_options);
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_sync)
            {
                _options = CloneOptions(value);
                if (_options.PingAddresses.Length == 0)
                {
                    _options.PingAddresses = ["8.8.8.8"];
                }

                _addressIndex = 0;
            }
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (IsMonitoring)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            IsMonitoring = true;
            CancellationToken token = _loopCts.Token;
            _ = Task.Run(() => RunLoopAsync(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            if (!IsMonitoring)
            {
                return;
            }

            cts = _loopCts;
            _loopCts = null;
            IsMonitoring = false;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts?.Dispose();
    }

    public async Task CheckOnceAsync(CancellationToken cancellationToken = default)
    {
        string address;
        int timeoutMs;
        int failThreshold;
        lock (_sync)
        {
            string[] addresses = _options.PingAddresses;
            if (addresses.Length == 0)
            {
                return;
            }

            if (_addressIndex >= addresses.Length)
            {
                _addressIndex = 0;
            }

            address = addresses[_addressIndex];
            _addressIndex++;
            timeoutMs = Math.Max(200, _options.PingTimeoutMs);
            failThreshold = Math.Max(1, _options.FailThreshold);
        }

        NetworkProbeResult result = await _probe.ProbeAsync(address, timeoutMs, cancellationToken).ConfigureAwait(false);
        DateTime now = _clock();

        NetworkLatencySample sample = new()
        {
            Timestamp = now,
            Success = result.Success,
            RoundtripMs = result.RoundtripMs,
            Address = address
        };

        NetworkStatusEvent? statusEvent = null;

        lock (_sync)
        {
            LastAddress = address;
            LastSample = sample;

            if (result.Success)
            {
                _failCount = 0;
                if (!IsConnected)
                {
                    IsConnected = true;
                    if (_isFirstEvent)
                    {
                        _isFirstEvent = false;
                    }
                    else
                    {
                        statusEvent = new NetworkStatusEvent
                        {
                            Timestamp = now,
                            IsConnected = true,
                            RoundtripMs = result.RoundtripMs
                        };
                    }
                }
                else if (_isFirstEvent)
                {
                    _isFirstEvent = false;
                }
            }
            else
            {
                _failCount++;
                if (IsConnected)
                {
                    if (_failCount == 1)
                    {
                        _firstFailDate = now;
                    }

                    if (_failCount >= failThreshold)
                    {
                        IsConnected = false;
                        DisconnectCount++;
                        if (_isFirstEvent)
                        {
                            _isFirstEvent = false;
                        }

                        statusEvent = new NetworkStatusEvent
                        {
                            Timestamp = _firstFailDate,
                            IsConnected = false
                        };
                    }
                }
                else if (_isFirstEvent && _failCount >= failThreshold)
                {
                    _isFirstEvent = false;
                }
            }
        }

        SampleReceived?.Invoke(sample);
        if (statusEvent != null)
        {
            StatusChanged?.Invoke(statusEvent);
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

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckOnceAsync(cancellationToken).ConfigureAwait(false);
                int delayMs;
                lock (_sync)
                {
                    delayMs = Math.Max(200, _options.PingIntervalMs);
                }

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Network monitor loop");
        }
    }

    private static NetworkMonitorOptions CloneOptions(NetworkMonitorOptions source)
    {
        return new NetworkMonitorOptions
        {
            FailThreshold = source.FailThreshold,
            PingIntervalMs = source.PingIntervalMs,
            PingTimeoutMs = source.PingTimeoutMs,
            PingAddresses = [.. source.PingAddresses]
        };
    }
}
