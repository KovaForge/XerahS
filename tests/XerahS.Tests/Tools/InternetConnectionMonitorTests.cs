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
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].IsBaseline, Is.True);
        Assert.That(events[0].IsConnected, Is.True);

        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        Assert.That(monitor.IsConnected, Is.True);
        Assert.That(events, Has.Count.EqualTo(1));

        DateTime firstFail = now.AddSeconds(-2);
        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();

        Assert.That(monitor.IsConnected, Is.False);
        Assert.That(monitor.DisconnectCount, Is.EqualTo(1));
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[1].IsConnected, Is.False);
        Assert.That(events[1].IsBaseline, Is.False);
        Assert.That(events[1].Timestamp, Is.EqualTo(firstFail));
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
        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0].IsBaseline, Is.True);
        Assert.That(events[0].IsConnected, Is.True);
        Assert.That(events[1].IsConnected, Is.False);
        Assert.That(events[1].IsBaseline, Is.False);
        Assert.That(events[2].IsConnected, Is.True);
        Assert.That(events[2].IsBaseline, Is.False);
        Assert.That(events[2].RoundtripMs, Is.EqualTo(18));
    }

    [Test]
    public async Task CheckOnce_ProbesEveryAddressAndKeepsFastestSuccess()
    {
        MappedNetworkProbe probe = new(new Dictionary<string, NetworkProbeResult>
        {
            ["8.8.8.8"] = new(false, null, "8.8.8.8 timed out."),
            ["1.1.1.1"] = new(true, 11, string.Empty, "ICMP"),
            ["9.9.9.9"] = new(true, 40, string.Empty, "ICMP")
        });
        using InternetConnectionMonitor monitor = new(
            probe,
            new NetworkMonitorOptions
            {
                FailThreshold = 2,
                PingAddresses = ["8.8.8.8", "1.1.1.1", "9.9.9.9"]
            });

        await monitor.CheckOnceAsync();

        Assert.Multiple(() =>
        {
            Assert.That(monitor.IsConnected, Is.True);
            Assert.That(monitor.LastSample, Is.Not.Null);
            Assert.That(monitor.LastSample!.RoundtripMs, Is.EqualTo(11));
            Assert.That(monitor.LastSample.Address, Is.EqualTo("1.1.1.1"));
            Assert.That(monitor.LastSample.Method, Is.EqualTo("ICMP"));
            Assert.That(probe.Hosts, Is.EquivalentTo(new[] { "8.8.8.8", "1.1.1.1", "9.9.9.9" }));
        });
    }

    [Test]
    public async Task CheckOnce_CountsOneRoundWhenEveryAddressFails()
    {
        MappedNetworkProbe probe = new(new Dictionary<string, NetworkProbeResult>
        {
            ["1.1.1.1"] = new(false, null, "1.1.1.1 timed out."),
            ["8.8.8.8"] = new(false, null, "8.8.8.8 timed out.")
        });
        DateTime now = new(2026, 9, 25, 8, 0, 0);
        using InternetConnectionMonitor monitor = new(
            probe,
            new NetworkMonitorOptions
            {
                FailThreshold = 2,
                PingAddresses = ["1.1.1.1", "8.8.8.8"]
            },
            () => now);
        List<NetworkStatusEvent> events = [];
        monitor.StatusChanged += events.Add;

        await monitor.CheckOnceAsync();
        Assert.That(monitor.IsConnected, Is.False);
        Assert.That(monitor.HasConnectionState, Is.False);
        Assert.That(monitor.LastSample!.Error, Does.Contain("1.1.1.1 timed out."));
        Assert.That(probe.Hosts, Has.Count.EqualTo(2));
        Assert.That(events, Is.Empty);

        now = now.AddSeconds(1);
        await monitor.CheckOnceAsync();
        Assert.That(monitor.IsConnected, Is.False);
        Assert.That(monitor.HasConnectionState, Is.True);
        Assert.That(monitor.DisconnectCount, Is.EqualTo(0));
        Assert.That(probe.Hosts, Has.Count.EqualTo(4));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].IsBaseline, Is.True);
        Assert.That(events[0].IsConnected, Is.False);
        Assert.That(events[0].Timestamp, Is.EqualTo(new DateTime(2026, 9, 25, 8, 0, 0)));
    }

    [Test]
    public void GetDelayAfterProbe_ConfirmsFailuresWithoutWaitingTheFullInterval()
    {
        Assert.That(InternetConnectionMonitor.GetDelayAfterProbe(true, 30000), Is.EqualTo(1000));
        Assert.That(InternetConnectionMonitor.GetDelayAfterProbe(false, 30000), Is.EqualTo(30000));
        Assert.That(InternetConnectionMonitor.GetDelayAfterProbe(true, 200), Is.EqualTo(200));
    }

    [Test]
    public async Task Host_WritesReadableOutageLogAndKeepsItWhenTheListIsCleared()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-network-monitor-{Guid.NewGuid():N}.log");
        DateTime now = new(2026, 9, 25, 9, 0, 0);
        ScriptedNetworkProbe probe = new(
        [
            new NetworkProbeResult(false, null, "1.1.1.1 timed out."),
            new NetworkProbeResult(false, null, "1.1.1.1 timed out."),
            new NetworkProbeResult(false, null, "1.1.1.1 timed out."),
            new NetworkProbeResult(true, 18, string.Empty, "ICMP")
        ]);

        try
        {
            using NetworkMonitorHost host = new(
                probe,
                persist: false,
                logFilePath: path,
                clock: () => now);
            host.Monitor.Options = new NetworkMonitorOptions
            {
                FailThreshold = 2,
                PingAddresses = ["1.1.1.1"]
            };

            await host.Monitor.CheckOnceAsync();
            now = now.AddSeconds(1);
            await host.Monitor.CheckOnceAsync();

            string down = File.ReadAllText(path);
            Assert.That(down, Does.Contain("# XerahS network outage log"));
            Assert.That(down, Does.Contain("2026-09-25 09:00:00  DOWN"));
            Assert.That(down, Does.Contain("already down"));
            Assert.That(down, Does.Contain("1.1.1.1 timed out."));

            now = now.Add(NetworkMonitorLogLines.StillDownInterval);
            await host.Monitor.CheckOnceAsync();
            string stillDown = File.ReadAllText(path);
            Assert.That(stillDown, Does.Contain("Still down for 5m 1s"));

            now = now.AddSeconds(4);
            await host.Monitor.CheckOnceAsync();
            string recovered = File.ReadAllText(path);
            Assert.That(recovered, Does.Contain("2026-09-25 09:05:05  UP"));
            Assert.That(recovered, Does.Contain("Internet back."));
            Assert.That(recovered, Does.Contain("Reply from 1.1.1.1 via ICMP in 18 ms."));
            Assert.That(recovered, Does.Contain("Outage lasted 5m 5s."));

            host.ClearHistory();
            string kept = File.ReadAllText(path);
            Assert.That(kept, Does.Contain("already down"));
            Assert.That(kept, Does.Contain("Internet back."));
            Assert.That(kept, Does.Contain("This outage log was kept."));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void Host_LogsWhenMonitoringStartsAndPauses()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xerahs-network-monitor-{Guid.NewGuid():N}.log");
        try
        {
            using NetworkMonitorHost host = new(new BlockingNetworkProbe(), persist: false, logFilePath: path);
            host.EnsureStarted();
            host.Stop();

            string text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("WATCH"));
            Assert.That(text, Does.Contain("Monitoring started. Checking 1.1.1.1, 8.8.8.8, 9.9.9.9 every 2 s."));
            Assert.That(text, Does.Contain("PAUSED"));
            Assert.That(text, Does.Contain("Outages will not be recorded"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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

    private sealed class BlockingNetworkProbe : INetworkProbe
    {
        public async Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new NetworkProbeResult(false, null);
        }
    }

    private sealed class MappedNetworkProbe : INetworkProbe
    {
        private readonly Dictionary<string, NetworkProbeResult> _results;
        private readonly object _sync = new();

        public MappedNetworkProbe(Dictionary<string, NetworkProbeResult> results)
        {
            _results = results;
        }

        public List<string> Hosts { get; } = [];

        public Task<NetworkProbeResult> ProbeAsync(string host, int timeoutMs, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                Hosts.Add(host);
            }

            return Task.FromResult(_results.TryGetValue(host, out NetworkProbeResult result)
                ? result
                : new NetworkProbeResult(false, null, $"{host} missing."));
        }
    }
}
