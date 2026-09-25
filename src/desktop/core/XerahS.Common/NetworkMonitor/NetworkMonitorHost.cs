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

public sealed class NetworkMonitorHost : IDisposable
{
    private static readonly Lazy<NetworkMonitorHost> SharedHost = new(() => new NetworkMonitorHost());
    private readonly string _storePath;
    private readonly NetworkMonitorEventLog _eventLog;
    private readonly Func<DateTime> _clock;
    private bool _outageOpen;
    private DateTime _lastDownNotice;
    private bool _disposed;

    public static NetworkMonitorHost Shared => SharedHost.Value;

    public NetworkMonitorHost(
        INetworkProbe? probe = null,
        string? storePath = null,
        Func<DateTime>? clock = null,
        bool persist = true,
        string? logFilePath = null)
    {
        _storePath = storePath ?? NetworkMonitorStore.GetDefaultPath();
        _clock = clock ?? (() => DateTime.Now);
        Persist = persist;
        History = persist ? NetworkMonitorStore.Load(_storePath) : new NetworkMonitorHistory();
        _eventLog = new NetworkMonitorEventLog(logFilePath ?? (persist ? NetworkMonitorEventLog.GetDefaultPath() : null));
        Monitor = new InternetConnectionMonitor(probe, clock: _clock);
        Monitor.StatusChanged += OnStatusChanged;
        Monitor.SampleReceived += OnSampleReceived;
    }

    public InternetConnectionMonitor Monitor { get; }
    public NetworkMonitorHistory History { get; }
    public bool Persist { get; }
    public string? LogFilePath => _eventLog.FilePath;

    public void EnsureStarted()
    {
        if (Monitor.IsMonitoring)
        {
            return;
        }

        NetworkMonitorOptions options = Monitor.Options;
        _eventLog.AppendRaw(NetworkMonitorLogLines.Watch(
            _clock(),
            options.PingAddresses,
            TimeSpan.FromMilliseconds(Math.Max(200, options.PingIntervalMs))));
        Monitor.Start();
    }

    public void Stop()
    {
        if (!Monitor.IsMonitoring)
        {
            return;
        }

        Monitor.Stop();
        _eventLog.AppendRaw(NetworkMonitorLogLines.Paused(_clock()));
    }

    public void ClearHistory()
    {
        History.Clear();
        if (Persist)
        {
            NetworkMonitorStore.Save(History, _storePath);
        }

        _outageOpen = Monitor.HasConnectionState && !Monitor.IsConnected;
        _lastDownNotice = _clock();
        _eventLog.AppendRaw(NetworkMonitorLogLines.HistoryCleared(_clock()));
    }

    public bool EnsureLogFile()
    {
        return _eventLog.EnsureFileExists();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        bool wasMonitoring = Monitor.IsMonitoring;
        Monitor.StatusChanged -= OnStatusChanged;
        Monitor.SampleReceived -= OnSampleReceived;
        if (wasMonitoring)
        {
            _eventLog.AppendRaw(NetworkMonitorLogLines.Stopped(_clock()));
        }

        Monitor.Dispose();
    }

    private void OnStatusChanged(NetworkStatusEvent statusEvent)
    {
        History.AddEvent(statusEvent);
        NetworkLatencySample? sample = Monitor.LastSample;
        if (statusEvent.IsConnected)
        {
            _outageOpen = false;
            _eventLog.AppendRaw(NetworkMonitorLogLines.Up(statusEvent.Timestamp, statusEvent, sample));
        }
        else
        {
            _outageOpen = true;
            _lastDownNotice = statusEvent.Timestamp;
            _eventLog.AppendRaw(NetworkMonitorLogLines.Down(statusEvent.Timestamp, sample, statusEvent.IsBaseline));
        }

        if (Persist)
        {
            NetworkMonitorStore.Save(History, _storePath);
        }
    }

    private void OnSampleReceived(NetworkLatencySample sample)
    {
        History.AddSample(sample);
        if (!_outageOpen || sample.Success || sample.Timestamp - _lastDownNotice < NetworkMonitorLogLines.StillDownInterval)
        {
            return;
        }

        DateTime outageStart = History.Events.LastOrDefault(item => !item.IsConnected)?.Timestamp ?? _lastDownNotice;
        _lastDownNotice = sample.Timestamp;
        _eventLog.AppendRaw(NetworkMonitorLogLines.StillDown(sample.Timestamp, sample, sample.Timestamp - outageStart));
    }
}
