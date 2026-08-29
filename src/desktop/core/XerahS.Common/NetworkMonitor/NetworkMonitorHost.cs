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
    private bool _disposed;

    public static NetworkMonitorHost Shared => SharedHost.Value;

    public NetworkMonitorHost(
        INetworkProbe? probe = null,
        string? storePath = null,
        Func<DateTime>? clock = null,
        bool persist = true)
    {
        _storePath = storePath ?? NetworkMonitorStore.GetDefaultPath();
        Persist = persist;
        History = persist ? NetworkMonitorStore.Load(_storePath) : new NetworkMonitorHistory();
        Monitor = new InternetConnectionMonitor(probe, clock: clock);
        Monitor.StatusChanged += OnStatusChanged;
        Monitor.SampleReceived += OnSampleReceived;
    }

    public InternetConnectionMonitor Monitor { get; }
    public NetworkMonitorHistory History { get; }
    public bool Persist { get; }

    public void EnsureStarted()
    {
        Monitor.Start();
    }

    public void Stop()
    {
        Monitor.Stop();
    }

    public void ClearHistory()
    {
        History.Clear();
        if (Persist)
        {
            NetworkMonitorStore.Save(History, _storePath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Monitor.StatusChanged -= OnStatusChanged;
        Monitor.SampleReceived -= OnSampleReceived;
        Monitor.Dispose();
    }

    private void OnStatusChanged(NetworkStatusEvent statusEvent)
    {
        History.AddEvent(statusEvent);
        if (Persist)
        {
            NetworkMonitorStore.Save(History, _storePath);
        }
    }

    private void OnSampleReceived(NetworkLatencySample sample)
    {
        History.AddSample(sample);
    }
}
