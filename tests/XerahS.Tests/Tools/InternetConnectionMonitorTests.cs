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

using NUnit.Framework;
using XerahS.Common.NetworkMonitor;

namespace XerahS.Tests.Tools;

[TestFixture]
public class InternetConnectionMonitorTests
{
    [Test]
    public async Task CheckOnce_RequiresFailThresholdBeforeDisconnect()
    {
        ScriptedNetworkProbe probe = new(
        [
            new NetworkProbeResult(true, 12),
            new NetworkProbeResult(false, null),
            new NetworkProbeResult(false, null),
            new NetworkProbeResult(false, null),
            new NetworkProbeResult(false, null)
        ]);
        DateTime now = new(2026, 8, 29, 10, 0, 0);
        using InternetConnectionMonitor monitor = new(
            probe,
            new NetworkMonitorOptions { FailThreshold = 4, PingAddresses = ["8.8.8.8"] },
            () => now);

        List<NetworkStatusEvent> events = [];
        monitor.StatusChanged += events.Add;

        Assert.That(monitor.HasConnectionState, Is.False);

        await monitor.CheckOnceAsync();
        Assert.That(monitor.IsConnected, Is.True);
        Assert.That(monitor.HasConnectionState, Is.True);
        Assert.That(events, Is.Empty);

        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        Assert.That(monitor.IsConnected, Is.True);
        Assert.That(events, Is.Empty);

        DateTime firstFail = now.AddSeconds(-2);
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();

        Assert.That(monitor.IsConnected, Is.False);
        Assert.That(monitor.DisconnectCount, Is.EqualTo(1));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].IsConnected, Is.False);
        Assert.That(events[0].Timestamp, Is.EqualTo(firstFail));
    }

    [Test]
    public async Task CheckOnce_ReconnectEmitsConnectedEvent()
    {
        ScriptedNetworkProbe probe = new(
        [
            new NetworkProbeResult(true, 10),
            new NetworkProbeResult(false, null),
            new NetworkProbeResult(false, null),
            new NetworkProbeResult(true, 18)
        ]);
        DateTime now = new(2026, 8, 29, 11, 0, 0);
        using InternetConnectionMonitor monitor = new(
            probe,
            new NetworkMonitorOptions { FailThreshold = 2, PingAddresses = ["1.1.1.1"] },
            () => now);

        List<NetworkStatusEvent> events = [];
        monitor.StatusChanged += events.Add;

        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();

        Assert.That(monitor.IsConnected, Is.True);
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].IsConnected, Is.False);
        Assert.That(events[1].IsConnected, Is.True);
        Assert.That(events[1].RoundtripMs, Is.EqualTo(18));
    }

    private sealed class ScriptedNetworkProbe : INetworkProbe
    {
        private readonly Queue<NetworkProbeResult> _results;

        public ScriptedNetworkProbe(IEnumerable<NetworkProbeResult> results)
        {
            _results = new Queue<NetworkProbeResult>(results);
        }

        public Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken)
        {
            if (_results.Count == 0)
            {
                return Task.FromResult(new NetworkProbeResult(false, null));
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
